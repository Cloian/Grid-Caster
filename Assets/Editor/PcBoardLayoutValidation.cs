using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

// 실제 실행 화면과 계산상 해상도를 구분한다. 사람의 재미나 완주 검사가 아니다.
[InitializeOnLoad]
public static class PcBoardLayoutValidation
{
    private const string Pending = "GridCaster.PcBoardValidation";
    private static readonly List<string> checks = new List<string>();
    private static IEnumerator routine;
    private static int frame;
    private static double deadline;
    private static string runtimeError;
    private static bool previousRunInBackground;

    static PcBoardLayoutValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            checks.Clear(); runtimeError = null; frame = -1;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 180;
            Application.logMessageReceived += Error;
            routine = Run(); EditorApplication.update += Tick;
        };
    }

    private static string DetectPipeline()
    {
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null) return "BuiltIn";
        string name = pipeline.GetType().FullName ?? string.Empty;
        return name.Contains("Universal") ? "URP" : name.Contains("HighDefinition") ? "HDRP" : "Custom";
    }

    [MenuItem("Tools/Playtest/Validate PC Board Layout")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlaying || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(index => UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty))
            throw new InvalidOperationException("Exit Play and save the scene before validation.");
        Debug.Log("PC_LAYOUT_PIPELINE " + DetectPipeline());
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void Error(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error)
            && (stack.Contains("Assets/Codes/") || stack.Contains("PcBoardLayoutValidation"))) runtimeError = message + stack;
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
        Application.runInBackground = previousRunInBackground;
        string report = "PC_BOARD_LAYOUT_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Actual Editor geometry/raycast + separately labelled layout calculations. Not human play evidence.\n"
            + string.Join("\n", checks);
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "pc-board-layout-native-"
            + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), report);
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

    private static Rect Pixels(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static void Click(RectTransform rect)
    {
        Canvas.ForceUpdateCanvases();
        Vector2 point = rect.TransformPoint(rect.rect.center);
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        var button = rect.GetComponent<UnityEngine.UI.Button>();
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button,
            "actual GraphicRaycaster reaches " + rect.name + " (hits: "
            + string.Join(",", hits.Select(hit => hit.gameObject.name)) + ")");
        button.OnPointerClick(pointer);
    }

    private static IEnumerator Run()
    {
        yield return null; yield return null; yield return null;
        Check(DetectPipeline() == "URP", "runtime pipeline detected as URP; render settings unchanged");
        Check(!MobilePlaytestController.UsesMobileLayout(false, false, false), "desktop Web is not inherently mobile");
        Check(MobilePlaytestController.UsesMobileLayout(false, true, false)
            && MobilePlaytestController.UsesMobileLayout(true, false, false)
            && MobilePlaytestController.UsesMobileLayout(false, false, true), "phone/native/forced adapter retained");
        foreach (Vector2Int size in new[] { new Vector2Int(1024, 768), new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080), new Vector2Int(2560, 1080), new Vector2Int(3840, 2160),
            new Vector2Int(800, 1200), new Vector2Int(960, 540) })
        {
            float scale = PcBoardLayout.Scale(size.x, size.y);
            Rect board = PcBoardLayout.BoardPixelRect(size.x, size.y);
            Check(board.width == board.height && board.x >= 0 && board.xMax <= size.x
                && board.y >= PcBoardLayout.ActionSpace * scale - 1f
                && board.yMax <= size.y - PcBoardLayout.HeaderSpace * scale + 1f,
                "calculation: whole board uses available screen with only header/action margins at " + size);
            Check(board.width > 800f * scale, "board larger than previous side-panel layout at " + size);
        }
        GameHudController hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        Move player = UnityEngine.Object.FindAnyObjectByType<Move>();
        GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        Check(hud.GetComponent<MobilePlaytestController>() == null, "native PC does not attach mobile adapter");
        Check(grid.GroundTilemap.cellBounds.size.x == 15 && grid.GroundTilemap.cellBounds.size.y == 15,
            "13x13 internal board plus outer walls preserved");
        hud.SelectTrait(0); yield return null;
        Canvas.ForceUpdateCanvases();
        Click(Find(hud.transform, "UpgradeChoice_1"));
        yield return null; yield return null;
        Canvas.ForceUpdateCanvases();
        Camera camera = Camera.main;
        var runtimeSize = (Vector2Int)typeof(CameraController).GetField("lastScreenSize",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(camera.GetComponent<CameraController>());
        Check(camera.rect == new Rect(0, 0, 1, 1)
            && runtimeSize == new Vector2Int(camera.pixelWidth, camera.pixelHeight),
            $"actual runtime viewport dimensions ({runtimeSize}); Editor callback Screen={Screen.width}x{Screen.height}");
        Rect expected = PcBoardLayout.BoardPixelRect(runtimeSize.x, runtimeSize.y);
        BoundsInt cells = grid.GroundTilemap.cellBounds;
        Vector3 min = Camera.main.WorldToScreenPoint(grid.GroundTilemap.CellToWorld(cells.min));
        Vector3 max = Camera.main.WorldToScreenPoint(grid.GroundTilemap.CellToWorld(cells.max));
        Rect boardRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        Check(Vector2.Distance(boardRect.center, expected.center) < 1f
            && Mathf.Abs(boardRect.width - expected.width) < 1f, "actual full board matches common PC geometry");
        foreach (string name in new[] { "BuildInventoryButton", "WaveHud", "TimeHud", "HealthHud",
            "UltimateFrame", "TraitHud", "AttackActionFrame", "MoveActionFrame", "ActionModeHud" })
        {
            Rect rect = Pixels(Find(hud.transform, name));
            Check(!rect.Overlaps(boardRect) && rect.xMin >= -1 && rect.yMin >= -1
                && rect.xMax <= runtimeSize.x + 1 && rect.yMax <= runtimeSize.y + 1,
                "actual HUD outside board and inside screen: " + name);
        }
        Check(!Find(hud.transform, "BuildHudPanel").gameObject.activeInHierarchy
            && !Find(hud.transform, "RelicHudPanel").gameObject.activeInHierarchy,
            "owned upgrades and relics hidden during battle");
        Click(Find(hud.transform, "AttackActionFrame"));
        Check(player.SelectionMode == PlayerActionSelectionMode.Attack
            && player.GetComponent<DirectionalActionIndicator>().ActiveChoiceCount == 8, "attack UI opens eight directions");
        Click(Find(hud.transform, "MoveActionFrame"));
        Check(player.SelectionMode == PlayerActionSelectionMode.Move, "movement UI remains usable");
        RunProgressionUiController inventory = hud.GetComponent<RunProgressionUiController>();
        int consumedTurns = 0;
        player.MoveCompleted += () => consumedTurns++;
        Vector3Int position = player.GridPosition;
        int health = player.CurrentHealth;
        Click(Find(hud.transform, "BuildInventoryButton"));
        yield return null;
        Check(inventory.IsInventoryOpen && inventory.IsModalOpen && !player.CanAct
            && Find(hud.transform, "BuildHudPanel").gameObject.activeInHierarchy
            && Find(hud.transform, "RelicHudPanel").gameObject.activeInHierarchy,
            "compact build button opens owned upgrades/relics and blocks battlefield input");
        Rect inventoryRect = Pixels(Find(hud.transform, "BuildInventoryCard"));
        Check(inventoryRect.xMin >= -1 && inventoryRect.yMin >= -1
            && inventoryRect.xMax <= runtimeSize.x + 1 && inventoryRect.yMax <= runtimeSize.y + 1,
            "build window fits actual screen");
        Click(Find(hud.transform, "MovementArtDetails"));
        Check(inventory.IsDetailOpen && inventory.IsInventoryOpen, "nested build effect details open");
        inventory.CloseDetails();
        Check(inventory.IsInventoryOpen && !player.CanAct, "closing details keeps build window blocking battle");
        yield return null;
        Click(Find(hud.transform, "CloseBuildInventory"));
        yield return null; yield return null;
        Check(!inventory.IsModalOpen && player.CanAct && consumedTurns == 0
            && player.GridPosition == position && player.CurrentHealth == health
            && player.SelectionMode == PlayerActionSelectionMode.Move,
            "closing build window restores selection without move, damage, or enemy turn");
        Check(EventSystem.current != null && hud.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null,
            "existing input UI infrastructure retained");
        hud.ReportPcPlaytestState();
    }
}
