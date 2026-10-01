using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(DirectionalActionIndicator))]
public class Move : MonoBehaviour
{
    public event Action MoveCompleted;
    public event Action PlayerMoved;
    public event Action AttackHit;
    public event Action<PlayerActionSelectionMode> SelectionModeChanged;

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 2.67f;

    [Header("전투 설정")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int attackDamage = 1;

    [Header("이동 불가 피드백")]
    [SerializeField] private AudioClip blockedMoveSound;
    [SerializeField, Range(0f, 1f)] private float blockedMoveSoundVolume = 0.45f;

    [Header("행동 선택 표시")]
    [SerializeField] private DirectionalActionIndicator actionIndicator;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private bool cardinalOnly;

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private const int FeedbackSampleRate = 22050;
    private const float BlockedSoundDuration = 0.12f;
    private static readonly Vector2Int[] EightDirections =
    {
        Vector2Int.up,
        new Vector2Int(1, 1),
        Vector2Int.right,
        new Vector2Int(1, -1),
        Vector2Int.down,
        new Vector2Int(-1, -1),
        Vector2Int.left,
        new Vector2Int(-1, 1)
    };

    private static readonly Vector3Int[] KnightOffsets =
    {
        new Vector3Int(1, 2, 0), new Vector3Int(2, 1, 0),
        new Vector3Int(2, -1, 0), new Vector3Int(1, -2, 0),
        new Vector3Int(-1, -2, 0), new Vector3Int(-2, -1, 0),
        new Vector3Int(-2, 1, 0), new Vector3Int(-1, 2, 0)
    };

    private static readonly Vector3Int[] DiagonalDirections =
    {
        new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0),
        new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0)
    };

    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left
    };

    private Animator playerAnimator;
    private SpriteRenderer playerSprite;
    private CharacterHealth characterHealth;
    private AudioSource feedbackAudioSource;
    private AudioClip generatedBlockedMoveSound;
    private GridManager gridManager;
    private MonsterSpawner monsterSpawner;
    private ProjectileManager projectileManager;
    private PlayerTraitSystem playerTraitSystem;
    private RunProgressionSystem progressionSystem;
    private readonly List<Vector3Int> selectableCells = new List<Vector3Int>(8);
    private readonly HashSet<Vector3Int> movementArtCells = new HashSet<Vector3Int>();
    private readonly List<Vector3> selectableWorldPositions = new List<Vector3>(8);
    private readonly List<bool> emphasizedChoices = new List<bool>(8);
    private readonly List<Vector3> attackPathWorldPositions = new List<Vector3>(16);
    private Vector3 targetPosition;
    private Vector3Int hoveredAttackCell = new Vector3Int(int.MinValue, int.MinValue, 0);
    private PlayerActionSelectionMode selectionMode;
    private bool moving;
    private bool waitingForMonsters;
    private bool inputEnabled = true;
    private Vector3Int activeAttackOriginCell;
    private Vector3Int activeAttackDirection;
    private int activeAttackDamage;
    private int activeAttackPenetrations;
    private int remainingAttackCasts;
    private bool activeAttackKnockback;
    private bool attackHitReported;
    private PlayerAttackTraitRoll activeTraitRoll;
    private int activeAttackCastIndex;
    private int activeAttackHitIndex;
    private bool choosingEchoDirection;
    private bool kingsFreeMove;
    private Vector3Int gridPosition;
    private bool gridPositionReady;
    private bool inspectionOpen;
    private int inspectionClosedFrame = -1;

    public int CurrentHealth => characterHealth != null ? characterHealth.CurrentHealth : 0;
    public PlayerActionSelectionMode SelectionMode => selectionMode;
    public Vector3Int GridPosition { get { ResolveReferences(); return gridPosition; } }
    public bool CanAct => enabled && inputEnabled && !inspectionOpen
        && Time.frameCount > inspectionClosedFrame && !moving && !waitingForMonsters;
    public bool IsChoosingEchoDirection => choosingEchoDirection;
    public bool IsChoosingFreeMove => kingsFreeMove;
    public bool IsActionInProgress => moving || waitingForMonsters || choosingEchoDirection || kingsFreeMove;

    private void Awake()
    {
        // Move가 붙어 있는 플레이어 자신의 컴포넌트만 사용한다.
        playerAnimator = GetComponent<Animator>();
        playerSprite = GetComponent<SpriteRenderer>();
        characterHealth = GetComponent<CharacterHealth>();
        feedbackAudioSource = GetComponent<AudioSource>();
        actionIndicator = GetComponent<DirectionalActionIndicator>();
        progressionSystem = GetComponent<RunProgressionSystem>();
        if (progressionSystem == null)
        {
            progressionSystem = gameObject.AddComponent<RunProgressionSystem>();
        }

        if (characterHealth == null)
        {
            characterHealth = gameObject.AddComponent<CharacterHealth>();
        }

        characterHealth.Initialize(maxHealth);
        characterHealth.Died += HandleDeath;

        ConfigureFeedbackAudio();

        targetPosition = transform.position;
        SetMoving(false);
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        // 상세 설명은 턴을 소비하지 않으며 기존 공격/이동 선택도 보존한다.
        if (inspectionOpen) return;
        if (!inputEnabled || waitingForMonsters)
        {
            CancelSelection();
            return;
        }

        if (moving)
        {
            MoveOneTile();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        ProcessInputFrame(
            keyboard != null && keyboard.aKey.wasPressedThisFrame,
            keyboard != null && keyboard.sKey.wasPressedThisFrame,
            mouse != null && mouse.leftButton.wasPressedThisFrame,
            mouse != null && mouse.rightButton.wasPressedThisFrame,
            mouse != null ? mouse.position.ReadValue() : Vector2.zero,
            keyboard != null && keyboard.fKey.wasPressedThisFrame);

        if (mouse != null)
        {
            RefreshAttackPathPreview(mouse.position.ReadValue());
        }
        else
        {
            ClearAttackPathPreview();
        }
    }

    // 키 선택과 클릭을 한 프레임에 처리한다. 자동 검증도 이 입력 경로를 사용한다.
    public void ProcessInputFrame(bool attackPressed, bool movePressed,
        bool confirmPressed, bool cancelPressed, Vector2 pointerScreenPosition,
        bool movementArtPressed = false)
    {
        if (!CanAct) return;

        // 회전 거울의 추가 공격 방향을 고르는 동안에는 다른 행동으로 전환할 수 없다.
        // 첫 공격의 원래 발사 위치를 보존해야 하므로 이동·이동술·취소 입력을 모두 무시한다.
        if (choosingEchoDirection)
        {
            if (confirmPressed && selectionMode == PlayerActionSelectionMode.Attack)
                TryConfirmSelectedCell(pointerScreenPosition);
            return;
        }

        if (attackPressed) ToggleSelectionMode(PlayerActionSelectionMode.Attack);
        if (movePressed) ToggleSelectionMode(PlayerActionSelectionMode.Move);
        // 이동술은 별도 F 행동이 아니라 S 이동 선택지에 항상 합쳐서 표시한다.
        if (cancelPressed)
        {
            if (choosingEchoDirection || kingsFreeMove) return;
            CancelSelection();
            return;
        }
        if (confirmPressed && selectionMode != PlayerActionSelectionMode.None)
            TryConfirmSelectedCell(pointerScreenPosition);
    }

    public void SelectAttackMode()
    {
        SetSelectionMode(PlayerActionSelectionMode.Attack);
    }

    public void SetInspectionOpen(bool value)
    {
        // 상세창을 닫은 클릭이 같은 프레임에 뒤쪽 보드의 공격/이동으로 전달되지 않게 한다.
        if (inspectionOpen && !value) inspectionClosedFrame = Time.frameCount;
        inspectionOpen = value;
    }

    public void SelectMoveMode()
    {
        SetSelectionMode(PlayerActionSelectionMode.Move);
    }

    public void SelectMovementArtMode()
    {
        SelectMoveMode();
    }

    public void CancelSelection()
    {
        if (selectionMode == PlayerActionSelectionMode.None)
            return;

        selectionMode = PlayerActionSelectionMode.None;
        selectableCells.Clear();
        movementArtCells.Clear();
        selectableWorldPositions.Clear();
        emphasizedChoices.Clear();
        actionIndicator?.ClearChoices();
        hoveredAttackCell = new Vector3Int(int.MinValue, int.MinValue, 0);
        SelectionModeChanged?.Invoke(selectionMode);
    }

    private void ToggleSelectionMode(PlayerActionSelectionMode requestedMode)
    {
        if (selectionMode == requestedMode)
        {
            CancelSelection();
            return;
        }

        SetSelectionMode(requestedMode);
    }

    private void SetSelectionMode(PlayerActionSelectionMode requestedMode)
    {
        ResolveReferences();

        if (!inputEnabled || inspectionOpen || waitingForMonsters || moving)
            return;

        if (choosingEchoDirection && requestedMode != PlayerActionSelectionMode.Attack)
            return;

        if (requestedMode == PlayerActionSelectionMode.MovementArt)
            requestedMode = PlayerActionSelectionMode.Move;

        if (gridManager == null || !gridManager.IsReady
            || monsterSpawner == null || projectileManager == null
            || actionIndicator == null)
        {
            Debug.LogError(
                "행동 선택에 GridManager, MonsterSpawner, ProjectileManager와 방향 표시기가 필요합니다.",
                this
            );
            return;
        }

        selectionMode = requestedMode;
        BuildSelectableCells();
        actionIndicator.ShowChoices(
            transform.position,
            selectableWorldPositions,
            selectionMode,
            emphasizedChoices
        );
        hoveredAttackCell = new Vector3Int(int.MinValue, int.MinValue, 0);
        actionIndicator.ClearAttackPath();
        SelectionModeChanged?.Invoke(selectionMode);
    }

    private void RefreshAttackPathPreview(Vector2 pointerScreenPosition)
    {
        if (selectionMode != PlayerActionSelectionMode.Attack
            || worldCamera == null
            || gridManager == null
            || !gridManager.IsReady
            || actionIndicator == null
            || !worldCamera.pixelRect.Contains(pointerScreenPosition))
        {
            ClearAttackPathPreview();
            return;
        }

        // 카메라 깊이와 무관하게 실제 타일맵 평면 위의 커서 셀을 구한다.
        Ray ray = worldCamera.ScreenPointToRay(pointerScreenPosition);
        Plane plane = new Plane(
            gridManager.GroundTilemap.transform.forward,
            gridManager.GetCellCenterWorld(GridPosition)
        );

        if (!plane.Raycast(ray, out float distance))
        {
            ClearAttackPathPreview();
            return;
        }

        Vector3Int hoveredCell = gridManager.WorldToCell(ray.GetPoint(distance));

        if (!selectableCells.Contains(hoveredCell))
        {
            ClearAttackPathPreview();
            return;
        }

        if (hoveredCell == hoveredAttackCell)
            return;

        hoveredAttackCell = hoveredCell;
        ShowAttackPathPreview(hoveredCell);
    }

    private void ShowAttackPathPreview(Vector3Int directionCell)
    {
        attackPathWorldPositions.Clear();

        Vector3Int currentCell = GridPosition;
        Vector3Int direction = directionCell - currentCell;
        int emphasizedCellIndex = -1;

        // 첫 몬스터까지 표시하되, 공격이 소멸하는 바깥 경계 벽 칸은 미리보기에서 제외한다.
        while (true)
        {
            Vector3Int nextCell = currentCell + direction;

            if (!gridManager.IsInsideMapCell(nextCell))
                break;

            if (!gridManager.IsWalkableCell(nextCell))
            {
                // 벽 셀의 쇠뇌 룩은 노란 바닥 점유에서 제외하고 마지막 바닥 칸만 강조한다.
                if (monsterSpawner.TryGetMonsterAtCell(nextCell, out _)
                    && attackPathWorldPositions.Count > 0)
                    emphasizedCellIndex = attackPathWorldPositions.Count - 1;
                break;
            }

            currentCell = nextCell;
            attackPathWorldPositions.Add(gridManager.GetCellCenterWorld(currentCell));

            if (monsterSpawner.TryGetMonsterAtCell(currentCell, out _))
            {
                emphasizedCellIndex = attackPathWorldPositions.Count - 1;
                break;
            }
        }

        actionIndicator.ShowAttackPath(
            attackPathWorldPositions,
            emphasizedCellIndex
        );
    }

    private void ClearAttackPathPreview()
    {
        actionIndicator?.ClearAttackPath();
        hoveredAttackCell = new Vector3Int(int.MinValue, int.MinValue, 0);
    }

    private void BuildSelectableCells()
    {
        selectableCells.Clear();
        movementArtCells.Clear();
        selectableWorldPositions.Clear();
        emphasizedChoices.Clear();

        Vector3Int currentCell = GridPosition;

        foreach (Vector2Int direction in EightDirections)
        {
            if (cardinalOnly && direction.x != 0 && direction.y != 0)
                continue;

            Vector3Int destinationCell = currentCell
                + new Vector3Int(direction.x, direction.y, 0);

            if (selectionMode == PlayerActionSelectionMode.Move)
            {
                if (!gridManager.IsWalkableCell(destinationCell)
                    || monsterSpawner.TryGetMonsterAtCell(destinationCell, out _))
                {
                    continue;
                }
            }
            else if (!gridManager.IsInsideMapCell(destinationCell))
            {
                continue;
            }

            bool hasAttackTarget = selectionMode == PlayerActionSelectionMode.Attack
                && monsterSpawner.TryGetMonsterAtCell(destinationCell, out _);

            selectableCells.Add(destinationCell);
            selectableWorldPositions.Add(gridManager.GetCellCenterWorld(destinationCell));
            emphasizedChoices.Add(hasAttackTarget);
        }

        // 왕의 차례 무료 이동은 기본 한 칸만 허용한다. 일반 S 이동에는 보유 이동술을 합친다.
        if (selectionMode == PlayerActionSelectionMode.Move
            && !kingsFreeMove
            && progressionSystem != null
            && progressionSystem.HasMovementArt)
        {
            BuildMovementArtCells(currentCell);
        }
    }

    public bool TryConfirmSelectedCell(Vector2 pointerScreenPosition)
    {
        if (!CanAct || selectionMode == PlayerActionSelectionMode.None)
            return false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

        ResolveReferences();

        if (worldCamera == null || gridManager == null || !gridManager.IsReady)
            return false;

        if (!worldCamera.pixelRect.Contains(pointerScreenPosition))
            return false;
        // 화면에서 타일맵 평면으로 광선을 투영한다. 카메라 깊이/뷰포트 크기에 의존하지 않는다.
        Ray ray = worldCamera.ScreenPointToRay(pointerScreenPosition);
        Plane plane = new Plane(gridManager.GroundTilemap.transform.forward,
            gridManager.GetCellCenterWorld(GridPosition));
        if (!plane.Raycast(ray, out float distance))
            return false;
        Vector3 pointerWorldPosition = ray.GetPoint(distance);
        Vector3Int selectedCell = gridManager.WorldToCell(pointerWorldPosition);

        if (!selectableCells.Contains(selectedCell))
            return false;

        if (selectionMode == PlayerActionSelectionMode.Move)
        {
            if (movementArtCells.Contains(selectedCell))
                return TryUseMovementArtAtCell(selectedCell);
            TryBeginMove(selectedCell);
        }
        else if (selectionMode == PlayerActionSelectionMode.Attack)
        {
            if (choosingEchoDirection)
            {
                activeAttackDirection = selectedCell - GridPosition;
                choosingEchoDirection = false;
                CancelSelection();
                waitingForMonsters = true;
                LaunchNextAttackCast();
            }
            else
            {
                ExecuteAttack(selectedCell);
            }
        }
        return true;
    }

    private void BuildMovementArtCells(Vector3Int origin)
    {
        if (progressionSystem == null) return;

        switch (progressionSystem.ActiveMovementArt)
        {
            case PlayerMovementArt.Knight:
                foreach (Vector3Int offset in KnightOffsets)
                    AddMovementArtCell(origin + offset);
                break;

            case PlayerMovementArt.Bishop:
                BuildSlidingMovementArtCells(origin, DiagonalDirections,
                    2 + progressionSystem.MovementArtLevel);
                break;

            case PlayerMovementArt.Rook:
                BuildSlidingMovementArtCells(origin, CardinalDirections,
                    3 + progressionSystem.MovementArtLevel);
                break;
        }
    }

    private void BuildSlidingMovementArtCells(Vector3Int origin,
        IReadOnlyList<Vector3Int> directions, int maximumDistance)
    {
        foreach (Vector3Int direction in directions)
        {
            for (int distance = 1; distance <= maximumDistance; distance++)
            {
                Vector3Int cell = origin + direction * distance;
                if (!gridManager.IsWalkableCell(cell)
                    || monsterSpawner.TryGetMonsterAtCell(cell, out _)) break;
                AddMovementArtCell(cell);
            }
        }
    }

    private void AddMovementArtCell(Vector3Int cell)
    {
        if (!gridManager.IsWalkableCell(cell)
            || monsterSpawner.TryGetMonsterAtCell(cell, out _)) return;
        // 비숍/룩의 1칸 목적지는 기본 이동으로 처리하고 중복 표시하지 않는다.
        if (selectableCells.Contains(cell)) return;
        selectableCells.Add(cell);
        movementArtCells.Add(cell);
        selectableWorldPositions.Add(gridManager.GetCellCenterWorld(cell));
        emphasizedChoices.Add(false);
    }

    public bool TryUseMovementArtAtCell(Vector3Int destinationCell)
    {
        ResolveReferences();
        if (!CanAct || selectionMode != PlayerActionSelectionMode.Move
            || !selectableCells.Contains(destinationCell)
            || !movementArtCells.Contains(destinationCell)
            || destinationCell == GridPosition
            || !gridManager.IsWalkableCell(destinationCell)
            || monsterSpawner.TryGetMonsterAtCell(destinationCell, out _)
            || progressionSystem == null
            || !progressionSystem.HasMovementArt)
            return false;

        Vector3Int originCell = gridPosition;
        Vector3Int difference = destinationCell - gridPosition;
        UpdateFacing(new Vector2Int(difference.x, difference.y));
        gridPosition = destinationCell;
        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        // 이동술은 즉시 착지하지만 반드시 플레이어 행동 하나를 소비한다.
        transform.position = targetPosition;
        ClearMovementArtFire(originCell, destinationCell);
        CancelSelection();
        progressionSystem.NotifyMoveAction();
        progressionSystem.NotifyMovementArtUsed();
        PlayerMoved?.Invoke();
        waitingForMonsters = true;
        // 이동 제단과 동일하게 이동술 착지 즉시 유물을 획득한다.
        // 슬롯 교체 UI가 열리면 선택 완료 후 아래 콜백으로 턴을 재개한다.
        if (progressionSystem != null
            && progressionSystem.TryHandleAltarAtCell(GridPosition, CompleteMovementArtAction))
            return true;
        CompleteMovementArtAction();
        return true;
    }

    private void ClearMovementArtFire(Vector3Int origin, Vector3Int destination)
    {
        BishopFireTrail fireTrail = monsterSpawner != null ? monsterSpawner.FireTrail : null;
        if (fireTrail == null) return;

        if (progressionSystem.ActiveMovementArt == PlayerMovementArt.Bishop)
        {
            Vector3Int delta = destination - origin;
            Vector3Int step = new Vector3Int(Math.Sign(delta.x), Math.Sign(delta.y), 0);
            for (Vector3Int cell = origin + step; cell != destination + step; cell += step)
                fireTrail.RemoveFire(cell);
        }
        else if (progressionSystem.ActiveMovementArt == PlayerMovementArt.Knight
            && progressionSystem.MovementArtLevel >= 2)
        {
            fireTrail.RemoveFire(destination);
            foreach (Vector3Int direction in CardinalDirections)
                fireTrail.RemoveFire(destination + direction);
        }
    }

    private void CompleteMovementArtAction()
    {
        BeginMonsterTurn();
    }

    private void TryBeginMove(Vector3Int destinationCell)
    {
        if (!gridManager.IsWalkableCell(destinationCell)
            || monsterSpawner.TryGetMonsterAtCell(destinationCell, out _))
        {
            PlayBlockedMoveFeedback();
            BuildSelectableCells();
            actionIndicator.ShowChoices(
                transform.position,
                selectableWorldPositions,
                selectionMode,
                emphasizedChoices
            );
            return;
        }

        Vector3Int currentCell = GridPosition;
        Vector3Int difference = destinationCell - currentCell;
        UpdateFacing(new Vector2Int(difference.x, difference.y));

        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        gridPosition = destinationCell;

        if (!kingsFreeMove)
        {
            progressionSystem?.NotifyMoveAction();
        }

        CancelSelection();
        SetMoving(true);
    }

    private void ExecuteAttack(Vector3Int attackCell)
    {
        Vector3Int currentCell = GridPosition;
        Vector3Int difference = attackCell - currentCell;
        UpdateFacing(new Vector2Int(difference.x, difference.y));

        PlayerAttackTraitRoll traitRoll = playerTraitSystem != null
            ? playerTraitSystem.RollBasicAttack()
            : default;

        progressionSystem?.BeginAttack(traitRoll);
        activeTraitRoll = traitRoll;
        activeAttackOriginCell = currentCell;
        activeAttackDirection = difference;
        activeAttackDamage = attackDamage;
        activeAttackPenetrations = progressionSystem != null
            ? progressionSystem.ModifyPenetrations(traitRoll)
            : traitRoll.Pierce ? 1 : 0;
        remainingAttackCasts = progressionSystem != null
            ? progressionSystem.AttackCastCount(traitRoll)
            : traitRoll.DoubleCast ? 2 : 1;
        activeAttackKnockback = traitRoll.Knockback;
        attackHitReported = false;
        activeAttackCastIndex = -1;

        CancelSelection();
        // 플레이어 투사체의 전체 경로 비행이 끝날 때까지 추가 입력과 적 턴을 막는다.
        waitingForMonsters = true;

        LaunchNextAttackCast();
    }

    private void LaunchNextAttackCast()
    {
        if (remainingAttackCasts <= 0)
        {
            BeginMonsterTurn();
            return;
        }

        remainingAttackCasts--;
        activeAttackCastIndex++;
        activeAttackHitIndex = 0;
        int castIndex = activeAttackCastIndex;
        int castDamage = progressionSystem != null
            ? progressionSystem.ModifyAttackDamage(
                activeAttackDamage, activeTraitRoll, activeAttackCastIndex)
            : activeAttackDamage + (activeTraitRoll.DamageBoost ? 1 : 0);
        int castPenetrations = activeAttackPenetrations;
        if (activeAttackCastIndex > 0 && progressionSystem != null)
            castPenetrations += progressionSystem.Stack("echo_warhead");

        bool projectileCreated = projectileManager.SpawnPlayerProjectile(
            activeAttackOriginCell,
            activeAttackDirection,
            castDamage,
            castPenetrations,
            HandleProjectileHit,
            HandleAttackCastCompleted,
            hitIndex => progressionSystem != null
                ? progressionSystem.ModifyPenetrationDamage(
                    progressionSystem.ModifyAttackDamage(
                        activeAttackDamage, activeTraitRoll, castIndex, hitIndex),
                    activeTraitRoll, hitIndex)
                : castDamage
        );

        if (!projectileCreated)
        {
            Debug.LogError("플레이어 투사체를 생성하지 못했습니다.", this);
            remainingAttackCasts = 0;
            waitingForMonsters = false;
        }
    }

    private void HandleAttackCastCompleted()
    {
        if (remainingAttackCasts > 0)
        {
            if (progressionSystem != null && progressionSystem.HasRotatingMirror
                && activeTraitRoll.DoubleCast)
            {
                waitingForMonsters = false;
                choosingEchoDirection = true;
                SetSelectionMode(PlayerActionSelectionMode.Attack);
                return;
            }
            LaunchNextAttackCast();
            return;
        }

        if (progressionSystem != null && progressionSystem.CompleteAttack(activeTraitRoll))
        {
            waitingForMonsters = false;
            kingsFreeMove = true;
            SetSelectionMode(PlayerActionSelectionMode.Move);
            if (selectableCells.Count > 0) return;
            kingsFreeMove = false;
        }
        BeginMonsterTurn();
    }

    private void HandleProjectileHit(MonsterMovement targetMonster)
    {
        Vector3Int impactCell = targetMonster != null ? targetMonster.GridPosition : GridPosition;
        // 한 행동에 여러 번 적중해도 외부 적중 알림은 한 번만 보낸다.
        if (!attackHitReported)
        {
            attackHitReported = true;
            AttackHit?.Invoke();
        }

        progressionSystem?.NotifyAttackHit(
            targetMonster, activeTraitRoll, activeAttackCastIndex, activeAttackHitIndex);
        activeAttackHitIndex++;

        MonsterMovement collision = null;
        if (activeAttackKnockback && targetMonster != null && !targetMonster.IsDead)
        {
            int distance = progressionSystem != null ? progressionSystem.KnockbackDistance : 1;
            bool moved = monsterSpawner.TryKnockbackMonster(
                targetMonster, activeAttackDirection, distance, out collision);
            if (!moved && progressionSystem != null && progressionSystem.HasRelic("iron_nail"))
                targetMonster.TakeDamage(1);
            if (collision != null && progressionSystem != null
                && progressionSystem.HasRelic("domino_crest"))
            {
                targetMonster.TakeDamage(1);
                collision.TakeDamage(1);
            }
            // 범위 피해가 아닌 실제 충돌 처치만 기록한다. 통지 중의 폭발과 구분한다.
            bool targetKilledByImpact = targetMonster.IsDead;
            bool collisionKilledByImpact = collision != null && collision.IsDead;
            if (targetKilledByImpact)
                progressionSystem?.NotifyDirectKill(targetMonster, activeTraitRoll, activeAttackCastIndex);
            if (collisionKilledByImpact)
                progressionSystem?.NotifyDirectKill(collision, activeTraitRoll, activeAttackCastIndex);
        }
        if (activeAttackKnockback && targetMonster != null)
            progressionSystem?.NotifyKnockbackResult(targetMonster, collision, impactCell);
    }

    private void UpdateFacing(Vector2Int direction)
    {
        if (direction.x < 0)
        {
            playerSprite.flipX = true;
        }
        else if (direction.x > 0)
        {
            playerSprite.flipX = false;
        }
    }

    private void ConfigureFeedbackAudio()
    {
        if (feedbackAudioSource == null)
        {
            feedbackAudioSource = gameObject.AddComponent<AudioSource>();
        }

        feedbackAudioSource.playOnAwake = false;
        feedbackAudioSource.loop = false;
        feedbackAudioSource.spatialBlend = 0f;

        if (blockedMoveSound == null)
        {
            generatedBlockedMoveSound = CreateBlockedMoveSound();
        }
    }

    private void PlayBlockedMoveFeedback()
    {
        AudioClip sound = blockedMoveSound != null
            ? blockedMoveSound
            : generatedBlockedMoveSound;

        if (feedbackAudioSource != null && sound != null)
        {
            feedbackAudioSource.PlayOneShot(sound, blockedMoveSoundVolume);
        }
    }

    private AudioClip CreateBlockedMoveSound()
    {
        int sampleCount = Mathf.CeilToInt(FeedbackSampleRate * BlockedSoundDuration);
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = (float)i / sampleCount;
            float frequency = Mathf.Lerp(190f, 90f, progress);
            float envelope = (1f - progress) * (1f - progress);

            phase += 2f * Mathf.PI * frequency / FeedbackSampleRate;
            samples[i] = (
                Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.35f
            ) * envelope * 0.3f;
        }

        AudioClip clip = AudioClip.Create(
            "GeneratedBlockedMove",
            sampleCount,
            1,
            FeedbackSampleRate,
            false
        );

        clip.hideFlags = HideFlags.DontSave;
        clip.SetData(samples, 0);
        return clip;
    }

    private void MoveOneTile()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if (transform.position == targetPosition)
        {
            transform.position = targetPosition;
            SetMoving(false);
            if (!kingsFreeMove) PlayerMoved?.Invoke();
            kingsFreeMove = false;
            waitingForMonsters = true;
            if (progressionSystem != null
                && progressionSystem.TryHandleAltarAtCell(GridPosition, BeginMonsterTurn)) return;
            BeginMonsterTurn();
        }
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;

        if (!inputEnabled)
        {
            CancelSelection();
        }
    }

    // UI와 자동 검증 모두 실제 이동/공격 진입점을 사용한다.
    public bool TryPerformAction(PlayerActionSelectionMode mode, Vector3Int direction)
    {
        if (!CanAct || mode == PlayerActionSelectionMode.None)
            return false;
        if (mode == PlayerActionSelectionMode.MovementArt)
            mode = PlayerActionSelectionMode.Move;
        SetSelectionMode(mode);
        if (selectionMode != mode) return false;
        Vector3Int destination = GridPosition + direction;
        if (!selectableCells.Contains(destination))
            return false;
        if (mode == PlayerActionSelectionMode.Move)
        {
            if (movementArtCells.Contains(destination))
                return TryUseMovementArtAtCell(destination);
            TryBeginMove(destination);
        }
        else if (choosingEchoDirection)
        {
            activeAttackDirection = direction;
            choosingEchoDirection = false;
            CancelSelection();
            waitingForMonsters = true;
            LaunchNextAttackCast();
        }
        else
            ExecuteAttack(destination);
        return true;
    }

    public void ResetPlaytest(Vector3Int startCell)
    {
        cardinalOnly = false;
        ResolveReferences();
        moving = false;
        waitingForMonsters = false;
        inputEnabled = true;
        enabled = true;
        gridPosition = startCell;
        gridPositionReady = true;
        targetPosition = gridManager.GetCellCenterWorld(startCell);
        transform.position = targetPosition;
        characterHealth.Initialize(maxHealth);
        GetComponent<PlayerDeathMarker>()?.ResetMarker();
        playerSprite.enabled = true;
        playerAnimator.enabled = true;
        SetMoving(false);
        CancelSelection();
    }

    private void BeginMonsterTurn()
    {
        if (MoveCompleted == null)
        {
            waitingForMonsters = false;
            return;
        }

        waitingForMonsters = true;
        MoveCompleted.Invoke();
    }

    public void TakeDamage(int damage, PlayerDamageKind kind = PlayerDamageKind.Normal)
    {
        if (progressionSystem != null)
            damage = progressionSystem.ModifyIncomingDamage(damage, kind);
        characterHealth.TakeDamage(damage);
    }

    public void CompleteMonsterTurn()
    {
        waitingForMonsters = false;
    }

    private void SetMoving(bool value)
    {
        moving = value;
        playerAnimator.SetBool(IsMoving, value);

        // IsMoving 변경을 즉시 평가해 이동 첫 프레임부터 상태를 전환한다.
        playerAnimator.Update(0f);
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
        {
            gridManager = FindAnyObjectByType<GridManager>();
        }

        if (monsterSpawner == null)
        {
            monsterSpawner = FindAnyObjectByType<MonsterSpawner>();
        }

        if (projectileManager == null)
        {
            projectileManager = FindAnyObjectByType<ProjectileManager>();
        }

        if (playerTraitSystem == null)
        {
            playerTraitSystem = GetComponent<PlayerTraitSystem>();
        }

        if (progressionSystem == null)
        {
            progressionSystem = GetComponent<RunProgressionSystem>();
        }

        if (actionIndicator == null)
        {
            actionIndicator = GetComponent<DirectionalActionIndicator>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;

            if (worldCamera == null)
            {
                worldCamera = FindAnyObjectByType<Camera>();
            }
        }
        if (!gridPositionReady && gridManager != null && gridManager.IsReady)
        {
            gridPosition = gridManager.WorldToCell(transform.position);
            gridPositionReady = true;
        }
    }

    private void HandleDeath(CharacterHealth defeatedCharacter)
    {
        waitingForMonsters = true;
        CancelSelection();
        SetMoving(false);
        enabled = false;
        Debug.Log("플레이어가 사망했습니다. 이동 입력을 중지합니다.", this);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        maxHealth = Mathf.Max(1, maxHealth);
        attackDamage = Mathf.Max(1, attackDamage);
        blockedMoveSoundVolume = Mathf.Clamp01(blockedMoveSoundVolume);
    }

    private void OnDestroy()
    {
        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDeath;
        }

        if (generatedBlockedMoveSound != null)
        {
            Destroy(generatedBlockedMoveSound);
        }
    }
}
