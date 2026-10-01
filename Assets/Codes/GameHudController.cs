using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameHudController : MonoBehaviour
{
    private const float BoardEdgeHudReservation = 96f;
    public const string StartupControlsText =
        "조작 안내   <color=#FF6569>A</color> 공격 방향 · <color=#59D1FF>S</color> 이동 / 이동술\n"
        + "공격 화살표에 마우스: 경로 확인 · 좌클릭: 실행\n"
        + "우클릭: 선택 취소 · 행동하면 적도 한 번 행동\n"
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

    public bool IsTraitSelectionOpen => selectionOverlay != null && selectionOverlay.activeSelf;
    public bool IsGameOver => playerIsDead;
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
        cameraController?.SetVerticalHudReservation(BoardEdgeHudReservation);
        ConfigureTraitChoices();
        ConfigureActionButtons();
        ConfigureSessionButtons();
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
    }

    private void Start()
    {
        Time.timeScale = 1f;
        ConfigureStartupControls();
        RefreshGauge();
        RefreshActionMode(playerMovement != null
            ? playerMovement.SelectionMode
            : PlayerActionSelectionMode.None);
        RefreshElapsedTime();
        RefreshWave();
        RefreshHealth();

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

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (runProgressionUi != null && runProgressionUi.IsModalOpen)
            {
                if (runProgressionUi.IsDetailOpen) runProgressionUi.CloseDetails();
                return;
            }
            if (IsGameOver)
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
    }

    private void OnDisable()
    {
        if (progressionSystem != null)
            progressionSystem.MovementArtChanged -= HandleMovementArtChanged;
        UnsubscribePlayerMovement();
        UnsubscribeHealth();
        UnsubscribeWave();

        bool wasPauseMenuOpen = IsPauseMenuOpen;

        if (wasPauseMenuOpen || IsGameOver)
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
        if (!IsValidTraitIndex(index))
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
                    "damage_boost" => "발동한 공격의 직접 피해가 1 증가합니다.",
                    "knockback" => "살아남은 적을 공격 방향으로 1칸 밉니다.",
                    _ => traitOptions[i].Description
                };
                traitChoiceDescriptionTexts[i].text = "평균 발동 확률 "
                    + RunProgressionDescriptions.Chance(traitOptions[i].ActivationBagSize)
                    + "\n" + shortEffect;
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
        }
    }

    public void TogglePauseMenu()
    {
        if (IsGameOver || pauseMenuOverlay == null)
            return;

        SetPauseMenuOpen(!IsPauseMenuOpen);
    }

    private void ToggleGameOverReview()
    {
        if (!IsGameOver || gameOverOverlay == null)
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
        SetPauseMenuOpen(false);
    }

    private void SetPauseMenuOpen(bool isOpen)
    {
        if (pauseMenuOverlay == null)
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

        if (!IsTraitSelectionOpen && !IsGameOver)
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
        waveSubscribed = true;
    }

    private void UnsubscribeWave()
    {
        if (!waveSubscribed || monsterSpawner == null)
            return;

        monsterSpawner.WaveStarted -= HandleWaveStarted;
        waveSubscribed = false;
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

        if (gameOverOverlay != null)
        {
            gameOverOverlay.SetActive(true);
            gameOverOverlay.transform.SetAsLastSibling();
        }

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
            actionGuideText.text = "A 공격 · S 이동/이동술 · 아이콘 클릭 가능 · 우클릭 취소 · Esc 메뉴";
        }

        SetActionCardColors(mode);

        if (actionModeText == null)
            return;

        switch (mode)
        {
            case PlayerActionSelectionMode.Attack:
                actionModeText.text = "공격 방향을 선택하세요";
                actionModeText.color = AttackModeColor;
                SetActionFrameColor(AttackModeColor);
                break;

            case PlayerActionSelectionMode.Move:
                actionModeText.text = "이동할 타일을 선택하세요";
                actionModeText.color = MoveModeColor;
                SetActionFrameColor(MoveModeColor);
                break;

            case PlayerActionSelectionMode.MovementArt:
                actionModeText.text = $"{MovementArtDisplayName()}의 착지 칸을 선택하세요";
                actionModeText.color = GaugeFullColor;
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
            actionModeFrameImage.color = color;
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
        if (!IsTraitSelectionOpen
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
