using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameHudController : MonoBehaviour
{
    public const string StartupControlsText =
        "조작 안내   <color=#FF6569>A</color> 공격 방향 · <color=#59D1FF>S</color> 이동 / 이동술\n"
        + "일반 공격 적중으로 충전 · F / 특성 아이콘: 특수 공격\n"
        + "마우스: 화살표 경로 확인 / 좌클릭 실행 · 우클릭 취소\n"
        + "특성 선택: 카드 클릭 / 숫자 1 · 2 · 3 · 4";
    public static event Action QuitRequested;
    public event Action<string> TraitSelected;

    [Header("게임 참조")]
    [SerializeField] private Move playerMovement;
    [SerializeField] private UltimateGauge ultimateGauge;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private CharacterHealth playerHealth;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private PlayerTraitSystem playerTraitSystem;

    [Header("교체할 아이콘")]
    [Tooltip("각 항목의 Icon에 제작한 특성 아이콘 Sprite를 넣습니다.")]
    [SerializeField] private TraitOptionData[] traitOptions;
    [Tooltip("A 기본 공격 명령에 표시할 아이콘 Sprite를 넣습니다.")]
    [SerializeField] private Sprite basicAttackIcon;
    [Tooltip("S 이동 명령에 표시할 아이콘 Sprite를 넣습니다.")]
    [SerializeField] private Sprite moveIcon;
    [Tooltip("제작한 이동술 아이콘 Sprite를 넣습니다.")]
    [SerializeField] private Sprite ultimateIcon;
    [SerializeField] private Sprite knightMovementIcon;
    [SerializeField] private Sprite bishopMovementIcon;
    [SerializeField] private Sprite rookMovementIcon;

    [Header("UI 참조")]
    [SerializeField, HideInInspector] private Font uiFont;
    [SerializeField, HideInInspector] private GameObject selectionOverlay;
    [SerializeField, HideInInspector] private Button[] traitChoiceButtons;
    [SerializeField, HideInInspector] private Image[] traitChoiceIconImages;
    [SerializeField, HideInInspector] private Text[] traitChoicePlaceholderTexts;
    [SerializeField, HideInInspector] private Text[] traitChoiceNameTexts;
    [SerializeField, HideInInspector] private Text[] traitChoiceDescriptionTexts;
    private bool traitCardsPolished;
    [SerializeField, HideInInspector] private Image selectedTraitIconImage;
    [SerializeField, HideInInspector] private Text selectedTraitPlaceholderText;
    [SerializeField, HideInInspector] private Text selectedTraitNameText;
    [SerializeField, HideInInspector] private Image ultimateIconImage;
    [SerializeField, HideInInspector] private Text ultimatePlaceholderText;
    [SerializeField, HideInInspector] private Image ultimateGaugeFillImage;
    [SerializeField, HideInInspector] private Image ultimateFrameImage;
    [SerializeField, HideInInspector] private Image ultimateReadyGlowImage;
    [SerializeField, HideInInspector] private Text ultimateGaugeValueText;
    [SerializeField, HideInInspector] private Text ultimateStateText;
    [SerializeField, HideInInspector] private Image actionModeFrameImage;
    [SerializeField, HideInInspector] private Button attackActionButton;
    [SerializeField, HideInInspector] private Button moveActionButton;
    [SerializeField, HideInInspector] private Image attackActionFrameImage;
    [SerializeField, HideInInspector] private Image moveActionFrameImage;
    [SerializeField, HideInInspector] private Image attackActionIconImage;
    [SerializeField, HideInInspector] private Text attackActionPlaceholderText;
    [SerializeField, HideInInspector] private Image moveActionIconImage;
    [SerializeField, HideInInspector] private Text moveActionPlaceholderText;
    [SerializeField, HideInInspector] private Text actionModeText;
    [SerializeField, HideInInspector] private Text actionGuideText;
    [SerializeField, HideInInspector] private Text elapsedTimeText;
    [SerializeField, HideInInspector] private Text currentWaveText;
    [SerializeField, HideInInspector] private Image healthFillImage;
    [SerializeField, HideInInspector] private Text healthValueText;
    [SerializeField, HideInInspector] private GameObject gameOverOverlay;
    [SerializeField, HideInInspector] private Text gameOverSummaryText;
    [SerializeField, HideInInspector] private Button replayButton;
    [SerializeField, HideInInspector] private GameObject pauseMenuOverlay;
    [SerializeField, HideInInspector] private Button continueButton;
    [SerializeField, HideInInspector] private Button pauseRestartButton;
    [SerializeField, HideInInspector] private Button pauseQuitButton;
    [SerializeField, HideInInspector] private int layoutVersion;
    [SerializeField, HideInInspector] private int typographyVersion;

    private static readonly Color GaugeEmptyColor = new Color32(49, 105, 183, 255);
    private static readonly Color GaugeFullColor = new Color32(255, 201, 84, 255);
    private static readonly Color FrameNormalColor = new Color32(82, 132, 220, 255);
    private static readonly Color FrameReadyColor = new Color32(255, 210, 99, 255);
    private static readonly Color AttackModeColor = new Color32(255, 101, 105, 255);
    private static readonly Color MoveModeColor = new Color32(89, 209, 255, 255);
    private static readonly Color MainTextColor = new Color32(235, 244, 255, 255);

    private int selectedTraitIndex = -1;
    private bool gaugeSubscribed;
    private bool playerMovementSubscribed;
    private bool healthSubscribed;
    private bool waveSubscribed;
    private bool cameraWasEnabledBeforePause;
    private bool playerIsDead;
    private Button ultimateActionButton;
    private float elapsedPlayTime;
    private RunProgressionUiController runProgressionUi;
    private RunProgressionSystem progressionSystem;
    private PlayerMovementArt displayedMovementArt = (PlayerMovementArt)(-1);
    private int displayedMovementArtLevel = -1;
    private UnityEngine.UI.Button traitAttackButton;
    private UnityEngine.UI.Text traitChargeText;
    private EnemyThreatPreview enemyThreatPreview;
    private bool displayingThreatDescription;
    private int completedWorldTurns;
    private StageFlowManager subscribedStageFlow;

    public bool IsTraitSelectionOpen => selectionOverlay != null && selectionOverlay.activeSelf;
    public bool IsGameOver => playerIsDead;
    public bool IsStageCleared => monsterSpawner != null && monsterSpawner.StageFlow != null
        && monsterSpawner.StageFlow.State == StageFlowState.StageCleared;
    public bool IsRunEnded => IsGameOver || IsStageCleared;
    public float ElapsedPlayTime => elapsedPlayTime;
    public bool IsGameOverOverlayVisible => gameOverOverlay != null
        && gameOverOverlay.activeSelf;
    public bool IsPauseMenuOpen => pauseMenuOverlay != null && pauseMenuOverlay.activeSelf;
    public int LayoutVersion => layoutVersion;
    public string SelectedTraitId => IsValidTraitIndex(selectedTraitIndex)
        ? traitOptions[selectedTraitIndex].Id
        : string.Empty;

    private void Awake()
    {
        ApplyUiFont();
        ResolveGameplayReferences();
        GameAudio.GetOrCreate();
        PauseAudioSettings.Attach(pauseMenuOverlay);
        if (!MobilePlaytestController.IsActive)
        {
            UnityEngine.UI.CanvasScaler scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scaler.referenceResolution = PcBoardLayout.ReferenceResolution;
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            }
        }
        ConfigureTraitChoices();
        ConfigureActionButtons();
        ConfigureSessionButtons();
        if (actionModeText != null)
        {
            actionModeText.fontSize = 16;
            actionModeText.resizeTextForBestFit = false;
            actionModeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            actionModeText.verticalOverflow = VerticalWrapMode.Truncate;
        }
        ApplyActionIcon();
        ApplyUltimateIcon();
    }

    private void ApplyUiFont()
    {
        if (uiFont == null)
            return;

        foreach (Text text in GetComponentsInChildren<Text>(true))
        {
            text.font = uiFont;
            // 픽셀 서체의 작은 속공간이 막히지 않도록 합성 Bold를 사용하지 않는다.
            text.fontStyle = FontStyle.Normal;
        }
    }

    private void OnEnable()
    {
        ResolveGameplayReferences();
        progressionSystem = playerMovement != null
            ? playerMovement.GetComponent<RunProgressionSystem>() : null;
        if (progressionSystem != null)
        {
            progressionSystem.MovementArtChanged -= HandleMovementArtChanged;
            progressionSystem.MovementArtChanged += HandleMovementArtChanged;
        }
        SubscribePlayerMovement();
        SubscribeHealth();
        SubscribeWave();
        SubscribeStageFlow();
        if (playerTraitSystem != null) playerTraitSystem.ChargeChanged += RefreshTraitCharge;
    }

    private void Start()
    {
        if (MobilePlaytestController.IsActive && GetComponent<MobilePlaytestController>() == null)
            gameObject.AddComponent<MobilePlaytestController>();
        Time.timeScale = 1f;
        ConfigureStartupControls();
        RefreshGauge();
        RefreshActionMode(playerMovement != null
            ? playerMovement.SelectionMode
            : PlayerActionSelectionMode.None);
        RefreshElapsedTime();
        RefreshWave();
        RefreshHealth();
        // 모든 씬 Awake 이후 생성된 StageFlow도 연결한다. 재활성화 시에는 OnEnable이 연결한다.
        SubscribeStageFlow();

        RunProgressionSystem progression = playerMovement != null
            ? playerMovement.GetComponent<RunProgressionSystem>() : null;
        if (progression != null)
        {
            progressionSystem = progression;
            runProgressionUi = GetComponent<RunProgressionUiController>();
            if (runProgressionUi == null)
                runProgressionUi = gameObject.AddComponent<RunProgressionUiController>();
            runProgressionUi.Initialize(progression, cameraController);
            ConfigureProgressionDetails();
        }

        if (gameOverOverlay != null)
        {
            gameOverOverlay.SetActive(false);
        }

        if (pauseMenuOverlay != null)
        {
            pauseMenuOverlay.SetActive(false);
        }

        if (selectionOverlay == null || traitOptions == null || traitOptions.Length == 0)
        {
            Debug.LogError("특성 선택 UI 참조가 없어 플레이어 입력을 그대로 시작합니다.", this);
            return;
        }

        // 게임 시작 전에는 특성을 고를 때까지 플레이어 행동만 잠근다.
        selectionOverlay.SetActive(true);
        // 저장 씬에서는 ViewportHudRoot 아래에 있다. 런타임 사이드 패널보다 위에 표시한다.
        selectionOverlay.transform.SetParent(transform, true);
        selectionOverlay.transform.SetAsLastSibling();
        playerMovement?.SetInputEnabled(false);
        ShowUnselectedTrait();
    }

    private void ConfigureStartupControls()
    {
        if (selectionOverlay == null)
            return;

        Transform panel = selectionOverlay.transform.Find("SelectionPanel");
        UnityEngine.UI.Text footer = panel != null
            ? panel.Find("SelectionFooter")?.GetComponent<UnityEngine.UI.Text>()
            : null;
        if (footer == null)
            return;

        // 기존 시작 선택 화면 안에만 안내를 넣는다. 별도 팝업·입력 잠금·추가 클릭은 만들지 않는다.
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x,
                Mathf.Max(panelRect.sizeDelta.y, 832f));

        footer.text = StartupControlsText;
        footer.supportRichText = true;
        footer.fontSize = 18;
        footer.fontStyle = FontStyle.Normal;
        footer.lineSpacing = 1.1f;
        footer.alignment = TextAnchor.MiddleCenter;
        footer.resizeTextForBestFit = false;
        footer.horizontalOverflow = HorizontalWrapMode.Wrap;
        footer.verticalOverflow = VerticalWrapMode.Truncate;
        footer.raycastTarget = false;
        RectTransform footerRect = footer.rectTransform;
        footerRect.anchorMin = footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 18f);
        footerRect.sizeDelta = new Vector2(744f, 104f);
    }

    private bool displayingAttackOutcome;

    private void LateUpdate()
    {
        // 초기화 순서상 Awake/Start에서는 카드 배치가 덮일 수 있어, 창이 실제로 보일 때 한 번 맞춘다.
        if (!traitCardsPolished && IsTraitSelectionOpen && !MobilePlaytestController.IsActive)
        {
            PolishTraitCards();
            traitCardsPolished = true;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (runProgressionUi != null && runProgressionUi.IsModalOpen)
            {
                if (runProgressionUi.IsDetailOpen) runProgressionUi.CloseDetails();
                else if (runProgressionUi.IsInventoryOpen) runProgressionUi.CloseInventory();
                return;
            }
            if (IsRunEnded)
            {
                ToggleGameOverReview();
                return;
            }

            TogglePauseMenu();
            return;
        }

        if (!IsPauseMenuOpen)
        {
            HandleTraitShortcutInput();
        }

        UpdateElapsedTime();
        RefreshHealth();
        RefreshMovementArtVisualIfChanged();
        RefreshTraitCharge();
        if (enemyThreatPreview == null && playerMovement != null)
            enemyThreatPreview = playerMovement.GetComponent<EnemyThreatPreview>();
        if (enemyThreatPreview != null && !string.IsNullOrEmpty(enemyThreatPreview.Description))
        {
            if (actionModeText != null)
            {
                actionModeText.text = enemyThreatPreview.Description;
                actionModeText.color = MainTextColor;
            }
            displayingThreatDescription = true;
        }
        else if (!IsRunEnded && playerMovement.SelectionMode == PlayerActionSelectionMode.None
            && !string.IsNullOrEmpty(CombatFeedback.CurrentOutcome))
        {
            if (actionModeText != null)
            {
                actionModeText.text = CombatFeedback.CurrentOutcome;
                actionModeText.color = new Color(1f, 0.8f, 0.38f);
            }
            displayingAttackOutcome = true;
        }
        else if (displayingThreatDescription || displayingAttackOutcome)
        {
            displayingAttackOutcome = false;
            displayingThreatDescription = false;
            RefreshActionMode(playerMovement.SelectionMode);
        }
    }

    private void OnDisable()
    {
        if (playerTraitSystem != null) playerTraitSystem.ChargeChanged -= RefreshTraitCharge;
        if (progressionSystem != null)
            progressionSystem.MovementArtChanged -= HandleMovementArtChanged;
        UnsubscribePlayerMovement();
        UnsubscribeHealth();
        UnsubscribeWave();
        UnsubscribeStageFlow();

        bool wasPauseMenuOpen = IsPauseMenuOpen;

        if (wasPauseMenuOpen || IsRunEnded)
        {
            Time.timeScale = 1f;
        }

        if (wasPauseMenuOpen && cameraController != null)
        {
            cameraController.enabled = cameraWasEnabledBeforePause;
        }
    }

    public void SelectTrait(int index)
    {
        if (IsRunEnded || !IsValidTraitIndex(index))
            return;

        selectedTraitIndex = index;
        TraitOptionData selectedTrait = traitOptions[index];

        ApplyIcon(
            selectedTraitIconImage,
            selectedTraitPlaceholderText,
            selectedTrait.Icon,
            selectedTrait.Placeholder
        );

        if (selectedTraitNameText != null)
        {
            selectedTraitNameText.text = selectedTrait.DisplayName;
        }

        selectionOverlay.SetActive(false);
        playerMovement?.SetInputEnabled(true);
        playerTraitSystem?.SelectTrait(
            selectedTrait.Id,
            selectedTrait.ActivationBagSize
        );
        TraitSelected?.Invoke(selectedTrait.Id);
    }

    private void ResolveGameplayReferences()
    {
        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<Move>();
        }

        if (ultimateGauge == null && playerMovement != null)
        {
            ultimateGauge = playerMovement.GetComponent<UltimateGauge>();
        }
        if (ultimateGauge != null)
        {
            // 이동술이 상시 이동 선택지가 되면서 구형 게이지는 진행과 UI에서 완전히 정지한다.
            ultimateGauge.enabled = false;
        }

        if (cameraController == null)
        {
            cameraController = FindAnyObjectByType<CameraController>();
        }

        if (playerHealth == null && playerMovement != null)
        {
            playerHealth = playerMovement.GetComponent<CharacterHealth>();
        }

        if (playerTraitSystem == null && playerMovement != null)
        {
            playerTraitSystem = playerMovement.GetComponent<PlayerTraitSystem>();
        }

        if (monsterSpawner == null)
        {
            monsterSpawner = FindAnyObjectByType<MonsterSpawner>();
        }
    }

    // 시작 특성 카드: 위쪽에 아이콘·이름, 아래쪽 전체 폭에 준비 조건·효과를 둬 줄이 잘리지 않게 한다.
    private void PolishTraitCards()
    {
        if (traitChoiceButtons == null) return;
        // Awake 시점에는 레이아웃이 계산되지 않아 카드 크기가 0일 수 있다.
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < traitChoiceButtons.Length; i++)
        {
            if (traitChoiceButtons[i] == null) continue;
            RectTransform card = traitChoiceButtons[i].GetComponent<RectTransform>();
            float width = card.rect.width > 1f ? card.rect.width : 340f;
            float height = card.rect.height > 1f ? card.rect.height : 244f;
            PlaceTopLeft(card.Find("ShortcutBadge") as RectTransform, 8f, 8f, 28f, 28f);
            RectTransform iconFrame = card.Find("TraitIconFrame") as RectTransform;
            PlaceTopLeft(iconFrame, 26f, 28f, 84f, 84f);
            if (iconFrame != null && iconFrame.childCount > 0)
            {
                RectTransform iconBackground = iconFrame.GetChild(0) as RectTransform;
                iconBackground.anchorMin = Vector2.zero;
                iconBackground.anchorMax = Vector2.one;
                iconBackground.anchoredPosition = Vector2.zero;
                iconBackground.sizeDelta = new Vector2(-8f, -8f);
            }
            if (traitChoiceNameTexts != null && i < traitChoiceNameTexts.Length && traitChoiceNameTexts[i] != null)
            {
                Text name = traitChoiceNameTexts[i];
                PlaceTopLeft(name.rectTransform, 126f, 44f, width - 142f, 52f);
                name.fontSize = 24;
                name.alignment = TextAnchor.MiddleLeft;
            }
            if (traitChoiceDescriptionTexts != null && i < traitChoiceDescriptionTexts.Length
                && traitChoiceDescriptionTexts[i] != null)
            {
                Text description = traitChoiceDescriptionTexts[i];
                PlaceTopLeft(description.rectTransform, 20f, 128f, width - 40f, height - 140f);
                description.fontSize = 17;
                description.lineSpacing = 1.15f;
                description.alignment = TextAnchor.UpperLeft;
                description.horizontalOverflow = HorizontalWrapMode.Wrap;
                description.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }
    }

    private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void ConfigureTraitChoices()
    {
        int optionCount = traitOptions != null ? traitOptions.Length : 0;

        for (int i = 0; i < optionCount; i++)
        {
            int capturedIndex = i;

            if (traitChoiceButtons != null && i < traitChoiceButtons.Length
                && traitChoiceButtons[i] != null)
            {
                traitChoiceButtons[i].onClick.AddListener(() => SelectTrait(capturedIndex));
            }

            if (traitChoiceNameTexts != null && i < traitChoiceNameTexts.Length
                && traitChoiceNameTexts[i] != null)
            {
                traitChoiceNameTexts[i].text = traitOptions[i].DisplayName;
            }

            if (traitChoiceDescriptionTexts != null && i < traitChoiceDescriptionTexts.Length
                && traitChoiceDescriptionTexts[i] != null)
            {
                string shortEffect = traitOptions[i].Id switch
                {
                    "double_cast" => "같은 방향으로 기본공격을 한 번 더 시전합니다.",
                    "pierce" => "첫 적을 넘어 뒤쪽 적 1명까지 공격합니다.",
                    "damage_boost" => "적중 주변 8칸 공격 · 직격 피해 +1",
                    "knockback" => "생존한 적을 1칸 밀고, 벽·적 충돌 시 피해 1.",
                    _ => traitOptions[i].Description
                };
                int hits = Mathf.Max(1, traitOptions[i].ActivationBagSize - 1);
                string cost = MobilePlaytestController.IsActive
                    ? "일반 공격 적중 " + hits + "회로 준비 · 수동 사용"
                    : "적중 " + hits + "회로 준비 · 수동 사용";
                // PC 카드는 준비 조건을 강조색 한 줄로, 효과를 그 아래에 둔다(모바일은 기존 문구).
                traitChoiceDescriptionTexts[i].supportRichText = true;
                traitChoiceDescriptionTexts[i].text = MobilePlaytestController.IsActive
                    ? cost + "\n" + shortEffect
                    : "<color=#67CAFF>" + cost + "</color>\n" + shortEffect;
            }

            Image iconImage = traitChoiceIconImages != null && i < traitChoiceIconImages.Length
                ? traitChoiceIconImages[i]
                : null;
            Text placeholderText = traitChoicePlaceholderTexts != null
                && i < traitChoicePlaceholderTexts.Length
                ? traitChoicePlaceholderTexts[i]
                : null;

            ApplyIcon(
                iconImage,
                placeholderText,
                traitOptions[i].Icon,
                traitOptions[i].Placeholder
            );
        }
    }

    private void HandleTraitShortcutInput()
    {
        if (!IsTraitSelectionOpen || Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SelectTrait(0);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SelectTrait(1);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SelectTrait(2);
        }
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            SelectTrait(3);
        }
    }

    private void ConfigureActionButtons()
    {
        if (playerMovement == null)
            return;

        if (attackActionButton != null)
        {
            attackActionButton.onClick.AddListener(playerMovement.SelectAttackMode);
        }

        if (moveActionButton != null)
        {
            moveActionButton.onClick.AddListener(playerMovement.SelectMoveMode);
        }

        // 기존 프레임은 보유 이동술을 보여주는 상태 카드로만 사용한다.
        if (ultimateFrameImage != null)
        {
            ultimateActionButton = ultimateFrameImage.GetComponent<Button>();
            if (ultimateActionButton != null)
            {
                ultimateActionButton.onClick.RemoveAllListeners();
                ultimateActionButton.interactable = false;
            }
        }
    }

    private void ConfigureSessionButtons()
    {
        if (replayButton != null)
        {
            replayButton.onClick.AddListener(RestartGame);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ContinueGame);
        }

        if (pauseRestartButton != null)
        {
            pauseRestartButton.onClick.AddListener(RestartGame);
        }

        if (pauseQuitButton != null)
        {
            pauseQuitButton.onClick.AddListener(QuitGame);
        }
    }

    private void ConfigureProgressionDetails()
    {
        if (runProgressionUi == null) return;
        if (ultimateFrameImage != null)
        {
            ultimateActionButton = ultimateFrameImage.GetComponent<UnityEngine.UI.Button>();
            if (ultimateActionButton == null)
                ultimateActionButton = ultimateFrameImage.gameObject.AddComponent<UnityEngine.UI.Button>();
            ultimateFrameImage.raycastTarget = true;
            ultimateActionButton.targetGraphic = ultimateFrameImage;
            ultimateActionButton.onClick.RemoveAllListeners();
            ultimateActionButton.onClick.AddListener(runProgressionUi.ShowMovementDetails);
            ultimateActionButton.interactable = true;
        }
        // 기존 특성 HUD의 배경만 클릭 대상으로 바꾸고 아이콘/글자는 입력을 통과시킨다.
        Transform traitHud = selectedTraitIconImage != null
            ? selectedTraitIconImage.transform.parent?.parent : null;
        if (traitHud != null && traitHud.TryGetComponent(out UnityEngine.UI.Image frame))
        {
            UnityEngine.UI.Button button = traitHud.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = traitHud.gameObject.AddComponent<UnityEngine.UI.Button>();
            frame.raycastTarget = true;
            button.targetGraphic = frame;
            button.onClick.AddListener(runProgressionUi.ShowTraitDetails);
            traitChargeText = traitHud.Find("TraitHeader")?.GetComponent<UnityEngine.UI.Text>();
            if (traitChargeText != null) traitChargeText.fontSize = 12;
            Transform iconFrame = selectedTraitIconImage.transform.parent;
            UnityEngine.UI.Image iconBackground = iconFrame.GetComponent<UnityEngine.UI.Image>();
            if (iconBackground != null)
            {
                traitAttackButton = iconFrame.GetComponent<UnityEngine.UI.Button>();
                if (traitAttackButton == null)
                    traitAttackButton = iconFrame.gameObject.AddComponent<UnityEngine.UI.Button>();
                iconBackground.raycastTarget = true;
                traitAttackButton.targetGraphic = iconBackground;
                traitAttackButton.onClick.RemoveAllListeners();
                traitAttackButton.onClick.AddListener(() => playerMovement.SelectTraitAttackMode());
            }
            RefreshTraitCharge();
        }
    }

    private void RefreshTraitCharge()
    {
        if (playerTraitSystem == null) return;
        if (traitChargeText != null)
            traitChargeText.text = playerTraitSystem.IsReady ? "F 준비 1/1"
                : $"F 충전 {playerTraitSystem.ChargeProgress}/{playerTraitSystem.ChargeRequired}";
        if (traitAttackButton != null)
            traitAttackButton.interactable = playerTraitSystem.IsReady && playerMovement.CanAct
                && !playerMovement.IsChoosingFreeMove && !playerMovement.IsChoosingEchoDirection;
    }

    public void TogglePauseMenu()
    {
        if (IsRunEnded || pauseMenuOverlay == null)
            return;

        SetPauseMenuOpen(!IsPauseMenuOpen);
    }

    public void ToggleGameOverReview()
    {
        if (!IsRunEnded || gameOverOverlay == null)
            return;

        bool shouldShowOverlay = !gameOverOverlay.activeSelf;
        gameOverOverlay.SetActive(shouldShowOverlay);

        if (shouldShowOverlay)
        {
            gameOverOverlay.transform.SetAsLastSibling();
        }
    }

    public void ContinueGame()
    {
        if (IsRunEnded) return;
        SetPauseMenuOpen(false);
    }

    // 웹의 백그라운드 전환은 모바일 어댑터가 없는 PC에서도 일시정지한다.
    public void PauseFromBrowser()
    {
        if (!MobilePlaytestController.IsActive && !IsRunEnded && !IsPauseMenuOpen)
            SetPauseMenuOpen(true);
    }

    private void SetPauseMenuOpen(bool isOpen)
    {
        if (pauseMenuOverlay == null || (isOpen && IsRunEnded))
            return;

        if (isOpen)
        {
            playerMovement?.SetInputEnabled(false);
            cameraWasEnabledBeforePause = cameraController != null && cameraController.enabled;

            if (cameraController != null)
            {
                cameraController.enabled = false;
            }

            pauseMenuOverlay.SetActive(true);
            // 시작 선택과 보드 옆 빌드 UI보다 위에서 전체 Canvas 입력을 막는다.
            RectTransform pauseRect = pauseMenuOverlay.GetComponent<RectTransform>();
            pauseRect.SetParent(transform, false);
            pauseRect.anchorMin = Vector2.zero;
            pauseRect.anchorMax = Vector2.one;
            pauseRect.offsetMin = pauseRect.offsetMax = Vector2.zero;
            pauseRect.localScale = Vector3.one;
            pauseMenuOverlay.transform.SetAsLastSibling();
            Time.timeScale = 0f;
            return;
        }

        pauseMenuOverlay.SetActive(false);
        Time.timeScale = 1f;

        if (cameraController != null)
        {
            cameraController.enabled = cameraWasEnabledBeforePause;
        }

        if (!IsTraitSelectionOpen && !IsRunEnded)
        {
            playerMovement?.SetInputEnabled(true);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        Scene currentScene = gameObject.scene;

        if (!string.IsNullOrEmpty(currentScene.path))
        {
            SceneManager.LoadScene(currentScene.path);
        }
        else
        {
            SceneManager.LoadScene(currentScene.buildIndex);
        }
    }

    public void QuitGame()
    {
        Debug.Log("게임 종료를 요청했습니다. PC 빌드에서 애플리케이션을 종료합니다.", this);
        QuitRequested?.Invoke();
        Application.Quit();
    }

    private void ApplyActionIcon()
    {
        ApplyIcon(
            attackActionIconImage,
            attackActionPlaceholderText,
            basicAttackIcon,
            "ATK"
        );
        ApplyIcon(
            moveActionIconImage,
            moveActionPlaceholderText,
            moveIcon,
            "MOVE"
        );
    }

    private void ApplyUltimateIcon()
    {
        ApplyIcon(
            ultimateIconImage,
            ultimatePlaceholderText,
            CurrentMovementArtIcon(),
            "이동"
        );
    }

    private Sprite CurrentMovementArtIcon()
    {
        if (progressionSystem == null && playerMovement != null)
            progressionSystem = playerMovement.GetComponent<RunProgressionSystem>();
        if (progressionSystem == null) return ultimateIcon;
        return progressionSystem.ActiveMovementArt switch
        {
            PlayerMovementArt.Knight => knightMovementIcon != null ? knightMovementIcon : ultimateIcon,
            PlayerMovementArt.Bishop => bishopMovementIcon != null ? bishopMovementIcon : ultimateIcon,
            PlayerMovementArt.Rook => rookMovementIcon != null ? rookMovementIcon : ultimateIcon,
            _ => ultimateIcon
        };
    }

    private void ApplyIcon(Image image, Text placeholderText, Sprite sprite, string placeholder)
    {
        if (image != null)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : Color.clear;
            image.preserveAspect = true;
        }

        if (placeholderText != null)
        {
            placeholderText.gameObject.SetActive(sprite == null);
            placeholderText.text = placeholder;
        }
    }

    private void ShowUnselectedTrait()
    {
        ApplyIcon(selectedTraitIconImage, selectedTraitPlaceholderText, null, "?");

        if (selectedTraitNameText != null)
        {
            selectedTraitNameText.text = "특성 선택 대기";
        }
    }

    private void SubscribeGauge()
    {
        if (gaugeSubscribed || ultimateGauge == null)
            return;

        ultimateGauge.GaugeChanged += HandleGaugeChanged;
        ultimateGauge.ReadyStateChanged += HandleReadyStateChanged;
        gaugeSubscribed = true;
    }

    private void UnsubscribeGauge()
    {
        if (!gaugeSubscribed || ultimateGauge == null)
            return;

        ultimateGauge.GaugeChanged -= HandleGaugeChanged;
        ultimateGauge.ReadyStateChanged -= HandleReadyStateChanged;
        gaugeSubscribed = false;
    }

    private void SubscribePlayerMovement()
    {
        if (playerMovementSubscribed || playerMovement == null)
            return;

        playerMovement.SelectionModeChanged += RefreshActionMode;
        playerMovementSubscribed = true;
    }

    private void UnsubscribePlayerMovement()
    {
        if (!playerMovementSubscribed || playerMovement == null)
            return;

        playerMovement.SelectionModeChanged -= RefreshActionMode;
        playerMovementSubscribed = false;
    }

    private void SubscribeHealth()
    {
        if (healthSubscribed || playerHealth == null)
            return;

        playerHealth.Died += HandlePlayerDied;
        healthSubscribed = true;
    }

    private void UnsubscribeHealth()
    {
        if (!healthSubscribed || playerHealth == null)
            return;

        playerHealth.Died -= HandlePlayerDied;
        healthSubscribed = false;
    }

    private void SubscribeWave()
    {
        if (waveSubscribed || monsterSpawner == null)
            return;

        monsterSpawner.WaveStarted += HandleWaveStarted;
        monsterSpawner.WorldTurnCompleted += CountWorldTurn;
        waveSubscribed = true;
    }

    private void UnsubscribeWave()
    {
        if (!waveSubscribed || monsterSpawner == null)
            return;

        monsterSpawner.WaveStarted -= HandleWaveStarted;
        monsterSpawner.WorldTurnCompleted -= CountWorldTurn;
        waveSubscribed = false;
    }

    private void CountWorldTurn() { completedWorldTurns++; }

    private void SubscribeStageFlow()
    {
        StageFlowManager flow = monsterSpawner != null ? monsterSpawner.StageFlow : null;
        if (flow == null || subscribedStageFlow == flow) return;
        UnsubscribeStageFlow();
        subscribedStageFlow = flow;
        subscribedStageFlow.StageCleared += HandleStageCleared;
    }

    private void UnsubscribeStageFlow()
    {
        if (subscribedStageFlow != null) subscribedStageFlow.StageCleared -= HandleStageCleared;
        subscribedStageFlow = null;
    }

    private void HandleStageCleared()
    {
        // 사망과 클리어는 다른 상태지만 복기/재도전은 기존 결과 패널을 공유한다.
        playerMovement?.SetInputEnabled(false);
        if (IsPauseMenuOpen) SetPauseMenuOpen(false);
        if (gameOverSummaryText != null)
            gameOverSummaryText.text = $"WAVE {monsterSpawner.CurrentWave} 클리어\n플레이 시간 {FormatElapsedTime()}";
        ShowRunResult(true);
        RefreshActionMode(PlayerActionSelectionMode.None);
        Time.timeScale = 0f;
    }

    private void ShowRunResult(bool cleared)
    {
        if (gameOverOverlay == null) return;
        foreach (Text text in gameOverOverlay.GetComponentsInChildren<Text>(true))
        {
            switch (text.name)
            {
                case "GameOverTitle": text.text = cleared ? "스테이지 클리어" : "GAME OVER"; break;
                case "GameOverEyebrow":
                    text.text = cleared ? "RUN CLEARED" : "RUN ENDED";
                    text.color = cleared ? FrameReadyColor : new Color32(255, 122, 130, 255);
                    break;
                case "ReplayLabel": text.text = cleared ? "새 빌드 시작" : "REPLAY"; break;
                case "QuitHint": text.text = "ESC 전장 확인 · 버튼으로 새 빌드 시작"; break;
            }
        }
        Outline border = gameOverOverlay.transform.Find("GameOverPanel")?.GetComponent<Outline>();
        if (border != null) border.effectColor = cleared ? FrameReadyColor : new Color32(236, 77, 88, 255);
        // 보드 밖에 생성한 성장 패널보다 위에서 클릭을 받아야 한다.
        gameOverOverlay.transform.SetParent(transform, true);
        RectTransform overlayRect = gameOverOverlay.GetComponent<RectTransform>();
        overlayRect.localScale = Vector3.one;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        gameOverOverlay.SetActive(true);
        gameOverOverlay.transform.SetAsLastSibling();
    }

    [Serializable]
    private sealed class PcPlaytestState
    {
        public int width, height, x, y, hp, wave, worldTurns, charge;
        public bool mobile, startSelection, rewardSelection, detailOpen, canAct, paused, ready;
        public string action;
        public float playerX, playerY, tilePixels;
        public float[] board;
        public PcUiTarget[] ui;
    }

    [Serializable]
    private sealed class PcUiTarget
    {
        public string name;
        public float x, y, width, height;
    }

    // 화면/입력 회귀 검사와 버그 보고용 읽기 전용 정보. 전투를 조작하는 테스트 API는 아니다.
    public void ReportPcPlaytestState()
    {
        Camera camera = Camera.main;
        GridManager grid = FindAnyObjectByType<GridManager>();
        if (camera == null || grid == null || playerMovement == null) return;
        BoundsInt cells = grid.GroundTilemap.cellBounds;
        Vector3 min = camera.WorldToScreenPoint(grid.GroundTilemap.CellToWorld(cells.min));
        Vector3 max = camera.WorldToScreenPoint(grid.GroundTilemap.CellToWorld(cells.max));
        Vector3 player = camera.WorldToScreenPoint(grid.GetCellCenterWorld(playerMovement.GridPosition));
        var targets = new System.Collections.Generic.List<PcUiTarget>();
        string[] names = { "BuildHudPanel", "RelicHudPanel", "BuildInventoryButton", "CloseBuildInventory", "WaveHud", "TimeHud", "HealthHud", "TraitHud",
            "UltimateFrame", "AttackActionFrame", "MoveActionFrame", "ActionModeHud", "SelectedTraitIconFrame",
            "TraitCard_1", "UpgradeChoice_1", "Details", "CloseDetails", "ContinueButton", "MovementArtDetails" };
        foreach (RectTransform rect in GetComponentsInChildren<RectTransform>(true))
        {
            if (!rect.gameObject.activeInHierarchy || Array.IndexOf(names, rect.name) < 0) continue;
            Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
            targets.Add(new PcUiTarget { name = rect.name, x = (corners[0].x + corners[2].x) * 0.5f,
                y = Screen.height - (corners[0].y + corners[2].y) * 0.5f,
                width = corners[2].x - corners[0].x, height = corners[2].y - corners[0].y });
        }
        var state = new PcPlaytestState { width = Screen.width, height = Screen.height,
            x = playerMovement.GridPosition.x, y = playerMovement.GridPosition.y, hp = playerMovement.CurrentHealth,
            wave = monsterSpawner.CurrentWave, worldTurns = completedWorldTurns,
            mobile = MobilePlaytestController.IsActive, startSelection = IsTraitSelectionOpen,
            rewardSelection = progressionSystem != null && progressionSystem.CurrentUpgradeOptions.Count > 0,
            detailOpen = runProgressionUi != null && runProgressionUi.IsDetailOpen,
            canAct = playerMovement.CanAct, paused = IsPauseMenuOpen, action = playerMovement.SelectionMode.ToString(),
            charge = playerTraitSystem.ChargeProgress, ready = playerTraitSystem.IsReady,
            playerX = player.x, playerY = Screen.height - player.y,
            tilePixels = Vector3.Distance(player, camera.WorldToScreenPoint(grid.GetCellCenterWorld(
                playerMovement.GridPosition + Vector3Int.right))),
            board = new[] { min.x, Screen.height - max.y, max.x - min.x, max.y - min.y }, ui = targets.ToArray() };
        Debug.Log("PC_PLAYTEST_STATE " + JsonUtility.ToJson(state));
    }

    private void HandlePlayerDied(CharacterHealth defeatedCharacter)
    {
        playerIsDead = true;
        playerMovement?.SetInputEnabled(false);

        if (IsPauseMenuOpen)
        {
            SetPauseMenuOpen(false);
        }

        if (gameOverSummaryText != null)
        {
            int waveNumber = monsterSpawner != null
                ? Mathf.Max(1, monsterSpawner.CurrentWave)
                : 1;
            gameOverSummaryText.text = $"WAVE {waveNumber}\n생존 시간 {FormatElapsedTime()}";
        }

        ShowRunResult(false);

        // 사망 위치와 적 배치를 그대로 복기할 수 있도록 게임 규칙과 연출을 정지한다.
        Time.timeScale = 0f;
    }

    private void HandleWaveStarted(int waveNumber)
    {
        RefreshWave(waveNumber);
    }

    private void HandleGaugeChanged(int currentGauge, int maxGauge)
    {
        RefreshGauge();
    }

    private void HandleReadyStateChanged(bool isReady)
    {
        RefreshGauge();
    }

    private void RefreshActionMode(PlayerActionSelectionMode mode)
    {
        if (actionGuideText != null)
        {
            actionGuideText.text = "A 일반 공격 · F 특수 공격 · S 이동/이동술 · 우클릭 취소";
        }

        SetActionCardColors(mode);

        if (actionModeText == null)
            return;

        if (IsRunEnded)
        {
            actionModeText.text = IsStageCleared ? "스테이지 클리어 · ESC 전장 확인"
                : "런 종료 · ESC 전장 확인";
            actionModeText.color = MainTextColor;
            return;
        }

        if (playerMovement != null && playerMovement.IsChoosingFreeMove)
        {
            actionModeText.text = "무료 기본 1칸 · 우클릭 생략 · 이후 적 행동 1회";
            actionModeText.color = MainTextColor;
            SetActionFrameColor(MoveModeColor);
            return;
        }

        switch (mode)
        {
            case PlayerActionSelectionMode.Attack:
                actionModeText.text = playerMovement != null && playerMovement.IsChoosingEchoDirection
                    ? "추가 시전 재조준 · 현재 보드 기준 · 이동 불가"
                    : playerMovement != null && playerMovement.IsTraitAttackSelected
                        ? "특성 공격 · 확정 시 소모 · 옅은 경로는 적중 후 달라질 수 있음"
                        : "일반 공격 방향 · 적중하면 충전 +1";
                actionModeText.color = MainTextColor;
                SetActionFrameColor(AttackModeColor);
                break;

            case PlayerActionSelectionMode.Move:
                actionModeText.text = progressionSystem != null
                    && progressionSystem.ActiveMovementArt == PlayerMovementArt.Rook
                    ? "룩 이동: 중간 벽·적을 통과하지 못함 · 불길 진입 주의"
                    : "이동할 타일을 선택하세요";
                actionModeText.color = MainTextColor;
                SetActionFrameColor(MoveModeColor);
                break;

            case PlayerActionSelectionMode.MovementArt:
                actionModeText.text = $"{MovementArtDisplayName()}의 착지 칸을 선택하세요";
                actionModeText.color = MainTextColor;
                SetActionFrameColor(GaugeFullColor);
                break;

            default:
                actionModeText.text = "행동을 선택하세요";
                actionModeText.color = MainTextColor;
                SetActionFrameColor(FrameNormalColor);
                break;
        }
    }

    private void SetActionFrameColor(Color color)
    {
        if (actionModeFrameImage != null)
        {
            // 색상은 배경에 은은하게 반영하고 글자와 같은 색으로 채우지 않는다.
            actionModeFrameImage.color = Color.Lerp(new Color32(7, 16, 31, 255), color, 0.25f);
        }
    }

    private void SetActionCardColors(PlayerActionSelectionMode mode)
    {
        if (attackActionFrameImage != null)
        {
            attackActionFrameImage.color = mode == PlayerActionSelectionMode.Attack
                ? AttackModeColor
                : FrameNormalColor;
        }

        if (moveActionFrameImage != null)
        {
            moveActionFrameImage.color = mode == PlayerActionSelectionMode.Move
                ? MoveModeColor
                : FrameNormalColor;
        }
    }

    private void UpdateElapsedTime()
    {
        // 시작 특성을 고르는 동안과 사망 후에는 생존 시간이 흐르지 않는다.
        if (!IsRunEnded && !IsTraitSelectionOpen
            && !IsPauseMenuOpen
            && (runProgressionUi == null || !runProgressionUi.IsModalOpen)
            && (playerHealth == null || !playerHealth.IsDead))
        {
            elapsedPlayTime += Time.unscaledDeltaTime;
        }

        RefreshElapsedTime();
    }

    private void RefreshElapsedTime()
    {
        if (elapsedTimeText == null)
            return;

        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedPlayTime));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        elapsedTimeText.text = $"{minutes:00}:{seconds:00}";
    }

    private string FormatElapsedTime()
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedPlayTime));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void RefreshWave(int waveNumber = -1)
    {
        if (currentWaveText == null)
            return;

        int displayedWave = waveNumber > 0
            ? waveNumber
            : monsterSpawner != null
                ? Mathf.Max(1, monsterSpawner.CurrentWave)
                : 1;
        currentWaveText.text = $"WAVE {displayedWave}";
    }

    private void RefreshHealth()
    {
        if (playerHealth == null)
        {
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = 1f;
            }

            if (healthValueText != null)
            {
                healthValueText.text = "--/--";
            }

            return;
        }

        int maxHealth = Mathf.Max(1, playerHealth.MaxHealth);
        int currentHealth = Mathf.Clamp(playerHealth.CurrentHealth, 0, maxHealth);
        float normalizedHealth = (float)currentHealth / maxHealth;

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = normalizedHealth;
            healthFillImage.color = Color.Lerp(
                new Color32(235, 70, 78, 255),
                new Color32(86, 221, 137, 255),
                normalizedHealth
            );
        }

        if (healthValueText != null)
        {
            healthValueText.text = $"{currentHealth}/{maxHealth}";
        }
    }

    private void RefreshGauge()
    {
        bool hasMovementArt = progressionSystem != null && progressionSystem.HasMovementArt;
        ApplyUltimateIcon();
        displayedMovementArt = progressionSystem != null
            ? progressionSystem.ActiveMovementArt : PlayerMovementArt.None;
        displayedMovementArtLevel = progressionSystem != null
            ? progressionSystem.MovementArtLevel : 0;

        if (ultimateGaugeFillImage != null)
        {
            ultimateGaugeFillImage.transform.parent.gameObject.SetActive(false);
        }

        if (ultimateGaugeValueText != null)
        {
            ultimateGaugeValueText.text = hasMovementArt
                ? $"{MovementArtDisplayName()}\n현재 Lv.{progressionSystem.MovementArtLevel}"
                : "미보유";
            ultimateGaugeValueText.fontSize = hasMovementArt ? 10 : 11;
        }

        if (ultimateStateText != null)
        {
            ultimateStateText.text = hasMovementArt
                ? "S 이동 · 클릭하면 상세 설명"
                : "2웨이브 보상에서 이동술 습득";
            ultimateStateText.color = hasMovementArt
                ? GaugeFullColor : new Color32(166, 190, 224, 255);
        }

        if (ultimateFrameImage != null)
        {
            ultimateFrameImage.color = hasMovementArt ? FrameReadyColor : FrameNormalColor;
            Transform shortcutKey = ultimateFrameImage.transform.Find(
                "UltimateShortcutBadge/UltimateShortcutKey");
            if (shortcutKey != null && shortcutKey.TryGetComponent(out Text keyText))
                keyText.text = "S+";
        }

        if (ultimateReadyGlowImage != null)
        {
            ultimateReadyGlowImage.gameObject.SetActive(false);
        }
    }

    private void RefreshMovementArtVisualIfChanged()
    {
        if (progressionSystem == null && playerMovement != null)
            progressionSystem = playerMovement.GetComponent<RunProgressionSystem>();
        if (progressionSystem == null) return;
        if (displayedMovementArt == progressionSystem.ActiveMovementArt
            && displayedMovementArtLevel == progressionSystem.MovementArtLevel)
            return;
        RefreshGauge();
    }

    private void RefreshUltimateButton()
    {
        if (ultimateActionButton != null) ultimateActionButton.interactable = runProgressionUi != null;
    }

    private void AnimateReadyGlow()
    {
        if (ultimateReadyGlowImage != null)
            ultimateReadyGlowImage.gameObject.SetActive(false);
    }

    private void HandleMovementArtChanged(PlayerMovementArt movementArt, int level)
    {
        RefreshGauge();
        RefreshActionMode(playerMovement != null
            ? playerMovement.SelectionMode : PlayerActionSelectionMode.None);
    }

    private string MovementArtDisplayName()
    {
        if (progressionSystem == null && playerMovement != null)
            progressionSystem = playerMovement.GetComponent<RunProgressionSystem>();
        return progressionSystem != null ? progressionSystem.MovementArtName : "이동술";
    }

    private bool IsValidTraitIndex(int index)
    {
        return traitOptions != null && index >= 0 && index < traitOptions.Length;
    }
}
