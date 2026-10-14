using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Safari 플레이테스트용 화면/입력 어댑터. 전투·성장 판정은 기존 시스템에 위임한다.
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class MobilePlaytestController : MonoBehaviour
{
    public static bool ForceMobileForPlaytest { get; set; }
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int GridCaster_IsMobileBrowser();
    private static bool? mobileBrowser;
#endif

    public static bool UsesMobileLayout(bool mobilePlatform, bool browserMobile, bool forced)
        => mobilePlatform || browserMobile || forced;

    public static bool IsActive
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!mobileBrowser.HasValue) mobileBrowser = GridCaster_IsMobileBrowser() != 0;
            return UsesMobileLayout(Application.isMobilePlatform, mobileBrowser.Value, ForceMobileForPlaytest);
#else
            return UsesMobileLayout(Application.isMobilePlatform, false, ForceMobileForPlaytest);
#endif
        }
    }

    private Move player;
    private GameHudController hud;
    private RunProgressionUiController progressionUi;
    private PlayerTraitSystem trait;
    private CharacterHealth health;
    private EnemyThreatPreview threat;
    private MonsterSpawner spawner;
    private Camera worldCamera;
    private Font font;
    private RectTransform canvasRect;
    private RectTransform controls;
    private RectTransform drawer;
    private RectTransform buildScroll;
    private RectTransform relicScroll;
    private RectTransform top;
    private RectTransform bottom;
    private RectTransform movePreview;
    private UnityEngine.UI.Image previewGhost;
    private SpriteRenderer playerSprite;
    private GridManager grid;
    private readonly RectTransform[] backdrop = new RectTransform[4];
    private UnityEngine.UI.Text summary;
    private UnityEngine.UI.Text healthSummary;
    private UnityEngine.UI.Text hint;
    private UnityEngine.UI.Button moveButton;
    private UnityEngine.UI.Button attackButton;
    private UnityEngine.UI.Button specialButton;
    private UnityEngine.UI.Button cancelButton;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private Vector2Int lastSize;
    private Rect lastSafe;
    private Vector3Int pendingCell;
    private Vector3Int pendingOrigin;
    private PlayerActionSelectionMode pendingMode;
    private bool pendingTrait;
    private bool hasPreview;
    private bool pointerStartedOnBoard;
    private bool pointerTracking;
    private int activeTouchId;
    private int blockedPointerFrame = -1;
    private Vector2 pointerStart;
    private int completedWorldTurns;

    public bool HasPreview => hasPreview;
    public bool IsDrawerOpen => drawer != null && drawer.gameObject.activeSelf;
    public bool IsMovePreviewVisible => movePreview != null && movePreview.gameObject.activeSelf;

    public static Rect BoardPixelRect(int width, int height, Rect safe)
    {
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, width, height);
        float scale = safe.width / 390f;
        // 전투 화면은 보드 + 얇은 상단 정보/하단 한 줄 조작만 예약한다.
        float side = Mathf.Max(1f, Mathf.Min(safe.width - 4f * scale, safe.height - 184f * scale));
        return new Rect(safe.center.x - side * 0.5f,
            safe.center.y - side * 0.5f, side, side);
    }

    private void Awake()
    {
        UnityEngine.UI.CanvasScaler scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.matchWidthOrHeight = 0f;
        }
    }

    private void Start()
    {
        hud = GetComponent<GameHudController>();
        progressionUi = GetComponent<RunProgressionUiController>();
        player = FindAnyObjectByType<Move>();
        worldCamera = Camera.main;
        spawner = FindAnyObjectByType<MonsterSpawner>();
        if (spawner != null) spawner.WorldTurnCompleted += CountWorldTurn;
        canvasRect = GetComponent<RectTransform>();
        if (player == null || hud == null || canvasRect == null) { enabled = false; return; }
        trait = player.GetComponent<PlayerTraitSystem>();
        health = player.GetComponent<CharacterHealth>();
        playerSprite = player.GetComponent<SpriteRenderer>();
        grid = FindAnyObjectByType<GridManager>();
        threat = player.GetComponent<EnemyThreatPreview>();
        foreach (UnityEngine.UI.Text text in GetComponentsInChildren<UnityEngine.UI.Text>(true))
            if (text.font != null) { font = text.font; break; }
        controls = Rect("MobileControls", transform);
        Stretch(controls, 0, 0, 0, 0);
        controls.SetAsFirstSibling();
        // 부분 카메라 뷰포트 밖은 지워지지 않는다. 불투명 UI 여백으로 이전 팝업/리사이즈 잔상을 덮는다.
        // 보드 내부에는 배경을 그리지 않으므로 맵을 가리지 않고, 추가 카메라도 필요 없다.
        for (int i = 0; i < backdrop.Length; i++)
        {
            backdrop[i] = Panel("MobileBackdrop_" + i, controls, false);
            backdrop[i].GetComponent<UnityEngine.UI.Image>().color = new Color32(8, 13, 23, 255);
        }
        top = Rect("Top", controls);
        bottom = Rect("Bottom", controls);
        summary = Text("Summary", top, "", 15);
        Place(summary.rectTransform, 62, 0, 242, 44);
        Button("Build", top, "강화", 0, 0, 56, 44, () => OpenDrawer(false));
        Button("Relic", top, "유물", 310, 0, 56, 44, () => OpenDrawer(true));
        Button("Menu", top, "메뉴", 310, 44, 56, 44, () =>
        {
            blockedPointerFrame = Time.frameCount;
            if (hud.IsRunEnded) hud.ToggleGameOverReview(); else hud.TogglePauseMenu();
        });
        healthSummary = Text("HealthSummary", top, "", 14);
        Place(healthSummary.rectTransform, 0, 44, 304, 40);
        hint = Text("Hint", bottom, "행동 선택 → 칸 탭 → 실행", 14);
        Place(hint.rectTransform, 0, 0, 314, 36);
        moveButton = Button("Move", bottom, "이동", 0, 46, 114, 44, () => ActionButton(false, false));
        attackButton = Button("Attack", bottom, "공격", 126, 46, 114, 44, () => ActionButton(true, false));
        specialButton = Button("Special", bottom, "특수공격", 252, 46, 114, 44, () => ActionButton(true, true));
        cancelButton = Button("Cancel", bottom, "취소", 318, 0, 48, 44, CancelPreview);
        cancelButton.GetComponentInChildren<UnityEngine.UI.Text>().fontSize = 12;
        // 이동 가능 칸과 선택한 착지점은 별도 표현한다. 판정/점유에는 관여하지 않는다.
        movePreview = Panel("SelectedMoveCell", controls, false);
        movePreview.SetSiblingIndex(backdrop.Length);
        movePreview.GetComponent<UnityEngine.UI.Image>().color = new Color32(80, 225, 255, 55);
        var outline = movePreview.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color32(135, 245, 255, 255);
        outline.effectDistance = new Vector2(2, -2);
        RectTransform ghost = Rect("PlayerGhost", movePreview);
        Stretch(ghost, 1, 1, 1, 1);
        previewGhost = ghost.gameObject.AddComponent<UnityEngine.UI.Image>();
        previewGhost.raycastTarget = false; previewGhost.preserveAspect = true;
        previewGhost.color = new Color(1, 1, 1, 0.65f);
        movePreview.gameObject.SetActive(false);
        CreateDrawer();
        foreach (string name in new[] { "TimeHud", "WaveHud", "HealthHud", "TraitHud", "UltimateFrame",
            "AttackActionFrame", "MoveActionFrame", "ActionModeHud" })
        {
            RectTransform old = Find(name);
            if (old != null) old.gameObject.SetActive(false);
        }
        player.SelectionModeChanged += OnSelectionChanged;
        Layout();
    }

    private void OnDestroy()
    {
        if (player != null) player.SelectionModeChanged -= OnSelectionChanged;
        if (spawner != null) spawner.WorldTurnCompleted -= CountWorldTurn;
    }

    private void CountWorldTurn() { completedWorldTurns++; }

    [System.Serializable]
    private class PlaytestState
    {
        public int wave, x, y, hp, worldTurns, charge;
        public bool startSelection, rewardSelection, preview, drawerOpen, canAct, paused;
        public string action;
        public bool movePreviewVisible;
        public int pendingX, pendingY;
        public string caption;
        public float playerScreenX, playerScreenY, tilePixels;
        public int screenWidth, screenHeight;
        public float[] board;
        public UiTarget[] ui;
    }

    [System.Serializable]
    private class UiTarget { public string name; public float x, y, width, height; }

    // 브라우저 검증/버그 보고용 읽기 전용 스냅샷. 게임 상태를 조작하는 테스트 훅은 제공하지 않는다.
    public void ReportPlaytestState()
    {
        if (player == null || hud == null) return;
        var state = new PlaytestState { wave = spawner != null ? spawner.CurrentWave : 0,
            x = player.GridPosition.x, y = player.GridPosition.y, hp = player.CurrentHealth,
            worldTurns = completedWorldTurns, charge = trait != null ? trait.ChargeProgress : 0,
            startSelection = hud.IsTraitSelectionOpen, preview = hasPreview, drawerOpen = IsDrawerOpen,
            canAct = player.CanAct, paused = hud.IsPauseMenuOpen,
            rewardSelection = player.GetComponent<RunProgressionSystem>().CurrentUpgradeOptions.Count > 0,
            action = player.SelectionMode.ToString(), movePreviewVisible = IsMovePreviewVisible,
            pendingX = pendingCell.x, pendingY = pendingCell.y, caption = hint.text,
            screenWidth = Screen.width, screenHeight = Screen.height };
        Vector3 screen = worldCamera.WorldToScreenPoint(grid.GetCellCenterWorld(player.GridPosition));
        state.playerScreenX = screen.x; state.playerScreenY = Screen.height - screen.y;
        state.tilePixels = Vector3.Distance(screen, worldCamera.WorldToScreenPoint(
            grid.GetCellCenterWorld(player.GridPosition + Vector3Int.right)));
        Rect board = worldCamera.pixelRect;
        state.board = new[] { board.x, Screen.height - board.yMax, board.width, board.height };
        var targets = new List<UiTarget>();
        foreach (string name in new[] { "Move", "Attack", "Special", "Cancel", "Build", "Relic", "Menu", "Close", "ContinueButton" })
        {
            RectTransform rect = Find(name);
            if (rect == null || !rect.gameObject.activeInHierarchy) continue;
            Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            targets.Add(new UiTarget { name = name, x = center.x, y = Screen.height - center.y,
                width = corners[2].x - corners[0].x, height = corners[2].y - corners[0].y });
        }
        state.ui = targets.ToArray();
        Debug.Log("MOBILE_PLAYTEST_STATE " + JsonUtility.ToJson(state));
    }

    private void OnSelectionChanged(PlayerActionSelectionMode mode) { hasPreview = false; }

    private void Update()
    {
        if (controls == null) return;
        if (lastSize != new Vector2Int(Screen.width, Screen.height) || lastSafe != Screen.safeArea) Layout();
        if (IsDrawerOpen) player.SetInspectionOpen(true);
        bool modal = IsDrawerOpen || hud.IsTraitSelectionOpen || hud.IsPauseMenuOpen || hud.IsRunEnded
            || (progressionUi != null && progressionUi.IsModalOpen);
        bool can = player.CanAct && !modal;
        if (hasPreview && (!can || player.GridPosition != pendingOrigin || player.SelectionMode != pendingMode
            || player.IsTraitAttackSelected != pendingTrait)) hasPreview = false;
        // 추가 시전 재조준은 현재 공격 버튼으로 확정한다. 이동/다른 공격 전환은 계속 차단한다.
        bool echo = player.IsChoosingEchoDirection;
        bool moveSelected = player.SelectionMode == PlayerActionSelectionMode.Move;
        bool attackSelected = player.SelectionMode == PlayerActionSelectionMode.Attack && !player.IsTraitAttackSelected;
        bool specialSelected = player.SelectionMode == PlayerActionSelectionMode.Attack && player.IsTraitAttackSelected;
        moveButton.interactable = can && !echo;
        attackButton.interactable = can && !player.IsChoosingFreeMove && (!echo || attackSelected);
        specialButton.interactable = can && !player.IsChoosingFreeMove
            && (specialSelected && echo || !echo && trait != null && trait.IsReady);
        ActionLabel(moveButton, "이동", moveSelected);
        ActionLabel(attackButton, "공격", attackSelected);
        ActionLabel(specialButton, "특수공격", specialSelected);
        cancelButton.gameObject.SetActive(player.SelectionMode != PlayerActionSelectionMode.None && !modal && !echo);
        cancelButton.interactable = can && !player.IsChoosingEchoDirection;
        cancelButton.GetComponentInChildren<UnityEngine.UI.Text>().text = player.IsChoosingFreeMove ? "이동 생략" : "취소";
        // PC HUD·결과 패널과 같은 시계를 표시한다. 상세 설명 중에 별도 시간이 흐르지 않는다.
        int elapsed = Mathf.Max(0, Mathf.FloorToInt(hud.ElapsedPlayTime));
        summary.text = $"WAVE {(spawner != null ? spawner.CurrentWave : 1)}  ·  {elapsed / 60:00}:{elapsed % 60:00}";
        healthSummary.text = $"체력 {player.CurrentHealth}/{(health != null ? health.MaxHealth : 10)}  ·  "
            + (trait != null && trait.IsReady ? "특수공격 준비!" : $"특수공격 {trait?.ChargeProgress ?? 0}/{trait?.ChargeRequired ?? 0}");
        if (!hasPreview) hint.text = player.IsChoosingEchoDirection ? "추가 방향 선택 → 공격 실행"
            : player.IsChoosingFreeMove ? "무료 한 칸 이동 · 뒤이어 적 턴 진행"
            : player.SelectionMode == PlayerActionSelectionMode.Move ? "이동할 칸을 선택하세요"
            : player.SelectionMode == PlayerActionSelectionMode.Attack ? "공격할 방향을 선택하세요"
            : threat != null && !string.IsNullOrEmpty(threat.Description) ? threat.Description
            : "행동 선택 · 적 탭으로 위험 확인";
        UpdateMovePreview(can);
        PollPointer(can);
    }

    private void ActionLabel(UnityEngine.UI.Button button, string label, bool selected)
    {
        button.GetComponentInChildren<UnityEngine.UI.Text>().text = label + (selected && hasPreview ? " 실행" : "");
        button.GetComponent<UnityEngine.UI.Image>().color = selected
            ? new Color32(60, 114, 145, 255) : new Color32(38, 69, 100, 255);
    }

    private void ActionButton(bool attack, bool special)
    {
        bool same = attack ? player.SelectionMode == PlayerActionSelectionMode.Attack
            && player.IsTraitAttackSelected == special : player.SelectionMode == PlayerActionSelectionMode.Move;
        if (same && hasPreview) ConfirmPreview();
        else if (!same) SelectAction(attack, special);
    }

    private void UpdateMovePreview(bool can)
    {
        bool visible = can && hasPreview && pendingMode == PlayerActionSelectionMode.Move;
        movePreview.gameObject.SetActive(visible);
        if (!visible || grid == null || worldCamera == null) return;
        Vector3 center = worldCamera.WorldToScreenPoint(grid.GetCellCenterWorld(pendingCell));
        float size = Vector3.Distance(center, worldCamera.WorldToScreenPoint(
            grid.GetCellCenterWorld(pendingCell + Vector3Int.right)));
        float scale = Screen.width / 390f;
        Place(movePreview, (center.x - size / 2) / scale,
            (Screen.height - center.y - size / 2) / scale, size / scale, size / scale);
        if (playerSprite != null) { previewGhost.sprite = playerSprite.sprite;
            previewGhost.rectTransform.localScale = new Vector3(playerSprite.flipX ? -1 : 1, 1, 1); }
    }

    private bool OverUi(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        return hits.Count > 0;
    }

    private void PollPointer(bool can)
    {
        var touch = Touchscreen.current != null ? Touchscreen.current.primaryTouch : null;
        bool touchActive = touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame);
        Vector2 point = touchActive ? touch.position.ReadValue() : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        bool began = touchActive ? touch.press.wasPressedThisFrame : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool ended = touchActive ? touch.press.wasReleasedThisFrame : Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        if (began)
        {
            pointerStart = point;
            activeTouchId = touchActive ? touch.touchId.ReadValue() : -1;
            pointerTracking = true;
            pointerStartedOnBoard = can && !OverUi(point) && worldCamera != null && worldCamera.pixelRect.Contains(point);
        }
        if (!ended || !pointerTracking) return;
        pointerTracking = false;
        if (touchActive && activeTouchId != touch.touchId.ReadValue()) return;
        if (pointerStartedOnBoard && can && Time.frameCount > blockedPointerFrame && !OverUi(point)
            && Vector2.Distance(point, pointerStart) < Screen.width * 0.04f) PreviewBoardPoint(point);
    }

    public void SelectAction(bool attack, bool special)
    {
        blockedPointerFrame = Time.frameCount;
        if (!player.CanAct || IsDrawerOpen || (progressionUi != null && progressionUi.IsModalOpen)) return;
        hasPreview = false;
        threat?.ClearMobileInspection();
        if (special) player.SelectTraitAttackMode();
        else if (attack) player.SelectAttackMode();
        else player.SelectMoveMode();
    }

    public bool PreviewBoardPoint(Vector2 point)
    {
        if (!player.CanAct || IsDrawerOpen || (progressionUi != null && progressionUi.IsModalOpen)) return false;
        if (player.SelectionMode == PlayerActionSelectionMode.None)
        {
            threat?.InspectMobilePoint(point);
            return false;
        }
        hasPreview = player.TryPreviewBoardPoint(point, out pendingCell);
        if (!hasPreview) { hint.text = "표시된 이동 칸 또는 공격 방향을 선택하세요"; return false; }
        pendingOrigin = player.GridPosition;
        pendingMode = player.SelectionMode;
        pendingTrait = player.IsTraitAttackSelected;
        Vector3Int delta = pendingCell - pendingOrigin;
        string direction = DirectionName(delta);
        hint.text = pendingMode == PlayerActionSelectionMode.Attack
            ? $"{direction} · {(pendingTrait ? "특수공격" : "공격")} 실행"
            : $"{direction} {Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y))}칸 · 이동 실행";
        UpdateMovePreview(true);
        return true;
    }

    private static string DirectionName(Vector3Int delta)
    {
        if (delta.x == 0) return delta.y > 0 ? "위" : "아래";
        if (delta.y == 0) return delta.x > 0 ? "오른쪽" : "왼쪽";
        return (delta.x > 0 ? "오른쪽 " : "왼쪽 ") + (delta.y > 0 ? "위" : "아래");
    }

    public void ConfirmPreview()
    {
        blockedPointerFrame = Time.frameCount;
        if (!hasPreview || pendingOrigin != player.GridPosition || pendingMode != player.SelectionMode
            || pendingTrait != player.IsTraitAttackSelected || IsDrawerOpen
            || (progressionUi != null && progressionUi.IsModalOpen)) return;
        if (player.ConfirmPreviewCell(pendingCell)) { hasPreview = false; movePreview.gameObject.SetActive(false); }
    }

    public void CancelPreview()
    {
        blockedPointerFrame = Time.frameCount;
        if (!player.CanAct || player.IsChoosingEchoDirection) return;
        hasPreview = false;
        movePreview.gameObject.SetActive(false);
        if (player.IsChoosingFreeMove) player.SkipFreeMove();
        else player.CancelSelection();
    }

    public void OpenDrawer(bool relics)
    {
        blockedPointerFrame = Time.frameCount;
        if (hud.IsTraitSelectionOpen || hud.IsPauseMenuOpen || hud.IsRunEnded
            || (progressionUi != null && progressionUi.IsModalOpen)
            || (!player.CanAct && !IsDrawerOpen)) return;
        hasPreview = false;
        drawer.gameObject.SetActive(true);
        drawer.SetAsLastSibling();
        buildScroll.gameObject.SetActive(!relics);
        relicScroll.gameObject.SetActive(relics);
        player.SetInspectionOpen(true);
    }

    public void CloseDrawer()
    {
        blockedPointerFrame = Time.frameCount;
        progressionUi?.CloseDetails();
        drawer.gameObject.SetActive(false);
        player.SetInspectionOpen(false);
    }

    private void CreateDrawer()
    {
        drawer = Panel("MobileInventory", transform, true);
        Stretch(drawer, 0, 0, 0, 0);
        Button("Close", drawer, "닫기", 12, 12, 100, 48, CloseDrawer);
        Button("TraitInfo", drawer, "시작 특성 설명", 124, 12, 254, 48,
            () => progressionUi?.ShowTraitDetails());
        buildScroll = InventoryScroll("BuildScroll", Find("BuildHudPanel"));
        relicScroll = InventoryScroll("RelicScroll", Find("RelicHudPanel"));
        drawer.gameObject.SetActive(false);
    }

    private RectTransform InventoryScroll(string name, RectTransform existing)
    {
        RectTransform root = Rect(name, drawer);
        Stretch(root, 12, 12, 72, 12);
        var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        RectTransform viewport = Panel("Viewport", root, true);
        Stretch(viewport, 0, 0, 0, 0);
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        if (existing != null)
        {
            existing.SetParent(content, false);
            existing.localScale = Vector3.one;
            var element = existing.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight = 820;
            foreach (UnityEngine.UI.Text label in existing.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (label.fontSize < 14) label.fontSize = 14;
            // 빌드 목록의 행은 PC의 42px에서 모바일 최소 44pt 터치 대상으로 키운다.
            foreach (UnityEngine.UI.Button button in existing.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                RectTransform row = button.GetComponent<RectTransform>();
                if (row.sizeDelta.y < 44) row.sizeDelta = new Vector2(row.sizeDelta.x, 44);
            }
        }
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        return root;
    }

    private void Layout()
    {
        Canvas.ForceUpdateCanvases();
        float scale = Screen.width / 390f;
        Rect safe = Screen.safeArea;
        if (safe.width <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        Rect board = BoardPixelRect(Screen.width, Screen.height, safe);
        float boardBottom = board.y / scale;
        float boardTop = (Screen.height - board.yMax) / scale;
        float left = board.x / scale;
        float side = board.width / scale;
        float width = Screen.width / scale;
        float canvasHeight = Screen.height / scale;
        Place(backdrop[0], 0, 0, width, boardTop);
        Place(backdrop[1], 0, boardTop + side, width, canvasHeight - boardTop - side);
        Place(backdrop[2], 0, boardTop, left, side);
        Place(backdrop[3], left + side, boardTop, width - left - side, side);
        Place(top, (width - 366) / 2, boardTop - 90, 366, 88);
        Place(bottom, (width - 366) / 2, canvasHeight - boardBottom + 2, 366, 90);
        Stretch(drawer, safe.x / scale, (Screen.width - safe.xMax) / scale,
            (Screen.height - safe.yMax) / scale, safe.y / scale);
        LayoutSelection(safe.height / scale);
        LayoutRewards(safe.height / scale);
        LayoutSmallMenu("PauseMenuPanel", "PauseHint", "메뉴에서 계속 진행을 선택하세요");
        RectTransform quit = Find("PauseQuitButton");
        if (quit != null) quit.gameObject.SetActive(false);
        LayoutSmallMenu("GameOverPanel", "QuitHint", "다시하기를 눌러 새로운 빌드에 도전");
        lastSize = new Vector2Int(Screen.width, Screen.height); lastSafe = Screen.safeArea;
    }

    private void LayoutSelection(float height)
    {
        RectTransform panel = Find("SelectionPanel");
        if (panel == null) return;
        float h = Mathf.Min(height - 24, 760);
        Center(panel, 366, h);
        SetText(panel, "SelectionEyebrow", 0, 12, 366, 20, 11);
        SetText(panel, "SelectionTitle", 8, 36, 350, 32, 21);
        SetText(panel, "SelectionSubtitle", 8, 70, 350, 30, 13);
        float cardHeight = Mathf.Min(124, (h - 194) / 4);
        for (int i = 0; i < 4; i++)
        {
            RectTransform card = panel.Find($"TraitCard_{i + 1}") as RectTransform;
            if (card == null) continue;
            Place(card, 10, 108 + i * (cardHeight + 8), 346, cardHeight);
            card.Find("ShortcutBadge")?.gameObject.SetActive(false);
            RectTransform icon = card.Find("TraitIconFrame") as RectTransform;
            if (icon != null) Place(icon, 10, 12, 48, 48);
            SetText(card, "TraitName", 70, 8, 264, 28, 17);
            SetText(card, "TraitDescription", 70, 38, 264, cardHeight - 42, 13);
        }
        UnityEngine.UI.Text footer = panel.Find("SelectionFooter")?.GetComponent<UnityEngine.UI.Text>();
        if (footer != null)
        {
            Place(footer.rectTransform, 8, h - 62, 350, 56);
            footer.fontSize = 13;
            footer.text = "행동 선택 → 보드 탭 → 같은 버튼으로 실행\n이동은 선택 칸의 잔상으로 확인\n보유 강화 · 유물은 보드 위에서 확인";
        }
    }

    private void LayoutRewards(float height)
    {
        RectTransform upgrade = Find("UpgradeSelectionPanel");
        if (upgrade != null)
        {
            SetText(upgrade, "Title", 12, 20, 366, 36, 22);
            SetText(upgrade, "Guide", 12, 60, 366, 40, 13);
            float h = Mathf.Min(190, (height - 128) / 3);
            for (int i = 0; i < 3; i++)
            {
                RectTransform card = upgrade.Find($"UpgradeChoice_{i + 1}") as RectTransform;
                if (card == null) continue;
                Place(card, 12, 108 + i * (h + 8), 366, h);
                RectTransform icon = card.Find("Icon") as RectTransform;
                if (icon != null) Place(icon, 12, 10, 42, 42);
                RectTransform details = card.Find("Details") as RectTransform;
                if (details != null) { Place(details, 278, 10, 76, 44); SetText(details, "Label", 0, 0, 76, 44, 14); }
                SetText(card, "Label", 12, 62, 342, h - 70, 14);
            }
        }
        RectTransform replacement = Find("RelicReplacementPanel");
        if (replacement != null)
        {
            float h = Mathf.Min(height - 24, 770);
            Center(replacement, 366, h);
            SetText(replacement, "Title", 12, 12, 342, 32, 18);
            SetText(replacement, "Guide", 12, 46, 342, 36, 13);
            replacement.Find("Guide").GetComponent<UnityEngine.UI.Text>().text = "교체할 유물 또는 해체를 선택하세요";
            float cardHeight = (h - 100) / 5 - 8;
            for (int i = 0; i < 5; i++)
            {
                RectTransform card = replacement.Find($"ReplacementChoice_{i + 1}") as RectTransform;
                if (card == null) continue;
                Place(card, 12, 94 + i * (cardHeight + 8), 342, cardHeight);
                SetText(card, "Label", 76, 4, 252, cardHeight - 48, 12);
                RectTransform details = card.Find("Details") as RectTransform;
                if (details != null)
                {
                    Place(details, 76, cardHeight - 44, 252, 44);
                    SetText(details, "Label", 0, 0, 252, 44, 14);
                }
            }
        }
        RectTransform detail = Find("ProgressionDetailCard");
        if (detail != null)
        {
            float h = Mathf.Min(height - 24, 700);
            Center(detail, 366, h);
            SetText(detail, "Title", 16, 16, 334, 54, 18);
            RectTransform description = detail.Find("Description") as RectTransform;
            RectTransform scrollRoot = detail.Find("DetailScroll") as RectTransform;
            if (description != null && scrollRoot == null) scrollRoot = CreateDescriptionScroll(detail, description);
            if (scrollRoot != null) Stretch(scrollRoot, 16, 16, 80, 76);
            RectTransform close = detail.Find("CloseDetails") as RectTransform;
            if (close != null) { Place(close, 16, h - 60, 334, 44); SetText(close, "Label", 0, 0, 334, 44, 15); }
        }
    }

    private RectTransform CreateDescriptionScroll(RectTransform parent, RectTransform description)
    {
        RectTransform root = Rect("DetailScroll", parent);
        var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        RectTransform viewport = Panel("Viewport", root, true); Stretch(viewport, 0, 0, 0, 0);
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0, 1); content.sizeDelta = Vector2.zero;
        var group = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        group.childControlHeight = true; group.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        description.SetParent(content, false);
        var text = description.GetComponent<UnityEngine.UI.Text>(); text.fontSize = 14;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        return root;
    }

    private void LayoutSmallMenu(string name, string hintName, string message)
    {
        RectTransform panel = Find(name);
        if (panel == null) return;
        bool pause = name == "PauseMenuPanel";
        Center(panel, 366, pause ? 510 : 410);
        SetText(panel, pause ? "PauseEyebrow" : "GameOverEyebrow", 8, 16, 350, 24, 13);
        SetText(panel, pause ? "PauseTitle" : "GameOverTitle", 8, 48, 350, 48, 28);
        if (pause)
        {
            foreach (string rowName in new[] { "SfxVolumeRow", "MusicVolumeRow" })
            {
                RectTransform row = panel.Find(rowName) as RectTransform;
                if (row != null) Place(row, 24, rowName == "SfxVolumeRow" ? 116 : 196, 318, 68);
            }
            foreach (string buttonName in new[] { "ContinueButton", "PauseRestartButton" })
            {
                RectTransform button = panel.Find(buttonName) as RectTransform;
                if (button == null) continue;
                Place(button, 24, buttonName == "ContinueButton" ? 308 : 378, 318, 52);
                foreach (UnityEngine.UI.Text text in button.GetComponentsInChildren<UnityEngine.UI.Text>())
                { Stretch(text.rectTransform, 0, 0, 0, 0); text.fontSize = 18; }
            }
        }
        else
        {
            SetText(panel, "GameOverSummary", 12, 110, 342, 80, 18);
            RectTransform replay = panel.Find("ReplayButton") as RectTransform;
            if (replay != null)
            {
                Place(replay, 24, 214, 318, 52);
                SetText(replay, "ReplayLabel", 0, 0, 318, 52, 18);
            }
            if (panel.Find("ReviewBoard") == null)
                Button("ReviewBoard", panel, "전장 확인", 24, 278, 318, 52, hud.ToggleGameOverReview);
        }
        UnityEngine.UI.Text label = panel.Find(hintName)?.GetComponent<UnityEngine.UI.Text>();
        if (label != null)
        {
            label.text = pause ? "볼륨: 드래그 / −·+ 조절 · 자동 저장" : message;
            Place(label.rectTransform, 12, pause ? 458 : 358, 342, 36); label.fontSize = 13;
        }
    }

    public void PauseFromBrowser()
    {
        if (hud != null && !hud.IsPauseMenuOpen && !hud.IsRunEnded) hud.TogglePauseMenu();
    }
    private void OnApplicationPause(bool paused) { if (paused) PauseFromBrowser(); }
    private void OnApplicationFocus(bool focused) { if (!focused) PauseFromBrowser(); }

    private RectTransform Find(string name)
    {
        foreach (RectTransform rect in GetComponentsInChildren<RectTransform>(true)) if (rect.name == name) return rect;
        return null;
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
    private static void Stretch(RectTransform rect, float left, float right, float up, float down)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, down); rect.offsetMax = new Vector2(-right, -up);
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
    private static void Center(RectTransform rect, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(width, height); rect.localScale = Vector3.one;
    }
    private RectTransform Panel(string name, Transform parent, bool blocking)
    {
        RectTransform rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color32(12, 22, 37, 255); image.raycastTarget = blocking;
        return rect;
    }
    private UnityEngine.UI.Text Text(string name, Transform parent, string value, int size)
    {
        RectTransform rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
        text.font = font; text.fontSize = size; text.color = Color.white; text.text = value;
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }
    private UnityEngine.UI.Button Button(string name, Transform parent, string label,
        float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Panel(name, parent, true); Place(rect, x, y, width, height);
        rect.GetComponent<UnityEngine.UI.Image>().color = new Color32(38, 69, 100, 255);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>(); button.onClick.AddListener(action);
        Stretch(Text("Label", rect, label, 16).rectTransform, 2, 2, 2, 2);
        return button;
    }
    private static void SetText(Transform parent, string name, float x, float y, float w, float h, int size)
    {
        var text = parent.Find(name)?.GetComponent<UnityEngine.UI.Text>();
        if (text == null) return;
        Place(text.rectTransform, x, y, w, h); text.fontSize = size; text.fontStyle = FontStyle.Normal;
    }
}
