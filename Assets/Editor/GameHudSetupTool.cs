using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameHudSetupTool
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const int CurrentLayoutVersion = 9;
    private const int MinimumPixelFontSize = 16;
    private const string UiFontPath = "Assets/Undead Survivor/Fonts/neodgm.ttf";
    private const string AsepriteSourcePath = "Assets/UI/Icons/yosulbong.aseprite";
    private const string IconSheetPath = "Assets/UI/Icons/yosulbong-Sheet.png";
    private const string BasicAttackIconPath = "Assets/UI/Icons/Actions/BasicAttack.png";
    private const string MoveIconPath = "Assets/UI/Icons/Actions/Move.png";
    private const string TeleportIconPath = "Assets/UI/Icons/Traits/Teleport.png";
    private const string DoubleCastIconPath = "Assets/UI/Icons/Traits/DoubleCast.png";
    private const string PierceIconPath = "Assets/UI/Icons/Traits/Pierce.png";
    private const string DamageBoostIconPath = "Assets/UI/Icons/Traits/DamageBoost.png";
    private const string KnockbackIconPath = "Assets/UI/Icons/Traits/Knockback.png";
    private const string HeartIconPath = "Assets/UI/Icons/Health/heart.aseprite";

    private static readonly Color32 PanelColor = new Color32(12, 18, 34, 238);
    private static readonly Color32 PanelInnerColor = new Color32(18, 28, 50, 250);
    private static readonly Color32 CardColor = new Color32(22, 33, 58, 255);
    private static readonly Color32 BorderColor = new Color32(82, 132, 220, 255);
    private static readonly Color32 AccentColor = new Color32(103, 202, 255, 255);
    private static readonly Color32 GoldColor = new Color32(255, 201, 84, 255);
    private static readonly Color32 MainTextColor = new Color32(235, 244, 255, 255);
    private static readonly Color32 MutedTextColor = new Color32(159, 181, 214, 255);

    private static Font uiFont;
    private static Sprite uiSprite;

    [MenuItem("Tools/UI/특성 및 필살기 HUD 만들기")]
    public static void BuildGameHud()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;

        if (openedForSetup)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            BuildGameHudInScene(scene);
        }
        finally
        {
            if (openedForSetup && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    public static void BuildGameHudInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("SampleScene을 열 수 없습니다.");

        Move playerMovement = FindComponentInScene<Move>(scene);

        if (playerMovement == null)
            throw new InvalidOperationException("SampleScene에서 player의 Move를 찾을 수 없습니다.");

        ConfigureUiFontImporter();
        uiFont = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);

        if (uiFont == null)
            throw new InvalidOperationException($"UI 폰트를 찾을 수 없습니다: {UiFontPath}");

        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        ConfigureUiIconImporters();
        ConfigureHeartIconImporter();
        Sprite heartSprite = LoadHeartSprite();

        GameHudController existingHud = FindComponentInScene<GameHudController>(scene);

        if (existingHud != null)
        {
            // 이 도구로 생성한 HUD만 교체해 현재 레이아웃을 일관되게 적용한다.
            Undo.DestroyObjectImmediate(existingHud.gameObject);
        }

        UltimateGauge ultimateGauge = playerMovement.GetComponent<UltimateGauge>();

        if (ultimateGauge == null)
        {
            ultimateGauge = Undo.AddComponent<UltimateGauge>(playerMovement.gameObject);
        }

        PlayerTraitSystem playerTraitSystem = playerMovement.GetComponent<PlayerTraitSystem>();

        if (playerTraitSystem == null)
        {
            playerTraitSystem = Undo.AddComponent<PlayerTraitSystem>(playerMovement.gameObject);
        }

        CharacterHealth playerHealth = playerMovement.GetComponent<CharacterHealth>();
        CameraController cameraController = FindComponentInScene<CameraController>(scene);
        MonsterSpawner monsterSpawner = FindComponentInScene<MonsterSpawner>(scene);

        if (monsterSpawner == null)
            throw new InvalidOperationException("SampleScene에서 MonsterSpawner를 찾을 수 없습니다.");

        GameObject hudObject = new GameObject(
            "GameHUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(GameHudController)
        );
        Undo.RegisterCreatedObjectUndo(hudObject, "게임 HUD 만들기");
        SceneManager.MoveGameObjectToScene(hudObject, scene);

        Canvas canvas = hudObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = hudObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        // 카메라가 세로 전체를 사용하는 4:5 영역이므로 세로 크기를 기준으로 맞춘다.
        scaler.matchWidthOrHeight = 1f;

        RectTransform viewportRoot = CreateViewportHudRoot(hudObject.transform);

        BuildTimeHud(viewportRoot, out Text elapsedTime);
        BuildWaveHud(viewportRoot, out Text currentWave);
        BuildMinimapPlaceholder(viewportRoot);
        BuildCameraControlHud(viewportRoot, out Image cameraFrame, out Text cameraMode);
        BuildUltimateHud(
            viewportRoot,
            out Image ultimateIcon,
            out Text ultimatePlaceholder,
            out Image gaugeFill,
            out Image ultimateFrame,
            out Image readyGlow,
            out Text gaugeValue,
            out Text ultimateState
        );
        BuildHealthHud(
            viewportRoot,
            heartSprite,
            out Image healthFill,
            out Text healthValue
        );
        BuildTraitHud(
            viewportRoot,
            out Image selectedTraitIcon,
            out Text selectedTraitPlaceholder,
            out Text selectedTraitName
        );
        BuildActionCommandHud(
            viewportRoot,
            out Image actionFrame,
            out Button attackButton,
            out Button moveButton,
            out Image attackCardFrame,
            out Image moveCardFrame,
            out Image attackIcon,
            out Text attackPlaceholder,
            out Image moveIcon,
            out Text movePlaceholder,
            out Text actionMode,
            out Text actionGuide
        );
        BuildTraitSelection(
            viewportRoot,
            out GameObject selectionOverlay,
            out Button[] choiceButtons,
            out Image[] choiceIcons,
            out Text[] choicePlaceholders,
            out Text[] choiceNames,
            out Text[] choiceDescriptions
        );
        BuildGameOverHud(
            viewportRoot,
            out GameObject gameOverOverlay,
            out Text gameOverSummary,
            out Button replayButton
        );
        BuildPauseMenuHud(
            viewportRoot,
            out GameObject pauseMenuOverlay,
            out Button continueButton,
            out Button pauseRestartButton,
            out Button pauseQuitButton
        );

        EnsureEventSystem(scene);
        EnsureDirectionalActionIndicator(scene, playerMovement);

        GameHudController controller = hudObject.GetComponent<GameHudController>();
        ConfigureController(
            controller,
            playerMovement,
            ultimateGauge,
            cameraController,
            playerHealth,
            monsterSpawner,
            playerTraitSystem,
            selectionOverlay,
            choiceButtons,
            choiceIcons,
            choicePlaceholders,
            choiceNames,
            choiceDescriptions,
            selectedTraitIcon,
            selectedTraitPlaceholder,
            selectedTraitName,
            ultimateIcon,
            ultimatePlaceholder,
            gaugeFill,
            ultimateFrame,
            readyGlow,
            gaugeValue,
            ultimateState,
            actionFrame,
            attackButton,
            moveButton,
            attackCardFrame,
            moveCardFrame,
            attackIcon,
            attackPlaceholder,
            moveIcon,
            movePlaceholder,
            actionMode,
            actionGuide,
            cameraFrame,
            cameraMode,
            elapsedTime,
            currentWave,
            healthFill,
            healthValue,
            gameOverOverlay,
            gameOverSummary,
            replayButton,
            pauseMenuOverlay,
            continueButton,
            pauseRestartButton,
            pauseQuitButton
        );

        selectionOverlay.transform.SetAsLastSibling();
        ApplyUiFontToScene(scene);
        EditorUtility.SetDirty(playerMovement);
        EditorUtility.SetDirty(ultimateGauge);
        EditorUtility.SetDirty(playerTraitSystem);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("GAME_HUD_SETUP_COMPLETE", controller);
    }

    private static RectTransform CreateViewportHudRoot(Transform canvas)
    {
        GameObject rootObject = new GameObject("ViewportHudRoot", typeof(RectTransform));
        rootObject.transform.SetParent(canvas, false);

        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(864f, 1080f);
        return root;
    }

    private static void BuildTimeHud(Transform root, out Text elapsedTime)
    {
        Image frame = CreatePanelFrame(
            "TimeHud",
            root,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -18f),
            new Vector2(174f, 58f),
            BorderColor
        );

        CreateText(
            "TimeLabel",
            frame.transform,
            "TIME",
            13,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            AccentColor,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(12f, 0f),
            new Vector2(58f, 30f)
        );

        elapsedTime = CreateText(
            "ElapsedTime",
            frame.transform,
            "00:00",
            24,
            FontStyle.Bold,
            TextAnchor.MiddleRight,
            MainTextColor,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-12f, 0f),
            new Vector2(100f, 38f)
        );
    }

    private static void BuildWaveHud(Transform root, out Text currentWave)
    {
        Image frame = CreatePanelFrame(
            "WaveHud",
            root,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(200f, -18f),
            new Vector2(150f, 58f),
            GoldColor
        );

        currentWave = CreateCenteredText(
            "CurrentWave",
            frame.transform,
            "WAVE 1",
            20,
            MainTextColor
        );
    }

    private static void BuildMinimapPlaceholder(Transform root)
    {
        Image frame = CreatePanelFrame(
            "MinimapHud",
            root,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-18f, -18f),
            new Vector2(176f, 176f),
            BorderColor
        );

        Image map = CreateImage(
            "MinimapPlaceholder",
            frame.transform,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -8f),
            new Vector2(-18f, -34f),
            new Color32(7, 13, 25, 255)
        );

        for (int i = 1; i < 4; i++)
        {
            float anchor = i * 0.25f;
            CreateImage(
                $"GridVertical_{i}",
                map.transform,
                new Vector2(anchor, 0f),
                new Vector2(anchor, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1f, 0f),
                new Color32(65, 92, 135, 120)
            );
            CreateImage(
                $"GridHorizontal_{i}",
                map.transform,
                new Vector2(0f, anchor),
                new Vector2(1f, anchor),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(0f, 1f),
                new Color32(65, 92, 135, 120)
            );
        }

        Image playerMarker = CreateImage(
            "PlayerMarker",
            map.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(12f, 12f),
            AccentColor
        );
        playerMarker.sprite = uiSprite;

        CreateText(
            "MinimapLabel",
            frame.transform,
            "MINIMAP · 예시",
            12,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MutedTextColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -8f),
            new Vector2(150f, 22f)
        );
    }

    private static void BuildCameraControlHud(
        Transform root,
        out Image cameraFrame,
        out Text cameraMode
    )
    {
        cameraFrame = CreatePanelFrame(
            "CameraControlHud",
            root,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-18f, -202f),
            new Vector2(176f, 54f),
            BorderColor
        );

        Image badge = CreateImage(
            "CameraShortcutBadge",
            cameraFrame.transform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(25f, 0f),
            new Vector2(36f, 36f),
            BorderColor
        );
        CreateCenteredText("CameraShortcutKey", badge.transform, "Y", 18, MainTextColor);

        cameraMode = CreateText(
            "CameraMode",
            cameraFrame.transform,
            "플레이어 고정",
            14,
            FontStyle.Bold,
            TextAnchor.MiddleRight,
            MainTextColor,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-10f, 0f),
            new Vector2(126f, 34f)
        );
    }

    private static void BuildUltimateHud(
        Transform root,
        out Image ultimateIcon,
        out Text ultimatePlaceholder,
        out Image gaugeFill,
        out Image ultimateFrame,
        out Image readyGlow,
        out Text gaugeValue,
        out Text ultimateState
    )
    {
        readyGlow = CreateImage(
            "UltimateReadyGlow",
            root,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(14f, 14f),
            new Vector2(104f, 104f),
            new Color32(255, 205, 75, 70)
        );
        readyGlow.gameObject.SetActive(false);

        ultimateFrame = CreatePanelFrame(
            "UltimateFrame",
            root,
            Vector2.zero,
            Vector2.zero,
            new Vector2(18f, 18f),
            new Vector2(96f, 96f),
            BorderColor
        );

        Image iconBackground = CreateImage(
            "UltimateIconFrame",
            ultimateFrame.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -8f),
            new Vector2(68f, 68f),
            new Color32(35, 30, 44, 255)
        );
        AddOutline(iconBackground, GoldColor, 1f);
        ultimateIcon = CreateStretchImage("UltimateIcon", iconBackground.transform, 5f, Color.clear);
        ultimatePlaceholder = CreateCenteredText(
            "UltimatePlaceholder",
            iconBackground.transform,
            "ULT",
            20,
            GoldColor
        );

        Image gaugeBackground = CreateImage(
            "GaugeBackground",
            ultimateFrame.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 7f),
            new Vector2(78f, 10f),
            new Color32(7, 13, 25, 255)
        );
        gaugeFill = CreateStretchImage("GaugeFill", gaugeBackground.transform, 1f, BorderColor);
        gaugeFill.sprite = uiSprite;
        gaugeFill.type = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        gaugeFill.fillOrigin = 0;
        gaugeFill.fillAmount = 0f;

        gaugeValue = CreateText(
            "GaugeValue",
            iconBackground.transform,
            "0%",
            11,
            FontStyle.Bold,
            TextAnchor.LowerRight,
            MainTextColor,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            new Vector2(-3f, 3f),
            new Vector2(-6f, -6f)
        );
        ultimateState = CreateCenteredText(
            "UltimateState",
            ultimateFrame.transform,
            string.Empty,
            1,
            Color.clear
        );
        ultimateState.gameObject.SetActive(false);
    }

    private static void BuildHealthHud(
        Transform root,
        Sprite heartSprite,
        out Image healthFill,
        out Text healthValue
    )
    {
        Image frame = CreatePanelFrame(
            "HealthHud",
            root,
            Vector2.zero,
            Vector2.zero,
            new Vector2(122f, 18f),
            new Vector2(320f, 64f),
            new Color32(77, 189, 121, 255)
        );

        Image heartIcon = CreateImage(
            "HeartIcon",
            frame.transform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(22f, 0f),
            new Vector2(28f, 28f),
            Color.white
        );
        heartIcon.sprite = heartSprite;
        heartIcon.preserveAspect = true;

        CreateText(
            "HealthLabel",
            frame.transform,
            "HP",
            16,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            new Color32(103, 232, 151, 255),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(44f, 0f),
            new Vector2(36f, 28f)
        );

        healthValue = CreateText(
            "HealthValue",
            frame.transform,
            "--/--",
            16,
            FontStyle.Normal,
            TextAnchor.MiddleRight,
            MainTextColor,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-12f, 0f),
            new Vector2(64f, 28f)
        );

        Image barBackground = CreateImage(
            "HealthBarBackground",
            frame.transform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(86f, 0f),
            new Vector2(150f, 24f),
            new Color32(7, 13, 25, 255)
        );
        healthFill = CreateStretchImage(
            "HealthFill",
            barBackground.transform,
            3f,
            new Color32(86, 221, 137, 255)
        );
        healthFill.sprite = uiSprite;
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = 0;
        healthFill.fillAmount = 1f;
    }

    private static void BuildTraitHud(
        Transform root,
        out Image selectedIcon,
        out Text selectedPlaceholder,
        out Text selectedName
    )
    {
        Image frame = CreatePanelFrame(
            "TraitHud",
            root,
            Vector2.zero,
            Vector2.zero,
            new Vector2(450f, 18f),
            new Vector2(92f, 96f),
            BorderColor
        );

        CreateText(
            "TraitHeader",
            frame.transform,
            "TRAIT",
            10,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            AccentColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -4f),
            new Vector2(72f, 18f)
        );

        Image iconBackground = CreateImage(
            "SelectedTraitIconFrame",
            frame.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -20f),
            new Vector2(58f, 58f),
            new Color32(9, 18, 34, 255)
        );
        selectedIcon = CreateStretchImage("SelectedTraitIcon", iconBackground.transform, 4f, Color.clear);
        selectedPlaceholder = CreateCenteredText(
            "SelectedTraitPlaceholder",
            iconBackground.transform,
            "?",
            24,
            AccentColor
        );
        selectedName = CreateText(
            "SelectedTraitName",
            frame.transform,
            "선택 대기",
            10,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MainTextColor,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 4f),
            new Vector2(84f, 18f)
        );
    }

    private static void BuildActionCommandHud(
        Transform root,
        out Image actionFrame,
        out Button attackButton,
        out Button moveButton,
        out Image attackCardFrame,
        out Image moveCardFrame,
        out Image attackIcon,
        out Text attackPlaceholder,
        out Image moveIcon,
        out Text movePlaceholder,
        out Text actionMode,
        out Text actionGuide
    )
    {
        attackButton = BuildActionCard(
            root,
            "AttackAction",
            new Vector2(550f, 18f),
            "A",
            "기본공격",
            "ATK",
            out attackCardFrame,
            out attackIcon,
            out attackPlaceholder
        );
        moveButton = BuildActionCard(
            root,
            "MoveAction",
            new Vector2(650f, 18f),
            "S",
            "이동",
            "MOVE",
            out moveCardFrame,
            out moveIcon,
            out movePlaceholder
        );

        actionFrame = CreatePanelFrame(
            "ActionModeHud",
            root,
            Vector2.zero,
            Vector2.zero,
            new Vector2(550f, 122f),
            new Vector2(196f, 30f),
            BorderColor
        );
        actionMode = CreateCenteredText(
            "ActionMode",
            actionFrame.transform,
            "행동을 선택하세요",
            12,
            MainTextColor
        );
        actionGuide = CreateCenteredText(
            "ActionGuide",
            actionFrame.transform,
            string.Empty,
            1,
            Color.clear
        );
        actionGuide.gameObject.SetActive(false);
    }

    private static Button BuildActionCard(
        Transform root,
        string name,
        Vector2 anchoredPosition,
        string shortcut,
        string label,
        string placeholder,
        out Image cardFrame,
        out Image icon,
        out Text placeholderText
    )
    {
        cardFrame = CreatePanelFrame(
            $"{name}Frame",
            root,
            Vector2.zero,
            Vector2.zero,
            anchoredPosition,
            new Vector2(92f, 96f),
            BorderColor
        );
        cardFrame.raycastTarget = true;

        Button button = cardFrame.gameObject.AddComponent<Button>();
        button.targetGraphic = cardFrame;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(210, 235, 255, 255);
        colors.pressedColor = new Color32(150, 201, 244, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Image iconBackground = CreateImage(
            "IconBackground",
            cardFrame.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -12f),
            new Vector2(62f, 62f),
            new Color32(9, 18, 34, 255)
        );
        icon = CreateStretchImage("ActionIcon", iconBackground.transform, 4f, Color.clear);
        placeholderText = CreateCenteredText(
            "ActionIconPlaceholder",
            iconBackground.transform,
            placeholder,
            placeholder.Length > 3 ? 16 : 20,
            AccentColor
        );

        Image shortcutBadge = CreateImage(
            "ShortcutBadge",
            cardFrame.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(15f, -15f),
            new Vector2(28f, 28f),
            BorderColor
        );
        CreateCenteredText("ShortcutKey", shortcutBadge.transform, shortcut, 14, MainTextColor);

        CreateText(
            "ActionLabel",
            cardFrame.transform,
            label,
            11,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MainTextColor,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 4f),
            new Vector2(84f, 18f)
        );
        return button;
    }

    private static void BuildTraitSelection(
        Transform root,
        out GameObject overlay,
        out Button[] buttons,
        out Image[] icons,
        out Text[] placeholders,
        out Text[] names,
        out Text[] descriptions
    )
    {
        Image overlayImage = CreateImage(
            "TraitSelectionOverlay",
            root,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color32(3, 7, 16, 224)
        );
        overlay = overlayImage.gameObject;
        overlayImage.raycastTarget = true;

        Image panel = CreateImage(
            "SelectionPanel",
            overlay.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(804f, 760f),
            PanelColor
        );
        AddOutline(panel, BorderColor, 3f);

        CreateText(
            "SelectionEyebrow",
            panel.transform,
            "CHOOSE YOUR TRAIT",
            13,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            AccentColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -22f),
            new Vector2(400f, 22f)
        );
        CreateText(
            "SelectionTitle",
            panel.transform,
            "시작 특성을 선택하세요",
            28,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MainTextColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -52f),
            new Vector2(600f, 44f)
        );
        CreateText(
            "SelectionSubtitle",
            panel.transform,
            "이번 생존 동안 사용할 능력 하나를 고릅니다",
            14,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            MutedTextColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -92f),
            new Vector2(640f, 26f)
        );

        buttons = new Button[4];
        icons = new Image[4];
        placeholders = new Text[4];
        names = new Text[4];
        descriptions = new Text[4];

        string[] defaultNames = { "더블 캐스트", "관통", "데미지 강화", "넉백" };
        string[] defaultDescriptions =
        {
            "20% · 기본 공격을 한 번 더\n연속으로 시전",
            "33% · 첫 대상을 넘어\n다음 대상 1명까지 관통",
            "25% · 이번 기본공격의\n피해량을 1 증가",
            "25% · 살아남은 적을\n한 타일 밀어냄"
        };
        string[] defaultPlaceholders = { "×2", "P", "DMG", "KB" };

        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -180f : 180f;
            float y = i < 2 ? 102f : -164f;
            BuildTraitChoiceCard(
                panel.transform,
                i,
                new Vector2(x, y),
                defaultNames[i],
                defaultDescriptions[i],
                defaultPlaceholders[i],
                out buttons[i],
                out icons[i],
                out placeholders[i],
                out names[i],
                out descriptions[i]
            );
        }

        CreateText(
            "SelectionFooter",
            panel.transform,
            "카드를 클릭하거나 숫자 키  1 · 2 · 3 · 4  로 선택",
            14,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color32(194, 211, 235, 255),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 20f),
            new Vector2(650f, 28f)
        );
    }

    private static void BuildTraitChoiceCard(
        Transform parent,
        int index,
        Vector2 anchoredPosition,
        string displayName,
        string description,
        string placeholder,
        out Button button,
        out Image icon,
        out Text placeholderText,
        out Text nameText,
        out Text descriptionText
    )
    {
        Image card = CreateImage(
            $"TraitCard_{index + 1}",
            parent,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            new Vector2(340f, 244f),
            CardColor
        );
        AddOutline(card, new Color32(62, 91, 140, 255), 2f);
        card.raycastTarget = true;

        button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(200, 229, 255, 255);
        colors.pressedColor = new Color32(145, 195, 242, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Image numberBadge = CreateImage(
            "ShortcutBadge",
            card.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(24f, -24f),
            new Vector2(36f, 36f),
            BorderColor
        );
        CreateCenteredText(
            "ShortcutNumber",
            numberBadge.transform,
            (index + 1).ToString(),
            17,
            MainTextColor
        );

        Image iconFrame = CreateImage(
            "TraitIconFrame",
            card.transform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(72f, 0f),
            new Vector2(104f, 104f),
            BorderColor
        );
        Image iconBackground = CreateStretchImage(
            "IconBackground",
            iconFrame.transform,
            4f,
            PanelInnerColor
        );
        icon = CreateStretchImage("TraitIcon", iconBackground.transform, 7f, Color.clear);
        placeholderText = CreateCenteredText(
            "IconPlaceholder",
            iconBackground.transform,
            placeholder,
            placeholder.Length > 2 ? 22 : 28,
            AccentColor
        );

        nameText = CreateText(
            "TraitName",
            card.transform,
            displayName,
            21,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            MainTextColor,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(138f, 36f),
            new Vector2(184f, 36f)
        );
        descriptionText = CreateText(
            "TraitDescription",
            card.transform,
            description,
            14,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            MutedTextColor,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(138f, -10f),
            new Vector2(186f, 78f)
        );
    }

    private static void BuildGameOverHud(
        Transform root,
        out GameObject overlay,
        out Text summary,
        out Button replayButton
    )
    {
        Image overlayImage = CreateImage(
            "GameOverOverlay",
            root,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color32(3, 7, 16, 232)
        );
        overlayImage.raycastTarget = true;
        overlay = overlayImage.gameObject;

        Image panel = CreatePanelFrame(
            "GameOverPanel",
            overlay.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(600f, 356f),
            new Color32(236, 77, 88, 255)
        );

        CreateText(
            "GameOverEyebrow",
            panel.transform,
            "RUN ENDED",
            14,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color32(255, 122, 130, 255),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -28f),
            new Vector2(300f, 24f)
        );
        CreateText(
            "GameOverTitle",
            panel.transform,
            "GAME OVER",
            42,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MainTextColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -58f),
            new Vector2(500f, 62f)
        );
        summary = CreateText(
            "GameOverSummary",
            panel.transform,
            "WAVE 1\n생존 시간 00:00",
            20,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MutedTextColor,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 20f),
            new Vector2(420f, 70f)
        );

        Image replayFrame = CreatePanelFrame(
            "ReplayButton",
            panel.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 66f),
            new Vector2(232f, 64f),
            AccentColor
        );
        replayFrame.raycastTarget = true;
        replayButton = replayFrame.gameObject.AddComponent<Button>();
        replayButton.targetGraphic = replayFrame;
        replayButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = replayButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(210, 235, 255, 255);
        colors.pressedColor = new Color32(150, 201, 244, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        replayButton.colors = colors;
        CreateCenteredText("ReplayLabel", replayFrame.transform, "REPLAY", 24, MainTextColor);

        CreateText(
            "QuitHint",
            panel.transform,
            "ESC 전장 확인  ·  REPLAY 버튼으로 다시 도전",
            14,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MutedTextColor,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 22f),
            new Vector2(460f, 28f)
        );

        overlay.SetActive(false);
    }

    private static void BuildPauseMenuHud(
        Transform root,
        out GameObject overlay,
        out Button continueButton,
        out Button restartButton,
        out Button quitButton
    )
    {
        Image overlayImage = CreateImage(
            "PauseMenuOverlay",
            root,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color32(3, 7, 16, 220)
        );
        overlayImage.raycastTarget = true;
        overlay = overlayImage.gameObject;

        Image panel = CreatePanelFrame(
            "PauseMenuPanel",
            overlay.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(600f, 500f),
            BorderColor
        );

        CreateText(
            "PauseEyebrow",
            panel.transform,
            "GAME MENU",
            14,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            AccentColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -30f),
            new Vector2(300f, 24f)
        );
        CreateText(
            "PauseTitle",
            panel.transform,
            "일시정지",
            38,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MainTextColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -62f),
            new Vector2(420f, 58f)
        );

        continueButton = BuildPauseMenuButton(
            panel.transform,
            "ContinueButton",
            "계속 진행",
            250f,
            AccentColor
        );
        restartButton = BuildPauseMenuButton(
            panel.transform,
            "PauseRestartButton",
            "다시하기",
            166f,
            GoldColor
        );
        quitButton = BuildPauseMenuButton(
            panel.transform,
            "PauseQuitButton",
            "게임 종료",
            82f,
            new Color32(236, 77, 88, 255)
        );

        CreateText(
            "PauseHint",
            panel.transform,
            "ESC를 한 번 더 누르면 계속 진행",
            13,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            MutedTextColor,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 24f),
            new Vector2(420f, 26f)
        );

        overlay.SetActive(false);
    }

    private static Button BuildPauseMenuButton(
        Transform parent,
        string name,
        string label,
        float bottomPosition,
        Color borderColor
    )
    {
        Image frame = CreatePanelFrame(
            name,
            parent,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, bottomPosition),
            new Vector2(288f, 64f),
            borderColor
        );
        frame.raycastTarget = true;

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(210, 235, 255, 255);
        colors.pressedColor = new Color32(150, 201, 244, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        CreateCenteredText($"{name}Label", frame.transform, label, 22, MainTextColor);
        return button;
    }

    private static void ConfigureController(
        GameHudController controller,
        Move playerMovement,
        UltimateGauge ultimateGauge,
        CameraController cameraController,
        CharacterHealth playerHealth,
        MonsterSpawner monsterSpawner,
        PlayerTraitSystem playerTraitSystem,
        GameObject selectionOverlay,
        Button[] choiceButtons,
        Image[] choiceIcons,
        Text[] choicePlaceholders,
        Text[] choiceNames,
        Text[] choiceDescriptions,
        Image selectedTraitIcon,
        Text selectedTraitPlaceholder,
        Text selectedTraitName,
        Image ultimateIcon,
        Text ultimatePlaceholder,
        Image gaugeFill,
        Image ultimateFrame,
        Image readyGlow,
        Text gaugeValue,
        Text ultimateState,
        Image actionFrame,
        Button attackButton,
        Button moveButton,
        Image attackCardFrame,
        Image moveCardFrame,
        Image attackIcon,
        Text attackPlaceholder,
        Image moveIcon,
        Text movePlaceholder,
        Text actionMode,
        Text actionGuide,
        Image cameraFrame,
        Text cameraMode,
        Text elapsedTime,
        Text currentWave,
        Image healthFill,
        Text healthValue,
        GameObject gameOverOverlay,
        Text gameOverSummary,
        Button replayButton,
        GameObject pauseMenuOverlay,
        Button continueButton,
        Button pauseRestartButton,
        Button pauseQuitButton
    )
    {
        Sprite basicAttackSprite = LoadIcon(BasicAttackIconPath);
        Sprite moveSprite = LoadIcon(MoveIconPath);
        Sprite ultimateSprite = LoadIcon(TeleportIconPath);
        Sprite[] traitSprites =
        {
            LoadIcon(DoubleCastIconPath),
            LoadIcon(PierceIconPath),
            LoadIcon(DamageBoostIconPath),
            LoadIcon(KnockbackIconPath)
        };

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("uiFont").objectReferenceValue = uiFont;
        serializedController.FindProperty("playerMovement").objectReferenceValue = playerMovement;
        serializedController.FindProperty("ultimateGauge").objectReferenceValue = ultimateGauge;
        serializedController.FindProperty("cameraController").objectReferenceValue = cameraController;
        serializedController.FindProperty("playerHealth").objectReferenceValue = playerHealth;
        serializedController.FindProperty("monsterSpawner").objectReferenceValue = monsterSpawner;
        serializedController.FindProperty("playerTraitSystem").objectReferenceValue =
            playerTraitSystem;
        serializedController.FindProperty("basicAttackIcon").objectReferenceValue = basicAttackSprite;
        serializedController.FindProperty("moveIcon").objectReferenceValue = moveSprite;
        serializedController.FindProperty("ultimateIcon").objectReferenceValue = ultimateSprite;
        serializedController.FindProperty("layoutVersion").intValue = CurrentLayoutVersion;
        serializedController.FindProperty("selectionOverlay").objectReferenceValue = selectionOverlay;
        serializedController.FindProperty("selectedTraitIconImage").objectReferenceValue = selectedTraitIcon;
        serializedController.FindProperty("selectedTraitPlaceholderText").objectReferenceValue = selectedTraitPlaceholder;
        serializedController.FindProperty("selectedTraitNameText").objectReferenceValue = selectedTraitName;
        serializedController.FindProperty("ultimateIconImage").objectReferenceValue = ultimateIcon;
        serializedController.FindProperty("ultimatePlaceholderText").objectReferenceValue = ultimatePlaceholder;
        serializedController.FindProperty("ultimateGaugeFillImage").objectReferenceValue = gaugeFill;
        serializedController.FindProperty("ultimateFrameImage").objectReferenceValue = ultimateFrame;
        serializedController.FindProperty("ultimateReadyGlowImage").objectReferenceValue = readyGlow;
        serializedController.FindProperty("ultimateGaugeValueText").objectReferenceValue = gaugeValue;
        serializedController.FindProperty("ultimateStateText").objectReferenceValue = ultimateState;
        serializedController.FindProperty("actionModeFrameImage").objectReferenceValue = actionFrame;
        serializedController.FindProperty("attackActionButton").objectReferenceValue = attackButton;
        serializedController.FindProperty("moveActionButton").objectReferenceValue = moveButton;
        serializedController.FindProperty("attackActionFrameImage").objectReferenceValue = attackCardFrame;
        serializedController.FindProperty("moveActionFrameImage").objectReferenceValue = moveCardFrame;
        serializedController.FindProperty("attackActionIconImage").objectReferenceValue = attackIcon;
        serializedController.FindProperty("attackActionPlaceholderText").objectReferenceValue = attackPlaceholder;
        serializedController.FindProperty("moveActionIconImage").objectReferenceValue = moveIcon;
        serializedController.FindProperty("moveActionPlaceholderText").objectReferenceValue = movePlaceholder;
        serializedController.FindProperty("actionModeText").objectReferenceValue = actionMode;
        serializedController.FindProperty("actionGuideText").objectReferenceValue = actionGuide;
        serializedController.FindProperty("cameraModeFrameImage").objectReferenceValue = cameraFrame;
        serializedController.FindProperty("cameraModeText").objectReferenceValue = cameraMode;
        serializedController.FindProperty("elapsedTimeText").objectReferenceValue = elapsedTime;
        serializedController.FindProperty("currentWaveText").objectReferenceValue = currentWave;
        serializedController.FindProperty("healthFillImage").objectReferenceValue = healthFill;
        serializedController.FindProperty("healthValueText").objectReferenceValue = healthValue;
        serializedController.FindProperty("gameOverOverlay").objectReferenceValue = gameOverOverlay;
        serializedController.FindProperty("gameOverSummaryText").objectReferenceValue = gameOverSummary;
        serializedController.FindProperty("replayButton").objectReferenceValue = replayButton;
        serializedController.FindProperty("pauseMenuOverlay").objectReferenceValue = pauseMenuOverlay;
        serializedController.FindProperty("continueButton").objectReferenceValue = continueButton;
        serializedController.FindProperty("pauseRestartButton").objectReferenceValue = pauseRestartButton;
        serializedController.FindProperty("pauseQuitButton").objectReferenceValue = pauseQuitButton;

        SetObjectArray(serializedController.FindProperty("traitChoiceButtons"), choiceButtons);
        SetObjectArray(serializedController.FindProperty("traitChoiceIconImages"), choiceIcons);
        SetObjectArray(serializedController.FindProperty("traitChoicePlaceholderTexts"), choicePlaceholders);
        SetObjectArray(serializedController.FindProperty("traitChoiceNameTexts"), choiceNames);
        SetObjectArray(serializedController.FindProperty("traitChoiceDescriptionTexts"), choiceDescriptions);

        SerializedProperty traits = serializedController.FindProperty("traitOptions");
        traits.arraySize = 4;
        SetTrait(traits.GetArrayElementAtIndex(0), "double_cast", "더블 캐스트", "20% · 기본 공격을 한 번 더\n연속으로 시전", "×2", traitSprites[0], 5);
        SetTrait(traits.GetArrayElementAtIndex(1), "pierce", "관통", "33% · 첫 대상을 넘어\n다음 대상 1명까지 관통", "P", traitSprites[1], 3);
        SetTrait(traits.GetArrayElementAtIndex(2), "damage_boost", "데미지 강화", "25% · 이번 기본공격의\n피해량을 1 증가", "DMG", traitSprites[2], 4);
        SetTrait(traits.GetArrayElementAtIndex(3), "knockback", "넉백", "25% · 살아남은 적을\n한 타일 밀어냄", "KB", traitSprites[3], 4);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        AssignPreviewIcon(attackIcon, attackPlaceholder, basicAttackSprite);
        AssignPreviewIcon(moveIcon, movePlaceholder, moveSprite);
        AssignPreviewIcon(ultimateIcon, ultimatePlaceholder, ultimateSprite);

        for (int i = 0; i < traitSprites.Length; i++)
        {
            AssignPreviewIcon(choiceIcons[i], choicePlaceholders[i], traitSprites[i]);
        }
    }

    private static void EnsureDirectionalActionIndicator(Scene scene, Move playerMovement)
    {
        DirectionalActionIndicator indicator = playerMovement.GetComponent<DirectionalActionIndicator>();

        if (indicator == null)
        {
            indicator = Undo.AddComponent<DirectionalActionIndicator>(playerMovement.gameObject);
        }

        Camera worldCamera = FindComponentInScene<Camera>(scene);
        SerializedObject serializedMovement = new SerializedObject(playerMovement);
        serializedMovement.FindProperty("actionIndicator").objectReferenceValue = indicator;
        serializedMovement.FindProperty("worldCamera").objectReferenceValue = worldCamera;
        serializedMovement.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(indicator);
    }

    private static void SetTrait(
        SerializedProperty trait,
        string id,
        string displayName,
        string description,
        string placeholder,
        Sprite icon,
        int activationBagSize
    )
    {
        trait.FindPropertyRelative("id").stringValue = id;
        trait.FindPropertyRelative("displayName").stringValue = displayName;
        trait.FindPropertyRelative("description").stringValue = description;
        trait.FindPropertyRelative("placeholder").stringValue = placeholder;
        trait.FindPropertyRelative("icon").objectReferenceValue = icon;
        trait.FindPropertyRelative("activationBagSize").intValue =
            Mathf.Max(1, activationBagSize);
    }

    private static void ConfigureHeartIconImporter()
    {
        AssetDatabase.ImportAsset(
            HeartIconPath,
            ImportAssetOptions.ForceSynchronousImport
        );

        if (AssetImporter.GetAtPath(HeartIconPath) is not AsepriteImporter importer)
            throw new InvalidOperationException(
                $"하트 Aseprite 에셋을 불러올 수 없습니다: {HeartIconPath}"
            );

        bool changed = importer.importMode != FileImportModes.AnimatedSprite
            || importer.layerImportMode != LayerImportModes.MergeFrame
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 32f)
            || importer.spriteMeshType != SpriteMeshType.FullRect
            || importer.pivotAlignment != SpriteAlignment.Center
            || importer.includeHiddenLayers
            || importer.filterMode != FilterMode.Point
            || importer.mipmapEnabled
            || importer.wrapMode != TextureWrapMode.Clamp
            || importer.generateAnimationClips
            || importer.generateModelPrefab
            || importer.generatePhysicsShape;

        if (!changed)
            return;

        importer.importMode = FileImportModes.AnimatedSprite;
        importer.layerImportMode = LayerImportModes.MergeFrame;
        importer.spritePixelsPerUnit = 32f;
        importer.spriteMeshType = SpriteMeshType.FullRect;
        importer.pivotAlignment = SpriteAlignment.Center;
        importer.includeHiddenLayers = false;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.generateAnimationClips = false;
        importer.generateModelPrefab = false;
        importer.generatePhysicsShape = false;
        importer.SaveAndReimport();
    }

    private static Sprite LoadHeartSprite()
    {
        Sprite heartSprite = AssetDatabase.LoadAllAssetsAtPath(HeartIconPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (heartSprite == null)
            throw new InvalidOperationException("heart.aseprite에서 Sprite를 찾을 수 없습니다.");

        return heartSprite;
    }

    private static void ConfigureUiIconImporters()
    {
        string[] texturePaths =
        {
            IconSheetPath,
            BasicAttackIconPath,
            MoveIconPath,
            TeleportIconPath,
            DoubleCastIconPath,
            PierceIconPath,
            DamageBoostIconPath,
            KnockbackIconPath
        };

        foreach (string path in texturePaths)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException($"UI 아이콘을 불러올 수 없습니다: {path}");

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(importer.spritePixelsPerUnit, 32f)
                || importer.filterMode != FilterMode.Point
                || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Clamp
                || !importer.alphaIsTransparency;

            if (!changed)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        AssetDatabase.ImportAsset(AsepriteSourcePath, ImportAssetOptions.ForceSynchronousImport);

        if (AssetImporter.GetAtPath(AsepriteSourcePath) is AsepriteImporter asepriteImporter)
        {
            bool changed = !Mathf.Approximately(asepriteImporter.spritePixelsPerUnit, 32f)
                || asepriteImporter.filterMode != FilterMode.Point
                || asepriteImporter.mipmapEnabled
                || asepriteImporter.generateModelPrefab
                || asepriteImporter.generateAnimationClips
                || asepriteImporter.generatePhysicsShape;

            if (changed)
            {
                asepriteImporter.textureType = TextureImporterType.Sprite;
                asepriteImporter.spritePixelsPerUnit = 32f;
                asepriteImporter.filterMode = FilterMode.Point;
                asepriteImporter.mipmapEnabled = false;
                asepriteImporter.wrapMode = TextureWrapMode.Clamp;
                asepriteImporter.generateModelPrefab = false;
                asepriteImporter.generateAnimationClips = false;
                asepriteImporter.generatePhysicsShape = false;
                asepriteImporter.SaveAndReimport();
            }
        }
    }

    private static void ConfigureUiFontImporter()
    {
        AssetDatabase.ImportAsset(UiFontPath, ImportAssetOptions.ForceSynchronousImport);

        if (AssetImporter.GetAtPath(UiFontPath) is not TrueTypeFontImporter importer)
            throw new InvalidOperationException($"UI 폰트를 불러올 수 없습니다: {UiFontPath}");

        bool changed = importer.fontRenderingMode != FontRenderingMode.HintedRaster
            || importer.fontSize != MinimumPixelFontSize
            || importer.characterPadding != 1
            || !importer.shouldRoundAdvanceValue;

        if (!changed)
            return;

        // 안티앨리어싱 없이 힌팅된 픽셀 경계로 렌더링해 작은 한글도 선명하게 유지한다.
        importer.fontRenderingMode = FontRenderingMode.HintedRaster;
        importer.fontSize = MinimumPixelFontSize;
        importer.characterPadding = 1;
        importer.shouldRoundAdvanceValue = true;
        importer.SaveAndReimport();
    }

    private static Sprite LoadIcon(string path)
    {
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (icon == null)
            throw new InvalidOperationException($"Sprite 아이콘을 불러올 수 없습니다: {path}");

        return icon;
    }

    private static void AssignPreviewIcon(Image image, Text placeholder, Sprite sprite)
    {
        if (image != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        if (placeholder != null)
        {
            placeholder.gameObject.SetActive(false);
        }
    }

    private static void SetObjectArray<T>(SerializedProperty property, T[] values)
        where T : UnityEngine.Object
    {
        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void EnsureEventSystem(Scene scene)
    {
        EventSystem eventSystem = FindComponentInScene<EventSystem>(scene);

        if (eventSystem != null)
            return;

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule)
        );
        Undo.RegisterCreatedObjectUndo(eventSystemObject, "EventSystem 만들기");
        SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
    }

    private static Image CreatePanelFrame(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Color borderColor
    )
    {
        Image frame = CreateImage(
            name,
            parent,
            anchor,
            anchor,
            pivot,
            anchoredPosition,
            size,
            PanelColor
        );
        AddOutline(frame, borderColor, 2f);
        return frame;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color
    )
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        gameObject.transform.SetParent(parent, false);

        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;

        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateStretchImage(
        string name,
        Transform parent,
        float inset,
        Color color
    )
    {
        return CreateImage(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-inset * 2f, -inset * 2f),
            color
        );
    }

    private static Text CreateCenteredText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        Color color
    )
    {
        return CreateText(
            name,
            parent,
            value,
            fontSize,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            color,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta
    )
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text)
        );
        gameObject.transform.SetParent(parent, false);

        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;

        Text text = gameObject.GetComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = Mathf.Max(MinimumPixelFontSize, fontSize);
        // Neo둥근모는 Regular 단일 스타일이므로 합성 Bold를 사용하지 않는다.
        text.fontStyle = FontStyle.Normal;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void AddOutline(Graphic graphic, Color color, float distance)
    {
        Outline outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            T component = rootObject.GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        return null;
    }

    private static void ApplyUiFontToScene(Scene scene, bool recordSceneChange = true)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (Text text in rootObject.GetComponentsInChildren<Text>(true))
            {
                if (text.font == uiFont && text.fontStyle == FontStyle.Normal)
                    continue;

                if (recordSceneChange)
                    Undo.RecordObject(text, "UI 폰트 적용");

                text.font = uiFont;
                text.fontStyle = FontStyle.Normal;

                if (recordSceneChange)
                    EditorUtility.SetDirty(text);
            }
        }
    }

    [InitializeOnLoadMethod]
    private static void QueueAutomaticHudUpgrade()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess())
            return;

        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

        if (EditorApplication.isPlaying)
            EditorApplication.delayCall += ApplyUiFontToPlayingScenes;
        else
            EditorApplication.delayCall += TryAutomaticHudUpgrade;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            ApplyUiFontToPlayingScenes();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += TryAutomaticHudUpgrade;
        }
    }

    private static void ApplyUiFontToPlayingScenes()
    {
        if (!EditorApplication.isPlaying)
            return;

        uiFont = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);

        if (uiFont == null)
            return;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (scene.IsValid() && scene.isLoaded)
                ApplyUiFontToScene(scene, false);
        }
    }

    private static void TryAutomaticHudUpgrade()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutomaticHudUpgrade;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);

        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameHudController controller = FindComponentInScene<GameHudController>(scene);

        if (controller != null && controller.LayoutVersion >= CurrentLayoutVersion)
            return;

        try
        {
            BuildGameHudInScene(scene);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
