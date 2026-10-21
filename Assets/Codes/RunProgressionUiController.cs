using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// PC 보유 빌드는 필요할 때 여는 창에 표시한다. 모바일 목록은 기존 어댑터가 담당한다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
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
    private const float PanelHeight = PcBoardLayout.PanelHeight;
    private const float DefaultPanelWidth = PcBoardLayout.PanelWidth;
    private const float BoardPanelGap = PcBoardLayout.PanelGap;
    private const float ScreenEdgeMargin = 18f;

    private RunProgressionSystem progression;
    private GameHudController gameHud;
    private CameraController boardCamera;
    private Font font;
    private RectTransform canvasRect;
    private RectTransform leftPanel;
    private RectTransform rightPanel;
    private RectTransform upgradePanel;
    private RectTransform replacementPanel;
    private RectTransform detailPanel;
    private RectTransform detailCard;
    private RectTransform inventoryPanel;
    private RectTransform inventoryCard;
    private RectTransform inventoryButton;
    private UnityEngine.UI.Text inventoryButtonLabel;
    private UnityEngine.UI.Button inventoryToggle;
    private RectTransform boardActionHudGroup;
    private RectTransform boardHeaderHudGroup;
    private Bounds boardHeaderHudBounds;
    private Bounds boardActionHudBounds;
    private int rewardOpenedFrame = -1;
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
    // PC 카드 구성: 이름 · 등급 · 레벨 변화를 효과 설명(upgradeTexts)과 나눠 보여 준다.
    private readonly UnityEngine.UI.Text[] upgradeNames = new UnityEngine.UI.Text[3];
    private readonly UnityEngine.UI.Text[] upgradeGrades = new UnityEngine.UI.Text[3];
    private readonly UnityEngine.UI.Text[] upgradeLevels = new UnityEngine.UI.Text[3];
    private readonly GameObject[] upgradePcOnly = new GameObject[3];
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
        || IsDetailOpen || IsInventoryOpen;
    public bool IsDetailOpen => detailPanel != null && detailPanel.gameObject.activeSelf;
    public bool IsInventoryOpen => inventoryPanel != null && inventoryPanel.gameObject.activeSelf;

    public void Initialize(RunProgressionSystem target, CameraController cameraController)
    {
        if (initialized || target == null)
            return;

        progression = target;
        gameHud = GetComponent<GameHudController>();
        boardCamera = cameraController != null
            ? cameraController : FindAnyObjectByType<CameraController>();
        progression.TryInitialize();
        font = GetComponentsInChildren<UnityEngine.UI.Text>(true)
            .Select(item => item.font).FirstOrDefault(item => item != null);
        canvasRect = GetComponent<RectTransform>();

        BuildLeftPanel();
        BuildRightPanel();
        if (!MobilePlaytestController.IsActive) BuildInventoryPanel();
        BuildUpgradePanel();
        BuildReplacementPanel();
        BuildDetailPanel();
        BuildHoverTooltip();

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
        if (inventoryToggle != null)
        {
            Move player = progression.GetComponent<Move>();
            GameHudController hud = GetComponent<GameHudController>();
            inventoryToggle.interactable = !IsDetailOpen && !upgradePanel.gameObject.activeSelf
                && !replacementPanel.gameObject.activeSelf && !hud.IsTraitSelectionOpen
                && !hud.IsPauseMenuOpen && !hud.IsRunEnded
                && (IsInventoryOpen || player.CanAct || player.IsChoosingEchoDirection || player.IsChoosingFreeMove);
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (IsDetailOpen) return;
        if (inventoryToggle != null && keyboard.bKey.wasPressedThisFrame)
        {
            if (IsInventoryOpen) CloseInventory();
            else if (inventoryToggle.interactable) OpenInventory();
            return;
        }
        if (upgradePanel != null && upgradePanel.gameObject.activeSelf)
        {
            if (Time.frameCount <= rewardOpenedFrame) return;
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

    private void LateUpdate()
    {
        // 카메라(실행 순서 100)가 새 창 크기를 반영한 뒤 HUD의 보드 경계를 계산한다.
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (initialized && size != lastScreenSize) LayoutPanels();
        if (initialized)
        {
            if (IsHoverBlocked)
            {
                tooltipFromBoard = false;
                HideHoverTooltip();
            }
            else UpdateBoardAltarHover();
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
            AddHoverTooltip(row, () =>
            {
                string id = buildUpgradeIds[captured];
                if (string.IsNullOrEmpty(id) || progression.Stack(id) == 0) return NoTooltip;
                UpgradeDefinition item = RunProgressionCatalog.Upgrade(id);
                return (item.Name, RunProgressionDescriptions.OwnedDescription(item, progression));
            });
            buildRows[i] = row.gameObject;
            buildIcons[i] = CreateIcon("Icon", row, new Vector2(7f, -5f), new Vector2(32f, 32f));
            buildTexts[i] = CreateText("Label", row, string.Empty, 13, TextAnchor.MiddleLeft,
                TextColor, new Vector2(48f, -3f), new Vector2(-56f, 36f));
        }
    }

    private void BuildInventoryPanel()
    {
        inventoryPanel = CreateCenterOverlay("BuildInventoryOverlay", transform);
        UnityEngine.UI.Button backdrop = inventoryPanel.gameObject.AddComponent<UnityEngine.UI.Button>();
        backdrop.targetGraphic = inventoryPanel.GetComponent<UnityEngine.UI.Image>();
        backdrop.transition = UnityEngine.UI.Selectable.Transition.None;
        backdrop.onClick.AddListener(CloseInventory);
        inventoryCard = CreateFixedCard("BuildInventoryCard", inventoryPanel,
            Vector2.zero, new Vector2(724f, 920f), BorderColor);
        inventoryCard.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        // 창 안의 빈 영역 클릭이 바깥 닫기 버튼까지 전달되지 않게 한다.
        UnityEngine.UI.Button cardInput = inventoryCard.gameObject.AddComponent<UnityEngine.UI.Button>();
        cardInput.targetGraphic = inventoryCard.GetComponent<UnityEngine.UI.Image>();
        cardInput.transition = UnityEngine.UI.Selectable.Transition.None;
        UnityEngine.UI.Text title = CreateText("Title", inventoryCard, "보유 빌드", 26,
            TextAnchor.MiddleLeft, TextColor, new Vector2(22f, -14f), new Vector2(-174f, 52f));
        RectTransform close = CreateFixedCard("CloseBuildInventory", inventoryCard,
            new Vector2(276f, 420f), new Vector2(132f, 42f), BorderColor);
        AddDetailButton(close, CloseInventory);
        UnityEngine.UI.Text label = CreateText("Label", close, "닫기 · B / Esc", 14,
            TextAnchor.MiddleCenter, TextColor, Vector2.zero, Vector2.zero);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;
        foreach (RectTransform panel in new[] { leftPanel, rightPanel })
        {
            // 빌드 창(PC 전용) 글씨를 키운다. 12~13px 설명이 1080p에서 읽기 어려웠다.
            foreach (UnityEngine.UI.Text text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                text.fontSize = Mathf.RoundToInt(text.fontSize * 1.2f);
            panel.SetParent(inventoryCard, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
            panel.anchoredPosition = new Vector2(panel == leftPanel ? -177f : 177f, -36f);
            panel.sizeDelta = new Vector2(DefaultPanelWidth, PanelHeight);
        }
        inventoryPanel.gameObject.SetActive(false);

        inventoryButton = CreateFixedCard("BuildInventoryButton", transform,
            Vector2.zero, new Vector2(190f, 44f), BorderColor);
        inventoryToggle = AddDetailButton(inventoryButton, OpenInventory);
        inventoryButtonLabel = CreateText("Label", inventoryButton, "빌드 · B", 16,
            TextAnchor.MiddleCenter, TextColor, Vector2.zero, Vector2.zero);
        inventoryButtonLabel.rectTransform.anchorMin = Vector2.zero;
        inventoryButtonLabel.rectTransform.anchorMax = Vector2.one;
        inventoryButtonLabel.rectTransform.sizeDelta = Vector2.zero;
    }

    public void OpenInventory()
    {
        if (inventoryPanel == null || IsInventoryOpen || IsDetailOpen
            || upgradePanel.gameObject.activeSelf || replacementPanel.gameObject.activeSelf) return;
        GameHudController hud = GetComponent<GameHudController>();
        Move player = progression.GetComponent<Move>();
        if (hud.IsTraitSelectionOpen || hud.IsPauseMenuOpen || hud.IsRunEnded
            || (!player.CanAct && !player.IsChoosingEchoDirection && !player.IsChoosingFreeMove)) return;
        player.SetInspectionOpen(true);
        leftPanel.gameObject.SetActive(true);
        rightPanel.gameObject.SetActive(true);
        inventoryPanel.gameObject.SetActive(true);
        inventoryPanel.SetAsLastSibling();
    }

    public void CloseInventory()
    {
        if (inventoryPanel == null || !IsInventoryOpen) return;
        HideHoverTooltip();
        inventoryPanel.gameObject.SetActive(false);
        progression.GetComponent<Move>().SetInspectionOpen(IsDetailOpen);
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
            AddHoverTooltip(row, () => RelicTooltip(captured));
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
            AddHoverTooltip(row, () => AltarTooltip(captured));
            altarIcons[i] = CreateIcon("Icon", row, new Vector2(9f, -26f), new Vector2(58f, 58f));
            altarTexts[i] = CreateText("Label", row, string.Empty, 12, TextAnchor.MiddleLeft,
                TextColor, new Vector2(78f, -7f), new Vector2(-86f, 100f));
            row.gameObject.SetActive(false);
        }
    }

    private void BuildUpgradePanel()
    {
        upgradePanel = CreateCenterOverlay("UpgradeSelectionPanel", transform);
        // 제목·안내·카드를 하나의 받침 패널 위에 묶는다.
        RectTransform frame = CreateFixedCard("UpgradeFrame", upgradePanel,
            new Vector2(0f, -10f), new Vector2(1068f, 540f), BorderColor);
        frame.GetComponent<UnityEngine.UI.Image>().color = new Color32(14, 24, 44, 245);
        upgradeTitle = CreateText("Title", upgradePanel, "웨이브 강화 선택", 30,
            TextAnchor.MiddleCenter, TextColor, Vector2.zero, new Vector2(900f, 60f));
        SetCentered(upgradeTitle.rectTransform, new Vector2(0f, 222f), new Vector2(1000f, 52f));
        upgradeGuide = CreateText("Guide", upgradePanel,
            "하나를 선택하면 다음 웨이브가 바로 시작됩니다  ·  클릭 또는 1 · 2 · 3", 16,
            TextAnchor.MiddleCenter, MutedColor, Vector2.zero, new Vector2(900f, 34f));
        SetCentered(upgradeGuide.rectTransform, new Vector2(0f, 184f), new Vector2(1000f, 30f));
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            RectTransform card = CreateFixedCard($"UpgradeChoice_{i + 1}", upgradePanel,
                new Vector2((i - 1) * 342f, -40f), new Vector2(322f, 380f), BorderColor);
            UnityEngine.UI.Button button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image cardImage = card.GetComponent<UnityEngine.UI.Image>();
            cardImage.raycastTarget = true;
            button.targetGraphic = cardImage;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.highlightedColor = new Color32(220, 236, 255, 255);
            colors.pressedColor = new Color32(180, 205, 235, 255);
            button.colors = colors;
            button.onClick.AddListener(() => SelectUpgrade(captured));
            upgradeButtons[i] = button;

            // 번호 배지 · 아이콘 · 이름/등급 · 상세 버튼 (위쪽 줄)
            GameObject pcOnly = new GameObject("PcLayout", typeof(RectTransform));
            pcOnly.transform.SetParent(card, false);
            RectTransform pcRect = pcOnly.GetComponent<RectTransform>();
            pcRect.anchorMin = Vector2.zero;
            pcRect.anchorMax = Vector2.one;
            pcRect.sizeDelta = Vector2.zero;
            upgradePcOnly[i] = pcOnly;
            RectTransform badge = CreateCard("Badge", pcRect, new Vector2(14f, -14f), new Vector2(30f, 30f), BorderColor);
            badge.anchorMax = new Vector2(0f, 1f);
            badge.GetComponent<UnityEngine.UI.Image>().color = BorderColor;
            UnityEngine.UI.Text number = CreateText("Number", badge, (i + 1).ToString(), 17,
                TextAnchor.MiddleCenter, TextColor, Vector2.zero, Vector2.zero);
            number.rectTransform.anchorMin = Vector2.zero;
            number.rectTransform.anchorMax = Vector2.one;
            number.rectTransform.sizeDelta = Vector2.zero;
            upgradeIcons[i] = CreateIcon("Icon", card, new Vector2(18f, -56f), new Vector2(80f, 80f));
            upgradeGrades[i] = CreateText("Grade", pcRect, string.Empty, 14, TextAnchor.LowerLeft,
                LegendaryColor, new Vector2(112f, -58f), new Vector2(-126f, 24f));
            upgradeNames[i] = CreateText("Name", pcRect, string.Empty, 22, TextAnchor.UpperLeft,
                TextColor, new Vector2(112f, -84f), new Vector2(-126f, 56f));

            RectTransform details = CreateCard("Details", card, new Vector2(234f, -14f), new Vector2(74f, 32f), BorderColor);
            details.anchorMin = details.anchorMax = new Vector2(0f, 1f);
            details.sizeDelta = new Vector2(74f, 32f);
            AddDetailButton(details, () =>
            {
                UpgradeDefinition item = displayedUpgrades[captured];
                if (item != null) OpenDetails(RunProgressionDescriptions.ChoiceName(item, progression),
                    RunProgressionDescriptions.ChoiceDetails(item, progression));
            });
            CreateText("Label", details, "상세", 14, TextAnchor.MiddleCenter,
                TextColor, new Vector2(4f, -2f), new Vector2(-8f, 28f));

            // 레벨 변화 줄 · 구분선 · 효과
            RectTransform levelBox = CreateCard("LevelBox", pcRect, new Vector2(14f, -152f), new Vector2(-28f, 62f), BorderColor);
            levelBox.GetComponent<UnityEngine.UI.Image>().color = new Color32(10, 18, 34, 255);
            upgradeLevels[i] = CreateText("Level", levelBox, string.Empty, 16, TextAnchor.MiddleLeft,
                RareColor, new Vector2(12f, 0f), new Vector2(-24f, 62f));
            upgradeLevels[i].supportRichText = true;
            upgradeTexts[i] = CreateText("Label", card, string.Empty, 17, TextAnchor.UpperLeft,
                TextColor, new Vector2(20f, -230f), new Vector2(-40f, 140f));
            upgradeTexts[i].lineSpacing = 1.15f;
        }
        upgradePanel.gameObject.SetActive(false);
        // 모바일 어댑터는 기존 단일 문구 카드를 재배치한다.
        if (MobilePlaytestController.IsActive)
        {
            frame.gameObject.SetActive(false);
            foreach (GameObject pcOnly in upgradePcOnly) pcOnly.SetActive(false);
        }
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

    // 마우스를 올리면 클릭 없이 설명을 보여 주는 말풍선. 상세 창과 같은 문구를 쓴다.
    private RectTransform hoverTooltip;
    private UnityEngine.UI.Text hoverTitle;
    private UnityEngine.UI.Text hoverBody;
    private const float TooltipWidth = 380f;
    private static readonly (string title, string body) NoTooltip = (null, null);

    private bool IsHoverBlocked => IsDetailOpen || (gameHud != null
        && (gameHud.IsTraitSelectionOpen || gameHud.IsPauseMenuOpen || gameHud.IsGameOverOverlayVisible));

    private void BuildHoverTooltip()
    {
        hoverTooltip = CreateFixedCard("HoverTooltip", transform, Vector2.zero, new Vector2(TooltipWidth, 120f), LegendaryColor);
        hoverTooltip.pivot = new Vector2(0f, 1f);
        hoverTooltip.GetComponent<UnityEngine.UI.Image>().color = new Color32(10, 16, 30, 250);
        hoverTitle = CreateText("Title", hoverTooltip, string.Empty, 18, TextAnchor.UpperLeft,
            LegendaryColor, new Vector2(14f, -12f), new Vector2(-28f, 28f));
        hoverBody = CreateText("Body", hoverTooltip, string.Empty, 15, TextAnchor.UpperLeft,
            TextColor, new Vector2(14f, -44f), new Vector2(-28f, 400f));
        hoverBody.lineSpacing = 1.15f;
        hoverBody.verticalOverflow = VerticalWrapMode.Overflow;
        hoverTooltip.gameObject.SetActive(false);
    }

    private void AddHoverTooltip(RectTransform target, Func<(string title, string body)> content)
    {
        UnityEngine.UI.Image image = target.GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.raycastTarget = true;
        if (!target.TryGetComponent(out UnityEngine.EventSystems.EventTrigger trigger))
            trigger = target.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowHoverTooltip(target, content()));
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideHoverTooltip());
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    private void ShowHoverTooltip(RectTransform target, (string title, string body) content)
    {
        if (hoverTooltip == null || IsHoverBlocked || string.IsNullOrEmpty(content.title)) return;
        hoverTitle.text = content.title;
        hoverBody.text = content.body;
        float height = 44f + hoverBody.preferredHeight + 14f;
        hoverTooltip.sizeDelta = new Vector2(TooltipWidth, height);
        // 대상 오른쪽에 두고, 화면 밖으로 나가면 왼쪽에 둔다. 위아래는 캔버스 안으로 맞춘다.
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 rightTop = canvasRect.InverseTransformPoint(corners[2]);
        Vector2 leftTop = canvasRect.InverseTransformPoint(corners[1]);
        Rect canvas = canvasRect.rect;
        float x = rightTop.x + 10f;
        if (x + TooltipWidth > canvas.xMax - 8f) x = leftTop.x - 10f - TooltipWidth;
        float y = Mathf.Clamp(rightTop.y, canvas.yMin + height + 8f, canvas.yMax - 8f);
        hoverTooltip.anchoredPosition = new Vector2(x, y);
        hoverTooltip.gameObject.SetActive(true);
        hoverTooltip.SetAsLastSibling();
    }

    // 보드 위 제단 칸에 마우스를 올리면 그 유물 설명을 커서 옆에 보여 준다.
    private bool tooltipFromBoard;

    private void UpdateBoardAltarHover()
    {
        if (hoverTooltip == null || Mouse.current == null) return;
        bool shown = false;
        if (!IsHoverBlocked && !IsModalOpen && progression.ActiveAltars.Count > 0)
        {
            Camera camera = boardCamera != null ? boardCamera.GetComponent<Camera>() : null;
            GridManager grid = FindAnyObjectByType<GridManager>();
            if (camera != null && grid != null && grid.IsReady)
            {
                Vector2 mouse = Mouse.current.position.ReadValue();
                Vector3 world = camera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, -camera.transform.position.z));
                Vector3Int cell = grid.WorldToCell(world);
                for (int i = 0; i < progression.ActiveAltars.Count; i++)
                {
                    if (progression.ActiveAltars[i].Cell != cell) continue;
                    ShowTooltipAtScreen(mouse, AltarTooltip(i));
                    shown = tooltipFromBoard = true;
                    break;
                }
            }
        }
        if (!shown && tooltipFromBoard)
        {
            tooltipFromBoard = false;
            HideHoverTooltip();
        }
    }

    private void ShowTooltipAtScreen(Vector2 screen, (string title, string body) content)
    {
        if (IsHoverBlocked || string.IsNullOrEmpty(content.title)) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        hoverTitle.text = content.title;
        hoverBody.text = content.body;
        float height = 44f + hoverBody.preferredHeight + 14f;
        hoverTooltip.sizeDelta = new Vector2(TooltipWidth, height);
        Rect canvas = canvasRect.rect;
        float x = local.x + 18f;
        if (x + TooltipWidth > canvas.xMax - 8f) x = local.x - 18f - TooltipWidth;
        float y = Mathf.Clamp(local.y - 12f, canvas.yMin + height + 8f, canvas.yMax - 8f);
        hoverTooltip.anchoredPosition = new Vector2(x, y);
        hoverTooltip.gameObject.SetActive(true);
        hoverTooltip.SetAsLastSibling();
    }

    private void HideHoverTooltip()
    {
        if (hoverTooltip != null) hoverTooltip.gameObject.SetActive(false);
    }

    private (string title, string body) RelicTooltip(int index)
    {
        if (index < 0 || index >= progression.Relics.Count) return NoTooltip;
        RelicDefinition relic = progression.Relics[index];
        return (RarityName(relic.Rarity) + " 유물 · " + relic.Name, relic.Description);
    }

    private (string title, string body) AltarTooltip(int index)
    {
        if (index < 0 || index >= progression.ActiveAltars.Count) return NoTooltip;
        RelicOffer offer = progression.ActiveAltars[index];
        string howTo = offer.IsLocked
            ? $"\n\n잠김: 이번 웨이브 적을 {offer.KillsRemaining}마리 더 처치하면 풀립니다 ({offer.Kills}/{offer.KillsRequired})."
            : "\n\n제단 칸으로 이동하면 즉시 획득합니다.";
        return ((offer.IsRisky ? "위험" : "안전") + " 제단 · " + offer.Relic.Name,
            RarityName(offer.Relic.Rarity) + " 유물\n\n" + offer.Relic.Description + howTo);
    }

    private void OpenDetails(string title, string description)
    {
        HideHoverTooltip();
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
        if (progression != null) progression.GetComponent<Move>().SetInspectionOpen(IsInventoryOpen);
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
        OpenDetails("시작 특성 · 일반 적중 " + progression.TraitChargeRequired + "회로 준비",
            RunProgressionDescriptions.TraitEffect(progression.SelectedTraitId)
            + $"\n\n일반 공격 행동으로 적을 직접 맞히면 충전 +1. 필요한 적중 행동: {progression.TraitChargeRequired}회. 준비 1회만 저장하며 A로 일반 공격을 계속할 수도 있습니다. F 또는 특성 아이콘으로 특수 공격을 선택하고 방향을 클릭하면 사용합니다. 취소는 소모하지 않지만 확정한 빗나감은 소모합니다. 특성 공격·추가 영창·범위 피해는 재충전하지 않습니다. 웨이브를 넘어 유지되고 사망·클리어 때 초기화합니다.");
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
            AddHoverTooltip(card, () => captured < 4 ? RelicTooltip(captured)
                : incomingRelicDetails != null ? ("새 유물 · " + incomingRelicDetails.Name, incomingRelicDetails.Description)
                : NoTooltip);
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
        if (MobilePlaytestController.IsActive)
        {
            // 모바일 배치와 접이식 목록은 전용 화면 어댑터가 맡는다. PC 보드 예약 공간은 사용하지 않는다.
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            return;
        }
        // Canvas와 카메라 모두 동일한 PC 구성 배율을 쓴다. 열 폭/간격을 따로 늘리지 않는다.
        Canvas.ForceUpdateCanvases();
        float canvasWidth = canvasRect.rect.width > 0f ? canvasRect.rect.width : 1920f;
        float canvasHeight = canvasRect.rect.height > 0f ? canvasRect.rect.height : 1080f;
        float panelScale = PcBoardLayout.Scale(Screen.width, Screen.height)
            / Mathf.Max(0.01f, GetComponent<Canvas>().scaleFactor);
        float boardHalfWidth = GetBoardHalfWidthInCanvas(PcBoardLayout.BoardSide * panelScale * 0.5f);
        float innerEdge = boardHalfWidth + BoardPanelGap * panelScale;
        if (inventoryCard != null)
        {
            inventoryCard.localScale = Vector3.one * Mathf.Min(1f,
                (canvasWidth - 36f) / 724f, (canvasHeight - 36f) / 920f);
            Rect board = PcBoardLayout.BoardPixelRect(Screen.width, Screen.height);
            // 상하 모드: 보드 오른쪽 위 바깥(헤더 높이). 측면 모드는 LayoutSideHud가 다시 놓는다.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                new Vector2(board.xMax - 95f * PcBoardLayout.Scale(Screen.width, Screen.height),
                    board.yMax + 40f * PcBoardLayout.Scale(Screen.width, Screen.height)),
                null, out Vector2 buttonPosition);
            inventoryButton.anchoredPosition = buttonPosition;
            inventoryButton.localScale = Vector3.one * panelScale;
            replacementPanel.anchorMin = replacementPanel.anchorMax = replacementPanel.pivot = Vector2.one * 0.5f;
            replacementPanel.anchoredPosition = Vector2.zero;
            replacementPanel.sizeDelta = new Vector2(500f, PanelHeight);
            replacementPanel.localScale = Vector3.one * Mathf.Min(1f,
                (canvasWidth - 36f) / 500f, (canvasHeight - 36f) / PanelHeight);
        }
        else
        {
            LayoutSidePanel(leftPanel, false, innerEdge, panelScale);
            LayoutSidePanel(rightPanel, true, innerEdge, panelScale);
            LayoutSidePanel(replacementPanel, true, innerEdge, panelScale);
        }
        LayoutBoardEdgeHud();
        StretchViewportOverlays(canvasWidth, canvasHeight);
        if (detailCard != null)
        {
            float scale = Mathf.Min(1f, (canvasWidth - 36f) / 660f, (canvasHeight - 36f) / 590f);
            detailCard.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }

    // 시작 특성·일시정지·결과 창은 좁은 ViewportHudRoot 안에 있어 배경이 가운데 띠만 덮었다.
    // 배경을 캔버스 전체 폭으로 넓히고, 안쪽 패널은 가운데에 그대로 둔다.
    private void StretchViewportOverlays(float canvasWidth, float canvasHeight)
    {
        foreach (string name in new[] { "TraitSelectionOverlay", "PauseMenuOverlay", "GameOverOverlay" })
        {
            RectTransform overlay = transform.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == name);
            RectTransform root = overlay != null ? overlay.parent as RectTransform : null;
            if (overlay == null || root == null) continue;
            overlay.sizeDelta = new Vector2(Mathf.Max(0f, canvasWidth - root.rect.width),
                Mathf.Max(0f, canvasHeight - root.rect.height));
        }
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
        float referenceScale = PcBoardLayout.Scale(Screen.width, Screen.height);
        float desiredGap = BoardPanelGap * referenceScale;

        float wallPixels = (boardMaximumY - boardMinimumY) / Mathf.Max(1, bounds.size.y);
        bool side = PcBoardLayout.UsesSideHud(Screen.width, Screen.height);
        // 위 외벽은 룩이 서는 줄이라 헤더가 가리지 않는다. 상하 모드의 행동 HUD만 아래 외벽 줄에 겹친다.
        LayoutHeaderHud(boardMaximumY, desiredGap);
        LayoutActionHudGroup(camera, grid, boardMinimumY + wallPixels, desiredGap);
        ArrangeActionHud(side);
        if (side) LayoutSideHud();
    }

    // 측면 모드: 헤더·빌드 버튼은 보드 왼쪽 위, 행동 HUD는 보드 오른쪽 아래 열에 둔다. 보드는 가리지 않는다.
    private void LayoutSideHud()
    {
        Rect board = PcBoardLayout.BoardPixelRect(Screen.width, Screen.height);
        float gap = BoardPanelGap * PcBoardLayout.Scale(Screen.width, Screen.height);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
            new Vector2(board.xMax + gap, board.yMin), null, out Vector2 rightBottom);
        boardActionHudGroup.localScale = Vector3.one;
        boardActionHudGroup.anchoredPosition = rightBottom
            + new Vector2(SideHudWidth * 0.5f + SideHudPadding, SideHudHeight + SideHudPadding);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
            new Vector2(board.xMin - gap, board.yMax), null, out Vector2 leftTop);
        boardHeaderHudGroup.anchoredPosition = leftTop
            - new Vector2(boardHeaderHudBounds.max.x, boardHeaderHudBounds.max.y);
        if (inventoryButton != null)
        {
            Vector2 size = inventoryButton.sizeDelta * inventoryButton.localScale.x;
            inventoryButton.anchoredPosition = new Vector2(leftTop.x - size.x * 0.5f,
                leftTop.y - boardHeaderHudBounds.size.y - 14f - size.y * 0.5f);
        }
    }

    private const float SideHudWidth = 332f;
    // 상태 문구는 두 줄 설명이 332폭에서 네 줄로 접혀도(18pt 기준 93px) 잘리지 않는 높이로 둔다.
    private const float SideStatusHeight = 96f;
    private const float SideHudHeight = 376f;
    private const float SideHudPadding = 8f;
    private readonly Dictionary<RectTransform, Vector3> actionHudHome = new Dictionary<RectTransform, Vector3>();
    private Vector2 actionStatusHomeSize;
    private Vector2 actionBackingHomeSize;
    private bool actionHudInColumn;

    // 행동 HUD 요소를 가로 한 줄(상하 모드) 또는 세로 열(측면 모드)로 배치한다.
    private void ArrangeActionHud(bool column)
    {
        if (boardActionHudGroup == null || column == actionHudInColumn) return;
        RectTransform Find(string name) => boardActionHudGroup.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(item => item.name == name && item.parent == boardActionHudGroup);
        RectTransform status = Find("ActionModeHud");
        RectTransform backing = Find("ActionHudBacking");
        string[] names = { "ActionModeHud", "HealthHud", "UltimateFrame", "UltimateReadyGlow",
            "TraitHud", "AttackActionFrame", "MoveActionFrame", "ActionHudBacking" };
        if (actionHudHome.Count == 0)
        {
            foreach (string name in names)
            {
                RectTransform rect = Find(name);
                if (rect != null) actionHudHome[rect] = rect.localPosition;
            }
            if (status != null) actionStatusHomeSize = status.sizeDelta;
            if (backing != null) actionBackingHomeSize = backing.sizeDelta;
        }
        actionHudInColumn = column;
        if (!column)
        {
            foreach (var pair in actionHudHome) pair.Key.localPosition = pair.Value;
            if (status != null) status.sizeDelta = actionStatusHomeSize;
            if (backing != null) backing.sizeDelta = actionBackingHomeSize;
            return;
        }
        if (status != null) status.sizeDelta = new Vector2(SideHudWidth, SideStatusHeight);
        PlaceCenter(status, new Vector2(0f, -SideStatusHeight * 0.5f));
        PlaceCenter(Find("HealthHud"), new Vector2(0f, -136f));
        PlaceCenter(Find("UltimateFrame"), new Vector2(-52f, -224f));
        PlaceCenter(Find("UltimateReadyGlow"), new Vector2(-52f, -224f));
        PlaceCenter(Find("TraitHud"), new Vector2(52f, -224f));
        PlaceCenter(Find("AttackActionFrame"), new Vector2(-52f, -328f));
        PlaceCenter(Find("MoveActionFrame"), new Vector2(52f, -328f));
        if (backing != null)
        {
            backing.sizeDelta = new Vector2(SideHudWidth + SideHudPadding * 2f, SideHudHeight + SideHudPadding * 2f);
            PlaceCenter(backing, new Vector2(0f, -SideHudHeight * 0.5f));
        }
    }

    private static void PlaceCenter(RectTransform rect, Vector2 center)
    {
        if (rect == null) return;
        Vector2 offset = rect.rect.center;
        rect.localPosition = new Vector3(center.x - offset.x, center.y - offset.y, rect.localPosition.z);
    }

    private void LayoutActionHudGroup(Camera camera, GridManager grid, float bottomY, float gap)
    {
        if (boardActionHudGroup == null)
        {
            GameObject root = new GameObject("BoardActionHudGroup", typeof(RectTransform));
            boardActionHudGroup = root.GetComponent<RectTransform>();
            boardActionHudGroup.SetParent(canvasRect, false);
            boardActionHudGroup.anchorMin = boardActionHudGroup.anchorMax = Vector2.one * 0.5f;
            boardActionHudGroup.sizeDelta = Vector2.zero;
            boardActionHudGroup.SetAsFirstSibling();
            string[] names = { "UltimateFrame", "UltimateReadyGlow", "HealthHud", "TraitHud",
                "AttackActionFrame", "MoveActionFrame" };
            Vector3 minimum = Vector3.one * float.MaxValue;
            Vector3 maximum = Vector3.one * float.MinValue;
            Vector3[] corners = new Vector3[4];
            foreach (string name in names)
            {
                RectTransform rect = GameObject.Find(name)?.GetComponent<RectTransform>();
                if (rect == null) continue;
                rect.SetParent(boardActionHudGroup, true);
                rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = boardActionHudGroup.InverseTransformPoint(corner);
                    minimum = Vector3.Min(minimum, local);
                    maximum = Vector3.Max(maximum, local);
                }
            }
            RectTransform status = GameObject.Find("ActionModeHud")?.GetComponent<RectTransform>();
            if (status != null)
            {
                status.SetParent(boardActionHudGroup, true);
                // 상태 문구는 카드 위에서 전체 묶음 폭을 사용한다. 글자 AutoSize는 사용하지 않는다.
                status.anchorMin = status.anchorMax = Vector2.one * 0.5f;
                status.pivot = Vector2.one * 0.5f;
                status.sizeDelta = new Vector2(maximum.x - minimum.x, 36f);
                status.anchoredPosition = new Vector2((minimum.x + maximum.x) * 0.5f, maximum.y + 18f);
                maximum.y += 36f;
            }
            boardActionHudBounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
            PolishActionHud(minimum, maximum);
        }
        BoundsInt cells = grid.GroundTilemap.cellBounds;
        float leftX = camera.WorldToScreenPoint(grid.GroundTilemap.CellToWorld(cells.min)).x;
        float rightX = camera.WorldToScreenPoint(grid.GroundTilemap.CellToWorld(cells.max)).x;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(leftX, bottomY - gap),
            null, out Vector2 left);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(rightX, bottomY - gap),
            null, out Vector2 right);
        float horizontalScale = (right.x - left.x) / Mathf.Max(1f, boardActionHudBounds.size.x);
        float verticalScale = (left.y - canvasRect.rect.yMin - ScreenEdgeMargin)
            / Mathf.Max(1f, boardActionHudBounds.size.y);
        float scale = Mathf.Max(0.01f, Mathf.Min(1f, horizontalScale, verticalScale));
        boardActionHudGroup.localScale = Vector3.one * scale;
        boardActionHudGroup.anchoredPosition = new Vector2((left.x + right.x) * 0.5f
            - boardActionHudBounds.center.x * scale, left.y - boardActionHudBounds.max.y * scale);
    }

    // 하단 행동 HUD: 받침 패널로 외벽이 틈새로 비치지 않게 하고, 체력·충전 글자를 키우며
    // 체력 바를 둥근 흐림 스프라이트 대신 단색 사각형으로 그려 픽셀 경계를 선명하게 한다.
    private void PolishActionHud(Vector3 minimum, Vector3 maximum)
    {
        if (MobilePlaytestController.IsActive) return;
        GameObject backing = new GameObject("ActionHudBacking", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
        RectTransform rect = backing.GetComponent<RectTransform>();
        rect.SetParent(boardActionHudGroup, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        const float padding = 8f;
        rect.anchoredPosition = (minimum + maximum) * 0.5f;
        rect.sizeDelta = new Vector2(maximum.x - minimum.x + padding * 2f, maximum.y - minimum.y + padding * 2f);
        UnityEngine.UI.Image image = backing.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color32(12, 18, 34, 245);
        image.raycastTarget = false;
        UnityEngine.UI.Outline outline = backing.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        UnityEngine.UI.Image healthFill = GameObject.Find("HealthFill")?.GetComponent<UnityEngine.UI.Image>();
        if (healthFill != null)
        {
            // Filled 채움은 스프라이트가 있어야 동작하므로 1px 흰색 스프라이트(Point)로 단색 사각형을 만든다.
            Texture2D white = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
            healthFill.sprite = Sprite.Create(white, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            healthFill.type = UnityEngine.UI.Image.Type.Filled;
        }
        foreach (string name in new[] { "HealthValue", "HealthLabel" })
        {
            UnityEngine.UI.Text text = GameObject.Find(name)?.GetComponent<UnityEngine.UI.Text>();
            if (text == null) continue;
            text.fontSize = 20;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
        UnityEngine.UI.Text traitHeader = GameObject.Find("TraitHeader")?.GetComponent<UnityEngine.UI.Text>();
        if (traitHeader != null)
        {
            traitHeader.fontSize = 14;
            traitHeader.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
        UnityEngine.UI.Text mode = GameObject.Find("ActionMode")?.GetComponent<UnityEngine.UI.Text>();
        if (mode != null) mode.fontSize = 18;
    }

    private void LayoutHeaderHud(float boardTop, float gap)
    {
        if (boardHeaderHudGroup == null)
        {
            boardHeaderHudGroup = new GameObject("BoardHeaderHudGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            boardHeaderHudGroup.SetParent(canvasRect, false);
            boardHeaderHudGroup.anchorMin = boardHeaderHudGroup.anchorMax = Vector2.one * 0.5f;
            boardHeaderHudGroup.sizeDelta = Vector2.zero;
            Vector3 minimum = Vector3.one * float.MaxValue;
            Vector3 maximum = Vector3.one * float.MinValue;
            Vector3[] corners = new Vector3[4];
            foreach (string name in new[] { "WaveHud", "TimeHud" })
            {
                RectTransform rect = GameObject.Find(name)?.GetComponent<RectTransform>();
                if (rect == null) continue;
                rect.SetParent(boardHeaderHudGroup, true);
                rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = boardHeaderHudGroup.InverseTransformPoint(corner);
                    minimum = Vector3.Min(minimum, local);
                    maximum = Vector3.Max(maximum, local);
                }
            }
            boardHeaderHudBounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
            new Vector2(Screen.width * 0.5f, boardTop + gap), null, out Vector2 anchor);
        boardHeaderHudGroup.anchoredPosition = anchor
            - new Vector2(boardHeaderHudBounds.center.x, boardHeaderHudBounds.min.y);
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
        panel.anchoredPosition = new Vector2(right ? innerEdge : -innerEdge, PcBoardLayout.BoardCenterY * scale);
        panel.sizeDelta = new Vector2(DefaultPanelWidth, PanelHeight);
        panel.localScale = new Vector3(scale, scale, 1f);
    }

    private void ShowUpgradeOptions(IReadOnlyList<UpgradeDefinition> options)
    {
        rewardOpenedFrame = Time.frameCount;
        CloseDetails();
        CloseInventory();
        upgradeTitle.text = progression.IsStarterUpgradeSelection ? "시작 증강 선택" : "웨이브 증강 선택";
        upgradeGuide.text = progression.IsStarterUpgradeSelection
            ? "현재 → 선택 후를 비교하세요 · 하나를 고르면 전투 시작 · 클릭 또는 1 · 2 · 3"
            : "현재 → 선택 후를 비교하세요 · 하나를 고르면 다음 웨이브 · 클릭 또는 1 · 2 · 3";
        if (MobilePlaytestController.IsActive)
            upgradeGuide.text = "카드를 탭하여 하나 선택 · 상세 버튼으로 효과 확인\n"
                + (progression.IsStarterUpgradeSelection ? "선택하면 전투 시작" : "선택하면 다음 웨이브 시작");
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
            string name = RunProgressionDescriptions.ChoiceName(item, progression);
            string summary = RunProgressionDescriptions.ChoiceSummary(item, progression);
            if (MobilePlaytestController.IsActive)
                upgradeTexts[i].text = $"{i + 1}. {grade}{name}\n\n{summary}";
            else
            {
                // "현재: …\n선택 후: …\n\n효과" 를 레벨 변화와 효과로 나눈다.
                int split = summary.IndexOf("\n\n", StringComparison.Ordinal);
                string levels = split >= 0 ? summary.Substring(0, split) : string.Empty;
                string effect = split >= 0 ? summary.Substring(split + 2) : summary;
                upgradeNames[i].text = name;
                upgradeGrades[i].text = progression.IsStarterUpgradeSelection ? "시작 강화"
                    : item.IsAdvanced ? "상급 강화" : "강화";
                upgradeGrades[i].color = item.IsAdvanced ? LegendaryColor : RareColor;
                // 두 줄: 현재 / 선택 후 (선택 후 값은 금색으로 강조)
                string[] levelLines = levels.Split('\n');
                string now = levelLines.Length > 0 ? levelLines[0].Replace("현재: ", string.Empty) : string.Empty;
                string after = levelLines.Length > 1 ? levelLines[1].Replace("선택 후: ", string.Empty) : string.Empty;
                upgradeLevels[i].text = $"현재  {now}\n선택 후  <color=#FFC954>{after}</color>";
                upgradeTexts[i].text = effect;
            }
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
        if (inventoryButtonLabel != null)
            inventoryButtonLabel.text = offers.Count > 0 ? $"빌드 · 제단 {offers.Count} · B" : "빌드 · B";
        altarEmptyText.gameObject.SetActive(offers.Count == 0);
        for (int i = 0; i < altarTexts.Length; i++)
        {
            bool visible = i < offers.Count;
            altarTexts[i].transform.parent.gameObject.SetActive(visible);
            if (!visible) continue;
            RelicOffer offer = offers[i];
            string danger = offer.IsRisky
                ? offer.IsLocked ? $"위험 · 잠김 {offer.Kills}/{offer.KillsRequired}" : "위험"
                : "안전";
            altarTexts[i].text = $"{danger} · {RarityName(offer.Relic.Rarity)}\n{offer.Relic.Name}\n클릭하여 효과 확인";
            altarTexts[i].color = offer.IsRisky ? RiskyColor : SafeColor;
            ApplyIcon(altarIcons[i], offer.Relic.Id, offer.Relic.Name);
        }
    }

    private void ShowReplacement(RelicDefinition incoming,
        IReadOnlyList<RelicDefinition> equipped)
    {
        CloseDetails();
        CloseInventory();
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
        rightPanel.gameObject.SetActive(visiblePanel == rightPanel || inventoryPanel != null);
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
            case "alternating_gear":
                return "ChainBurst";
            case "movement_breath":
            case "impact_recovery":
                return "BindingLanding";
            // 준비 적중 수를 줄이는 강화
            case "quick_cast":
            case "thin_reload":
            case "dense_mana":
            case "compressed_impact":
                return "Bolt";
            // 더블 캐스트 추가 시전
            case "echo_warhead":
            case "echo_recovery":
            case "twin_focus":
            case "rotating_mirror":
            case "triple_echo":
                return "EchoPressure";
            // 관통
            case "long_needle":
            case "pierce_recovery":
            case "thorn_needle":
            case "suture_needle":
            case "infinite_orbit":
                return "Needle";
            // 범위 공격·마력
            case "overcharge":
            case "hot_afterglow":
            case "excess_reservoir":
                return "ManaGem";
            case "afterglow_recovery":
            case "blast_core":
                return "ChainBurst";
            // 넉백
            case "long_impact":
            case "iron_nail":
            case "domino_crest":
            case "battering_ram":
                return "Ram";
            // 체력
            case "life_tempering":
            case "healing_breath":
            case "blood_knot":
            case "glass_heart":
                return "Heart";
            case "vanguard_shield":
            case "landing_ward":
                return "Shield";
            case "ash_boots":
                return "Boot";
            case "kings_turn":
                return "Crown";
            case "hunters_mark":
            case "ambush_crest":
                return "Target";
            case "rapid_cycle":
            case "keen_magic":
            case "trait_mastery":
                return "Star";
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
        if (progression == null) return;
        progression.UpgradeOptionsReady -= ShowUpgradeOptions;
        progression.UpgradesChanged -= RefreshBuild;
        progression.RelicsChanged -= RefreshRelics;
        progression.AltarsChanged -= RefreshAltars;
        progression.RelicReplacementRequested -= ShowReplacement;
        progression.MovementArtChanged -= RefreshMovementSummary;
    }
}
