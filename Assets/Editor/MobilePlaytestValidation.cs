using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class MobilePlaytestValidation
{
    private const string Pending = "GridCaster.MobileValidation";
    private static IEnumerator routine;
    private static int frame;
    private static double deadline;
    private static readonly List<string> checks = new List<string>();
    private static string runtimeError;
    private static int turns;
    private static bool previousRunInBackground;

    static MobilePlaytestValidation()
    {
        // domain reload 이후에도 모바일 플래그를 씬 Awake보다 먼저 복구한다.
        if (SessionState.GetBool(Pending, false)) MobilePlaytestController.ForceMobileForPlaytest = true;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            checks.Clear(); runtimeError = null; turns = 0;
            frame = -1; deadline = EditorApplication.timeSinceStartup + 180;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Application.logMessageReceived += Error;
            routine = Run(); EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Mobile Touch Adapter")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Pending, true);
        MobilePlaytestController.ForceMobileForPlaytest = true;
        EditorApplication.isPlaying = true;
    }
    private static void Error(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error)
            && (stack.Contains("Assets/Codes/") || stack.Contains("MobilePlaytestValidation"))) runtimeError = message + stack;
    }
    private static void Tick()
    {
        try
        {
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new TimeoutException();
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (routine.MoveNext()) return;
            Finish(true);
        }
        catch (Exception ex) { checks.Add(ex.ToString()); Finish(false); }
    }
    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Error;
        MobilePlaytestController.ForceMobileForPlaytest = false;
        Application.runInBackground = previousRunInBackground;
        string report = "MOBILE_ADAPTER_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled Editor fixtures + mathematical portrait layout checks. Not physical iPhone evidence.\n"
            + string.Join("\n", checks);
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory,
            "mobile-adapter-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
    }
    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }
    private static RectTransform Find(Transform root, string name)
        => root.GetComponentsInChildren<RectTransform>(true).First(item => item.name == name);
    private static void Click(Transform root, string name)
    {
        RectTransform rect = Find(root, name);
        var button = rect.GetComponent<UnityEngine.UI.Button>();
        Canvas.ForceUpdateCanvases();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "raycast: " + name);
        button.OnPointerClick(pointer);
    }
    private static IEnumerator Run()
    {
        yield return null; yield return null; yield return null;
        GameHudController hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        MobilePlaytestController mobile = hud.GetComponent<MobilePlaytestController>();
        Move player = UnityEngine.Object.FindAnyObjectByType<Move>();
        MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        GridManager grid = spawner.GetComponent<GridManager>();
        PlayerTraitSystem trait = player.GetComponent<PlayerTraitSystem>();
        Sprite sprite = spawner.ActiveMonsters.First().GetComponent<SpriteRenderer>().sprite;
        Check(mobile != null, "mobile adapter automatically attached without scene edits");
        // batch GameView는 640x480이다. 작은 세로 화면의 배치는 별도로 수학 검증하고,
        // 실행 중인 씬에서는 실제 GraphicRaycaster/버튼 콜백과 기존 전투 경로를 검증한다.
        foreach (Vector2Int size in new[] { new Vector2Int(375, 667), new Vector2Int(390, 660), new Vector2Int(390, 844), new Vector2Int(430, 932) })
        {
            Rect safe = new Rect(0, 0, size.x, size.y);
            Rect board = MobilePlaytestController.BoardPixelRect(size.x, size.y, safe);
            float scale = size.x / 390f;
            Check(board.width == board.height && board.x >= 0 && board.xMax <= size.x
                && board.y >= 92 * scale - 0.01f && board.yMax + 90 * scale <= size.y,
                $"square full-board + separate 44pt controls/header at {size}");
        }
        Check(Find(hud.transform, "HealthHud").gameObject.activeSelf == false, "PC HUD hidden only in mobile mode");
        Check(Find(hud.transform, "Bottom").GetComponentsInChildren<UnityEngine.UI.Button>(false).Length == 3,
            "idle bottom has exactly three action buttons and no permanent confirm/cancel row");
        // 시작/보상 화면은 콜백으로 선택한다. 세로 기하 검증과 GUI 실물 확인은 별도 기록한다.
        hud.SelectTrait(0); yield return null;
        Find(hud.transform, "UpgradeChoice_1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        spawner.ResetPlaytestEncounter();
        Vector3Int origin = grid.GroundTilemap.cellBounds.min + new Vector3Int(4, 4, 0);
        player.ResetPlaytest(origin); player.SetInputEnabled(true);
        MonsterMovement target = spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour,
            origin + Vector3Int.right * 3, sprite, Color.white, 100, 0, 1, 4, 2);
        target.SkipTurns(100); spawner.WorldTurnCompleted += () => turns++;
        yield return null; yield return null;
        // 가로 batch GameView에서는 모바일 세로 전용 뷰포트가 1px로 축소된다.
        // 입력/판정 픽스처는 전체 카메라 뷰포트를 사용하고 세로 레이아웃은 위에서 별도 검사한다.
        Camera.main.rect = new Rect(0, 0, 1, 1);
        mobile.SelectAction(true, false);
        Vector2 point = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(origin + Vector3Int.right * 3));
        checks.Add($"INFO can={player.CanAct} mode={player.SelectionMode} modal={hud.GetComponent<RunProgressionUiController>().IsModalOpen} pause={hud.IsPauseMenuOpen} point={point} viewport={Camera.main.pixelRect}");
        Check(mobile.PreviewBoardPoint(point) && mobile.HasPreview && turns == 0 && target.GetComponent<CharacterHealth>().CurrentHealth == 100,
            "far board tap previews nearest 8-direction; no damage/turn");
        mobile.CancelPreview();
        Check(!mobile.HasPreview && turns == 0 && trait.ChargeProgress == 0, "cancel consumes neither turn nor charge");
        yield return null;
        mobile.OpenDrawer(false);
        Check(mobile.IsDrawerOpen && !player.CanAct, "build drawer blocks board input");
        Check(Find(hud.transform, "BuildHudPanel").GetComponentInParent<UnityEngine.UI.ScrollRect>() != null,
            "existing build list lives in masked scrolling drawer");
        mobile.CloseDrawer(); yield return null; yield return null;
        Check(!mobile.IsDrawerOpen && player.CanAct, "close restores input on a later frame");
        mobile.SelectAction(true, false); mobile.PreviewBoardPoint(point); mobile.ConfirmPreview();
        while (!player.CanAct) yield return null;
        Check(player.CanAct && target.GetComponent<CharacterHealth>().CurrentHealth == 99 && turns == 1 && trait.ChargeProgress == 1,
            "preview confirm deals one hit, charges once, advances exactly one enemy turn");
        mobile.ConfirmPreview();
        Check(turns == 1, "repeat confirm without draft does not duplicate action");
        mobile.SelectAction(false, false);
        point = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(origin + Vector3Int.up));
        Check(mobile.PreviewBoardPoint(point) && player.GridPosition == origin, "move tap is preview only");
        Check(mobile.IsMovePreviewVisible, "selected landing cell displays player ghost without moving the entity");
        yield return null;
        Check(Find(hud.transform, "Move").GetComponentInChildren<UnityEngine.UI.Text>().text == "이동 실행",
            "selected movement button becomes explicit execute button");
        Find(hud.transform, "Move").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        while (!player.CanAct) yield return null;
        Check(player.GridPosition == origin + Vector3Int.up && turns == 2, "move confirm = one tile + one enemy turn");
        Check(!mobile.IsMovePreviewVisible, "movement commit clears visual ghost");
        // 재조준 중 별도 실행 버튼이 없어도 공격 버튼으로 이어갈 수 있어야 한다.
        var growth = player.GetComponent<RunProgressionSystem>();
        var stacks = (Dictionary<string, int>)typeof(RunProgressionSystem)
            .GetField("upgradeStacks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(growth);
        stacks["echo_recovery"] = 1;
        for (int i = 0; i < 4; i++) trait.RegisterCompletedAttack(true, false);
        Vector3Int echoOrigin = player.GridPosition;
        mobile.SelectAction(true, true);
        point = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(echoOrigin + Vector3Int.right));
        Check(mobile.PreviewBoardPoint(point), "prepared special previews with combined action/execute UI");
        yield return null;
        Find(hud.transform, "Special").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        while (!player.IsChoosingEchoDirection) yield return null;
        yield return null;
        Check(Find(hud.transform, "Attack").GetComponent<UnityEngine.UI.Button>().interactable,
            "reaim keeps attack execute available after special charge is consumed");
        Check(!Find(hud.transform, "Move").GetComponent<UnityEngine.UI.Button>().interactable
            && !Find(hud.transform, "Cancel").gameObject.activeSelf, "reaim blocks movement and cancellation");
        point = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(echoOrigin + Vector3Int.up));
        Check(mobile.PreviewBoardPoint(point), "reaim can preview a second direction");
        yield return null;
        Find(hud.transform, "Attack").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        while (!player.CanAct || player.IsChoosingEchoDirection) yield return null;
        Check(turns == 3 && player.GridPosition == echoOrigin && !trait.IsReady,
            "combined button finishes echo with one enemy turn, no move or recharge");
        stacks.Clear();
        mobile.PauseFromBrowser();
        Check(hud.IsPauseMenuOpen && !player.CanAct && Time.timeScale == 0, "background pause requires explicit resume");
        hud.TogglePauseMenu(); yield return null;
        Check(!hud.IsPauseMenuOpen, "resume retains encounter");

        var progressionUi = hud.GetComponent<RunProgressionUiController>();
        stacks["echo_recovery"] = 1;
        progressionUi.ShowUpgradeDetails("echo_recovery");
        Check(progressionUi.IsDetailOpen, "shared details open for timer observation");
        yield return null;
        float frozenTimer = hud.ElapsedPlayTime;
        double timerHoldUntil = Time.realtimeSinceStartupAsDouble + 1.2;
        while (Time.realtimeSinceStartupAsDouble < timerHoldUntil) yield return null;
        Check(Mathf.Approximately(hud.ElapsedPlayTime, frozenTimer), "shared HUD timer freezes while inspecting details");
        int expectedSeconds = Mathf.Max(0, Mathf.FloorToInt(hud.ElapsedPlayTime));
        Check(Find(hud.transform, "Summary").GetComponent<UnityEngine.UI.Text>().text
            .EndsWith($"{expectedSeconds / 60:00}:{expectedSeconds % 60:00}"),
            "mobile time label uses the same clock as PC HUD and result panel");
        progressionUi.CloseDetails(); yield return null; yield return null;

        // 최종 상태만 압축한 통제 fixture. 실제 20웨이브 클리어나 아이폰 조작 증거는 아니다.
        const System.Reflection.BindingFlags stateField = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic;
        typeof(StageFlowManager).GetField("<CurrentWave>k__BackingField", stateField)
            .SetValue(spawner.StageFlow, WaveTemplateCatalog.FinalWave);
        typeof(StageFlowManager).GetField("<State>k__BackingField", stateField)
            .SetValue(spawner.StageFlow, StageFlowState.StageBoss);
        typeof(MonsterSpawner).GetField("currentWave", stateField)
            .SetValue(spawner, WaveTemplateCatalog.FinalWave);
        target.GetComponent<CharacterHealth>().TakeDamage(10000);
        while (!hud.IsStageCleared) yield return null;
        yield return null;
        Check(hud.IsRunEnded && !hud.IsGameOver && hud.IsGameOverOverlayVisible && !player.CanAct,
            "mobile clear uses shared result state, not death state");
        mobile.PauseFromBrowser();
        Check(!hud.IsPauseMenuOpen && !player.CanAct, "background pause cannot reopen a cleared run");
        Click(hud.transform, "ReviewBoard");
        Check(!hud.IsGameOverOverlayVisible && !player.CanAct, "mobile result review hides only overlay");
        Click(hud.transform, "Menu");
        Check(hud.IsGameOverOverlayVisible && !hud.IsPauseMenuOpen && !player.CanAct,
            "mobile menu restores clear result instead of a resumable pause menu");
    }
}
