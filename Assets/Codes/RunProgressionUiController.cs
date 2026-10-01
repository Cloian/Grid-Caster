using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// 보드를 가리지 않는 좌우 전용 영역에 현재 빌드와 선택지를 표시한다.
[DisallowMultipleComponent]
public sealed class RunProgressionUiController : MonoBehaviour
{
    private sealed class IconView
    {
        public UnityEngine.UI.Image Image;
        public UnityEngine.UI.Text Placeholder;
    }

    private static readonly Color PanelColor = new Color32(7, 16, 31, 242);
    private static readonly Color CardColor = new Color32(17, 34, 59, 250);
    private static readonly Color BorderColor = new Color32(73, 128, 196, 255);
    private static readonly Color TextColor = new Color32(235, 244, 255, 255);
    private static readonly Color MutedColor = new Color32(150, 177, 211, 255);
    private static readonly Color CommonColor = new Color32(201, 213, 226, 255);
    private static readonly Color RareColor = new Color32(83, 180, 255, 255);
    private static readonly Color LegendaryColor = new Color32(255, 188, 65, 255);
    private static readonly Color SafeColor = new Color32(68, 219, 232, 255);
    private static readonly Color RiskyColor = new Color32(255, 91, 76, 255);

    private const int MaximumBuildRows = 14;
    private const float PanelHeight = 820f;
    private const float DefaultPanelWidth = 330f;
    private const float BoardPanelGap = 18f;
    private const float ScreenEdgeMargin = 18f;
    private const float MinimumPanelScale = 0.35f;

    private RunProgressionSystem progression;
    private CameraController boardCamera;
    private Font font;
    private RectTransform canvasRect;
    private RectTransform leftPanel;
    private RectTransform rightPanel;
    private RectTransform upgradePanel;
    private RectTransform replacementPanel;
    private RectTransform detailPanel;
    private RectTransform detailCard;
    private UnityEngine.UI.Text detailTitle;
    private UnityEngine.UI.Text detailText;
    private UnityEngine.UI.Text movementSummary;
    private UnityEngine.UI.Text upgradeTitle;
    private UnityEngine.UI.Text upgradeGuide;
    private readonly string[] buildUpgradeIds = new string[MaximumBuildRows];
    private readonly UnityEngine.UI.Button[] buildButtons = new UnityEngine.UI.Button[MaximumBuildRows];
    private readonly UnityEngine.UI.Button[] relicButtons = new UnityEngine.UI.Button[RunProgressionSystem.MaxRelicSlots];
    private readonly GameObject[] buildRows = new GameObject[MaximumBuildRows];
    private readonly IconView[] buildIcons = new IconView[MaximumBuildRows];
    private readonly UnityEngine.UI.Text[] buildTexts = new UnityEngine.UI.Text[MaximumBuildRows];
    private readonly IconView[] relicIcons = new IconView[RunProgressionSystem.MaxRelicSlots];
    private readonly UnityEngine.UI.Text[] relicTexts = new UnityEngine.UI.Text[RunProgressionSystem.MaxRelicSlots];
    private readonly UnityEngine.UI.Text[] altarTexts = new UnityEngine.UI.Text[2];
    private readonly IconView[] altarIcons = new IconView[2];
    private readonly UnityEngine.UI.Button[] upgradeButtons = new UnityEngine.UI.Button[3];
    private readonly UnityEngine.UI.Text[] upgradeTexts = new UnityEngine.UI.Text[3];
    private readonly IconView[] upgradeIcons = new IconView[3];
    private readonly UpgradeDefinition[] displayedUpgrades = new UpgradeDefinition[3];
    private readonly UnityEngine.UI.Button[] replacementButtons = new UnityEngine.UI.Button[5];
    private readonly UnityEngine.UI.Text[] replacementTexts = new UnityEngine.UI.Text[5];
    private readonly IconView[] replacementIcons = new IconView[4];
    private UnityEngine.UI.Text emptyBuildText;
    private UnityEngine.UI.Text supplyText;
    private UnityEngine.UI.Text altarEmptyText;
    private RelicDefinition incomingRelicDetails;
    private bool initialized;
    private Vector2Int lastScreenSize;

    public bool IsModalOpen => (upgradePanel != null && upgradePanel.gameObject.activeSelf)
        || (replacementPanel != null && replacementPanel.gameObject.activeSelf)
        || IsDetailOpen;
    public bool IsDetailOpen => detailPanel != null && detailPanel.gameObject.activeSelf;

    public void Initialize(RunProgressionSystem target, CameraController cameraController)
    {
        if (initialized || target == null)
            return;

        progression = target;
        boardCamera = cameraController != null
            ? cameraController : FindAnyObjectByType<CameraController>();
        progression.TryInitialize();
        font = GetComponentsInChildren<UnityEngine.UI.Text>(true)
            .Select(item => item.font).FirstOrDefault(item => item != null);
        canvasRect = GetComponent<RectTransform>();

        BuildLeftPanel();
        BuildRightPanel();
        BuildUpgradePanel();
        BuildReplacementPanel();
        BuildDetailPanel();

        progression.UpgradeOptionsReady += ShowUpgradeOptions;
        progression.UpgradesChanged += RefreshBuild;
        progression.RelicsChanged += RefreshRelics;
        progression.AltarsChanged += RefreshAltars;
        progression.RelicReplacementRequested += ShowReplacement;
        progression.MovementArtChanged += RefreshMovementSummary;

        LayoutPanels();
        RefreshBuild();
        RefreshRelics(progression.Relics, progression.Supply);
        RefreshAltars(progression.ActiveAltars);
        initialized = true;
    }

    private void Update()
    {
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreenSize) LayoutPanels();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (IsDetailOpen) return;
        if (upgradePanel != null && upgradePanel.gameObject.activeSelf)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectUpgrade(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) SelectUpgrade(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) SelectUpgrade(2);
        }
        else if (replacementPanel != null && replacementPanel.gameObject.activeSelf)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) ResolveRelic(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) ResolveRelic(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) ResolveRelic(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) ResolveRelic(3);
            else if (keyboard.digit5Key.wasPressedThisFrame) ResolveRelic(-1);
        }
    }

    private void BuildLeftPanel()
    {
        leftPanel = CreatePanel("BuildHudPanel", transform, false);
        CreateText("BuildTitle", leftPanel, "강화 빌드", 22, TextAnchor.MiddleLeft,
            TextColor, new Vector2(18f, -18f), new Vector2(-36f, 40f));
        CreateText("BuildSubtitle", leftPanel, "보유 항목을 클릭하면 상세 설명", 12,
            TextAnchor.MiddleLeft, MutedColor, new Vector2(18f, -56f), new Vector2(-36f, 26f));
        RectTransform movementRow = CreateCard("MovementArtDetails", leftPanel,
            new Vector2(14f, -90f), new Vector2(-28f, 48f), LegendaryColor);
        AddDetailButton(movementRow, ShowMovementDetails);
        movementSummary = CreateText("Label", movementRow, string.Empty, 13,
            TextAnchor.MiddleLeft, TextColor, new Vector2(10f, -4f), new Vector2(-20f, 40f));
        emptyBuildText = CreateText("EmptyBuild", leftPanel, "아직 획득한 강화가 없습니다", 13,
            TextAnchor.MiddleCenter, MutedColor, new Vector2(18f, -150f), new Vector2(-36f, 48f));

        for (int i = 0; i < MaximumBuildRows; i++)
        {
            RectTransform row = CreateCard($"BuildRow_{i + 1}", leftPanel,
                new Vector2(14f, -148f - i * 46f), new Vector2(-28f, 42f), BorderColor);
            int captured = i;
            buildButtons[i] = AddDetailButton(row, () => ShowUpgradeDetails(buildUpgradeIds[captured]));
            buildRows[i] = row.gameObject;
            buildIcons[i] = CreateIcon("Icon", row, new Vector2(7f, -5f), new Vector2(32f, 32f));
            buildTexts[i] = CreateText("Label", row, string.Empty, 13, TextAnchor.MiddleLeft,
                TextColor, new Vector2(48f, -3f), new Vector2(-56f, 36f));
        }
    }

    private void BuildRightPanel()
    {
        rightPanel = CreatePanel("RelicHudPanel", transform, true);
        CreateText("RelicTitle", rightPanel, "유물", 22, TextAnchor.MiddleLeft,
            TextColor, new Vector2(18f, -18f), new Vector2(-36f, 40f));
        supplyText = CreateText("SupplyText", rightPanel, "보급 0", 13,
            TextAnchor.MiddleRight, LegendaryColor, new Vector2(18f, -20f), new Vector2(-36f, 36f));

        for (int i = 0; i < RunProgressionSystem.MaxRelicSlots; i++)
        {
            RectTransform row = CreateCard($"RelicSlot_{i + 1}", rightPanel,
                new Vector2(14f, -72f - i * 92f), new Vector2(-28f, 82f), BorderColor);
            int captured = i;
            relicButtons[i] = AddDetailButton(row, () => ShowRelicDetails(captured));
            relicIcons[i] = CreateIcon("Icon", row, new Vector2(9f, -11f), new Vector2(58f, 58f));
            relicTexts[i] = CreateText("Label", row, $"슬롯 {i + 1}  비어 있음", 12,
                TextAnchor.MiddleLeft, MutedColor, new Vector2(78f, -6f), new Vector2(-86f, 70f));
        }

        CreateText("AltarTitle", rightPanel, "현재 유물 제단", 16, TextAnchor.MiddleLeft,
            TextColor, new Vector2(18f, -454f), new Vector2(-36f, 32f));
        altarEmptyText = CreateText("AltarEmpty", rightPanel, "현재 웨이브에는 제단이 없습니다", 12,
            TextAnchor.MiddleCenter, MutedColor, new Vector2(18f, -500f), new Vector2(-36f, 52f));
        for (int i = 0; i < 2; i++)
        {
            Color border = i == 0 ? SafeColor : RiskyColor;
            RectTransform row = CreateCard(i == 0 ? "SafeAltarOffer" : "RiskyAltarOffer",
                rightPanel, new Vector2(14f, -496f - i * 126f), new Vector2(-28f, 114f), border);
            int captured = i;
            AddDetailButton(row, () => ShowAltarDetails(captured));
            altarIcons[i] = CreateIcon("Icon", row, new Vector2(9f, -26f), new Vector2(58f, 58f));
            altarTexts[i] = CreateText("Label", row, string.Empty, 12, TextAnchor.MiddleLeft,
                TextColor, new Vector2(78f, -7f), new Vector2(-86f, 100f));
            row.gameObject.SetActive(false);
        }
    }

    private void BuildUpgradePanel()
    {
        upgradePanel = CreateCenterOverlay("UpgradeSelectionPanel", transform);
        upgradeTitle = CreateText("Title", upgradePanel, "웨이브 강화 선택", 30,
            TextAnchor.MiddleCenter, TextColor, Vector2.zero, new Vector2(900f, 60f));
        SetCentered(upgradeTitle.rectTransform, new Vector2(0f, 310f), new Vector2(1000f, 60f));
        upgradeGuide = CreateText("Guide", upgradePanel,
            "하나를 선택하면 다음 웨이브가 바로 시작됩니다  ·  클릭 또는 1 · 2 · 3", 14,
            TextAnchor.MiddleCenter, MutedColor, Vector2.zero, new Vector2(900f, 34f));
        SetCentered(upgradeGuide.rectTransform, new Vector2(0f, 266f), new Vector2(1000f, 34f));
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            RectTransform card = CreateFixedCard($"UpgradeChoice_{i + 1}", upgradePanel,
                new Vector2((i - 1) * 326f, -10f), new Vector2(306f, 470f), BorderColor);
            UnityEngine.UI.Button button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image cardImage = card.GetComponent<UnityEngine.UI.Image>();
            cardImage.raycastTarget = true;
            button.targetGraphic = cardImage;
            button.onClick.AddListener(() => SelectUpgrade(captured));
            upgradeButtons[i] = button;
            upgradeIcons[i] = CreateIcon("Icon", card, new Vector2(121f, -20f), new Vector2(64f, 64f));
            RectTransform details = CreateCard("Details", card, new Vector2(214f, -24f), new Vector2(74f, 32f), BorderColor);
            details.anchorMin = details.anchorMax = new Vector2(0f, 1f);
            details.sizeDelta = new Vector2(74f, 32f);
            AddDetailButton(details, () =>
            {
                UpgradeDefinition item = displayedUpgrades[captured];
                if (item != null) OpenDetails(RunProgressionDescriptions.ChoiceName(item, progression),
                    RunProgressionDescriptions.ChoiceDetails(item, progression));
            });
            CreateText("Label", details, "상세", 13, TextAnchor.MiddleCenter,
                TextColor, new Vector2(4f, -2f), new Vector2(-8f, 28f));
            upgradeTexts[i] = CreateText("Label", card, string.Empty, 18, TextAnchor.UpperCenter,
                TextColor, new Vector2(18f, -96f), new Vector2(-36f, 354f));
        }
        upgradePanel.gameObject.SetActive(false);
    }

    private void BuildDetailPanel()
    {
        detailPanel = CreateCenterOverlay("ProgressionDetailOverlay", transform);
        UnityEngine.UI.Button backdrop = detailPanel.gameObject.AddComponent<UnityEngine.UI.Button>();
        backdrop.targetGraphic = detailPanel.GetComponent<UnityEngine.UI.Image>();
        backdrop.transition = UnityEngine.UI.Selectable.Transition.None;
        backdrop.onClick.AddListener(CloseDetails);
        detailCard = CreateFixedCard("ProgressionDetailCard", detailPanel,
            Vector2.zero, new Vector2(660f, 590f), BorderColor);
        detailCard.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        detailTitle = CreateText("Title", detailCard, string.Empty, 24,
            TextAnchor.MiddleLeft, TextColor, new Vector2(28f, -24f), new Vector2(-56f, 64f));
        detailText = CreateText("Description", detailCard, string.Empty, 18,
            TextAnchor.UpperLeft, TextColor, new Vector2(28f, -102f), new Vector2(-56f, 386f));
        RectTransform close = CreateFixedCard("CloseDetails", detailCard,
            new Vector2(0f, -248f), new Vector2(270f, 48f), BorderColor);
        AddDetailButton(close, CloseDetails);
        UnityEngine.UI.Text label = CreateText("Label", close, "닫기 · Esc / 바깥 클릭", 16,
            TextAnchor.MiddleCenter, TextColor, Vector2.zero, Vector2.zero);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;
        detailPanel.gameObject.SetActive(false);
    }

    private UnityEngine.UI.Button AddDetailButton(RectTransform card, UnityEngine.Events.UnityAction action)
    {
        UnityEngine.UI.Image image = card.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = true;
        UnityEngine.UI.Button button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        return button;
    }

    private void OpenDetails(string title, string description)
    {
        if (detailPanel == null) return;
        Move player = progression.GetComponent<Move>();
        if (player.IsActionInProgress && !player.IsChoosingEchoDirection && !player.IsChoosingFreeMove
            && !replacementPanel.gameObject.activeSelf && !upgradePanel.gameObject.activeSelf) return;
        detailTitle.text = title;
        detailText.text = description;
        player.SetInspectionOpen(true);
        detailPanel.gameObject.SetActive(true);
        detailPanel.SetAsLastSibling();
    }

    public void CloseDetails()
    {
        if (detailPanel != null) detailPanel.gameObject.SetActive(false);
        if (progression != null) progression.GetComponent<Move>().SetInspectionOpen(false);
    }

    public void ShowUpgradeDetails(string id)
    {
        if (string.IsNullOrEmpty(id) || progression.Stack(id) == 0) return;
        UpgradeDefinition item = RunProgressionCatalog.Upgrade(id);
        OpenDetails(item.Name, RunProgressionDescriptions.OwnedDescription(item, progression));
    }

    public void ShowMovementDetails()
    {
        string level = progression.HasMovementArt ? $" · 현재 Lv.{progression.MovementArtLevel} / 최대 Lv.3" : "";
        OpenDetails(progression.MovementArtName + level,
            RunProgressionDescriptions.MovementEffect(progression.ActiveMovementArt, progression.MovementArtLevel));
    }

    public void ShowTraitDetails()
    {
        if (string.IsNullOrEmpty(progression.SelectedTraitId)) return;
        OpenDetails("시작 특성 · 평균 발동 확률 " + RunProgressionDescriptions.Chance(progression.TraitActivationInterval),
            RunProgressionDescriptions.TraitEffect(progression.SelectedTraitId)
            + $"\n\n평균 발동 확률: {RunProgressionDescriptions.Chance(progression.TraitActivationInterval)}\n매 공격마다 독립 추첨하는 방식이 아니라, 공격 {progression.TraitActivationInterval}회 묶음 안에서 한 번 발동을 보장합니다. 빈도 강화를 얻으면 진행 중인 묶음 이후에 적용됩니다.");
    }

    private void ShowRelicDetails(int index)
    {
        if (index < 0 || index >= progression.Relics.Count) return;
        RelicDefinition relic = progression.Relics[index];
        OpenDetails(RarityName(relic.Rarity) + " 유물 · " + relic.Name, relic.Description);
    }

    private void ShowAltarDetails(int index)
    {
        if (index < 0 || index >= progression.ActiveAltars.Count) return;
        RelicOffer offer = progression.ActiveAltars[index];
        OpenDetails((offer.IsRisky ? "위험" : "안전") + " 제단 · " + offer.Relic.Name,
            RarityName(offer.Relic.Rarity) + " 유물\n\n" + offer.Relic.Description
            + "\n\n제단 칸으로 이동하면 즉시 획득하고 적 턴이 진행됩니다. 하나를 획득하면 다른 제단은 사라집니다.\n위험 제단은 안전 제단보다 높은 등급을 우선 보장하며, 상위 후보가 소진되면 같은 등급을 제공합니다.");
    }

    private void RefreshMovementSummary(PlayerMovementArt art, int level)
    {
        if (movementSummary != null)
            movementSummary.text = progression.HasMovementArt
                ? $"{progression.MovementArtName} · 현재 Lv.{level}" + (level == RunProgressionSystem.MaxMovementArtLevel ? " (최대)" : "")
                : "이동술 미보유 · 클릭하여 설명";
    }

    private void BuildReplacementPanel()
    {
        replacementPanel = CreatePanel("RelicReplacementPanel", transform, true);
        CreateText("Title", replacementPanel, "유물 슬롯이 가득 찼습니다", 19,
            TextAnchor.MiddleLeft, TextColor, new Vector2(18f, -18f), new Vector2(-36f, 40f));
        CreateText("Guide", replacementPanel, "교체할 유물 또는 해체 선택 · 1~5", 12,
            TextAnchor.MiddleLeft, MutedColor, new Vector2(18f, -58f), new Vector2(-36f, 28f));
        for (int i = 0; i < 5; i++)
        {
            int captured = i;
            RectTransform card = CreateCard($"ReplacementChoice_{i + 1}", replacementPanel,
                new Vector2(14f, -106f - i * 132f), new Vector2(-28f, 116f),
                i == 4 ? LegendaryColor : BorderColor);
            UnityEngine.UI.Button button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image cardImage = card.GetComponent<UnityEngine.UI.Image>();
            cardImage.raycastTarget = true;
            button.targetGraphic = cardImage;
            button.onClick.AddListener(() => ResolveRelic(captured == 4 ? -1 : captured));
            replacementButtons[i] = button;
            if (i < 4)
            {
                replacementIcons[i] = CreateIcon("Icon", card,
                    new Vector2(10f, -25f), new Vector2(58f, 58f));
            }
            replacementTexts[i] = CreateText("Label", card, string.Empty, 12,
                TextAnchor.MiddleLeft, TextColor, new Vector2(i < 4 ? 80f : 18f, -7f),
                new Vector2(i < 4 ? -92f : -36f, 68f));
            RectTransform details = CreateCard("Details", card,
                new Vector2(80f, -80f), new Vector2(-92f, 28f), BorderColor);
            AddDetailButton(details, () =>
            {
                if (captured < 4) ShowRelicDetails(captured);
                else if (incomingRelicDetails != null)
                    OpenDetails("새 유물 · " + incomingRelicDetails.Name, incomingRelicDetails.Description);
            });
            CreateText("Label", details, "효과·조건 상세", 12, TextAnchor.MiddleCenter,
                TextColor, new Vector2(4f, -2f), new Vector2(-8f, 24f));
        }
        replacementPanel.gameObject.SetActive(false);
    }

    private void LayoutPanels()
    {
        if (canvasRect == null || leftPanel == null) return;
        float canvasWidth = canvasRect.rect.width > 0f ? canvasRect.rect.width : 1920f;
        float canvasHeight = canvasRect.rect.height > 0f ? canvasRect.rect.height : 1080f;

        // 패널을 화면 모서리가 아니라 실제 보드 가장자리에 붙인다. 넓은 화면에서도
        // 공백이 벌어지지 않고, 좁은 화면에서는 패널 전체를 같은 비율로 축소한다.
        float panelScale = Mathf.Min(1f,
            (canvasHeight - ScreenEdgeMargin * 2f) / PanelHeight);
        float cameraPanelWidth = Mathf.Clamp((canvasWidth - 900f) * 0.5f - 24f,
            250f, DefaultPanelWidth);
        float cameraSideReservation = cameraPanelWidth + 34f;
        // 카메라 맞춤은 기존과 같은 값으로 한 번만 갱신한다. 패널 배치 반복 계산이
        // 보드의 상하 HUD 안전 간격에 영향을 주지 않게 한다.
        boardCamera?.SetSideHudReservation(cameraSideReservation);
        float boardHalfWidth = 432f;
        for (int pass = 0; pass < 3; pass++)
        {
            boardHalfWidth = GetBoardHalfWidthInCanvas(boardHalfWidth);
            float horizontalScale = (canvasWidth * 0.5f - boardHalfWidth
                - BoardPanelGap - ScreenEdgeMargin) / DefaultPanelWidth;
            float verticalScale = (canvasHeight - ScreenEdgeMargin * 2f) / PanelHeight;
            panelScale = Mathf.Clamp(Mathf.Min(horizontalScale, verticalScale),
                MinimumPanelScale, 1f);
        }

        float innerEdge = boardHalfWidth + BoardPanelGap;
        LayoutSidePanel(leftPanel, false, innerEdge, panelScale);
        LayoutSidePanel(rightPanel, true, innerEdge, panelScale);
        LayoutSidePanel(replacementPanel, true, innerEdge, panelScale);
        LayoutBoardEdgeHud();
        if (detailCard != null)
        {
            float scale = Mathf.Min(1f, (canvasWidth - 36f) / 660f, (canvasHeight - 36f) / 590f);
            detailCard.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }

    private void LayoutBoardEdgeHud()
    {
        Camera camera = boardCamera != null ? boardCamera.GetComponent<Camera>() : null;
        GridManager grid = FindAnyObjectByType<GridManager>();
        RectTransform waveHud = GameObject.Find("WaveHud")?.GetComponent<RectTransform>();
        RectTransform moveHud = GameObject.Find("MoveActionFrame")?.GetComponent<RectTransform>();
        if (camera == null || grid == null || grid.GroundTilemap == null
            || waveHud == null || moveHud == null || Screen.height <= 0) return;

        BoundsInt bounds = grid.GroundTilemap.cellBounds;
        float boardMinimumY = camera.WorldToScreenPoint(
            grid.GroundTilemap.CellToWorld(bounds.min)).y;
        float boardMaximumY = camera.WorldToScreenPoint(
            grid.GroundTilemap.CellToWorld(bounds.max)).y;
        float referenceScale = Screen.height / 1080f;
        float desiredGap = BoardPanelGap * referenceScale;

        Vector3[] corners = new Vector3[4];
        waveHud.GetWorldCorners(corners);
        float topDelta = (boardMaximumY + desiredGap - corners[0].y) / referenceScale;
        MoveHudGroupVertically(topDelta, "WaveHud", "TimeHud");

        moveHud.GetWorldCorners(corners);
        float bottomDelta = (boardMinimumY - desiredGap - corners[1].y) / referenceScale;
        MoveHudGroupVertically(bottomDelta, "UltimateFrame", "HealthHud", "TraitHud",
            "AttackActionFrame", "MoveActionFrame", "ActionModeHud");
    }

    private static void MoveHudGroupVertically(float delta, params string[] names)
    {
        if (Mathf.Abs(delta) < 0.01f) return;
        foreach (string name in names)
        {
            RectTransform rect = GameObject.Find(name)?.GetComponent<RectTransform>();
            if (rect == null) continue;
            Vector2 position = rect.anchoredPosition;
            position.y += delta;
            rect.anchoredPosition = position;
        }
    }

    private float GetBoardHalfWidthInCanvas(float fallback)
    {
        Camera camera = boardCamera != null ? boardCamera.GetComponent<Camera>() : null;
        GridManager grid = FindAnyObjectByType<GridManager>();
        if (camera == null || grid == null || grid.GroundTilemap == null)
            return fallback;

        BoundsInt bounds = grid.GroundTilemap.cellBounds;
        Vector3 worldMinimum = grid.GroundTilemap.CellToWorld(bounds.min);
        Vector3 worldMaximum = grid.GroundTilemap.CellToWorld(bounds.max);
        Vector2 localMinimum;
        Vector2 localMaximum;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                camera.WorldToScreenPoint(worldMinimum), null, out localMinimum)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                camera.WorldToScreenPoint(worldMaximum), null, out localMaximum))
            return fallback;

        return Mathf.Max(Mathf.Abs(localMinimum.x), Mathf.Abs(localMaximum.x));
    }

    private static void LayoutSidePanel(RectTransform panel, bool right,
        float innerEdge, float scale)
    {
        if (panel == null) return;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = right ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(right ? innerEdge : -innerEdge, 0f);
        panel.sizeDelta = new Vector2(DefaultPanelWidth, PanelHeight);
        panel.localScale = new Vector3(scale, scale, 1f);
    }

    private void ShowUpgradeOptions(IReadOnlyList<UpgradeDefinition> options)
    {
        CloseDetails();
        upgradeTitle.text = progression.IsStarterUpgradeSelection ? "시작 증강 선택" : "웨이브 증강 선택";
        upgradeGuide.text = progression.IsStarterUpgradeSelection
            ? "현재 → 선택 후를 비교하세요 · 하나를 고르면 전투 시작 · 클릭 또는 1 · 2 · 3"
            : "현재 → 선택 후를 비교하세요 · 하나를 고르면 다음 웨이브 · 클릭 또는 1 · 2 · 3";
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            bool available = i < options.Count;
            displayedUpgrades[i] = available ? options[i] : null;
            upgradeButtons[i].gameObject.SetActive(available);
            if (!available) continue;
            UpgradeDefinition item = options[i];
            string grade = progression.IsStarterUpgradeSelection
                ? "[시작 강화] "
                : item.IsAdvanced ? "[상급 강화] " : string.Empty;
            upgradeTexts[i].text = $"{i + 1}. {grade}{RunProgressionDescriptions.ChoiceName(item, progression)}\n\n{RunProgressionDescriptions.ChoiceSummary(item, progression)}";
            upgradeButtons[i].interactable = progression.CanSelectUpgrade(item);
            ApplyIcon(upgradeIcons[i], item.Id, item.Name);
        }
        SetRightMode(upgradePanel);
    }

    private void SelectUpgrade(int index)
    {
        if (IsDetailOpen) return;
        if (!progression.SelectUpgrade(index)) return;
        SetRightMode(rightPanel);
    }

    private void RefreshBuild()
    {
        RefreshMovementSummary(progression.ActiveMovementArt, progression.MovementArtLevel);
        List<UpgradeDefinition> acquired = RunProgressionCatalog.AllUpgrades
            .Where(item => progression.Stack(item.Id) > 0).ToList();
        emptyBuildText.gameObject.SetActive(acquired.Count == 0);
        for (int i = 0; i < buildRows.Length; i++)
        {
            bool visible = i < acquired.Count;
            buildRows[i].SetActive(visible);
            if (!visible) continue;
            UpgradeDefinition item = acquired[i];
            int stack = progression.Stack(item.Id);
            buildUpgradeIds[i] = item.Id;
            buildTexts[i].text = RunProgressionDescriptions.IsMovementUpgrade(item.Id)
                ? item.Name + " · 적용 완료"
                : item.Name + $" · 현재 Lv.{stack}" + (stack == item.MaxStacks ? " (최대)" : "");
            ApplyIcon(buildIcons[i], item.Id, item.Name);
        }
    }

    private void RefreshRelics(IReadOnlyList<RelicDefinition> relics, int supply)
    {
        supplyText.text = $"보급 {supply}";
        for (int i = 0; i < relicTexts.Length; i++)
        {
            if (i >= relics.Count)
            {
                relicButtons[i].interactable = false;
                relicTexts[i].text = $"슬롯 {i + 1}  비어 있음";
                relicTexts[i].color = MutedColor;
                ApplyIcon(relicIcons[i], string.Empty, "+");
                continue;
            }
            RelicDefinition relic = relics[i];
            relicButtons[i].interactable = true;
            relicTexts[i].text = $"{RarityName(relic.Rarity)} · {relic.Name}\n클릭하여 효과·조건 확인";
            relicTexts[i].color = RarityColor(relic.Rarity);
            ApplyIcon(relicIcons[i], relic.Id, relic.Name);
        }
    }

    private void RefreshAltars(IReadOnlyList<RelicOffer> offers)
    {
        altarEmptyText.gameObject.SetActive(offers.Count == 0);
        for (int i = 0; i < altarTexts.Length; i++)
        {
            bool visible = i < offers.Count;
            altarTexts[i].transform.parent.gameObject.SetActive(visible);
            if (!visible) continue;
            RelicOffer offer = offers[i];
            string danger = offer.IsRisky ? "위험" : "안전";
            altarTexts[i].text = $"{danger} · {RarityName(offer.Relic.Rarity)}\n{offer.Relic.Name}\n클릭하여 효과 확인";
            altarTexts[i].color = offer.IsRisky ? RiskyColor : SafeColor;
            ApplyIcon(altarIcons[i], offer.Relic.Id, offer.Relic.Name);
        }
    }

    private void ShowReplacement(RelicDefinition incoming,
        IReadOnlyList<RelicDefinition> equipped)
    {
        CloseDetails();
        incomingRelicDetails = incoming;
        for (int i = 0; i < 4; i++)
        {
            RelicDefinition relic = equipped[i];
            replacementTexts[i].text = $"{i + 1}. {relic.Name} 교체\n{RarityName(relic.Rarity)} · 이 유물 대신\n{incoming.Name} 획득";
            ApplyIcon(replacementIcons[i], relic.Id, relic.Name);
        }
        replacementTexts[4].text = $"5. 새 유물 해체 → 보급 +1\n{RarityName(incoming.Rarity)} · {incoming.Name}\n현재 유물은 모두 유지";
        SetRightMode(replacementPanel);
    }

    private void ResolveRelic(int replaceIndex)
    {
        if (IsDetailOpen) return;
        if (!progression.ResolvePendingRelic(replaceIndex)) return;
        SetRightMode(rightPanel);
    }

    private void SetRightMode(RectTransform visiblePanel)
    {
        rightPanel.gameObject.SetActive(visiblePanel == rightPanel);
        upgradePanel.gameObject.SetActive(visiblePanel == upgradePanel);
        replacementPanel.gameObject.SetActive(visiblePanel == replacementPanel);
    }

    private RectTransform CreatePanel(string name, Transform parent, bool right)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        rect.pivot = right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        rect.anchoredPosition = right ? new Vector2(-18f, 0f) : new Vector2(18f, 0f);
        rect.sizeDelta = new Vector2(DefaultPanelWidth, PanelHeight);
        UnityEngine.UI.Image image = panelObject.GetComponent<UnityEngine.UI.Image>();
        image.color = PanelColor;
        image.raycastTarget = false;
        UnityEngine.UI.Outline outline = panelObject.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(2f, -2f);
        return rect;
    }

    private RectTransform CreateCenterOverlay(string name, Transform parent)
    {
        GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image));
        overlay.transform.SetParent(parent, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        UnityEngine.UI.Image image = overlay.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color32(2, 6, 15, 222);
        image.raycastTarget = true;
        return rect;
    }

    private RectTransform CreateFixedCard(string name, Transform parent, Vector2 position,
        Vector2 size, Color border)
    {
        RectTransform rect = CreateCard(name, parent, Vector2.zero, size, border);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private RectTransform CreateCard(string name, Transform parent, Vector2 position,
        Vector2 sizeDelta, Color border)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        UnityEngine.UI.Image image = card.GetComponent<UnityEngine.UI.Image>();
        image.color = CardColor;
        image.raycastTarget = false;
        UnityEngine.UI.Outline outline = card.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(1f, -1f);
        return rect;
    }

    private IconView CreateIcon(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject iconObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
        iconObject.transform.SetParent(parent, false);
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        UnityEngine.UI.Image image = iconObject.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color32(29, 53, 83, 255);
        image.preserveAspect = true;
        image.raycastTarget = false;
        iconObject.GetComponent<UnityEngine.UI.Outline>().effectColor = BorderColor;
        UnityEngine.UI.Text placeholder = CreateText("Placeholder", rect, "?", 18,
            TextAnchor.MiddleCenter, MutedColor, Vector2.zero, Vector2.zero);
        placeholder.rectTransform.anchorMin = Vector2.zero;
        placeholder.rectTransform.anchorMax = Vector2.one;
        placeholder.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        placeholder.rectTransform.anchoredPosition = Vector2.zero;
        placeholder.rectTransform.sizeDelta = Vector2.zero;
        return new IconView { Image = image, Placeholder = placeholder };
    }

    private UnityEngine.UI.Text CreateText(string name, Transform parent, string value,
        int size, TextAnchor alignment, Color color, Vector2 position, Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        UnityEngine.UI.Text text = textObject.GetComponent<UnityEngine.UI.Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Normal;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private void ApplyIcon(IconView view, string id, string displayName)
    {
        if (view == null) return;
        string resourceName = ProgressionIconResourceName(id);
        Sprite sprite = string.IsNullOrEmpty(id)
            ? null : Resources.Load<Sprite>($"UI/ProgressionIcons/{resourceName}");
        view.Image.sprite = sprite;
        view.Image.color = sprite != null ? Color.white : new Color32(29, 53, 83, 255);
        view.Placeholder.gameObject.SetActive(sprite == null);
        view.Placeholder.text = string.IsNullOrEmpty(displayName)
            ? "+" : displayName.Substring(0, 1);
    }

    private string ProgressionIconResourceName(string id)
    {
        switch (id)
        {
            case "movement_knight":
            case "movement_training_1":
            case "movement_training_2":
                return "KnightMove";
            case "movement_bishop":
            case "movement_training_3":
                return "BishopMove";
            case "movement_rook":
            case "movement_training_4":
                return "RookMove";
            case "mana_circulation":
            case "pierce_recovery":
            case "alternating_overload":
            case "blast_core":
                return "ChainBurst";
            case "echo_recovery":
                return "EchoPressure";
            case "movement_breath":
            case "afterglow_recovery":
            case "impact_recovery":
            case "hot_afterglow":
                return "BindingLanding";
            default:
                return id;
        }
    }

    private static string RarityName(RelicRarity rarity)
    {
        return rarity == RelicRarity.Legendary ? "전설"
            : rarity == RelicRarity.Rare ? "희귀" : "일반";
    }

    private static Color RarityColor(RelicRarity rarity)
    {
        return rarity == RelicRarity.Legendary ? LegendaryColor
            : rarity == RelicRarity.Rare ? RareColor : CommonColor;
    }

    private void OnDestroy()
    {
        CloseDetails();
        boardCamera?.SetSideHudReservation(0f);
        if (progression == null) return;
        progression.UpgradeOptionsReady -= ShowUpgradeOptions;
        progression.UpgradesChanged -= RefreshBuild;
        progression.RelicsChanged -= RefreshRelics;
        progression.AltarsChanged -= RefreshAltars;
        progression.RelicReplacementRequested -= ShowReplacement;
        progression.MovementArtChanged -= RefreshMovementSummary;
    }
}
