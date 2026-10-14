using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 전투를 생략한 종료 상태 픽스처다. 정상 클리어/재미/길이 검사가 아니다.
[InitializeOnLoad]
public static class StageEndValidation
{
    private const string Pending = "GridCaster.StageEndValidation";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();
    private static IEnumerator routine;
    private static int frame;
    private static double deadline;
    private static bool previousRunInBackground;
    private static string runtimeError;

    static StageEndValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            checks.Clear(); frame = -1; runtimeError = null;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 180;
            Application.logMessageReceived += OnError;
            routine = Run();
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Stage End and Restart")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlaying || Enumerable.Range(0, SceneManager.sceneCount)
            .Any(index => SceneManager.GetSceneAt(index).isDirty))
            throw new InvalidOperationException("검사 전에 Play를 종료하고 씬을 저장하세요.");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnError(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(StageEndValidation))))
            runtimeError = message + "\n" + stack;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("종료 상태 검사 중단/시간 초과");
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (routine.MoveNext()) return;
            Finish(true);
        }
        catch (Exception exception) { checks.Add(exception.ToString()); Finish(false); }
    }

    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnError;
        Application.runInBackground = previousRunInBackground;
        string report = "STAGE_END_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled final-state fixtures + actual scene reload. NOT normal combat clears or human play.\n"
            + "Executed " + DateTime.Now.ToString("O") + "; Unity " + Application.unityVersion + "\n"
            + string.Join("\n", checks);
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "stage-end-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }

    private static Text TextIn(GameHudController hud, string name) =>
        hud.GetComponentsInChildren<Text>(true).First(text => text.name == name);

    private static void ClickResult(Button button)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.GetComponent<RectTransform>();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = rect.TransformPoint(rect.rect.center),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "actual result-panel GraphicRaycaster reaches replay button");
        button.OnPointerClick(pointer);
    }

    private static IEnumerator Run()
    {
        for (int traitIndex = 0; traitIndex < 4; traitIndex++)
        {
            yield return null; yield return null; yield return null;
            GameHudController hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
            Move player = UnityEngine.Object.FindAnyObjectByType<Move>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            RunProgressionSystem growth = player.GetComponent<RunProgressionSystem>();
            PlayerTraitSystem trait = player.GetComponent<PlayerTraitSystem>();
            Check(hud.IsTraitSelectionOpen && !hud.IsGameOver && !player.CanAct
                && trait.SelectedTraitId == string.Empty && trait.ChargeProgress == 0
                && growth.ActiveMovementArt == PlayerMovementArt.None && growth.Relics.Count == 0
                && RunProgressionCatalog.AllUpgrades.All(item => growth.Stack(item.Id) == 0)
                && spawner.CurrentWave == 1 && GameObject.Find("PlayerGrave") == null,
                "fresh scene resets build, charge, movement art, relics, wave and grave; trait=" + traitIndex);
            hud.SelectTrait(traitIndex); yield return null;
            GameObject.Find("UpgradeChoice_1").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Check(player.CanAct && RunProgressionCatalog.AllUpgrades.Any(item => growth.Stack(item.Id) > 0),
                "initial trait/upgrade still playable; trait=" + traitIndex);
            growth.SetMovementArtForPlaytest(PlayerMovementArt.Rook, 3);
            typeof(RunProgressionSystem).GetMethod("AddRelic", Private).Invoke(growth,
                new object[] { RunProgressionCatalog.Relics.First(item => item.Id == "kings_turn") });
            for (int i = 0; i < trait.ChargeRequired; i++) trait.RegisterCompletedAttack(true, false);
            // 종료 흐름만 압축한다. 정상 최종 웨이브의 전투/난이도를 시험했다고 주장하지 않는다.
            typeof(StageFlowManager).GetField("<CurrentWave>k__BackingField", Private)
                .SetValue(spawner.StageFlow, WaveTemplateCatalog.FinalWave);
            typeof(StageFlowManager).GetField("<State>k__BackingField", Private)
                .SetValue(spawner.StageFlow, StageFlowState.StageBoss);
            spawner.ResetPlaytestEncounter();
            typeof(MonsterSpawner).GetField("waveActive", Private).SetValue(spawner, false);
            Check(spawner.StartWave(WaveTemplateCatalog.FinalWave), "actual final-wave spawn API accepts compressed fixture");
            int stageEvents = 0;
            spawner.StageFlow.StageCleared += () => stageEvents++;
            foreach (MonsterMovement monster in spawner.ActiveMonsters.ToArray())
                monster.GetComponent<CharacterHealth>().TakeDamage(10000);
            while (spawner.StageFlow.State != StageFlowState.StageCleared) yield return null;
            yield return null;
            Check(stageEvents == 1 && !player.CanAct && !player.GetComponent<CharacterHealth>().IsDead,
                "final clear occurs once and locks living player; trait=" + traitIndex);
            hud.TogglePauseMenu(); hud.ContinueGame();
            Check(!player.CanAct && !hud.IsPauseMenuOpen,
                "pause/resume cannot reopen combat after stage clear; trait=" + traitIndex);
            string originalTrait = hud.SelectedTraitId;
            hud.SelectTrait((traitIndex + 1) % 4);
            Check(hud.SelectedTraitId == originalTrait && !player.CanAct
                && !player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.right),
                "stale selection and attack callbacks cannot change the finished build or advance combat");
            Check(hud.IsGameOverOverlayVisible && TextIn(hud, "GameOverTitle").text.Contains("클리어")
                && TextIn(hud, "GameOverSummary").text.Contains("WAVE 20"),
                "existing result panel clearly reports final-wave clear without a death claim");
            foreach (string name in new[] { "GameOverTitle", "GameOverSummary", "ReplayLabel", "QuitHint" })
            {
                Text text = TextIn(hud, name);
                Check(text.preferredHeight <= text.rectTransform.rect.height + 1f,
                    "result text fits existing pixel-font rectangle: " + name);
            }
            Image resultPanel = TextIn(hud, "GameOverTitle").transform.parent.GetComponent<Image>();
            Check(Mathf.Max(resultPanel.color.r, resultPanel.color.g, resultPanel.color.b) < 0.5f
                && resultPanel.GetComponent<Outline>().effectColor == new Color32(255, 210, 99, 255),
                "clear changes outline accent, never dark panel fill or pixel font");
            RectTransform overlay = resultPanel.transform.parent.GetComponent<RectTransform>();
            Check(overlay.parent == hud.transform && overlay.anchorMin == Vector2.zero
                && overlay.anchorMax == Vector2.one && overlay.offsetMin == Vector2.zero
                && overlay.offsetMax == Vector2.zero && overlay.localScale == Vector3.one,
                "result overlay blocks whole Canvas including board-side build/relic panels");
            if (traitIndex == 0)
            {
                string captureDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification/captures");
                Directory.CreateDirectory(captureDirectory);
                string capturePath = Path.Combine(captureDirectory, "stage-clear-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                ScreenCapture.CaptureScreenshot(capturePath);
                yield return null; yield return null; yield return null;
                Check(File.Exists(capturePath), "actual clear screenshot saved: " + Path.GetFileName(capturePath));
            }
            float elapsed = (float)typeof(GameHudController).GetField("elapsedPlayTime", Private).GetValue(hud);
            Vector3Int cell = player.GridPosition;
            hud.ToggleGameOverReview();
            Check(!hud.IsGameOverOverlayVisible && !player.CanAct, "clear review hides only result UI");
            yield return null; yield return null;
            Check(player.GridPosition == cell && stageEvents == 1
                && Mathf.Approximately(elapsed, (float)typeof(GameHudController).GetField("elapsedPlayTime", Private).GetValue(hud))
                && spawner.CurrentWave == 20, "clear review freezes timer/board and never starts wave 21");
            hud.ToggleGameOverReview();
            Check(hud.IsGameOverOverlayVisible && !player.CanAct, "review returns to result panel without resuming");
            ClickResult(TextIn(hud, "ReplayLabel").GetComponentInParent<Button>());
            yield return null; yield return null;
        }

        yield return null; yield return null;
        GameHudController deathHud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        Move deathPlayer = UnityEngine.Object.FindAnyObjectByType<Move>();
        deathPlayer.TakeDamage(10000);
        Check(deathHud.IsGameOver && deathHud.IsGameOverOverlayVisible && !deathPlayer.CanAct
            && GameObject.Find("PlayerGrave") != null && TextIn(deathHud, "GameOverTitle").text == "GAME OVER",
            "death result and grave remain distinct from clear result");
        deathHud.ToggleGameOverReview();
        deathHud.ContinueGame();
        Check(!deathHud.IsGameOverOverlayVisible && !deathPlayer.CanAct, "death review never unlocks input");
        deathHud.RestartGame();
        yield return null; yield return null; yield return null;
        deathHud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        deathPlayer = UnityEngine.Object.FindAnyObjectByType<Move>();
        Check(deathHud.IsTraitSelectionOpen && !deathHud.IsGameOver && !deathPlayer.CanAct
            && deathPlayer.CurrentHealth == deathPlayer.GetComponent<CharacterHealth>().MaxHealth
            && GameObject.Find("PlayerGrave") == null && Time.timeScale == 1f,
            "death replay starts clean trait selection with restored health/time");
    }
}
