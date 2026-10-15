using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 실제 SampleScene에서 착지·입력·피해·턴·충전의 연결을 검증한다.
[InitializeOnLoad]
public static class KnightCounterValidation
{
    private const string Pending = "GridCaster.KnightCounterValidation";
    private static readonly List<string> checks = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static bool previousRunInBackground;
    private static string runtimeError;

    static KnightCounterValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            checks.Clear(); frame = -1; runtimeError = null;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            // 공격·착지 모션 대기(약 0.16초씩)가 들어가 1920×1080 기준 약 190초 걸린다.
            deadline = EditorApplication.timeSinceStartup + 300;
            Application.logMessageReceived += Error;
            routine = Flatten(Run()); EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Knight Counter Opportunity")]
    public static void RunBatch()
    {
        if (Application.isBatchMode)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("검사 전에 Play를 종료하고 씬을 저장하세요.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("원본 SampleScene을 열어 검사하세요.");
        CombatAudioSetupTool.Configure();
        foreach (string key in new[] { GameAudio.SfxVolumeKey, GameAudio.MusicVolumeKey })
        {
            SessionState.SetBool(Pending + key, PlayerPrefs.HasKey(key));
            SessionState.SetFloat(Pending + key, PlayerPrefs.GetFloat(key));
        }
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void Error(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(KnightCounterValidation))))
            runtimeError = message + "\n" + stack;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("나이트 반격 검사 시간 초과/중단");
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!routine.MoveNext()) Finish(true);
        }
        catch (Exception error) { checks.Add(error.ToString()); Finish(false); }
    }

    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Error;
        Application.runInBackground = previousRunInBackground;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        foreach (string key in new[] { GameAudio.SfxVolumeKey, GameAudio.MusicVolumeKey })
        {
            if (SessionState.GetBool(Pending + key, false)) PlayerPrefs.SetFloat(key, SessionState.GetFloat(Pending + key, 0f));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        // OnDisable 시 검사 중 볼륨을 다시 저장하지 않도록 원래 값을 복원한다.
        GameAudio audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
        if (audio != null)
        {
            audio.SetSfxVolume(PlayerPrefs.GetFloat(GameAudio.SfxVolumeKey, GameAudio.DefaultSfxVolume));
            audio.SetMusicVolume(PlayerPrefs.GetFloat(GameAudio.MusicVolumeKey, GameAudio.DefaultMusicVolume));
        }
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory);
        string report = "KNIGHT_COUNTER_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled actual SampleScene fixtures; gameplay-rule validation, not human fun assessment.\n"
            + "Unity " + Application.unityVersion + "; " + DateTime.Now.ToString("O") + "\n" + string.Join("\n", checks);
        File.WriteAllText(Path.Combine(directory, "knight-counter-2026-10-04.txt"), report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }

    private static Move player;
    private static MonsterSpawner spawner;
    private static GridManager grid;
    private static PlayerTraitSystem trait;
    private static RunProgressionSystem progression;
    private static MonsterMovement knight;
    private static ChessMonsterBehaviour pattern;
    private static int worldStarts, worldCompletions;
    private static readonly Vector3Int[] Directions =
    {
        Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down,
        new Vector3Int(1,1,0), new Vector3Int(-1,1,0),
        new Vector3Int(-1,-1,0), new Vector3Int(1,-1,0)
    };
    private static readonly Vector3Int[] KnightSteps =
    {
        new Vector3Int(2,1,0), new Vector3Int(1,2,0),
        new Vector3Int(-1,2,0), new Vector3Int(-2,1,0),
        new Vector3Int(-2,-1,0), new Vector3Int(-1,-2,0),
        new Vector3Int(1,-2,0), new Vector3Int(2,-1,0)
    };
    private static IEnumerator Flatten(IEnumerator source)
    {
        while (source.MoveNext())
        {
            if (source.Current is IEnumerator nested)
            {
                IEnumerator child = Flatten(nested);
                while (child.MoveNext()) yield return child.Current;
            }
            else yield return source.Current;
        }
    }
    private static MonsterMovement Spawn(MonsterMovementPattern kind, Vector3Int cell, int hp = 20, int interval = 2)
        => spawner.SpawnPlaytestMonster(kind, cell,
            player.GetComponent<SpriteRenderer>().sprite, Color.white, hp, 0, interval, 4, 2);
    private static int Health(MonsterMovement target) => target.GetComponent<CharacterHealth>().CurrentHealth;
    private static void Trait(string id, bool ready = false)
    {
        // 런 시작 UI를 재진입하지 않고 각 공격 특성의 최소 상태만 구성한다.
        typeof(PlayerTraitSystem).GetField("selectedTraitId", Private).SetValue(trait, id);
        trait.SetActivationBagSize(5);
        trait.ResetCharge();
        if (ready) while (!trait.IsReady) trait.RegisterCompletedAttack(true, false);
    }
    private static IEnumerator Land(Vector3Int destination, int hp = 20, int interval = 2)
    {
        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(Vector3Int.zero);
        progression.SetMovementArtForPlaytest(PlayerMovementArt.None);
        ((Dictionary<string, int>)typeof(RunProgressionSystem).GetField("upgradeStacks", Private)
            .GetValue(progression)).Clear();
        Trait("double_cast");
        Vector3Int origin = KnightSteps.Select(step => destination + step)
            .Where(cell => grid.IsWalkableCell(cell))
            .OrderByDescending(cell => Mathf.Abs(cell.x) + Mathf.Abs(cell.y)).First();
        knight = Spawn(MonsterMovementPattern.Knight, origin, hp, interval);
        pattern = knight.GetComponent<ChessMonsterBehaviour>();
        int beforeHp = player.CurrentHealth;
        bool landed = false;
        knight.TakeTurn(() => landed = true, (_, target) => target == destination,
            cell => cell != destination);
        Check(knight.GridPosition == destination && !landed && !pattern.IsKnightCounterVulnerable
            && player.CurrentHealth == beforeHp, "reserved landing is not vulnerable or damaging before arrival " + destination);
        while (!landed) yield return null;
        Check(pattern.IsKnightCounterVulnerable && pattern.WillKnightJumpNextTurn == (interval == 1),
            "landing opens vulnerability while preserving next-turn intent " + destination);
        int crossDamage = Mathf.Abs(destination.x) + Mathf.Abs(destination.y) == 1 ? 1 : 0;
        Check(player.CurrentHealth == beforeHp - crossDamage,
            "original cardinal cross damage remains; diagonal landing is safe " + destination);
    }
    private static IEnumerator CompleteAction(int starts, int completions, string name)
    {
        while (player.IsActionInProgress)
        {
            if (player.IsChoosingEchoDirection)
                Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.right),
                    name + " reaim remains within the same attack action");
            yield return null;
        }
        Check(worldStarts == starts + 1 && worldCompletions == completions + 1,
            name + " advances enemies exactly once");
    }
    private static IEnumerator Attack(Vector3Int direction, bool special = false)
    {
        int starts = worldStarts, completions = worldCompletions;
        if (special) Check(player.SelectTraitAttackMode(), "ready trait attack selectable");
        else player.SelectAttackMode();
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, direction), "attack accepted " + direction);
        Check(!pattern.IsKnightCounterVulnerable, "accepted attack immediately consumes the window");
        yield return CompleteAction(starts, completions, "attack");
    }
    private static IEnumerator Run()
    {
        yield return null; yield return null; yield return null;
        PlaySessionRecorder.MarkControlledValidation();
        player = UnityEngine.Object.FindAnyObjectByType<Move>();
        spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        trait = player.GetComponent<PlayerTraitSystem>();
        progression = player.GetComponent<RunProgressionSystem>();
        UnityEngine.Object.FindAnyObjectByType<GameHudController>().SelectTrait(0);
        yield return null;
        GameObject.Find("UpgradeChoice_1").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Time.captureDeltaTime = 1f / 120f;
        player.MoveCompleted += () => worldStarts++;
        spawner.WorldTurnCompleted += () => worldCompletions++;

        foreach (int interval in new[] { 2, 1 })
        {
            yield return Land(new Vector3Int(1,1,0), 20, interval);
            Canvas.ForceUpdateCanvases();
            Text status = (Text)typeof(GameHudController).GetField("actionModeText", Private)
                .GetValue(UnityEngine.Object.FindAnyObjectByType<GameHudController>());
            string description = EnemyThreatPreview.CollectCells(knight, grid, new HashSet<Vector3Int>());
            float height = new TextGenerator().GetPreferredHeight(description,
                status.GetGenerationSettings(status.rectTransform.rect.size)) / status.pixelsPerUnit;
            Check(height <= status.rectTransform.rect.height,
                "counter explanation and next-turn intent fit existing PC status text, interval " + interval);
        }

        foreach (Vector3Int direction in Directions)
        {
            yield return Land(direction);
            var cells = new HashSet<Vector3Int>();
            Check(pattern.StatusText.Contains("첫 직격 +1")
                && EnemyThreatPreview.CollectCells(knight, grid, cells).Contains("8방향 인접"),
                "vulnerability and next-turn danger are explained in public state");
            player.SelectAttackMode();
            player.CancelSelection();
            Check(!player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.zero)
                && pattern.IsKnightCounterVulnerable, "cancel and invalid attack preserve opportunity");
            Check(!player.TryPerformAction(PlayerActionSelectionMode.Move, direction)
                && pattern.IsKnightCounterVulnerable, "occupied move preserves opportunity");
            player.SetInspectionOpen(true);
            Check(!player.TryPerformAction(PlayerActionSelectionMode.Attack, direction)
                && pattern.IsKnightCounterVulnerable, "inspection blocks action without consuming opportunity");
            player.SetInspectionOpen(false);
            yield return null;
            player.SetInputEnabled(false);
            yield return null;
            Check(pattern.IsKnightCounterVulnerable, "disabled input does not consume opportunity");
            player.SetInputEnabled(true);
            int hp = Health(knight);
            yield return Attack(direction);
            Check(Health(knight) == hp - 2 && trait.ChargeProgress == 1,
                "adjacent first normal hit is 1+1 with one charge " + direction);
        }

        yield return Land(Vector3Int.right);
        int initialHp = Health(knight);
        int a = worldStarts, b = worldCompletions;
        Check(player.TryPerformAction(PlayerActionSelectionMode.Move, Vector3Int.left)
            && !pattern.IsKnightCounterVulnerable, "valid basic movement consumes opportunity immediately");
        yield return CompleteAction(a, b, "move");
        Check(Health(knight) == initialHp && !pattern.IsKnightCounterVulnerable && trait.ChargeProgress == 0,
            "move grants no damage or charge and preparation does not reopen window");
        yield return Attack(Vector3Int.right);
        Check(Health(knight) == initialHp - 1, "opportunity cannot be saved until an attack after movement");

        yield return Land(Vector3Int.right);
        initialHp = Health(knight);
        yield return Attack(Vector3Int.up);
        Check(Health(knight) == initialHp && trait.ChargeProgress == 0,
            "miss consumes opportunity without damage or charge");
        yield return Attack(Vector3Int.right);
        Check(Health(knight) == initialHp - 1, "missed opportunity does not increase later damage");

        yield return Land(new Vector3Int(1,-1,0));
        var other = Spawn(MonsterMovementPattern.CardinalFour, Vector3Int.left);
        initialHp = Health(knight);
        yield return Attack(Vector3Int.left);
        Check(Health(other) == 19 && Health(knight) == initialHp,
            "attacking another enemy consumes opportunity with ordinary damage");

        yield return Land(new Vector3Int(3,0,0));
        yield return Attack(Vector3Int.right);
        Check(Health(knight) == 19, "ranged hit on vulnerable Knight receives no bonus");

        yield return Land(Vector3Int.right);
        progression.SetMovementArtForPlaytest(PlayerMovementArt.Knight);
        player.SelectMoveMode();
        a = worldStarts; b = worldCompletions;
        Check(player.TryUseMovementArtAtCell(new Vector3Int(-2,1,0))
            && !pattern.IsKnightCounterVulnerable, "movement art consumes opportunity");
        yield return CompleteAction(a, b, "movement art");
        Check(trait.ChargeProgress == 0, "movement art receives no attack charge");

        foreach (string id in new[] { "double_cast", "pierce", "damage_boost", "knockback" })
        {
            yield return Land(Vector3Int.right);
            MonsterMovement behind = id == "pierce" ? Spawn(MonsterMovementPattern.CardinalFour, new Vector3Int(3,0,0)) : null;
            Trait(id, true);
            yield return Attack(Vector3Int.right, true);
            int expected = id == "double_cast" || id == "damage_boost" ? 3 : 2;
            Check(Health(knight) == 20 - expected && trait.ChargeProgress == 0,
                id + " adds vulnerability once and does not recharge a trait attack");
            if (behind != null) Check(Health(behind) == 19, "later penetration target receives no vulnerability bonus");
            if (id == "knockback") Check(knight.GridPosition == new Vector3Int(2,0,0),
                "counter damage preserves existing logical knockback");
        }

        yield return Land(Vector3Int.right);
        knight.SkipTurns(1);
        a = worldStarts; b = worldCompletions;
        Check(player.TryPerformAction(PlayerActionSelectionMode.Move, Vector3Int.left), "move before blocked Knight accepted");
        yield return CompleteAction(a, b, "blocked turn");
        Check(!pattern.IsKnightCounterVulnerable && Health(knight) == 20,
            "blocked Knight does not keep the consumed window");

        yield return Land(Vector3Int.right, 2);
        bool defeated = false;
        knight.GetComponent<CharacterHealth>().Died += health => defeated = health.CurrentHealth == 0;
        var survivor = Spawn(MonsterMovementPattern.CardinalFour, new Vector3Int(5,5,0));
        survivor.SkipTurns(5);
        yield return Attack(Vector3Int.right);
        Check(defeated && !spawner.TryGetMonsterAtCell(Vector3Int.right, out _)
            && trait.ChargeProgress == 1, "lethal counter frees occupancy and charges once");

        yield return Land(new Vector3Int(1,1,0));
        knight.TakeDamage(1);
        Check(Health(knight) == 19 && pattern.IsKnightCounterVulnerable,
            "non-player direct damage does not automatically retaliate or apply bonus");
        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(Vector3Int.zero);
        knight = Spawn(MonsterMovementPattern.Knight, new Vector3Int(2,1,0));
        pattern = knight.GetComponent<ChessMonsterBehaviour>();
        Check(!pattern.IsKnightCounterVulnerable, "newly spawned Knight has no stale counter state");
        knight.TakeTurn(() => {}, (_, __) => false, _ => true);
        Check(!pattern.IsKnightCounterVulnerable && pattern.PatternExecutions == 0,
            "failed jump opens no vulnerability");
    }
}
