using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 선택적으로 켜는 Editor 전용 관찰 도구다. 게임 판정/입력/RNG에는 관여하지 않는다.
// 기록은 이 컴퓨터에만 저장하며 웹 업로드나 제품 빌드의 텔레메트리는 만들지 않는다.
[InitializeOnLoad]
public static class PlaySessionRecorder
{
    private const string ArmedKey = "GridCaster.PlaySessionRecorder.Armed";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Move player;
    private static MonsterSpawner spawner;
    private static RunProgressionSystem growth;
    private static PlayerTraitSystem trait;
    private static Report report;
    private static string outputPath;
    private static string pendingMovement;
    private static Vector3Int previousCell;
    private static int worldTurns;
    private static bool exitingPlay;
    private static double startedAt;

    [Serializable]
    public sealed class Entry
    {
        public string eventKind;
        public int frame;
        public double elapsedSeconds;
        public int wave;
        public int worldTurn;
        public string action;
        public Vector3Int playerCell;
        public Vector3Int attackOrigin;
        public Vector3Int direction;
        public int casts;
        public int directHits;
        public bool usedTrait;
        public bool bonusStep;
        public int health;
        public int charge;
        public string movementArt;
        public int movementLevel;
        public string[] upgrades;
        public string[] relics;
        public string[] enemies;
        public int fireCount;
    }

    [Serializable]
    public sealed class Report
    {
        public string recordedAt;
        public string unityVersion;
        public string evidenceKind = "Observed Editor session; not a fun score, human study, or inferred intent.";
        public string deathCause = "Not inferred. Review the board and add a human note if needed.";
        public string selectedTrait;
        public string ending;
        public int ordinaryAttacks;
        public int specialAttacks;
        public int basicMoves;
        public int artMoves;
        public int bonusSteps;
        public int worldTurns;
        public List<string> diagnostics = new List<string>();
        public List<Entry> entries = new List<Entry>();
    }

    public static bool IsRecording => report != null;
    public static string OutputPath => outputPath;
    public static int OrdinaryAttackCount => report != null ? report.ordinaryAttacks : 0;
    public static int SpecialAttackCount => report != null ? report.specialAttacks : 0;

    public static void MarkControlledValidation()
    {
        if (report != null) report.evidenceKind = "Controlled validation fixtures; NOT a human play session.";
    }

    static PlaySessionRecorder()
    {
        EditorApplication.update += AttachWhenReady;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) exitingPlay = false;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                exitingPlay = true;
                Finish("play_stopped");
            }
        };
        AssemblyReloadEvents.beforeAssemblyReload += () => Finish("assembly_reload");
    }

    [MenuItem("Tools/Playtest/Record Local Play Sessions")]
    public static void ToggleRecording()
    {
        bool armed = !SessionState.GetBool(ArmedKey, false);
        SessionState.SetBool(ArmedKey, armed);
        Menu.SetChecked("Tools/Playtest/Record Local Play Sessions", armed);
        if (!armed) Finish("recording_disabled");
        Debug.Log(armed ? "로컬 플레이 기록 켜짐: SampleScene Play에서 자동 기록합니다."
            : "로컬 플레이 기록 꺼짐. 기존 기록은 삭제하지 않습니다.");
    }

    private static void AttachWhenReady()
    {
        if (exitingPlay || !EditorApplication.isPlaying || EditorApplication.isCompiling
            || !SessionState.GetBool(ArmedKey, false)) return;
        if (report != null && player == null) Finish("scene_changed");
        if (report != null || SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity") return;
        Move candidate = UnityEngine.Object.FindAnyObjectByType<Move>();
        MonsterSpawner owner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        if (candidate == null || owner == null || owner.CurrentWave == 0 || owner.StageFlow == null) return;
        player = candidate;
        spawner = owner;
        growth = player.GetComponent<RunProgressionSystem>();
        trait = player.GetComponent<PlayerTraitSystem>();
        if (growth == null || trait == null) { player = null; return; }
        previousCell = player.GridPosition;
        startedAt = Time.realtimeSinceStartupAsDouble;
        worldTurns = 0;
        pendingMovement = null;
        report = new Report { recordedAt = DateTime.Now.ToString("O"), unityVersion = Application.unityVersion,
            selectedTrait = trait.SelectedTraitId };
        outputPath = Path.Combine(Path.GetDirectoryName(Application.dataPath),
            "docs/verification/play-sessions/session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
        player.PlayerMoved += OnMoved;
        player.MoveCompleted += OnAction;
        spawner.WorldTurnCompleted += OnWorldTurn;
        spawner.WaveStarted += OnWaveStarted;
        growth.UpgradesChanged += OnReward;
        growth.RelicsChanged += OnRelic;
        trait.TraitSelected += OnTrait;
        spawner.StageFlow.StateChanged += OnStageState;
        Record("attached");
    }

    private static void OnMoved()
    {
        Vector3Int delta = player.GridPosition - previousCell;
        pendingMovement = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > 1
            ? "movement_art" : "basic_move";
    }

    private static void OnAction()
    {
        if (report == null) return;
        try { RecordAction(); }
        catch (Exception exception)
        {
            // 관찰 도구 오류가 실제 플레이어 행동을 끊지 않게 한다.
            report.diagnostics.Add(exception.ToString());
            Debug.LogWarning("플레이 기록 관찰 실패(게임 계속 진행): " + exception.Message);
        }
    }

    private static void RecordAction()
    {
        // 이벤트 구독 순서상 적 계획이 이미 시작됐을 수 있다. '적 턴 전' 스냅샷으로 주장하지 않는다.
        Entry entry = Record("player_action_completed_signal");
        if (entry == null) return;
        entry.action = pendingMovement ?? "attack";
        if (pendingMovement != null)
        {
            if (pendingMovement == "movement_art") report.artMoves++;
            else report.basicMoves++;
        }
        else
        {
            // Editor 진단용 읽기만 수행한다. 공개 입력이나 전투 로직을 변경하지 않는다.
            entry.usedTrait = Read<bool>("activeAttackUsedTrait");
            entry.attackOrigin = Read<Vector3Int>("activeAttackOriginCell");
            entry.direction = Read<Vector3Int>("activeAttackDirection");
            entry.casts = Read<int>("activeAttackCastIndex") + 1;
            entry.directHits = (int)typeof(RunProgressionSystem).GetField("attackHitCount", Private).GetValue(growth);
            entry.bonusStep = player.GridPosition != entry.attackOrigin;
            if (entry.usedTrait) report.specialAttacks++; else report.ordinaryAttacks++;
            if (entry.bonusStep) report.bonusSteps++;
        }
        pendingMovement = null;
    }

    private static T Read<T>(string field) => (T)typeof(Move).GetField(field, Private).GetValue(player);

    private static void OnWorldTurn()
    {
        if (report == null) return;
        report.worldTurns = ++worldTurns;
        Record("world_turn_completed");
        previousCell = player.GridPosition;
    }

    private static void OnTrait(string id) { report.selectedTrait = id; Record("trait_selected"); }
    private static void OnWaveStarted(int _) => Record("wave_started");
    private static void OnReward() => Record("upgrade_selected");
    private static void OnRelic(IReadOnlyList<RelicDefinition> _, int supply) => Record("relic_changed");
    private static void OnStageState(StageFlowState state)
    {
        Record("stage_" + state);
        if (state == StageFlowState.GameOver || state == StageFlowState.StageCleared)
            report.ending = state.ToString();
    }

    private static Entry Record(string eventKind)
    {
        if (report == null || player == null || spawner == null) return null;
        Entry entry = new Entry { eventKind = eventKind, frame = Time.frameCount,
            elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt, wave = spawner.CurrentWave,
            worldTurn = worldTurns, playerCell = player.GridPosition, health = player.CurrentHealth,
            charge = trait.ChargeProgress, movementArt = growth.ActiveMovementArt.ToString(),
            movementLevel = growth.MovementArtLevel, fireCount = spawner.FireTrail != null ? spawner.FireTrail.Count : 0,
            upgrades = RunProgressionCatalog.AllUpgrades.Where(item => growth.Stack(item.Id) > 0)
                .Select(item => item.Id + ":" + growth.Stack(item.Id)).ToArray(),
            relics = growth.Relics.Select(item => item.Id).ToArray(),
            enemies = spawner.ActiveMonsters.Where(item => item != null && !item.IsDead)
                .Select(item => item.SpawnOrder + ":" + item.MovementPattern + ":" + item.GridPosition
                    + ":hp=" + item.GetComponent<CharacterHealth>().CurrentHealth).ToArray() };
        report.entries.Add(entry);
        return entry;
    }

    private static void Finish(string reason)
    {
        if (report == null) return;
        if (string.IsNullOrEmpty(report.ending)) report.ending = reason;
        if (player != null) { player.PlayerMoved -= OnMoved; player.MoveCompleted -= OnAction; }
        if (spawner != null)
        {
            spawner.WorldTurnCompleted -= OnWorldTurn;
            spawner.WaveStarted -= OnWaveStarted;
            if (spawner.StageFlow != null) spawner.StageFlow.StateChanged -= OnStageState;
        }
        if (growth != null) { growth.UpgradesChanged -= OnReward; growth.RelicsChanged -= OnRelic; }
        if (trait != null) trait.TraitSelected -= OnTrait;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            Debug.Log("로컬 플레이 기록 저장: " + outputPath);
        }
        catch (Exception exception) { Debug.LogWarning("로컬 기록 저장 실패(게임 판정과 별개): " + exception.Message); }
        finally { report = null; player = null; spawner = null; growth = null; trait = null; }
    }
}
