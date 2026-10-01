using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 실제 SampleScene의 입력/전투/성장 경로를 반복 실행한다. 판정이나 체력을 우회하지 않는다.
[InitializeOnLoad]
public static class RunBalanceValidation
{
    private const string PendingKey = "GridCaster.BalanceValidation";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector3Int[] Directions =
    {
        Vector3Int.up, new Vector3Int(1, 1, 0), Vector3Int.right,
        new Vector3Int(1, -1, 0), Vector3Int.down, new Vector3Int(-1, -1, 0),
        Vector3Int.left, new Vector3Int(-1, 1, 0)
    };
    private static IEnumerator routine;
    private static int lastFrame;
    private static Report report;
    private static Request request;
    private static double deadline;
    private static string runtimeError;
    private static float previousTimeScale;
    private static float previousFixedDelta;
    private static StackTraceLogType previousLogTrace;

    [Serializable]
    public sealed class Request
    {
        public string projectPath;
        public string label = "tuning";
        public string outputPath;
        public int firstSeed = 1000;
        public int seedsPerTrait = 8;
        public bool allowRelics = true;
        public bool avoidLegendary;
        public bool mechanicsOnly;
        public bool validateMechanics;
    }

    [Serializable]
    public sealed class RunResult
    {
        public string trait;
        public int seed;
        public bool won;
        public string ending;
        public int clearedWave;
        public int health;
        public int actions;
        public int worldTurns;
        public int moveActions;
        public int attackSelections;
        public int artMoves;
        public int initialMaxHealth;
        public int baseAttackDamage;
        public float clearHealRatio;
        public List<string> trace = new List<string>();
        public List<string> upgrades = new List<string>();
        public List<string> relics = new List<string>();
        public List<int> waveActions = new List<int>();
        public List<int> waveHealth = new List<int>();
    }

    [Serializable]
    public sealed class Report
    {
        public string label;
        public string policy = "heuristic-v4; public board/two-step chase prediction/knight intent, no hidden activation/RNG inspection";
        public string policyHash;
        public string unityVersion;
        public string sourceHash;
        public bool allowRelics;
        public bool avoidLegendary;
        public bool complete;
        public string error;
        public List<string> editorDiagnostics = new List<string>();
        public List<string> checks = new List<string>();
        public List<RunResult> runs = new List<RunResult>();
    }

    static RunBalanceValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            string json = SessionState.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            SessionState.EraseString(PendingKey);
            request = JsonUtility.FromJson<Request>(json);
            report = new Report { label = request.label, unityVersion = Application.unityVersion,
                sourceHash = SourceHash(), allowRelics = request.allowRelics,
                avoidLegendary = request.avoidLegendary };
            using (SHA256 sha = SHA256.Create())
                report.policyHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(
                    Path.Combine(Application.dataPath, "Editor/RunBalanceValidation.cs"))))
                    .Replace("-", "").ToLowerInvariant();
            deadline = EditorApplication.timeSinceStartup + 1800d;
            runtimeError = null;
            previousTimeScale = Time.timeScale;
            previousFixedDelta = Time.fixedDeltaTime;
            previousLogTrace = Application.GetStackTraceLogType(LogType.Log);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.logMessageReceived += CaptureRuntimeError;
            lastFrame = -1;
            routine = Run();
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Run Balance")]
    public static void Validate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Start(new Request { label = "manual", outputPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath), "docs/verification/balance-manual.json") });
    }

    public static void RunBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-balanceRequest");
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("-balanceRequest missing");
        Start(JsonUtility.FromJson<Request>(File.ReadAllText(args[index + 1])));
    }

    private static void Start(Request settings)
    {
        // 사용자의 저장하지 않은 씬을 버리지 않는다.
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the current scene before balance validation.");
        SessionState.SetString(PendingKey, JsonUtility.ToJson(settings));
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Validation interrupted/timed out");
            if (routine.MoveNext()) return;
            report.complete = true;
            Finish();
        }
        catch (Exception exception)
        {
            report.error = exception.ToString();
            Finish();
        }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CaptureRuntimeError;
        WriteReport();
        Debug.Log("GRID_CASTER_BALANCE_" + (report.complete ? "PASS" : "FAIL")
            + " " + report.label + " runs=" + report.runs.Count + " " + report.error);
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDelta;
        Application.SetStackTraceLogType(LogType.Log, previousLogTrace);
        routine = null;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(report.complete ? 0 : 1);
    }

    private static void CaptureRuntimeError(string message, string stackTrace, LogType type)
    {
        // Unity 6000.4의 시작 인덱싱 오류는 게임 판정과 분리해 그대로 보고한다.
        // 게임 스크립트가 포함된 오류나 다른 예외는 검증 실패로 처리한다.
        if (stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")
            && stackTrace.Contains("UnityEditor.Search.SearchDatabase")
            && !stackTrace.Contains("Assets/Codes/"))
        {
            report.editorDiagnostics.Add(message + "\n" + stackTrace);
            return;
        }
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            runtimeError = message + "\n" + stackTrace;
    }

    private static void WriteReport()
    {
        string destination = request.outputPath;
        if (string.IsNullOrEmpty(destination)) destination = Path.Combine(Path.GetTempPath(),
            "GridCasterBalance-" + request.label + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.WriteAllText(destination, JsonUtility.ToJson(report, true));
    }

    private static string SourceHash()
    {
        string[] paths = { "RunProgressionCatalog", "RunProgressionSystem", "PlayerTraitSystem",
            "Move", "MonsterMovement", "MonsterSpawner", "StageFlowManager", "WaveTemplate",
            "ChessMonsterBehaviour", "ChessWaveDisplay", "ProjectileManager", "TurnProjectile" };
        string contents = string.Join("\n", paths.Select(name => name + "\n"
            + File.ReadAllText(Path.Combine(Application.dataPath, "Codes", name + ".cs"))))
            + "\nSampleScene\n" + File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/SampleScene.unity"));
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(contents)))
                .Replace("-", "").ToLowerInvariant();
    }

    private static IEnumerator Run()
    {
        yield return null;
        if (request.mechanicsOnly || request.validateMechanics)
        {
            IEnumerator mechanics = Mechanics();
            while (mechanics.MoveNext()) yield return mechanics.Current;
            if (request.mechanicsOnly) yield break;
        }
        string[] traits = { "double_cast", "pierce", "damage_boost", "knockback" };
        for (int traitIndex = 0; traitIndex < traits.Length; traitIndex++)
        for (int n = 0; n < request.seedsPerTrait; n++)
        {
            int seed = request.firstSeed + n;
            UnityEngine.Random.InitState(seed);
            AsyncOperation loading = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            // Editor Tick은 Unity coroutine 스케줄러가 아니므로 로드 완료를 명시적으로 기다린다.
            while (!loading.isDone) yield return null;
            yield return null;
            Move player = UnityEngine.Object.FindAnyObjectByType<Move>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            RunProgressionSystem growth = player.GetComponent<RunProgressionSystem>();
            GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            while (spawner.StageFlow == null || spawner.CurrentWave == 0) yield return null;
            growth.TryInitialize();
            RunResult result = new RunResult { trait = traits[traitIndex], seed = seed };
            result.initialMaxHealth = player.GetComponent<CharacterHealth>().MaxHealth;
            result.baseAttackDamage = (int)typeof(Move).GetField("attackDamage", PrivateInstance).GetValue(player);
            result.clearHealRatio = (float)typeof(StageFlowManager)
                .GetField("waveClearHealRatio", PrivateInstance).GetValue(spawner.StageFlow);
            spawner.WorldTurnCompleted += () => result.worldTurns++;
            int lastWaveActions = 0;
            spawner.WaveCleared += wave =>
            {
                result.clearedWave = wave;
                result.waveActions.Add(result.actions - lastWaveActions);
                result.waveHealth.Add(player.CurrentHealth);
                lastWaveActions = result.actions;
            };
            UnityEngine.Object.FindAnyObjectByType<GameHudController>().SelectTrait(traitIndex);
            List<Vector3Int> recent = new List<Vector3Int>();
            int waveAtLimit = 0;
            int actionsThisWave = 0;
            int idleFrames = 0;
            while (player.CurrentHealth > 0 && spawner.StageFlow.State != StageFlowState.StageCleared)
            {
                // 변경하는 것은 연출 시간뿐이다. 적 AI/발동/피해/스폰은 그대로 실행한다.
                Time.timeScale = 100f;
                Time.fixedDeltaTime = 100f;
                SetSpeed(player);
                SetSpeed(UnityEngine.Object.FindAnyObjectByType<ProjectileManager>());
                foreach (MonsterMovement monster in spawner.ActiveMonsters) SetSpeed(monster);
                if (growth.CurrentUpgradeOptions.Count > 0)
                {
                    int choice = BestUpgrade(growth, player);
                    result.upgrades.Add(growth.CurrentUpgradeOptions[choice].Id);
                    growth.SelectUpgrade(choice);
                    yield return null;
                    continue;
                }
                if (spawner.CurrentWave != waveAtLimit)
                {
                    waveAtLimit = spawner.CurrentWave;
                    actionsThisWave = 0;
                    recent.Clear();
                }
                if (++idleFrames > 10000) throw new InvalidOperationException("Action stalled");
                if (!player.CanAct) { yield return null; continue; }
                if (!spawner.ActiveMonsters.Any(monster => monster != null && !monster.IsDead)
                    && !player.IsChoosingEchoDirection && !player.IsChoosingFreeMove)
                { yield return null; continue; }
                idleFrames = 0;
                if (actionsThisWave >= 200 || result.actions >= 3500)
                { result.ending = "turn_limit"; break; }
                ChooseAction(player, spawner, grid, growth, recent,
                    out PlayerActionSelectionMode mode, out Vector3Int direction);
                if (n == 0 && result.actions < 24)
                    result.trace.Add($"{result.actions} w={spawner.CurrentWave} hp={player.CurrentHealth} "
                        + $"pos={player.GridPosition} {mode} {direction} enemies="
                        + string.Join(";", spawner.ActiveMonsters.Where(monster => monster != null && !monster.IsDead)
                            .Select(monster => monster.MovementPattern + ":" + monster.GridPosition + ":"
                                + monster.GetComponent<CharacterHealth>().CurrentHealth)));
                if (!player.TryPerformAction(mode, direction))
                    throw new InvalidOperationException("Bot selected an invalid action " + mode + direction);
                if (mode == PlayerActionSelectionMode.Attack) result.attackSelections++;
                else
                {
                    result.moveActions++;
                    if (Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y)) > 1) result.artMoves++;
                }
                result.actions++;
                actionsThisWave++;
                recent.Add(player.GridPosition);
                if (recent.Count > 6) recent.RemoveAt(0);
                yield return null;
            }
            result.won = spawner.StageFlow.State == StageFlowState.StageCleared;
            if (string.IsNullOrEmpty(result.ending)) result.ending = result.won ? "clear" : "death";
            result.health = player.CurrentHealth;
            result.relics.AddRange(growth.Relics.Select(item => item.Id));
            report.runs.Add(result);
            WriteReport();
            Debug.Log($"BALANCE_RUN {request.label} {result.trait} seed={seed} "
                + $"{result.ending} cleared={result.clearedWave} actions={result.actions}");
        }
    }

    private static void SetSpeed(Component component)
    {
        if (component != null) component.GetType().GetField("moveSpeed", PrivateInstance)
            ?.SetValue(component, 10000f);
    }

    private static int BestUpgrade(RunProgressionSystem growth, Move player)
    {
        float Value(UpgradeDefinition item)
        {
            if (item.Id == "movement_knight") return 100f;
            if (item.Id == "trait_mastery") return 85f;
            if (item.Id == "rapid_cycle") return 95f;
            if (item.Id == "mana_circulation") return growth.Stack(item.Id) == 0 ? 88f : 65f;
            if (item.Id == "movement_breath") return growth.Stack(item.Id) == 0 ? 80f : 62f;
            if (item.Id == "movement_bishop") return 70f;
            if (item.Id == "movement_rook") return 25f;
            if (item.Id.StartsWith("movement_training")) return growth.MovementArtLevel < 2 ? 76f : 55f;
            if (item.Id == "keen_magic") return 60f;
            if (item.Id == "alternating_overload") return 90f;
            if (item.Id == "immortal_cycle") return 78f;
            if (item.Id == "combat_regeneration") return 50f;
            if (item.Id == "life_tempering") return player.CurrentHealth <= 3 ? 84f : 35f;
            if (item.Id == "healing_breath") return 44f;
            if (item.IsTraitUpgrade)
            {
                float score = growth.Stack(item.Id) == 0 ? 82f : 66f;
                if (item.Id == "overcharge" || item.Id == "thin_reload") score += 10f;
                if (item.Id == "echo_recovery" || item.Id == "impact_recovery") score += 2f;
                return score;
            }
            return 20f;
        }
        return Enumerable.Range(0, growth.CurrentUpgradeOptions.Count)
            .OrderByDescending(index => Value(growth.CurrentUpgradeOptions[index])).First();
    }

    private static void ChooseAction(Move player, MonsterSpawner spawner, GridManager grid,
        RunProgressionSystem growth, List<Vector3Int> recent,
        out PlayerActionSelectionMode mode, out Vector3Int direction)
    {
        float best = float.NegativeInfinity;
        mode = PlayerActionSelectionMode.Attack;
        direction = Vector3Int.up;
        string trait = player.GetComponent<PlayerTraitSystem>().SelectedTraitId;
        float proc = 1f / player.GetComponent<PlayerTraitSystem>().ActivationBagSize;
        foreach (Vector3Int step in Directions)
        {
            if (player.IsChoosingFreeMove) break;
            float value = ShotValue(player.GridPosition, step, spawner, grid, growth, trait, proc,
                out MonsterMovement certainlyKilled);
            value -= 12f * Danger(player.GridPosition, spawner, certainlyKilled);
            if (value > best) { best = value; mode = PlayerActionSelectionMode.Attack; direction = step; }
        }
        if (player.IsChoosingEchoDirection) return;
        // 안전한 직격 기회는 실제 공격으로 사용한다. 회피 평가가 장거리 견제를 가리지 않는다.
        if (!player.IsChoosingFreeMove && best > 0f
            && Danger(player.GridPosition, spawner, null) < 0.75f) return;
        player.SelectMoveMode();
        var cells = (List<Vector3Int>)typeof(Move).GetField("selectableCells", PrivateInstance).GetValue(player);
        foreach (Vector3Int cell in cells.ToArray())
        {
            bool art = Mathf.Max(Mathf.Abs(cell.x - player.GridPosition.x),
                Mathf.Abs(cell.y - player.GridPosition.y)) > 1;
            bool cleansFire = art && (growth.ActiveMovementArt == PlayerMovementArt.Bishop
                || (growth.ActiveMovementArt == PlayerMovementArt.Knight && growth.MovementArtLevel >= 2));
            float danger = Danger(cell, spawner, null);
            if (art) danger = Mathf.Max(0f, danger - 0.6f * (growth.Stack("movement_breath")
                + (growth.Stack("rapid_cycle") > 0 ? 1 : 0)));
            float value = -1.5f - 12f * danger
                - (!cleansFire && spawner.FireTrail.HasFire(cell) ? 11f : 0f);
            // 단순 직선 도망은 같은 속도의 추적자를 영원히 떼지 못한다.
            // 이동 이후의 인접 압박까지 계산해 대각선 재배치/맞교환을 고른다.
            float nextPressure = 0f;
            foreach (MonsterMovement monster in spawner.ActiveMonsters)
            {
                if (monster == null || monster.IsDead || monster.IsActionBlocked) continue;
                Vector3Int predicted = PredictChasePosition(monster, cell, spawner, grid);
                Vector3Int delta = predicted - cell;
                if (monster.MovementPattern == MonsterMovementPattern.CardinalFour
                    && Math.Abs(delta.x) + Math.Abs(delta.y) == 1) nextPressure += 0.8f;
                if (monster.MovementPattern == MonsterMovementPattern.EightDirection
                    && Distance(predicted, cell) == 1) nextPressure += 0.8f;
            }
            value -= nextPressure * 10f;
            float bestShot = PredictedShotValue(cell, spawner, grid);
            value += bestShot * 0.4f;
            value -= recent.Count(previous => previous == cell) * 1.2f;
            if (art && (growth.HasRelic("ambush_crest") || growth.MovementArtLevel == 3)) value += 2f;
            RelicOffer[] desiredAltars = growth.ActiveAltars.Where(altar =>
                !request.avoidLegendary || altar.Relic.Rarity < RelicRarity.Legendary).ToArray();
            if (request.allowRelics && desiredAltars.Length > 0)
            {
                int oldDistance = desiredAltars.Min(altar => Distance(altar.Cell, player.GridPosition));
                int newDistance = desiredAltars.Min(altar => Distance(altar.Cell, cell));
                value += (oldDistance - newDistance) * 2.5f;
                if (newDistance == 0) value += 18f;
            }
            if (growth.ActiveAltars.Any(altar => altar.Cell == cell
                && (!request.allowRelics || (request.avoidLegendary
                    && altar.Relic.Rarity == RelicRarity.Legendary))))
                value -= 1000f;
            if (value > best) { best = value; mode = PlayerActionSelectionMode.Move;
                direction = cell - player.GridPosition; }
        }
        player.CancelSelection();
    }

    private static float ShotValue(Vector3Int origin, Vector3Int step, MonsterSpawner spawner,
        GridManager grid, RunProgressionSystem growth, string trait, float proc,
        out MonsterMovement certainlyKilled)
    {
        certainlyKilled = null;
        List<MonsterMovement> line = new List<MonsterMovement>();
        for (Vector3Int cell = origin + step; grid.IsInsideMapCell(cell); cell += step)
        {
            if (spawner.TryGetMonsterAtCell(cell, out MonsterMovement monster)) line.Add(monster);
            if (!grid.IsWalkableCell(cell)) break;
        }
        if (line.Count == 0) return -4f;
        int health = line[0].GetComponent<CharacterHealth>().CurrentHealth;
        float damage = 1f + (growth.HasRelic("glass_heart") ? 1 : 0);
        float expected = damage + (trait == "damage_boost" ? proc * (1 + Math.Min(1, growth.Stack("overcharge"))) : 0);
        float score = Mathf.Min(health, expected) * 4f;
        if (health <= damage) { certainlyKilled = line[0]; score += 10f; }
        if (trait == "double_cast") score += proc * 5f;
        if (trait == "pierce") score += proc * Math.Min(line.Count - 1,
            growth.HasRelic("infinite_orbit") ? 99 : 1 + growth.Stack("long_needle")) * 5f;
        if (trait == "knockback") score += proc * 3f;
        int neighbors = spawner.ActiveMonsters.Count(monster => monster != null && !monster.IsDead
            && monster != line[0] && Distance(monster.GridPosition, line[0].GridPosition) == 1);
        if (health <= damage || (trait == "damage_boost" && health <= expected + 1f))
            score += neighbors * (growth.Stack("mana_circulation")
                + (growth.Stack("rapid_cycle") > 0 ? 1 : 0)) * 2f;
        return score;
    }

    private static float Danger(Vector3Int cell, MonsterSpawner spawner, MonsterMovement ignored)
    {
        float damage = 0f;
        foreach (MonsterMovement monster in spawner.ActiveMonsters)
        {
            if (monster == null || monster.IsDead || monster == ignored || monster.IsActionBlocked) continue;
            Vector3Int delta = monster.GridPosition - cell;
            int distance = Distance(monster.GridPosition, cell);
            if (monster.MovementPattern == MonsterMovementPattern.CardinalFour)
                damage += Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1 ? 1f : 0f;
            if (monster.MovementPattern == MonsterMovementPattern.EightDirection)
                damage += distance == 1 ? 1f : 0f;
            if (monster.MovementPattern == MonsterMovementPattern.Knight)
            {
                // 공개된 L자 패턴만 이용한다. 내부 예약/셔플 백/미래 RNG를 읽지 않는다.
                if (monster.GetComponent<ChessMonsterBehaviour>().WillKnightJumpNextTurn
                    && (Math.Abs(delta.x) <= 3 && Math.Abs(delta.y) <= 3)
                    && (Math.Abs(delta.x) + Math.Abs(delta.y) == 2
                        || Math.Abs(delta.x) + Math.Abs(delta.y) == 4)) damage += 0.8f;
            }
            ChessMonsterBehaviour behaviour = monster.GetComponent<ChessMonsterBehaviour>();
            if (behaviour != null && behaviour.IsRookCharged && behaviour.RookTargetColumn == cell.x)
                damage += 2f;
            if (monster.MovementPattern != MonsterMovementPattern.Rook)
                damage += Mathf.Max(0, 4 - distance) * 0.035f;
        }
        return damage;
    }

    private static float PredictedShotValue(Vector3Int cell, MonsterSpawner spawner, GridManager grid)
    {
        List<Vector3Int> positions = new List<Vector3Int>();
        foreach (MonsterMovement monster in spawner.ActiveMonsters)
        {
            if (monster == null || monster.IsDead) continue;
            Vector3Int predicted = PredictChasePosition(monster, cell, spawner, grid);
            positions.Add(predicted);
        }
        return Directions.Max(step =>
        {
            int count = 0;
            for (Vector3Int next = cell + step; grid.IsInsideMapCell(next); next += step)
            {
                if (positions.Contains(next)) count++;
                if (!grid.IsWalkableCell(next)) break;
            }
            return count > 0 ? 4f + Math.Min(3, count - 1) * 2f : -4f;
        });
    }

    private static Vector3Int PredictChasePosition(MonsterMovement monster, Vector3Int cell,
        MonsterSpawner spawner, GridManager grid)
    {
        Vector3Int predicted = monster.GridPosition;
        if (monster.IsActionBlocked || (monster.MovementPattern != MonsterMovementPattern.CardinalFour
            && monster.MovementPattern != MonsterMovementPattern.EightDirection)) return predicted;
        bool cardinal = monster.MovementPattern == MonsterMovementPattern.CardinalFour;
        int Metric(Vector3Int location) => cardinal
            ? Math.Abs(location.x - cell.x) + Math.Abs(location.y - cell.y) : Distance(location, cell);
        if (Metric(predicted) <= 1) return predicted;
        Vector3Int[] choices = Directions.Where(step => !cardinal || step.x == 0 || step.y == 0)
            .Select(step => monster.GridPosition + step)
            .Where(next => grid.IsWalkableCell(next) && next != cell
                && !spawner.TryGetMonsterAtCell(next, out _)).ToArray();
        return choices.Length > 0 ? choices.OrderBy(Metric).First() : predicted;
    }

    private static int Distance(Vector3Int a, Vector3Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.checks.Add(message);
    }

    private static IEnumerator Mechanics()
    {
        yield return null;
        Move player = UnityEngine.Object.FindAnyObjectByType<Move>();
        RunProgressionSystem growth = player.GetComponent<RunProgressionSystem>();
        MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        growth.TryInitialize();
        spawner.enabled = false;
        string pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
            ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName : "BuiltIn";
        report.checks.Add("pipeline detection: " + pipeline + " (rendering unchanged)");
        TextureImporter sheet = AssetImporter.GetAtPath("Assets/Monsters/Armadillo/armadillo_sheet.png")
            as TextureImporter;
        Check(sheet != null && sheet.spritePixelsPerUnit == 32f && sheet.filterMode == FilterMode.Point,
            "armadillo remains 32 PPU / Point");
        var stacks = (Dictionary<string, int>)typeof(RunProgressionSystem)
            .GetField("upgradeStacks", PrivateInstance).GetValue(growth);
        void Apply(string id)
        {
            stacks[id] = growth.Stack(id) + 1;
            typeof(RunProgressionSystem).GetMethod("ApplyImmediateUpgrade", PrivateInstance)
                .Invoke(growth, new object[] { id });
        }
        PlayerTraitSystem trait = player.GetComponent<PlayerTraitSystem>();
        trait.SelectTrait("damage_boost", 4);
        trait.RollBasicAttack();
        int bagIndex = (int)typeof(PlayerTraitSystem).GetField("nextBagIndex", PrivateInstance).GetValue(trait);
        Apply("life_tempering");
        Check((int)typeof(PlayerTraitSystem).GetField("nextBagIndex", PrivateInstance).GetValue(trait) == bagIndex,
            "unrelated upgrade preserves partially consumed activation bag");
        Apply("dense_mana");
        Check((int)typeof(PlayerTraitSystem).GetField("nextBagIndex", PrivateInstance).GetValue(trait) == bagIndex,
            "frequency upgrade preserves current bag until next refill");
        growth.SetMovementArtForPlaytest(PlayerMovementArt.Knight, 3);
        Apply("movement_bishop");
        Check(growth.ActiveMovementArt == PlayerMovementArt.Bishop && growth.MovementArtLevel == 3,
            "movement replacement preserves mastery");
        typeof(RunProgressionSystem).GetMethod("BuildAdvancedUpgradeOptions", PrivateInstance)
            .Invoke(growth, new object[] { 4 });
        Check(growth.CurrentUpgradeOptions.Count == 3
            && !growth.CurrentUpgradeOptions.Any(item => item.Id.StartsWith("movement_training")),
            "maxed movement training replaced with applicable card");
        stacks["trait_mastery"] = 1;
        growth.BeginAttack(new PlayerAttackTraitRoll(false, false, true, false));
        Check(growth.ModifyAttackDamage(1, new PlayerAttackTraitRoll(false, false, true, false), 0) == 2,
            "damage mastery uses area damage rather than redundant bag reduction");
        stacks["overcharge"] = 2;
        Check(growth.ModifyAttackDamage(1, new PlayerAttackTraitRoll(false, false, true, false), 0) == 3,
            "overcharge second stack keeps direct damage 3 and adds area effect");
        stacks["healing_breath"] = 2;
        Check(growth.WaveHealFlatBonus == 2, "both healing stacks have integer marginal value");
        spawner.ResetPlaytestEncounter();
        yield return null;
        GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        Vector3Int center = player.GridPosition;
        player.GetComponent<CharacterHealth>().Initialize(10);
        Sprite sprite = player.GetComponent<SpriteRenderer>().sprite;
        MonsterMovement Spawn(Vector3Int cell, int order) => spawner.SpawnPlaytestMonster(
            MonsterMovementPattern.CardinalFour, cell, sprite, Color.white, 20, order, 1, 4, 2);
        MonsterMovement first = Spawn(center + Vector3Int.right * 2, 100);
        MonsterMovement second = Spawn(center + Vector3Int.right * 3, 101);
        MonsterMovement neighbor = Spawn(center + new Vector3Int(2, 1, 0), 102);
        stacks.Clear(); stacks["pierce_recovery"] = 2;
        var pierce = new PlayerAttackTraitRoll(false, true, false, false);
        growth.BeginAttack(pierce);
        growth.NotifyAttackHit(first, pierce, 0, 1);
        growth.NotifyAttackHit(second, pierce, 0, 2);
        Check(neighbor.GetComponent<CharacterHealth>().CurrentHealth == 18,
            "overlapping pierce-wave centers damage the same target only once");
        stacks.Clear(); stacks["impact_recovery"] = 1;
        first.TakeDamage(100);
        growth.BeginAttack(new PlayerAttackTraitRoll(false, false, false, true));
        growth.NotifyKnockbackResult(first, null, first.GridPosition);
        Check(neighbor.IsActionBlocked || second.IsActionBlocked, "one-shot knockback keeps adjacent control");
        yield return null;

        stacks.Clear(); stacks["trait_mastery"] = 1;
        var doubleCast = new PlayerAttackTraitRoll(true, false, false, false);
        growth.BeginAttack(doubleCast);
        Check(growth.ModifyAttackDamage(1, doubleCast, 0) == 1
            && growth.ModifyAttackDamage(1, doubleCast, 1) == 2,
            "double mastery boosts additional casts only");
        growth.BeginAttack(pierce);
        Check(growth.ModifyPenetrationDamage(1, pierce, 0) == 1
            && growth.ModifyPenetrationDamage(1, pierce, 1) == 2,
            "pierce mastery boosts additional targets only");

        var heldRelics = (List<RelicDefinition>)typeof(RunProgressionSystem)
            .GetField("relics", PrivateInstance).GetValue(growth);
        void Relic(string id) => typeof(RunProgressionSystem).GetMethod("AddRelic", PrivateInstance)
            .Invoke(growth, new object[] { RunProgressionCatalog.Relic(id) });
        void Flag(string name, bool value) => typeof(RunProgressionSystem)
            .GetField(name, PrivateInstance).SetValue(growth, value);
        spawner.ResetPlaytestEncounter();
        yield return null;
        first = Spawn(center + Vector3Int.right * 2, 103);
        second = Spawn(center + Vector3Int.right * 3, 104);
        neighbor = Spawn(center + new Vector3Int(2, 1, 0), 105);
        var boost = new PlayerAttackTraitRoll(false, false, true, false);
        growth.BeginAttack(boost);
        growth.NotifyAttackHit(first, boost, 0, 0);
        Check(second.GetComponent<CharacterHealth>().CurrentHealth == 19
            && neighbor.GetComponent<CharacterHealth>().CurrentHealth == 19,
            "damage mastery hits both neighboring targets once");
        var knockback = new PlayerAttackTraitRoll(false, false, false, true);
        growth.BeginAttack(knockback);
        growth.NotifyKnockbackResult(first, null, first.GridPosition);
        Check(spawner.ActiveMonsters.Count(item => item.IsActionBlocked) == 2,
            "knockback mastery blocks two distinct living targets");

        stacks.Clear(); heldRelics.Clear();
        Relic("glass_heart");
        Check(player.GetComponent<CharacterHealth>().MaxHealth == 8,
            "glass heart costs two maximum health");
        first.GetComponent<CharacterHealth>().Initialize(3);
        growth.BeginAttack(default);
        Check(growth.ModifyAttackDamage(1, default, 0) == 2,
            "glass heart boosts non-proc direct damage");
        first.TakeDamage(8);
        growth.NotifyAttackHit(first, default, 0, 0);
        int totalOverflowDamage = 38 - second.GetComponent<CharacterHealth>().CurrentHealth
            - neighbor.GetComponent<CharacterHealth>().CurrentHealth;
        Check(totalOverflowDamage == 2, "glass overflow caps at two damage to one neighbor");
        int secondAfterOverflow = second.GetComponent<CharacterHealth>().CurrentHealth;
        int neighborAfterOverflow = neighbor.GetComponent<CharacterHealth>().CurrentHealth;
        growth.NotifyAttackHit(first, default, 0, 0);
        Check(second.GetComponent<CharacterHealth>().CurrentHealth == secondAfterOverflow
            && neighbor.GetComponent<CharacterHealth>().CurrentHealth == neighborAfterOverflow,
            "glass overflow does not repeat in one action");

        spawner.ResetPlaytestEncounter();
        yield return null;
        first = Spawn(center + Vector3Int.right * 2, 106);
        second = Spawn(center + Vector3Int.right * 3, 107);
        neighbor = Spawn(center + Vector3Int.right * 4, 108);
        first.GetComponent<CharacterHealth>().Initialize(3);
        second.GetComponent<CharacterHealth>().Initialize(1);
        stacks["mana_circulation"] = 1;
        growth.BeginAttack(default);
        first.TakeDamage(5);
        growth.NotifyAttackHit(first, default, 0, 0);
        Check(second.IsDead && neighbor.GetComponent<CharacterHealth>().CurrentHealth == 20,
            "overflow kill neither chains nor triggers a new kill explosion");

        stacks.Clear(); heldRelics.Clear();
        player.GetComponent<CharacterHealth>().Initialize(10);
        Relic("ash_boots"); Relic("landing_ward"); Relic("vanguard_shield");
        Flag("firstHazardBlocked", false); Flag("firstDamageBlocked", false);
        growth.NotifyMovementArtUsed();
        Check(growth.ModifyIncomingDamage(1, PlayerDamageKind.Hazard) == 0
            && growth.ModifyIncomingDamage(1, PlayerDamageKind.Normal) == 0
            && growth.ModifyIncomingDamage(1, PlayerDamageKind.Normal) == 0
            && growth.ModifyIncomingDamage(1, PlayerDamageKind.Normal) == 1,
            "hazard immunity and two guards are consumed on separate positive hits");

        heldRelics.Clear(); Relic("ambush_crest"); Relic("excess_reservoir");
        growth.SetMovementArtForPlaytest(PlayerMovementArt.Knight, 3);
        growth.NotifyMovementArtUsed(); growth.NotifyMovementArtUsed();
        growth.BeginAttack(boost);
        Check(growth.ModifyAttackDamage(1, boost, 0) == 4,
            "knight and ambush charges stack but repeated movement does not multiply them");
        growth.NotifyDirectKill(first, boost, 0);
        growth.NotifyMovementArtUsed();
        growth.BeginAttack(default);
        Check(growth.ModifyAttackDamage(1, default, 0) == 4,
            "three distinct stored damage sources sum on the first direct hit");
        growth.NotifyAttackHit(neighbor, default, 0, 0);
        Check(growth.ModifyAttackDamage(1, default, 1) == 1,
            "stored damage is not copied to later casts");

        heldRelics.Clear(); Relic("blood_knot");
        Flag("firstKillHealed", false);
        player.GetComponent<CharacterHealth>().TakeDamage(5);
        growth.BeginAttack(default);
        growth.NotifyDirectKill(first, default, 0);
        growth.NotifyDirectKill(first, default, 0);
        Check(player.CurrentHealth == 7, "blood knot heals two only once per wave and victim");
        heldRelics.Clear(); Relic("suture_needle");
        second = Spawn(center + Vector3Int.up * 2, 109);
        typeof(RunProgressionSystem).GetField("stitchHeals", PrivateInstance).SetValue(growth, 0);
        for (int i = 0; i < 3; i++)
        {
            growth.BeginAttack(pierce);
            growth.NotifyAttackHit(neighbor, pierce, 0, 0);
            growth.NotifyAttackHit(second, pierce, 0, 1);
            growth.CompleteAttack(pierce);
        }
        Check(player.CurrentHealth == 9, "suture needle heals at most twice per wave");

        heldRelics.Clear();
        for (int seed = 0; seed < 128; seed++)
        {
            UnityEngine.Random.InitState(seed);
            var shown = (HashSet<string>)typeof(RunProgressionSystem)
                .GetField("shownRelics", PrivateInstance).GetValue(growth);
            shown.Clear();
            typeof(RunProgressionSystem).GetMethod("SpawnRelicAltars", PrivateInstance)
                .Invoke(growth, new object[] { 16 });
            Check(growth.ActiveAltars.Count == 2
                && growth.ActiveAltars.All(item => item.Relic.Rarity >= RelicRarity.Rare),
                "wave 16 altar minimum rare seed=" + seed);
        }
        var shownRelics = (HashSet<string>)typeof(RunProgressionSystem)
            .GetField("shownRelics", PrivateInstance).GetValue(growth);
        shownRelics.Clear();
        foreach (RelicDefinition item in RunProgressionCatalog.Relics.Where(item => item.Rarity < RelicRarity.Legendary))
            shownRelics.Add(item.Id);
        typeof(RunProgressionSystem).GetMethod("SpawnRelicAltars", PrivateInstance)
            .Invoke(growth, new object[] { 16 });
        Check(growth.ActiveAltars.Count == 2
            && growth.ActiveAltars.All(item => item.Relic.Rarity == RelicRarity.Legendary),
            "exhausted rare pool falls back to legendary rather than common");

        void Build(string id, PlayerMovementArt art, int level, params (string Id, int Count)[] pieces)
        {
            stacks.Clear(); heldRelics.Clear();
            foreach (var piece in pieces) stacks[piece.Id] = piece.Count;
            trait.SelectTrait(id, id == "double_cast" ? 5 : id == "pierce" ? 3 : 4);
            growth.SetMovementArtForPlaytest(art, level);
            typeof(RunProgressionSystem).GetMethod("HandleWaveStarted", PrivateInstance)
                .Invoke(growth, new object[] { 1 });
            int normal = pieces.Where(piece => !RunProgressionCatalog.Upgrade(piece.Id).IsAdvanced)
                .Sum(piece => piece.Count);
            int advanced = pieces.Where(piece => RunProgressionCatalog.Upgrade(piece.Id).IsAdvanced)
                .Sum(piece => piece.Count);
            Check(normal == 6 && advanced == 4 && pieces.All(piece =>
                piece.Count <= RunProgressionCatalog.Upgrade(piece.Id).MaxStacks),
                id + " final build uses legal six normal / four advanced choices");
        }
        Build("double_cast", PlayerMovementArt.Knight, 3,
            ("quick_cast", 2), ("movement_knight", 1), ("echo_warhead", 1), ("echo_recovery", 1),
            ("mana_circulation", 1), ("movement_training_1", 1), ("movement_training_2", 1),
            ("alternating_overload", 1), ("trait_mastery", 1));
        growth.BeginAttack(doubleCast);
        Check(trait.ActivationBagSize == 3 && growth.AttackCastCount(doubleCast) == 2
            && growth.ModifyAttackDamage(1, doubleCast, 1) == 3 && growth.Stack("echo_warhead") == 1,
            "double final build: bag three, two casts, extra direct damage three and one extra penetration");
        Build("pierce", PlayerMovementArt.Bishop, 2,
            ("thin_reload", 1), ("movement_knight", 1), ("long_needle", 1), ("pierce_recovery", 2),
            ("mana_circulation", 1), ("rapid_cycle", 1), ("movement_bishop", 1),
            ("movement_training_3", 1), ("trait_mastery", 1));
        Check(trait.ActivationBagSize == 2 && growth.ModifyPenetrations(pierce) == 2
            && growth.ModifyPenetrationDamage(1, pierce, 1) == 2,
            "pierce final build: bag two, three direct targets and extra target damage two");
        Build("damage_boost", PlayerMovementArt.Bishop, 2,
            ("overcharge", 1), ("movement_knight", 1), ("dense_mana", 2), ("afterglow_recovery", 1),
            ("mana_circulation", 1), ("rapid_cycle", 1), ("movement_bishop", 1),
            ("movement_training_3", 1), ("trait_mastery", 1));
        spawner.ResetPlaytestEncounter();
        yield return null;
        first = Spawn(center + Vector3Int.right * 2, 110);
        neighbor = Spawn(center + new Vector3Int(2, 1, 0), 111);
        first.GetComponent<CharacterHealth>().Initialize(3);
        growth.BeginAttack(boost);
        int directDamage = growth.ModifyAttackDamage(1, boost, 0);
        first.TakeDamage(directDamage);
        growth.NotifyAttackHit(first, boost, 0, 0);
        Check(trait.ActivationBagSize == 2 && directDamage == 3
            && neighbor.GetComponent<CharacterHealth>().CurrentHealth == 17,
            "damage final build: bag two, direct three plus distinct kill/mastery area three");
        Build("knockback", PlayerMovementArt.Knight, 2,
            ("compressed_impact", 1), ("movement_knight", 1), ("long_impact", 1), ("impact_recovery", 2),
            ("mana_circulation", 1), ("rapid_cycle", 1), ("movement_training_2", 1),
            ("alternating_overload", 1), ("trait_mastery", 1));
        spawner.ResetPlaytestEncounter();
        yield return null;
        first = Spawn(center + Vector3Int.right * 2, 112);
        Vector3Int[] adjacent = { Vector3Int.right, Vector3Int.up,
            new Vector3Int(-1, 1, 0), new Vector3Int(1, 1, 0), Vector3Int.down };
        foreach (Vector3Int offset in adjacent) Spawn(first.GridPosition + offset, 113 + Array.IndexOf(adjacent, offset));
        growth.BeginAttack(knockback);
        growth.NotifyKnockbackResult(first, null, first.GridPosition);
        Check(trait.ActivationBagSize == 3 && growth.KnockbackDistance == 2
            && growth.ModifyAttackDamage(1, knockback, 0) == 2
            && spawner.ActiveMonsters.Count(item => item.IsActionBlocked) == 5,
            "knockback final build: bag three, direct two, push two and five distinct controlled targets");
        yield return null;
    }
}
