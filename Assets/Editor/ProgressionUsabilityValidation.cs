using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 실제 씬의 카드·클릭 콜백과 보상 API를 검사하는 편의성 회귀 검증이다.
// 전투 승률 검증이 아니며 레벨/후보 상태를 구성하는 테스트 픽스처를 사용한다.
[InitializeOnLoad]
public static class ProgressionUsabilityValidation
{
    private const string PendingKey = "GridCaster.ProgressionUsabilityValidation";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();
    private static IEnumerator routine;
    private static double deadline;
    private static int lastFrame;
    private static string runtimeError;
    private static bool previousRunInBackground;

    static ProgressionUsabilityValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            checks.Clear();
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            runtimeError = null;
            lastFrame = -1;
            Application.logMessageReceived += CaptureRuntimeError;
            routine = Run();
            deadline = EditorApplication.timeSinceStartup + 180d;
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Reward Usability")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the current scene before validation.");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        if (!Application.isBatchMode)
        {
            Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameView).Show();
        }
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        try
        {
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Reward usability validation interrupted/timed out.");
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (routine.MoveNext()) return;
            Complete(true);
        }
        catch (Exception exception)
        {
            checks.Add(exception.ToString());
            Complete(false);
        }
    }

    private static void Complete(bool passed)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CaptureRuntimeError;
        Application.runInBackground = previousRunInBackground;
        string report = "REWARD_USABILITY_" + (passed ? "PASS" : "FAIL") + "\n" + string.Join("\n", checks);
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory);
        string reportName = Environment.GetCommandLineArgs().Contains("-startupControlsValidation")
            ? "startup-controls-usability-2026-10-01.txt" : "reward-usability-2026-09-30.txt";
        File.WriteAllText(Path.Combine(directory, reportName), report);
        Debug.Log(report);
        routine = null;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode || Environment.GetCommandLineArgs().Contains("-rewardUxExitOnComplete"))
            EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }

    private static void CaptureRuntimeError(string message, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")
            && stack.Contains("UnityEditor.Search.SearchDatabase") && !stack.Contains("Assets/Codes/")) return;
        // 에디터 Asset Store 로그인 실패는 게임 Play 오류와 분리한다.
        if (message.StartsWith("[Package Manager Window]", StringComparison.Ordinal)
            && message.Contains("Failed to call Unity ID to get auth code.")
            && !stack.Contains("Assets/Codes/")) return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            runtimeError = message + "\n" + stack;
    }

    private static void ClickButton(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { position = point, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button,
            "실제 UI 레이캐스트 클릭 대상: " + button.name + " (" + point + "; "
            + string.Join(", ", hits.Select(hit => hit.gameObject.name)) + "; depth="
            + button.targetGraphic.depth + "; raycaster=" + button.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>().enabled + ")");
        button.OnPointerClick(pointer);
    }

    private static void CapturePreview(string name)
    {
        if (Application.isBatchMode || !Environment.GetCommandLineArgs().Contains("-rewardUxCapture")) return;
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    }

    private static object Call(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
    }

    private static void ChooseFixture(RunProgressionSystem run, UpgradeDefinition item)
    {
        List<UpgradeDefinition> choices = Field<List<UpgradeDefinition>>(run, "currentUpgradeOptions");
        choices.Clear();
        choices.Add(item);
        typeof(RunProgressionSystem).GetField("starterSelectionPending", PrivateInstance).SetValue(run, true);
        Check(run.SelectUpgrade(0), "선택 API 적용: " + item.Id);
    }

    private static void ValidateStartupControls(GameHudController hud, Move player)
    {
        Canvas.ForceUpdateCanvases();
        Check(hud.IsTraitSelectionOpen && !player.CanAct, "시작 조작 안내는 기존 특성 선택 중에만 표시");
        UnityEngine.UI.Text footer = hud.GetComponentsInChildren<UnityEngine.UI.Text>(true)
            .First(text => text.name == "SelectionFooter");
        Check(footer.text == GameHudController.StartupControlsText
            && footer.text.Contains("좌클릭") && footer.text.Contains("우클릭")
            && !footer.text.Contains("F키"), "A/S/마우스 안내가 현재 입력과 일치하며 구형 F 안내 없음");
        Check(!footer.raycastTarget && !footer.resizeTextForBestFit && footer.fontSize == 18,
            "조작 안내는 클릭을 가로채지 않고 기존 픽셀 글자 크기를 유지");
        Check(footer.cachedTextGenerator.Populate(footer.text,
            footer.GetGenerationSettings(footer.rectTransform.rect.size)), "조작 안내 글자 생성 성공");
        Check(footer.cachedTextGenerator.lineCount == 4, "조작 안내 네 줄이 추가 줄바꿈 없이 배치");

        Vector3[] guideCorners = new Vector3[4];
        footer.rectTransform.GetWorldCorners(guideCorners);
        UnityEngine.UI.Button[] buttons = Field<UnityEngine.UI.Button[]>(hud, "traitChoiceButtons");
        foreach (UnityEngine.UI.Button button in buttons)
        {
            Vector3[] cardCorners = new Vector3[4];
            button.GetComponent<RectTransform>().GetWorldCorners(cardCorners);
            Check(guideCorners[1].y < cardCorners[0].y,
                "시작 안내와 특성 카드가 겹치지 않음: " + button.name);
        }
    }

    private static IEnumerator Run()
    {
        yield return null;
        yield return null;
        Move player = UnityEngine.Object.FindAnyObjectByType<Move>();
        RunProgressionSystem run = player.GetComponent<RunProgressionSystem>();
        GameHudController hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        RunProgressionUiController ui = UnityEngine.Object.FindAnyObjectByType<RunProgressionUiController>();
        GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        ValidateStartupControls(hud, player);
        foreach (Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
        {
            if (!grid.IsWalkableCell(cell)) continue;
            Color expected = (cell.x + cell.y) % 2 == 0 ? Color.white : new Color(0.8f, 0.85f, 0.9f);
            Check(grid.GroundTilemap.GetColor(cell) == expected, "첫 Play 화면 바닥 체크무늬: " + cell);
        }
        hud.SelectTrait(0);
        yield return null;
        yield return null;
        Check(!GameObject.Find("SelectionFooter"), "특성 선택 후 조작 안내가 전장을 가리지 않음");
        CapturePreview("short-reward-card");
        yield return null;
        Check(run.SelectUpgrade(0), "시작 증강 선택 완료");
        Call(ui, "SelectUpgrade", 0); // 비어 있는 후보에 대한 추가 클릭은 상태를 바꾸지 않는다.
        Check(!run.SelectUpgrade(0), "중복 선택 차단");
        Call(ui, "SetRightMode", Field<RectTransform>(ui, "rightPanel"));
        // 유물/증강 행이 활성화된 뒤 실제 Canvas 렌더 프레임을 지나 클릭 판정을 검사한다.
        yield return null;
        CapturePreview("checkerboard-gameplay");
        yield return null;

        UnityEngine.UI.Button owned = GameObject.Find("BuildRow_1").GetComponent<UnityEngine.UI.Button>();
        Vector3Int position = player.GridPosition;
        ClickButton(owned);
        Check(ui.IsDetailOpen && !player.CanAct, "보유 증강 클릭으로 상세창 및 입력 차단");
        player.ProcessInputFrame(false, true, false, false, Vector2.zero);
        Check(player.GridPosition == position, "상세창 중 이동 입력은 턴을 소비하지 않음");
        ui.CloseDetails();
        Check(!player.CanAct, "상세창을 닫은 클릭의 같은 프레임 보드 전달 차단");
        yield return null;
        Check(!ui.IsDetailOpen && player.CanAct, "닫은 뒤 기존 전투 입력 복원");
        player.SelectAttackMode();
        owned.onClick.Invoke();
        ui.CloseDetails();
        Check(player.SelectionMode == PlayerActionSelectionMode.Attack, "상세창 전후 공격 방향 선택 보존");
        player.CancelSelection();
        yield return null;
        UnityEngine.UI.Button movementHud = GameObject.Find("UltimateFrame").GetComponent<UnityEngine.UI.Button>();
        ClickButton(movementHud);
        Check(ui.IsDetailOpen, "이동술 HUD 상세 클릭 연결");
        ui.CloseDetails();
        yield return null;
        ClickButton(GameObject.Find("TraitHud").GetComponent<UnityEngine.UI.Button>());
        Check(ui.IsDetailOpen, "시작 특성 HUD 상세 클릭 연결");
        ui.CloseDetails();
        yield return null;

        Dictionary<string, int> stacks = Field<Dictionary<string, int>>(run, "upgradeStacks");
        stacks.Clear();
        run.SetMovementArtForPlaytest(PlayerMovementArt.Knight, 1);
        UpgradeDefinition training = RunProgressionCatalog.Upgrade("movement_training_1");
        string preview = RunProgressionDescriptions.ChoiceDescription(training, run);
        Check(preview.Contains("현재: 나이트 도약 Lv.1") && preview.Contains("선택 후: 나이트 도약 Lv.2")
            && preview.Contains("불길 제거"), "나이트 1→2 실제 불길 제거 효과 예고");
        ChooseFixture(run, training);
        Check(run.MovementArtLevel == 2, "나이트 연마 실제 Lv.2 적용");
        preview = RunProgressionDescriptions.ChoiceDescription(RunProgressionCatalog.Upgrade("movement_training_2"), run);
        Check(preview.Contains("선택 후: 나이트 도약 Lv.3") && preview.Contains("피해 +1"), "나이트 2→3 피해 보너스 예고");
        ChooseFixture(run, RunProgressionCatalog.Upgrade("movement_training_2"));
        Check(run.MovementArtLevel == 3 && !run.CanSelectUpgrade(RunProgressionCatalog.Upgrade("movement_training_3")), "최대 이동술 재연마 차단");
        var staleOptions = Field<List<UpgradeDefinition>>(run, "currentUpgradeOptions");
        staleOptions.Add(RunProgressionCatalog.Upgrade("movement_training_3"));
        typeof(RunProgressionSystem).GetField("starterSelectionPending", PrivateInstance).SetValue(run, true);
        Check(!run.SelectUpgrade(0) && run.MovementArtLevel == 3 && run.Stack("movement_training_3") == 0,
            "오래된 최대 연마 카드도 실제 선택 API에서 효과 없이 소비되지 않음");
        preview = RunProgressionDescriptions.ChoiceDescription(RunProgressionCatalog.Upgrade("movement_bishop"), run);
        Check(preview.Contains("비숍 활보 Lv.3") && preview.Contains("최대 5칸") && preview.Contains("레벨은 유지"), "이동술 교체 예고에 보존 레벨과 거리 표시");
        ChooseFixture(run, RunProgressionCatalog.Upgrade("movement_bishop"));
        Check(run.ActiveMovementArt == PlayerMovementArt.Bishop && run.MovementArtLevel == 3, "실제 교체 레벨 보존");

        foreach (string trait in new[] { "double_cast", "pierce", "damage_boost", "knockback" })
        {
            stacks.Clear();
            player.GetComponent<PlayerTraitSystem>().SelectTrait(trait, trait == "double_cast" ? 5 : trait == "pierce" ? 3 : 4);
            foreach (UpgradeDefinition item in RunProgressionCatalog.AllUpgrades.Where(item => !item.IsTraitUpgrade || item.RequiredTrait == trait))
            {
                for (int level = 0; level < item.MaxStacks; level++)
                {
                    stacks[item.Id] = level;
                    IReadOnlyList<UpgradeDefinition> options = new[] { item };
                    Call(ui, "ShowUpgradeOptions", options);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    UnityEngine.UI.Text label = Field<UnityEngine.UI.Text[]>(ui, "upgradeTexts")[0];
                    Check(label.preferredHeight <= label.rectTransform.rect.height + 1f,
                        "선택 문구 잘림 없음: " + trait + "/" + item.Id + "/" + level
                        + " (" + label.preferredHeight + "/" + label.rectTransform.rect.height + ")");
                    Check(label.text.Length < 200 && !label.text.Contains("독립 추첨")
                        && !label.text.Contains("이번 선택:") && !label.text.Contains("진행 중인 묶음"),
                        "선택 카드는 핵심 효과만 표시: " + item.Id);
                    if (item.IsTraitUpgrade || RunProgressionDescriptions.IsFrequencyUpgrade(item.Id))
                        Check(label.text.Contains("평균 발동 확률"), "특성 증강 발동 확률 표시: " + item.Id);
                    ClickButton(GameObject.Find("UpgradeChoice_1").transform.Find("Details").GetComponent<UnityEngine.UI.Button>());
                    Check(ui.IsDetailOpen, "선택 카드 상세 확인: " + item.Id);
                    UnityEngine.UI.Text detail = Field<UnityEngine.UI.Text>(ui, "detailText");
                    Canvas.ForceUpdateCanvases();
                    Check(detail.preferredHeight <= detail.rectTransform.rect.height + 1f,
                        "선택 상세 문구 잘림 없음: " + item.Id + "/" + level
                        + " (" + detail.preferredHeight + "/" + detail.rectTransform.rect.height + ")");
                    ui.CloseDetails();
                    Check(!label.text.Contains("발동 백") && !label.text.Contains("셔플 백"), "플레이어 문구에 발동백 없음: " + item.Id);
                }
                stacks[item.Id] = item.MaxStacks;
                Check(!run.CanSelectUpgrade(item), "최대 증강 후보/선택 제외: " + item.Id);
            }
            stacks.Clear();
            run.SetMovementArtForPlaytest(PlayerMovementArt.Knight, 3);
            for (int tier = 1; tier <= 4; tier++)
            {
                Call(run, "BuildAdvancedUpgradeOptions", tier);
                Check(run.CurrentUpgradeOptions.All(item => !item.Id.StartsWith("movement_training_") && run.CanSelectUpgrade(item)),
                    "최대 이동술의 실제 상급 후보에서 연마 제외: " + trait + "/" + tier);
            }
        }
        Call(ui, "SetRightMode", Field<RectTransform>(ui, "rightPanel"));
        typeof(RunProgressionSystem).GetField("starterSelectionPending", PrivateInstance).SetValue(run, false);
        player.SetInputEnabled(true);
        yield return null;
        for (int level = 1; level <= 3; level++)
        foreach (PlayerMovementArt art in new[] { PlayerMovementArt.Knight, PlayerMovementArt.Bishop, PlayerMovementArt.Rook })
        {
            run.SetMovementArtForPlaytest(art, level);
            ui.ShowMovementDetails();
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.Text label = Field<UnityEngine.UI.Text>(ui, "detailText");
            Check(label.preferredHeight <= label.rectTransform.rect.height + 1f, "이동술 상세 문구 잘림 없음: " + art + "/" + level);
            ui.CloseDetails();
            yield return null;
        }
        foreach (RelicDefinition relic in RunProgressionCatalog.Relics)
        {
            Call(ui, "OpenDetails", relic.Name, relic.Description);
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.Text label = Field<UnityEngine.UI.Text>(ui, "detailText");
            Check(label.preferredHeight <= label.rectTransform.rect.height + 1f, "유물 상세 문구 잘림 없음: " + relic.Id);
            ui.CloseDetails();
            yield return null;
        }

        UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
        HashSet<string> shown = Field<HashSet<string>>(run, "shownRelics");
        List<RelicOffer> offers = Field<List<RelicOffer>>(run, "activeAltars");
        int pairCount = 0;
        foreach (string trait in new[] { "double_cast", "pierce", "damage_boost", "knockback" })
        {
            player.GetComponent<PlayerTraitSystem>().SelectTrait(trait, 4);
            for (int seed = 0; seed < 256; seed++)
            {
                shown.Clear();
                UnityEngine.Random.InitState(seed);
                foreach (int wave in new[] { 4, 8, 12, 16 })
                {
                    offers.Clear();
                    Call(run, "AddAltarOffer", wave, new Vector3Int(0, 0, 0), false);
                    Call(run, "AddAltarOffer", wave, new Vector3Int(1, 0, 0), true);
                    if (offers.Count != 2 || offers[0].IsRisky || !offers[1].IsRisky
                        || offers[0].Relic.Id == offers[1].Relic.Id || offers[1].Relic.Rarity < offers[0].Relic.Rarity
                        || (wave == 16 && offers.Any(item => item.Relic.Rarity < RelicRarity.Rare)))
                        throw new InvalidOperationException("제단 등급/중복 실패: " + trait + "/" + seed + "/" + wave);
                    List<RelicDefinition> remaining = RunProgressionCatalog.Relics.Where(item => item.MinimumWave <= wave
                        && (!item.IsTraitRelic || item.RequiredTrait == trait) && !shown.Contains(item.Id)).ToList();
                    if (offers[0].Relic.Rarity < RelicRarity.Legendary && remaining.Any(item => item.Rarity > offers[0].Relic.Rarity)
                        && offers[1].Relic.Rarity == offers[0].Relic.Rarity)
                        throw new InvalidOperationException("상위 후보가 남았는데 동급 위험 제단: " + trait + "/" + seed + "/" + wave);
                    pairCount++;
                }
            }
        }
        UnityEngine.Random.state = originalRandom;
        Check(pairCount == 4096, "4특성 × 256시드 × 4제단 웨이브: 위험 등급 하락 없음, 중복 없음, 16웨이브 최소 희귀");
        yield return null;
    }
}
