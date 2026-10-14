using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 실제 씬과 입력/턴 경로를 사용한다. 통제 배치·체력·봉쇄는 재현용 픽스처이며 승률/재미 검증이 아니다.
[InitializeOnLoad]
public static class TacticalCoreValidation
{
    private const string PendingKey = "GridCaster.TacticalCoreValidation";
    private const string AuditKey = "GridCaster.TacticalCoreValidation.StageOneAudit";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();
    private static IEnumerator routine;
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame;
    private static string runtimeError;
    private static Move player;
    private static MonsterSpawner spawner;
    private static GridManager grid;
    private static RunProgressionSystem growth;
    private static PlayerTraitSystem trait;
    private static Sprite monsterSprite;
    private static Vector3Int origin;
    private static int worldTurns;
    private static bool stageOneAudit;
    private static bool previousRunInBackground;

    static TacticalCoreValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            checks.Clear();
            stageOneAudit = SessionState.GetBool(AuditKey, false);
            SessionState.SetBool(AuditKey, false);
            worldTurns = 0;
            runtimeError = null;
            lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 180;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Application.logMessageReceived += CaptureError;
            routine = Run();
            routines.Clear();
            routines.Push(routine);
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Stage One Tactical Audit")]
    public static void RunStageOneAudit()
    {
        // 기존 기록은 덮어쓰지 않고 이번 실행의 시각으로 별도 보관한다.
        if (EditorApplication.isPlaying || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(index => UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty))
            throw new InvalidOperationException("검증 전 Play를 종료하고 현재 씬을 저장하세요.");
        SessionState.SetBool(AuditKey, true);
        RunBatch();
    }

    [MenuItem("Tools/Playtest/Validate Tactical Core")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlaying || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(index => UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty))
            throw new InvalidOperationException("검증 전 Play를 종료하고 현재 씬을 저장하세요.");
        // PixelPerfect 스킬의 currentRenderPipeline 기반 진단. 설정 변경은 하지 않는다.
        Debug.Log("TACTICAL_PIPELINE " + DetectPipeline());
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    private static string DetectPipeline()
    {
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null) return "BuiltIn";
        string name = pipeline.GetType().FullName ?? string.Empty;
        return name.Contains("Universal") ? "URP" : name.Contains("HighDefinition") ? "HDRP" : "Custom";
    }

    private static void CaptureError(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            && (stack.Contains("Assets/Codes/") || stack.Contains("TacticalCoreValidation")))
            runtimeError = message + "\n" + stack;
    }

    private static void Tick()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("검증 시간 초과/Play 중단");
            while (routines.Count > 0)
            {
                IEnumerator current = routines.Peek();
                if (!current.MoveNext()) { routines.Pop(); continue; }
                if (current.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Finish(true);
        }
        catch (Exception exception) { checks.Add(exception.ToString()); Finish(false); }
    }

    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CaptureError;
        Application.runInBackground = previousRunInBackground;
        string report = "TACTICAL_CORE_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Executed " + DateTime.Now.ToString("O") + "; Unity " + Application.unityVersion + "\n"
            + "Controlled fixtures; not normal-run survival or human enjoyment evidence.\n"
            + string.Join("\n", checks);
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory);
        string reportName = stageOneAudit
            ? "stage-one-tactical-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"
            : Environment.GetCommandLineArgs().Contains("-specialAttackFValidation")
            ? "special-attack-f-2026-10-02.txt" : "tactical-core-2026-10-01.txt";
        File.WriteAllText(Path.Combine(directory, reportName), report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }

    private static void Invoke(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Private).Invoke(target, args);


    private static void Reset(string traitId, int bagSize)
    {
        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(origin);
        trait.SelectTrait(traitId, bagSize);
        // 이후 배치는 시작 보상 획득을 반복하는 런이 아니다. 특성 소유 상태만 통제한다.
        ((Dictionary<string, int>)typeof(RunProgressionSystem).GetField("upgradeStacks", Private).GetValue(growth)).Clear();
        ((List<UpgradeDefinition>)typeof(RunProgressionSystem).GetField("currentUpgradeOptions", Private).GetValue(growth)).Clear();
        typeof(RunProgressionSystem).GetField("starterSelectionPending", Private).SetValue(growth, false);
        Invoke(growth, "ApplyTraitBagSize");
        RunProgressionUiController ui = UnityEngine.Object.FindAnyObjectByType<RunProgressionUiController>();
        ((RectTransform)typeof(RunProgressionUiController).GetField("upgradePanel", Private).GetValue(ui)).gameObject.SetActive(false);
        player.SetInputEnabled(true);
        Spawn(origin + new Vector3Int(5, 5, 0), 100);
    }

    private static MonsterMovement Spawn(Vector3Int cell, int hp,
        MonsterMovementPattern pattern = MonsterMovementPattern.CardinalFour)
    {
        MonsterMovement monster = spawner.SpawnPlaytestMonster(pattern, cell,
            monsterSprite, Color.white, hp, spawner.ActiveMonsters.Count, 1, 4, 2);
        monster.SkipTurns(100);
        return monster;
    }

    private static IEnumerator WaitAction()
    {
        do { yield return null; } while (!player.CanAct || spawner.IsWorldTurnInProgress);
    }

    private static bool ClickCell(Vector3Int cell)
    {
        // 입력 확정의 반환값으로 성공을 확인하고 이동 연출·적 턴은 별도로 기다린다.
        return player.TryConfirmSelectedCell(Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(cell)));
    }

    private static void CheckActionHint(string context)
    {
        Canvas.ForceUpdateCanvases();
        var text = GameObject.Find("ActionMode").GetComponent<UnityEngine.UI.Text>();
        var frame = GameObject.Find("ActionModeHud").GetComponent<UnityEngine.UI.Image>();
        Check(text.color != frame.color && text.preferredHeight <= text.rectTransform.rect.height + 1f,
            "visible, untruncated action hint: " + context + $" ({text.preferredHeight}/{text.rectTransform.rect.height})");
    }

    private static void ClickUi(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = button.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { position = point, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button,
            "UI raycast reaches " + button.name + " at " + point + " screen=" + Screen.width + "x" + Screen.height
            + " hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name)));
        button.OnPointerClick(pointer);
    }

    private static IEnumerator Run()
    {
        yield return null;
        yield return null;
        player = UnityEngine.Object.FindAnyObjectByType<Move>();
        spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        grid = spawner.GetComponent<GridManager>();
        growth = player.GetComponent<RunProgressionSystem>();
        trait = player.GetComponent<PlayerTraitSystem>();
        monsterSprite = spawner.ActiveMonsters.First().GetComponent<SpriteRenderer>().sprite;
        origin = grid.GroundTilemap.cellBounds.min + new Vector3Int(3, 3, 0);
        GameHudController hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        hud.SelectTrait(0);
        yield return null;
        ClickUi(GameObject.Find("UpgradeChoice_1").GetComponent<UnityEngine.UI.Button>());
        Check(!growth.IsStarterUpgradeSelection, "initial build selection through actual UI callback");
        spawner.WorldTurnCompleted += () => worldTurns++;
        checks.Add("INFO Runtime pipeline=" + DetectPipeline() + "; no render settings/assets changed");
        checks.Add($"INFO Existing fixture sprite: {monsterSprite.name}, PPU={monsterSprite.pixelsPerUnit}, filter={monsterSprite.texture.filterMode}; not modified");

        Reset("pierce", 3);
        MonsterMovement first = Spawn(origin + Vector3Int.right * 2, 20);
        MonsterMovement second = Spawn(origin + Vector3Int.right * 4, 20);
        Check(!player.SelectTraitAttackMode(), "unready manual selection rejected");
        player.ProcessInputFrame(false, false, false, false, Vector2.zero, false, true);
        Check(!player.IsTraitAttackSelected && player.SelectionMode == PlayerActionSelectionMode.None,
            "F does not select special attack before charge is ready");
        int before = worldTurns;
        player.SelectAttackMode();
        Check(ClickCell(origin + Vector3Int.up), "ordinary miss via screen click");
        yield return WaitAction();
        Check(trait.ChargeProgress == 0 && worldTurns == before + 1,
            $"miss does not charge; one world turn (charge={trait.ChargeProgress}, turns={before}->{worldTurns})");
        for (int hit = 1; hit <= 2; hit++)
        {
            player.SelectAttackMode();
            Check(ClickCell(origin + Vector3Int.right), "ordinary hit via screen click " + hit);
            yield return WaitAction();
            Check(trait.ChargeProgress == hit, "one completed normal hit = one charge " + hit);
        }
        Check(trait.IsReady, "two ordinary hits prepare pierce");
        player.SelectAttackMode();
        ClickCell(origin + Vector3Int.right);
        yield return WaitAction();
        Check(trait.IsReady && second.GetComponent<CharacterHealth>().CurrentHealth == 20,
            "normal attack holds ready trait and does not auto-pierce");
        player.ProcessInputFrame(false, false, false, false, Vector2.zero, false, true);
        Check(player.IsTraitAttackSelected && trait.IsReady,
            "F command selects special aim without consuming charge");
        player.ProcessInputFrame(false, false, false, true, Vector2.zero);
        Check(trait.IsReady && player.SelectionMode == PlayerActionSelectionMode.None,
            "aim cancellation does not consume readiness");
        player.ProcessInputFrame(true, false, false, false, Vector2.zero);
        Check(!player.IsTraitAttackSelected && trait.IsReady,
            "A switches special aim back to ordinary attack and preserves readiness");
        player.ProcessInputFrame(false, false, false, false, Vector2.zero, false, true);
        Invoke(player, "ShowAttackPathPreview", origin + Vector3Int.right);
        CheckActionHint("manual attack");
        Check(player.GetComponent<DirectionalActionIndicator>().ActivePathCount == 4,
            "prepared pierce preview includes second target but no wall");
        before = worldTurns;
        Check(ClickCell(origin + Vector3Int.right), "manual pierce via screen click");
        yield return WaitAction();
        Check(!trait.IsReady && trait.ChargeProgress == 0
            && second.GetComponent<CharacterHealth>().CurrentHealth == 19 && worldTurns == before + 1,
            "manual pierce hits aligned pair, no self recharge, one enemy turn");
        trait.RegisterCompletedAttack(true, false);
        trait.RegisterCompletedAttack(true, false);
        UnityEngine.UI.Button icon = GameObject.Find("SelectedTraitIconFrame").GetComponent<UnityEngine.UI.Button>();
        yield return null;
        ClickUi(icon);
        Check(player.IsTraitAttackSelected, "existing trait icon selects manual attack");
        ClickCell(origin + Vector3Int.up);
        yield return WaitAction();
        Check(!trait.IsReady && trait.ChargeProgress == 0, "committed manual miss consumes one use");
        trait.RegisterCompletedAttack(true, false);
        Invoke(growth, "HandleWaveStarted", 2);
        Invoke(growth, "HandleWaveCleared", 2);
        Check(trait.ChargeProgress == 1, "partial charge carries across ordinary wave events");
        trait.SetActivationBagSize(2);
        Check(trait.IsReady && trait.ChargeRequired == 1, "lower requirement immediately prepares existing charge");
        Invoke(growth, "HandleWaveCleared", 20);
        Check(trait.ChargeProgress == 0, "stage clear resets charge");

        Reset("double_cast", 5);
        MonsterMovement doubleTarget = Spawn(origin + Vector3Int.right * 2, 20);
        for (int i = 0; i < 4; i++) trait.RegisterCompletedAttack(true, false);
        before = worldTurns;
        player.SelectTraitAttackMode();
        ClickCell(origin + Vector3Int.right);
        yield return WaitAction();
        Check(doubleTarget.GetComponent<CharacterHealth>().CurrentHealth == 18
            && trait.ChargeProgress == 0 && worldTurns == before + 1,
            "double cast = two hits, zero charge, one world turn");

        // 재조준과 왕의 차례는 보상 소유 상태만 픽스처로 구성한다. 실제 공격·입력은 우회하지 않는다.
        var stacks = (Dictionary<string, int>)typeof(RunProgressionSystem).GetField("upgradeStacks", Private).GetValue(growth);
        stacks["echo_recovery"] = 1;
        for (int i = 0; i < 4; i++) trait.RegisterCompletedAttack(true, false);
        player.SelectTraitAttackMode();
        ClickCell(origin + Vector3Int.right);
        while (!player.IsChoosingEchoDirection) yield return null;
        CheckActionHint("reaim");
        Vector3Int echoOrigin = player.GridPosition;
        Check(!player.TryPerformAction(PlayerActionSelectionMode.Move, Vector3Int.up), "reaim blocks direct move API");
        player.ProcessInputFrame(false, true, false, true, Vector2.zero);
        Check(player.IsChoosingEchoDirection && player.GridPosition == echoOrigin, "reaim blocks S and cancel");
        ClickCell(origin + Vector3Int.up);
        yield return WaitAction();
        stacks.Clear();

        Reset("damage_boost", 4);
        MonsterMovement damageTarget = Spawn(origin + Vector3Int.right * 2, 20);
        for (int i = 0; i < 3; i++) trait.RegisterCompletedAttack(true, false);
        player.SelectTraitAttackMode();
        ClickCell(origin + Vector3Int.right);
        yield return WaitAction();
        Check(damageTarget.GetComponent<CharacterHealth>().CurrentHealth == 18 && trait.ChargeProgress == 0,
            "prepared damage attack gives two direct damage, no automatic recharge");

        Reset("knockback", 4);
        MonsterMovement pushed = Spawn(origin + Vector3Int.right * 2, 20);
        for (int i = 0; i < 3; i++) trait.RegisterCompletedAttack(true, false);
        player.SelectTraitAttackMode();
        ClickCell(origin + Vector3Int.right);
        yield return WaitAction();
        Check(pushed.GridPosition == origin + Vector3Int.right * 3 && trait.ChargeProgress == 0,
            "prepared knockback moves surviving target one cell without recharging");

        Reset("damage_boost", 4);
        Invoke(growth, "AddRelic", RunProgressionCatalog.Relics.First(item => item.Id == "kings_turn"));
        Spawn(origin + Vector3Int.right * 2, 1);
        before = worldTurns;
        player.SelectAttackMode();
        ClickCell(origin + Vector3Int.right);
        while (!player.IsChoosingFreeMove) yield return null;
        CheckActionHint("free king step");
        Check(worldTurns == before, "king bonus occurs before original attack enemy turn");
        player.ProcessInputFrame(false, false, false, false, Vector2.zero, false, true);
        Check(player.IsChoosingFreeMove && !player.IsTraitAttackSelected,
            "F cannot interrupt king free movement with special attack");
        player.ProcessInputFrame(true, false, false, false, Vector2.zero);
        Check(player.SelectionMode == PlayerActionSelectionMode.Move
            && !player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.up)
            && !player.SelectTraitAttackMode(), "free step rejects A/direct attack/manual trait attack");
        player.ProcessInputFrame(false, false, false, true, Vector2.zero);
        yield return WaitAction();
        Check(!player.IsChoosingFreeMove && player.GridPosition == origin && worldTurns == before + 1,
            "right click skips bonus and resumes exactly one enemy turn");

        foreach (PlayerMovementArt art in new[] { PlayerMovementArt.Knight, PlayerMovementArt.Bishop, PlayerMovementArt.Rook })
        {
            Reset("pierce", 3);
            growth.SetMovementArtForPlaytest(art, 1);
            Spawn(origin + Vector3Int.up * 4, 20);
            Vector3Int landing = origin + (art == PlayerMovementArt.Knight
                ? new Vector3Int(2, 1, 0) : art == PlayerMovementArt.Bishop
                ? new Vector3Int(2, 2, 0) : Vector3Int.right * 3);
            Vector3 departure = player.transform.position;
            Vector3 arrival = grid.GetCellCenterWorld(landing);
            Animator animator = player.GetComponent<Animator>();
            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
            int turnsBeforeTravel = worldTurns;
            int landingEvents = 0;
            Action onLanding = () => landingEvents++;
            player.PlayerMoved += onLanding;
            player.SelectMoveMode();
            Check(ClickCell(landing) && player.transform.position == departure && !player.CanAct
                && animator.GetBool("IsMoving") && worldTurns == turnsBeforeTravel,
                art + " starts walking at departure and blocks extra input/enemy turns");
            var seenFrames = new HashSet<Sprite>();
            bool sawIntermediatePosition = false;
            bool worldWaitedForLanding = true;
            while (animator.GetBool("IsMoving"))
            {
                seenFrames.Add(renderer.sprite);
                Vector3 position = player.transform.position;
                if (position != departure && position != arrival) sawIntermediatePosition = true;
                worldWaitedForLanding &= worldTurns == turnsBeforeTravel && landingEvents == 0;
                yield return null;
            }
            yield return WaitAction();
            player.PlayerMoved -= onLanding;
            Check(worldWaitedForLanding, art + " keeps world and landing effects waiting during travel");
            Check(sawIntermediatePosition && seenFrames.Count > 1,
                art + " visibly travels through intermediate positions with changing walk frames");
            Check(player.GridPosition == landing && player.transform.position == arrival
                && !animator.GetBool("IsMoving") && worldTurns == turnsBeforeTravel + 1 && landingEvents == 1,
                art + " lands once, returns to idle, and resolves exactly one enemy turn");
        }

        Reset("pierce", 3);
        growth.SetMovementArtForPlaytest(PlayerMovementArt.Rook, 1);
        player.ResetPlaytest(origin);
        Spawn(origin + Vector3Int.right * 2, 20);
        player.SelectMoveMode();
        Check(!player.TryUseMovementArtAtCell(origin + Vector3Int.right * 4),
            "rook cannot cross occupied intermediate tile to empty target");
        player.SelectMoveMode();
        spawner.FireTrail.AddFire(origin + Vector3Int.up * 3);
        before = player.CurrentHealth;
        Check(ClickCell(origin + Vector3Int.up * 3), "rook fire destination remains selectable via mouse");
        yield return WaitAction();
        Check(player.GridPosition == origin + Vector3Int.up * 3 && player.CurrentHealth == before - 1,
            "rook can cross fire, landing on fire hurts (not blocked by fire)");
        player.ResetPlaytest(origin);
        player.SelectMoveMode();
        Check(!player.TryUseMovementArtAtCell(origin + Vector3Int.up * 5), "rook Lv.1 cannot move beyond four cells");

        var danger = new HashSet<Vector3Int>();
        MonsterMovement knight = Spawn(origin + new Vector3Int(5, 2, 0), 4, MonsterMovementPattern.Knight);
        // 100-turn block is removed only for the read-only intent fixture.
        typeof(MonsterMovement).GetField("skippedTurns", Private).SetValue(knight, 0);
        UnityEngine.Random.State rngBefore = UnityEngine.Random.state;
        Vector3Int knightBefore = knight.GridPosition;
        Check(EnemyThreatPreview.CollectCells(knight, grid, danger).Contains("후보") && danger.Count > 0,
            "knight shows possible cross threats, not exact future landing");
        EnemyThreatPreview preview = player.GetComponent<EnemyThreatPreview>();
        ((HashSet<Vector3Int>)typeof(EnemyThreatPreview).GetField("cells", Private).GetValue(preview)).UnionWith(danger);
        Invoke(preview, "ShowCells", false);
        var marker = ((List<SpriteRenderer>)typeof(EnemyThreatPreview).GetField("markers", Private).GetValue(preview))[0];
        Check(marker.sprite.pixelsPerUnit == 32 && marker.sprite.texture.filterMode == FilterMode.Point,
            "new threat border uses 32 PPU / Point");
        EnemyThreatPreview.CollectCells(knight, grid, danger);
        Check(knight.GridPosition == knightBefore && UnityEngine.Random.state.Equals(rngBefore)
            && knight.GetComponent<ChessMonsterBehaviour>().PatternExecutions == 0,
            "hover query leaves position, pattern, and RNG unchanged");
        MonsterMovement bishop = Spawn(origin + new Vector3Int(2, 5, 0), 4, MonsterMovementPattern.Bishop);
        typeof(MonsterMovement).GetField("skippedTurns", Private).SetValue(bishop, 0);
        int firesBefore = spawner.FireTrail.Count;
        Check(EnemyThreatPreview.CollectCells(bishop, grid, danger).Contains("생성 즉시 피해 없음")
            && spawner.FireTrail.Count == firesBefore, "bishop separates possible creation from entry damage");
        if (stageOneAudit) yield return RunStageOneRegressions();
        trait.RegisterCompletedAttack(true, false);
        player.TakeDamage(1000);
        Check(trait.ChargeProgress == 0 && !player.CanAct, "death resets charge and stops input");
    }

    private static IEnumerator RunStageOneRegressions()
    {
        PlaySessionRecorder.MarkControlledValidation();
        AuditReset("pierce", 3);
        int recordedAttacks = PlaySessionRecorder.OrdinaryAttackCount;
        MonsterMovement target = Spawn(origin + Vector3Int.right * 2, 20);
        spawner.FireTrail.AddFire(origin + Vector3Int.right);
        spawner.FireTrail.AddFire(target.GridPosition);
        spawner.FireTrail.AddFire(origin + Vector3Int.right * 3);
        player.SelectAttackMode();
        Check(ClickCell(origin + Vector3Int.right), "fire-clearing attack commits through board input");
        yield return WaitAction();
        if (PlaySessionRecorder.IsRecording)
            Check(PlaySessionRecorder.OrdinaryAttackCount == recordedAttacks + 1,
                "read-only session recorder observes one ordinary attack, not projectile segments");
        Check(!spawner.FireTrail.HasFire(origin + Vector3Int.right)
            && !spawner.FireTrail.HasFire(target.GridPosition),
            "ordinary attack clears traversed and occupied impact floor fire");
        Check(spawner.FireTrail.HasFire(origin + Vector3Int.right * 3),
            "ordinary attack does not clear fire beyond its first impact");

        // 준비된 관통만 두 번째 대상까지 이어져 불길을 지운다.
        MonsterMovement second = Spawn(origin + Vector3Int.right * 4, 20);
        spawner.FireTrail.AddFire(target.GridPosition);
        spawner.FireTrail.AddFire(second.GridPosition);
        trait.RegisterCompletedAttack(true, false);
        player.SelectTraitAttackMode();
        int before = worldTurns;
        recordedAttacks = PlaySessionRecorder.SpecialAttackCount;
        Check(ClickCell(origin + Vector3Int.right), "charged pierce fire fixture commits");
        yield return WaitAction();
        if (PlaySessionRecorder.IsRecording)
            Check(PlaySessionRecorder.SpecialAttackCount == recordedAttacks + 1,
                "read-only session recorder counts one special action for two pierce hits");
        Check(!spawner.FireTrail.HasFire(target.GridPosition)
            && !spawner.FireTrail.HasFire(second.GridPosition)
            && !spawner.FireTrail.HasFire(origin + Vector3Int.right * 3)
            && worldTurns == before + 1,
            "pierce clears both impact floors and intervening fire in one world turn");

        // 공통 배치/체력에서 각 빌드의 고유 효과만 달라지는 통제 사례다.
        AuditReset("double_cast", 5);
        Stacks()["echo_recovery"] = 1;
        AddRelic("triple_echo");
        MonsterMovement a = Spawn(origin + Vector3Int.right * 2, 1);
        MonsterMovement b = Spawn(origin + Vector3Int.up * 2, 2);
        MonsterMovement c = Spawn(origin + Vector3Int.left * 2, 2);
        ReadyTrait();
        before = worldTurns;
        player.SelectTraitAttackMode();
        ClickCell(origin + Vector3Int.right);
        while (!player.IsChoosingEchoDirection) yield return null;
        Check(a == null || a.IsDead, "double cast first direct target killed before reaim");
        for (int i = 0; i < 3; i++) yield return null;
        Check(worldTurns == before && player.GridPosition == origin,
            "idle reaim does not advance enemy turn or change attack origin");
        ClickCell(origin + Vector3Int.up);
        while (!player.IsChoosingEchoDirection) yield return null;
        Check(b == null || b.IsDead, "second cast gains different-direction direct damage");
        Check(!player.TryPerformAction(PlayerActionSelectionMode.Move, Vector3Int.down),
            "third cast reaim also blocks movement");
        ClickCell(origin + Vector3Int.left);
        yield return WaitAction();
        Check((c == null || c.IsDead) && worldTurns == before + 1 && trait.ChargeProgress == 0,
            "triple reaim resolves three targets at original position in exactly one world turn");

        AuditReset("damage_boost", 4);
        Stacks()["overcharge"] = 2;
        Stacks()["afterglow_recovery"] = 2;
        MonsterMovement direct = Spawn(origin + Vector3Int.right * 2, 1);
        MonsterMovement neighbor = Spawn(origin + new Vector3Int(2, 1, 0), 4);
        MonsterMovement beyond = Spawn(origin + new Vector3Int(2, 2, 0), 4);
        MonsterMovement rangeGap = Spawn(origin + new Vector3Int(4, 1, 0), 4);
        ReadyTrait();
        player.SelectTraitAttackMode();
        ClickCell(origin + Vector3Int.right);
        yield return WaitAction();
        Check((direct == null || direct.IsDead) && neighbor.GetComponent<CharacterHealth>().CurrentHealth == 3
            && beyond.GetComponent<CharacterHealth>().CurrentHealth == 3
            && rangeGap.GetComponent<CharacterHealth>().CurrentHealth == 4,
            "damage range growth hits adjacent and outer cross targets for one while preserving gaps without chaining");

        AuditReset("knockback", 4);
        Stacks()["impact_recovery"] = 2;
        MonsterMovement pushed = Spawn(origin + Vector3Int.right * 2, 10);
        MonsterMovement collision = Spawn(origin + Vector3Int.right * 3, 10);
        MonsterMovement unrelated = Spawn(origin + new Vector3Int(2, 1, 0), 10);
        foreach (MonsterMovement monster in new[] { pushed, collision, unrelated })
            typeof(MonsterMovement).GetField("skippedTurns", Private).SetValue(monster, 0);
        ReadyTrait();
        player.SelectTraitAttackMode();
        ClickCell(origin + Vector3Int.right);
        yield return WaitAction();
        Check(pushed.GridPosition == origin + Vector3Int.right * 2
            && pushed.GetComponent<CharacterHealth>().CurrentHealth == 8,
            "blocked knockback deals direct plus actual obstruction damage without overlapping");
        Check(pushed.IsActionBlocked && !collision.IsActionBlocked && !unrelated.IsActionBlocked,
            "knockback locks direct target for two actions, first collision for one, not neighbors");

        AuditReset("pierce", 3);
        AddRelic("suture_needle");
        Spawn(origin + Vector3Int.right * 2, 20);
        Spawn(origin + Vector3Int.right * 4, 20);
        player.TakeDamage(4);
        for (int i = 0; i < 3; i++)
        {
            ReadyTrait();
            before = player.CurrentHealth;
            player.SelectTraitAttackMode();
            ClickCell(origin + Vector3Int.right);
            yield return WaitAction();
            Check(player.CurrentHealth == before + (i < 2 ? 1 : 0),
                "pierce direct-pair healing capped at twice per wave: " + (i + 1));
        }

        AuditReset("damage_boost", 4);
        growth.SetMovementArtForPlaytest(PlayerMovementArt.Knight, 2);
        Vector3Int landing = origin + new Vector3Int(2, 1, 0);
        spawner.FireTrail.AddFire(landing);
        spawner.FireTrail.AddFire(landing + Vector3Int.up);
        before = player.CurrentHealth;
        player.SelectMoveMode();
        Check(ClickCell(landing), "knight Lv.2 art commits from S choices");
        yield return WaitAction();
        Check(player.GridPosition == landing && player.CurrentHealth == before
            && !spawner.FireTrail.HasFire(landing) && !spawner.FireTrail.HasFire(landing + Vector3Int.up),
            "knight Lv.2 cleans landing fire before entry damage");

        // 아래쪽까지 발사하는 룩도 장전 턴에 정한 열만 공격하며 재조준하지 않는다.
        AuditReset("pierce", 3);
        Vector3Int mount = new Vector3Int(origin.x, grid.GroundTilemap.cellBounds.yMax - 1, 0);
        MonsterMovement rook = Spawn(mount, 20, MonsterMovementPattern.Rook);
        typeof(MonsterMovement).GetField("skippedTurns", Private).SetValue(rook, 0);
        ChessMonsterBehaviour rookPattern = rook.GetComponent<ChessMonsterBehaviour>();
        player.SelectAttackMode();
        ClickCell(origin + Vector3Int.down);
        yield return WaitAction();
        Check(rookPattern.IsRookCharged && grid.IsTopBoundaryWallCell(rook.GridPosition),
            "rook charges a fixed column on top boundary only");
        int targetColumn = rookPattern.RookTargetColumn;
        player.ResetPlaytest(new Vector3Int(targetColumn, origin.y, 0));
        before = player.CurrentHealth;
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.down),
            "rook fire fixture starts an actual player action");
        yield return WaitAction();
        Check(player.CurrentHealth == before - 2 && rookPattern.IsRookRecovering
            && !rookPattern.IsRookCharged, "fixed-column rook fires once for two damage");
        before = player.CurrentHealth;
        player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.down);
        yield return WaitAction();
        Check(player.CurrentHealth == before && !rookPattern.IsRookRecovering
            && !rookPattern.IsRookCharged, "rook recovery is one harmless enemy action");
    }

    private static Dictionary<string, int> Stacks() =>
        (Dictionary<string, int>)typeof(RunProgressionSystem).GetField("upgradeStacks", Private).GetValue(growth);

    private static void AuditReset(string traitId, int bagSize)
    {
        // 각 사례의 유물/이동술/파생 상태를 격리한다. 실제 런에서 이 초기화는 호출하지 않는다.
        ((List<RelicDefinition>)typeof(RunProgressionSystem).GetField("relics", Private).GetValue(growth)).Clear();
        growth.SetMovementArtForPlaytest(PlayerMovementArt.None);
        Reset(traitId, bagSize);
        Invoke(growth, "HandleWaveStarted", 1);
    }

    private static void ReadyTrait()
    {
        for (int i = 0; i < trait.ChargeRequired; i++) trait.RegisterCompletedAttack(true, false);
    }

    private static void AddRelic(string id) =>
        Invoke(growth, "AddRelic", RunProgressionCatalog.Relics.First(item => item.Id == id));
}
