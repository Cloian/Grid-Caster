using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameHudController : MonoBehaviour
{
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
    [Tooltip("제작한 필살기 아이콘 Sprite를 넣습니다.")]
    [SerializeField] private Sprite ultimateIcon;

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
    [SerializeField, HideInInspector] private Image cameraModeFrameImage;
    [SerializeField, HideInInspector] private Text cameraModeText;
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
    private bool cameraSubscribed;
    private bool healthSubscribed;
    private bool waveSubscribed;
    private bool cameraWasEnabledBeforePause;
    private float elapsedPlayTime;

    public bool IsTraitSelectionOpen => selectionOverlay != null && selectionOverlay.activeSelf;
    public bool IsGameOver => gameOverOverlay != null && gameOverOverlay.activeSelf;
    public bool IsPauseMenuOpen => pauseMenuOverlay != null && pauseMenuOverlay.activeSelf;
    public int LayoutVersion => layoutVersion;
    public string SelectedTraitId => IsValidTraitIndex(selectedTraitIndex)
        ? traitOptions[selectedTraitIndex].Id
        : string.Empty;

    private void Awake()
    {
        ApplyUiFont();
        ResolveGameplayReferences();
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
            // Neo둥근모는 Regular 단일 스타일이므로 합성 Bold를 끄고 원본 픽셀 형태를 사용한다.
            text.fontStyle = FontStyle.Normal;
        }
    }

    private void OnEnable()
    {
        ResolveGameplayReferences();
        SubscribeGauge();
        SubscribePlayerMovement();
        SubscribeCamera();
        SubscribeHealth();
        SubscribeWave();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        RefreshGauge();
        RefreshActionMode(playerMovement != null
            ? playerMovement.SelectionMode
            : PlayerActionSelectionMode.None);
        RefreshCameraMode(cameraController == null || cameraController.IsFollowingPlayer);
        RefreshElapsedTime();
        RefreshWave();
        RefreshHealth();

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

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePauseMenu();
            return;
        }

        if (!IsPauseMenuOpen)
        {
            HandleTraitShortcutInput();
        }

        AnimateReadyGlow();
        UpdateElapsedTime();
        RefreshHealth();
    }

    private void OnDisable()
    {
        UnsubscribeGauge();
        UnsubscribePlayerMovement();
        UnsubscribeCamera();
        UnsubscribeHealth();
        UnsubscribeWave();

        if (IsPauseMenuOpen)
        {
            Time.timeScale = 1f;

            if (cameraController != null)
            {
                cameraController.enabled = cameraWasEnabledBeforePause;
            }
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
                traitChoiceDescriptionTexts[i].text = traitOptions[i].Description;
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

    public void TogglePauseMenu()
    {
        if (IsGameOver || pauseMenuOverlay == null)
            return;

        SetPauseMenuOpen(!IsPauseMenuOpen);
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
            ultimateIcon,
            "U"
        );
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

    private void SubscribeCamera()
    {
        if (cameraSubscribed || cameraController == null)
            return;

        cameraController.FollowModeChanged += RefreshCameraMode;
        cameraSubscribed = true;
    }

    private void UnsubscribeCamera()
    {
        if (!cameraSubscribed || cameraController == null)
            return;

        cameraController.FollowModeChanged -= RefreshCameraMode;
        cameraSubscribed = false;
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
        playerMovement?.SetInputEnabled(false);

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

        if (IsPauseMenuOpen)
        {
            SetPauseMenuOpen(false);
        }
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
            actionGuideText.text = "A / S 키 또는 아이콘 클릭  ·  우클릭 취소  ·  Esc 메뉴";
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

    private void RefreshCameraMode(bool isFollowingPlayer)
    {
        if (cameraModeText != null)
        {
            cameraModeText.text = isFollowingPlayer
                ? "플레이어 고정"
                : "자유 시점";
            cameraModeText.color = isFollowingPlayer ? MainTextColor : MoveModeColor;
        }

        if (cameraModeFrameImage != null)
        {
            cameraModeFrameImage.color = isFollowingPlayer
                ? FrameNormalColor
                : MoveModeColor;
        }
    }

    private void UpdateElapsedTime()
    {
        // 시작 특성을 고르는 동안과 사망 후에는 생존 시간이 흐르지 않는다.
        if (!IsTraitSelectionOpen
            && !IsPauseMenuOpen
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
        if (ultimateGauge == null)
            return;

        float normalized = ultimateGauge.NormalizedGauge;
        bool isReady = ultimateGauge.IsReady;

        if (ultimateGaugeFillImage != null)
        {
            ultimateGaugeFillImage.fillAmount = normalized;
            ultimateGaugeFillImage.color = Color.Lerp(
                GaugeEmptyColor,
                GaugeFullColor,
                normalized
            );
        }

        if (ultimateGaugeValueText != null)
        {
            ultimateGaugeValueText.text = isReady
                ? "100%"
                : $"{ultimateGauge.CurrentGauge} / {ultimateGauge.MaxGauge}";
        }

        if (ultimateStateText != null)
        {
            ultimateStateText.text = isReady
                ? "READY · 필살기 사용 가능"
                : $"충전 중 · 이동 +{ultimateGauge.MoveGain} / 적중 +{ultimateGauge.AttackHitGain}";
            ultimateStateText.color = isReady
                ? GaugeFullColor
                : new Color32(166, 190, 224, 255);
        }

        if (ultimateFrameImage != null)
        {
            ultimateFrameImage.color = isReady ? FrameReadyColor : FrameNormalColor;
        }

        if (ultimateReadyGlowImage != null)
        {
            ultimateReadyGlowImage.gameObject.SetActive(isReady);
        }
    }

    private void AnimateReadyGlow()
    {
        if (ultimateGauge == null || !ultimateGauge.IsReady || ultimateReadyGlowImage == null)
            return;

        Color glowColor = ultimateReadyGlowImage.color;
        glowColor.a = Mathf.Lerp(0.12f, 0.42f, (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f);
        ultimateReadyGlowImage.color = glowColor;
    }

    private bool IsValidTraitIndex(int index)
    {
        return traitOptions != null && index >= 0 && index < traitOptions.Length;
    }
}
