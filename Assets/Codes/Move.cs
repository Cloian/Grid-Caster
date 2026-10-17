using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField, Min(0.1f)] private float movementArtTravelSeconds = 0.4f;

    [Header("전투 설정")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int attackDamage = 1;

    [Header("공격 결과 확인")]
    [SerializeField, Range(0f, 0.2f)] private float attackRecoverySeconds = 0.08f;

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
    private Coroutine pendingAttackRelease;
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
    private readonly List<Vector3> attackAreaWorldPositions = new List<Vector3>(8);
    private Vector3 targetPosition;
    private Vector3Int hoveredAttackCell = new Vector3Int(int.MinValue, int.MinValue, 0);
    private PlayerActionSelectionMode selectionMode;
    private bool moving;
    private bool movementArtInProgress;
    private Vector3Int movementArtOriginCell;
    private float movementArtTravelSpeed;
    private bool waitingForMonsters;
    private bool inputEnabled = true;
    private Vector3Int activeAttackOriginCell;
    private Vector3Int activeAttackDirection;
    private int activeAttackDamage;
    private readonly Dictionary<MonsterMovement, int> attackPrediction = new Dictionary<MonsterMovement, int>();
    // 주도권: 공격 한 번(연속 시전·범위·충돌 포함) 동안 피해를 준 적과 처치를 모은다.
    private InitiativeTracker initiative;
    private readonly HashSet<MonsterMovement> attackDamagedMonsters = new HashSet<MonsterMovement>();
    private readonly HashSet<MonsterMovement> attackKilledMonsters = new HashSet<MonsterMovement>();
    private bool collectingAttackResult;
    private bool attackHitAdjacent;
    public InitiativeTracker Initiative => initiative;
    // 조준 중인 공격이 각 적에게 줄 예상 피해(직격·관통·연속 시전·강화 폭발 합계). 조준하지 않으면 비어 있다.
    public IReadOnlyDictionary<MonsterMovement, int> AttackPrediction => attackPrediction;
    private int activeAttackPenetrations;
    private int remainingAttackCasts;
    private bool activeAttackKnockback;
    private bool activeAttackKnightCounter;
    private bool attackHitReported;
    private PlayerAttackTraitRoll activeTraitRoll;
    private int activeAttackCastIndex;
    private int activeAttackHitIndex;
    private bool choosingEchoDirection;
    private bool kingsFreeMove;
    private bool traitAttackSelected;
    private bool activeAttackUsedTrait;
    private bool attackRecoveryPending;
    private float attackRecoveryRemaining;
    private bool recoveryGrantsFreeMove;
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
    public bool IsTraitAttackSelected => traitAttackSelected;
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
        if (GetComponent<EnemyThreatPreview>() == null)
            gameObject.AddComponent<EnemyThreatPreview>();
        initiative = GetComponent<InitiativeTracker>();
        CharacterHealth.AnyDamaged += CollectAttackDamage;

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
        CharacterShadow.Attach(gameObject);
    }

    private void Update()
    {
        // 상세 설명은 턴을 소비하지 않으며 기존 공격/이동 선택도 보존한다.
        if (inspectionOpen) return;
        if (attackRecoveryPending)
        {
            if (!inputEnabled || Time.deltaTime <= 0f) return;
            attackRecoveryRemaining = Mathf.Max(0f, attackRecoveryRemaining - Time.deltaTime);
            if (attackRecoveryRemaining <= 0f && !CombatFeedback.HasPendingDisplacement)
            {
                attackRecoveryPending = false;
                FinishAttackTransition(recoveryGrantsFreeMove);
            }
            return;
        }
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

        // 모바일은 탭 미리보기와 실행 버튼을 분리한다. 마우스 에뮬레이션의 중복 행동을 막는다.
        if (MobilePlaytestController.IsActive) return;

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        ProcessInputFrame(
            keyboard != null && keyboard.aKey.wasPressedThisFrame,
            keyboard != null && keyboard.sKey.wasPressedThisFrame,
            mouse != null && mouse.leftButton.wasPressedThisFrame,
            mouse != null && mouse.rightButton.wasPressedThisFrame,
            mouse != null ? mouse.position.ReadValue() : Vector2.zero,
            false,
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
        bool movementArtPressed = false, bool traitAttackPressed = false)
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

        if (kingsFreeMove)
        {
            if (cancelPressed) SkipFreeMove();
            else if (confirmPressed) TryConfirmSelectedCell(pointerScreenPosition);
            return;
        }
        if (traitAttackPressed) SelectTraitAttackMode();
        else if (attackPressed)
        {
            if (traitAttackSelected) SelectAttackMode();
            else ToggleSelectionMode(PlayerActionSelectionMode.Attack);
        }
        if (movePressed) ToggleSelectionMode(PlayerActionSelectionMode.Move);
        // 이동술은 S 선택지에 통합하고, F는 준비된 특성 공격만 선택한다.
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
        if (kingsFreeMove || choosingEchoDirection) return;
        traitAttackSelected = false;
        SetSelectionMode(PlayerActionSelectionMode.Attack);
    }

    public bool SelectTraitAttackMode()
    {
        ResolveReferences();
        if (!CanAct || kingsFreeMove || choosingEchoDirection
            || playerTraitSystem == null || !playerTraitSystem.IsReady) return false;
        traitAttackSelected = true;
        SetSelectionMode(PlayerActionSelectionMode.Attack);
        return selectionMode == PlayerActionSelectionMode.Attack;
    }

    public bool SkipFreeMove()
    {
        if (!CanAct || !kingsFreeMove) return false;
        kingsFreeMove = false;
        CancelSelection();
        BeginMonsterTurn();
        return true;
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
        traitAttackSelected = false;
        if (selectionMode == PlayerActionSelectionMode.None)
            return;

        selectionMode = PlayerActionSelectionMode.None;
        selectableCells.Clear();
        movementArtCells.Clear();
        selectableWorldPositions.Clear();
        emphasizedChoices.Clear();
        actionIndicator?.ClearChoices();
        hoveredAttackCell = new Vector3Int(int.MinValue, int.MinValue, 0);
        attackPrediction.Clear();
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
        if (kingsFreeMove && requestedMode != PlayerActionSelectionMode.Move) return;
        if (requestedMode != PlayerActionSelectionMode.Attack) traitAttackSelected = false;

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
        attackAreaWorldPositions.Clear();

        Vector3Int currentCell = GridPosition;
        Vector3Int direction = directionCell - currentCell;
        int emphasizedCellIndex = -1;
        PlayerAttackTraitRoll previewEffect = choosingEchoDirection ? activeTraitRoll
            : playerTraitSystem != null ? playerTraitSystem.PreviewAttack(traitAttackSelected) : default;
        int remainingPenetrations = progressionSystem != null
            ? progressionSystem.ModifyPenetrations(previewEffect) : previewEffect.Pierce ? 1 : 0;
        int conditionalStartIndex = -1;

        // 첫 몬스터까지 표시하되, 공격이 소멸하는 바깥 경계 벽 칸은 미리보기에서 제외한다.
        for (int segment = 0; segment <= remainingPenetrations; segment++)
        {
            Vector3Int impact = TurnProjectile.FindImpact(gridManager, monsterSpawner,
                currentCell, direction, out MonsterMovement target);
            if (impact == currentCell) break;
            for (Vector3Int cell = currentCell + direction;
                cell != impact + direction; cell += direction)
                if (gridManager.IsWalkableCell(cell))
                    attackPathWorldPositions.Add(gridManager.GetCellCenterWorld(cell));
            if (target != null)
            {
                if (segment == 0)
                {
                    emphasizedCellIndex = attackPathWorldPositions.Count - 1;
                    conditionalStartIndex = attackPathWorldPositions.Count;
                    if (previewEffect.Knockback && progressionSystem != null && progressionSystem.HasKnockbackSpread)
                        foreach (Vector3Int offset in DamageBoostArea.Offsets(0))
                        {
                            Vector3Int cell = impact + offset;
                            if (cell != GridPosition && gridManager.IsWalkableCell(cell))
                                attackAreaWorldPositions.Add(gridManager.GetCellCenterWorld(cell));
                        }
                    if (previewEffect.DamageBoost)
                        foreach (Vector3Int offset in DamageBoostArea.Offsets(
                            progressionSystem != null ? progressionSystem.DamageBoostAreaLevel : 0))
                        {
                            Vector3Int cell = impact + offset;
                            if (cell != GridPosition && (gridManager.IsWalkableCell(cell)
                                || monsterSpawner.TryGetMonsterAtCell(cell, out _)))
                                attackAreaWorldPositions.Add(gridManager.GetCellCenterWorld(cell));
                        }
                }
                currentCell = impact;
            }
            else break;
            if (!gridManager.IsWalkableCell(impact)) break;
        }

        actionIndicator.ShowAttackPath(
            attackPathWorldPositions,
            emphasizedCellIndex,
            conditionalStartIndex
        );
        actionIndicator.ShowAttackArea(attackAreaWorldPositions);
        PredictAttack(direction, previewEffect);
    }

    // 실제 공격 규칙을 상태 변경 없이 따라간다. 죽을 적은 다음 시전·관통에서 제외한다.
    // 넉백 충돌·유물 연쇄처럼 맞은 뒤 위치에 따라 달라지는 피해는 포함하지 않는다.
    private void PredictAttack(Vector3Int direction, PlayerAttackTraitRoll roll)
    {
        attackPrediction.Clear();
        if (monsterSpawner == null || gridManager == null || direction == Vector3Int.zero) return;
        var health = new Dictionary<MonsterMovement, int>();
        int Hp(MonsterMovement monster)
        {
            if (!health.TryGetValue(monster, out int value))
                health[monster] = value = monster.GetComponent<CharacterHealth>().CurrentHealth;
            return value;
        }
        void Hit(MonsterMovement monster, int damage)
        {
            if (damage <= 0 || Hp(monster) <= 0) return;
            health[monster] = Hp(monster) - damage;
            attackPrediction.TryGetValue(monster, out int total);
            attackPrediction[monster] = total + damage;
        }

        int casts = progressionSystem != null ? progressionSystem.AttackCastCount(roll) : roll.DoubleCast ? 2 : 1;
        int penetrations = progressionSystem != null ? progressionSystem.ModifyPenetrations(roll) : roll.Pierce ? 1 : 0;
        Vector3Int step = new Vector3Int(Mathf.Clamp(direction.x, -1, 1), Mathf.Clamp(direction.y, -1, 1), 0);
        bool firstHit = true;
        bool firstCastKilled = false;
        for (int cast = 0; cast < casts; cast++)
        {
            Vector3Int origin = GridPosition;
            MonsterMovement previous = null;
            for (int hitIndex = 0; hitIndex <= penetrations; hitIndex++)
            {
                MonsterMovement target = null;
                Vector3Int cell = origin;
                while (gridManager.IsInsideMapCell(cell + step))
                {
                    cell += step;
                    if (monsterSpawner.TryGetMonsterAtCell(cell, out MonsterMovement candidate)
                        && candidate != previous && !candidate.IsDead && Hp(candidate) > 0)
                    { target = candidate; break; }
                    if (!gridManager.IsWalkableCell(cell)) break;
                }
                if (target == null) break;

                int damage = progressionSystem != null
                    ? progressionSystem.PreviewAttackDamage(attackDamage, roll, cast, hitIndex, firstHit, firstCastKilled)
                    : attackDamage + (roll.DamageBoost ? 1 : 0);
                bool counter = cast == 0 && hitIndex == 0
                    && Mathf.Max(Mathf.Abs(cell.x - GridPosition.x), Mathf.Abs(cell.y - GridPosition.y)) == 1
                    && target.TryGetComponent(out ChessMonsterBehaviour pattern) && pattern.IsKnightCounterVulnerable;
                Hit(target, damage + (counter ? 1 : 0));
                bool killed = Hp(target) <= 0;
                if (cast == 0 && killed) firstCastKilled = true;

                // 특성 강화 공격은 첫 직격 위치에서 한 번 폭발한다(RunProgressionSystem.NotifyAttackHit와 같은 범위).
                if (firstHit && roll.DamageBoost && progressionSystem != null)
                {
                    int splash = progressionSystem.DamageBoostSplashDamageFor(killed);
                    int level = progressionSystem.DamageBoostAreaLevel;
                    foreach (MonsterMovement other in monsterSpawner.ActiveMonsters)
                        if (other != null && other != target && !other.IsDead
                            && monsterSpawner.OccupiedCellsOf(other).Any(c => DamageBoostArea.Contains(c - cell, level)))
                            Hit(other, splash);
                }
                firstHit = false;
                previous = target;
                origin = cell;
                if (!gridManager.IsWalkableCell(cell)) break;
            }
        }
    }

    private void CollectAttackDamage(CharacterHealth health, int damage)
    {
        if (!collectingAttackResult || health == null || damage <= 0) return;
        if (!health.TryGetComponent(out MonsterMovement monster)) return;
        attackDamagedMonsters.Add(monster);
        if (health.IsDead) attackKilledMonsters.Add(monster);
    }

    private void ClearAttackPathPreview()
    {
        attackPrediction.Clear();
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
            && progressionSystem.HasMovementArt
            && (initiative == null || initiative.CanAffordMovementArt))
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

        return ConfirmPreviewCell(selectedCell);
    }

    public bool TryPreviewBoardPoint(Vector2 screenPoint, out Vector3Int choice)
    {
        choice = default;
        ResolveReferences();
        if (!CanAct || selectionMode == PlayerActionSelectionMode.None
            || worldCamera == null || gridManager == null
            || !worldCamera.pixelRect.Contains(screenPoint)) return false;
        Ray ray = worldCamera.ScreenPointToRay(screenPoint);
        Plane plane = new Plane(gridManager.GroundTilemap.transform.forward,
            gridManager.GetCellCenterWorld(GridPosition));
        if (!plane.Raycast(ray, out float distance)) return false;
        choice = gridManager.WorldToCell(ray.GetPoint(distance));
        if (selectionMode == PlayerActionSelectionMode.Attack)
        {
            Vector3Int delta = choice - GridPosition;
            if (delta == Vector3Int.zero) return false;
            // 8방향 중 가장 가까운 방향을 선택한다. 작은 화살표 대신 보드 전체를 조준에 사용한다.
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            int sector = Mathf.RoundToInt(angle / 45f);
            float radians = sector * Mathf.PI / 4f;
            choice = GridPosition + new Vector3Int(Mathf.RoundToInt(Mathf.Cos(radians)),
                Mathf.RoundToInt(Mathf.Sin(radians)), 0);
        }
        if (!selectableCells.Contains(choice)) return false;
        if (selectionMode == PlayerActionSelectionMode.Attack) ShowAttackPathPreview(choice);
        return true;
    }

    // UI 실행 버튼은 포인터의 UI 점유와 무관하게 이미 미리 본 칸만 확정한다.
    // 목적지 점유·이동술 경로·턴 상태는 기존 실행 경로에서 다시 검사한다.
    public bool ConfirmPreviewCell(Vector3Int selectedCell)
    {
        if (!CanAct || selectionMode == PlayerActionSelectionMode.None
            || !selectableCells.Contains(selectedCell))
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
                progressionSystem?.NotifyEchoDirectionSelected(selectedCell - GridPosition != activeAttackDirection);
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
        if (!CanReachCellFrom(GridPosition, cell)
            || !gridManager.IsWalkableCell(cell)
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
            || kingsFreeMove
            || !selectableCells.Contains(destinationCell)
            || !movementArtCells.Contains(destinationCell)
            || destinationCell == GridPosition
            || !gridManager.IsWalkableCell(destinationCell)
            || monsterSpawner.TryGetMonsterAtCell(destinationCell, out _)
            || progressionSystem == null
            || !progressionSystem.HasMovementArt
            || (initiative != null && !initiative.CanAffordMovementArt))
            return false;

        // 목적지가 비어 있어도 중간 점유가 바뀌었으면 비숍/룩은 통과할 수 없다.
        if (progressionSystem.ActiveMovementArt != PlayerMovementArt.Knight)
        {
            Vector3Int delta = destinationCell - GridPosition;
            Vector3Int step = new Vector3Int(Math.Sign(delta.x), Math.Sign(delta.y), 0);
            for (Vector3Int cell = GridPosition + step; cell != destinationCell; cell += step)
                if (!gridManager.IsWalkableCell(cell)
                    || monsterSpawner.TryGetMonsterAtCell(cell, out _)) return false;
        }

        // 경로 검사를 모두 통과한 뒤에만 주도권을 쓴다.
        if (initiative != null && !initiative.TrySpendMovementArt()) return false;
        monsterSpawner.CloseKnightCounterWindows();
        Vector3Int originCell = gridPosition;
        Vector3Int difference = destinationCell - gridPosition;
        UpdateFacing(new Vector2Int(difference.x, difference.y));
        gridPosition = destinationCell;
        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        // 목적지 판정은 타일 좌표로 확정하고, 이동 연출 동안 적 턴과 추가 입력을 막는다.
        movementArtOriginCell = originCell;
        movementArtTravelSpeed = Vector3.Distance(transform.position, targetPosition)
            / Mathf.Max(0.1f, movementArtTravelSeconds);
        movementArtInProgress = true;
        CancelSelection();
        progressionSystem.NotifyMoveAction();
        SetMoving(true);
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

        monsterSpawner.CloseKnightCounterWindows();
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
        movementArtInProgress = false;
        SetMoving(true);
    }

    private readonly HashSet<int> collisionDamagedTargets = new HashSet<int>();

    private void ExecuteAttack(Vector3Int attackCell)
    {
        if (kingsFreeMove) return;
        Vector3Int currentCell = GridPosition;
        Vector3Int difference = attackCell - currentCell;
        UpdateFacing(new Vector2Int(difference.x, difference.y));

        PlayerAttackTraitRoll traitRoll = default;
        activeAttackUsedTrait = traitAttackSelected;
        if (playerTraitSystem != null
            && !playerTraitSystem.TryBeginAttack(activeAttackUsedTrait, out traitRoll)) return;

        // 인접한 첫 직격 대상만 확정 시 저장한다. 추가 시전에는 보너스를 반복하지 않는다.
        activeAttackKnightCounter = Mathf.Max(Mathf.Abs(difference.x), Mathf.Abs(difference.y)) == 1
            && monsterSpawner.TryGetMonsterAtCell(attackCell, out MonsterMovement counterTarget)
            && counterTarget.TryGetComponent(out ChessMonsterBehaviour counterPattern)
            && counterPattern.IsKnightCounterVulnerable;
        monsterSpawner.CloseKnightCounterWindows();
        collisionDamagedTargets.Clear();
        CombatFeedback.BeginPlayerAttack(traitRoll.DamageBoost);
        progressionSystem?.BeginAttack(traitRoll);
        activeTraitRoll = traitRoll;
        attackDamagedMonsters.Clear();
        attackKilledMonsters.Clear();
        attackHitAdjacent = false;
        collectingAttackResult = true;
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
        attackRecoveryPending = false;
        attackHitReported = false;
        activeAttackCastIndex = -1;

        CancelSelection();
        ActorMotion.Trigger(this, ActorMotion.Attack);
        // 플레이어 투사체의 전체 경로 비행이 끝날 때까지 추가 입력과 적 턴을 막는다.
        waitingForMonsters = true;

        // 공격 모션이 지팡이를 내지르는 프레임에서 투사체를 낸다.
        float windup = ActorMotion.AttackReleaseSeconds(this);
        if (windup > 0f) pendingAttackRelease = StartCoroutine(LaunchAfterWindup(windup));
        else LaunchNextAttackCast();
    }

    public float AttackWindupSeconds => ActorMotion.AttackReleaseSeconds(this);

    private IEnumerator LaunchAfterWindup(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        pendingAttackRelease = null;
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
        int castDamage = DirectAttackDamage(castIndex, 0);
        int castPenetrations = activeAttackPenetrations;

        bool projectileCreated = projectileManager.SpawnPlayerProjectile(
            activeAttackOriginCell,
            activeAttackDirection,
            castDamage,
            castPenetrations,
            HandleProjectileHit,
            HandleAttackCastCompleted,
            hitIndex => DirectAttackDamage(castIndex, hitIndex),
            activeTraitRoll.DamageBoost
        );

        if (projectileCreated)
            GameAudio.Play(CombatSound.MagicLaunch);
        else
        {
            Debug.LogError("플레이어 투사체를 생성하지 못했습니다.", this);
            CombatFeedback.ResetAttackOutcome();
            remainingAttackCasts = 0;
            waitingForMonsters = false;
        }
    }

    private int DirectAttackDamage(int castIndex, int hitIndex)
    {
        int damage = progressionSystem != null
            ? progressionSystem.ModifyPenetrationDamage(
                progressionSystem.ModifyAttackDamage(
                    activeAttackDamage, activeTraitRoll, castIndex, hitIndex),
                activeTraitRoll, hitIndex)
            : activeAttackDamage + (activeTraitRoll.DamageBoost ? 1 : 0);
        return damage + (activeAttackKnightCounter && castIndex == 0 && hitIndex == 0 ? 1 : 0);
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

        CombatFeedback.CompletePlayerAttack();
        if (collectingAttackResult)
        {
            collectingAttackResult = false;
            initiative?.RegisterAttack(attackDamagedMonsters.Count, attackKilledMonsters.Count, attackHitAdjacent);
        }
        playerTraitSystem?.RegisterCompletedAttack(attackHitReported, activeAttackUsedTrait);
        bool grantsFreeMove = progressionSystem != null && progressionSystem.CompleteAttack(activeTraitRoll);
        if (characterHealth.IsDead) return;
        if (attackHitReported || CombatFeedback.HasPendingDisplacement)
        {
            waitingForMonsters = true;
            recoveryGrantsFreeMove = grantsFreeMove;
            attackRecoveryRemaining = attackRecoverySeconds;
            attackRecoveryPending = true;
            return;
        }
        FinishAttackTransition(grantsFreeMove);
    }

    private void FinishAttackTransition(bool grantsFreeMove)
    {
        if (characterHealth.IsDead) return;
        if (grantsFreeMove)
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
        Vector3Int impactCell = targetMonster != null
            ? monsterSpawner.ImpactCellOf(targetMonster, activeAttackOriginCell, activeAttackDirection) : GridPosition;
        // 한 행동에 여러 번 적중해도 외부 적중 알림은 한 번만 보낸다.
        if (!attackHitReported)
        {
            attackHitReported = true;
            AttackHit?.Invoke();
        }

        if (targetMonster != null && Mathf.Max(Mathf.Abs(impactCell.x - activeAttackOriginCell.x),
                Mathf.Abs(impactCell.y - activeAttackOriginCell.y)) == 1)
            attackHitAdjacent = true;
        if (activeAttackKnightCounter && activeAttackCastIndex == 0 && activeAttackHitIndex == 0)
            CombatFeedback.NoteCounter();
        progressionSystem?.NotifyAttackHit(
            targetMonster, activeTraitRoll, activeAttackCastIndex, activeAttackHitIndex, impactCell);
        activeAttackHitIndex++;

        if (!activeAttackKnockback || targetMonster == null) return;
        // 원래 적중 위치와 대상 목록을 먼저 고정한다. 직접 대상이 죽거나 밀려도 중심은 바뀌지 않는다.
        var surrounding = new List<(MonsterMovement Monster, Vector3Int Direction)>();
        if (progressionSystem != null && progressionSystem.HasKnockbackSpread)
            foreach (MonsterMovement monster in monsterSpawner.ActiveMonsters)
            {
                if (monster == null || monster == targetMonster || monster.IsDead
                    || monster.MovementPattern == MonsterMovementPattern.Rook) continue;
                Vector3Int offset = monster.GridPosition - impactCell;
                if (Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) == 1)
                    surrounding.Add((monster, offset));
            }
        if (!targetMonster.IsDead)
            ResolveKnockback(targetMonster, activeAttackDirection,
                progressionSystem != null ? progressionSystem.KnockbackDistance : 1, true);
        // 각 바깥 방향은 하나의 인접 칸으로 결정된다. 동시 이동 예약 대신 고정 목록의 각 적을 한 번만 민다.
        foreach (var entry in surrounding)
            if (entry.Monster != null && !entry.Monster.IsDead)
                ResolveKnockback(entry.Monster, entry.Direction, 1, false);
    }

    private void ResolveKnockback(MonsterMovement target, Vector3Int direction, int distance, bool direct)
    {
        Vector3Int impact = target.GridPosition;
        bool moved = monsterSpawner.TryKnockbackMonster(target, direction, distance,
            out MonsterMovement collision, out KnockbackBlockKind block);
        bool actualCollision = block == KnockbackBlockKind.Wall || block == KnockbackBlockKind.Monster;
        bool ironNail = !moved && progressionSystem != null && progressionSystem.HasRelic("iron_nail")
            && (actualCollision || block == KnockbackBlockKind.Immovable);
        int domino = collision != null && progressionSystem != null
            && progressionSystem.HasRelic("domino_crest") ? 1 : 0;
        float delay = moved ? CombatFeedback.KnockbackSeconds : 0f;
        if (actualCollision)
        {
            CombatFeedback.NoteCollision();
            DamageKnockbackCollision(target, direction, 1 + domino + (ironNail ? 1 : 0), delay);
            int mastery = progressionSystem != null ? progressionSystem.Stack("trait_mastery") : 0;
            DamageKnockbackCollision(collision, direction, 1 + domino + (mastery > 0 ? 1 : 0), delay);
        }
        else if (ironNail) DamageKnockbackCollision(target, direction, 1, 0f);
        if (!direct) return;
        progressionSystem?.NotifyKnockbackResult(target, collision, impact, block != KnockbackBlockKind.None);
        if (target.IsDead) progressionSystem?.NotifyDirectKill(target, activeTraitRoll, activeAttackCastIndex);
        if (collision != null && collision.IsDead)
            progressionSystem?.NotifyDirectKill(collision, activeTraitRoll, activeAttackCastIndex);
    }

    private void DamageKnockbackCollision(MonsterMovement target, Vector3Int direction, int damage, float delay)
    {
        if (target == null || target.IsDead || !collisionDamagedTargets.Add(target.GetInstanceID())) return;
        float previousDelay = CombatFeedback.DamageVisualDelay;
        CombatFeedback.DamageVisualDelay = delay;
        try { target.TakeDamage(damage, direction); }
        finally { CombatFeedback.DamageVisualDelay = previousDelay; }
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
            GameAudio.PlayBlockedMove(sound, blockedMoveSoundVolume);
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
            (movementArtInProgress ? movementArtTravelSpeed : moveSpeed) * Time.deltaTime
        );

        if (transform.position == targetPosition)
        {
            transform.position = targetPosition;
            SetMoving(false);
            if (movementArtInProgress)
            {
                // 착지 효과와 제단 획득은 도착 시 한 번만 처리한다. 경유 칸은 판정하지 않는다.
                movementArtInProgress = false;
                ClearMovementArtFire(movementArtOriginCell, GridPosition);
                progressionSystem.NotifyMovementArtUsed();
            }
            if (!kingsFreeMove) PlayerMoved?.Invoke();
            else monsterSpawner?.FireTrail?.ResolvePlayerEntry();
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

    public bool TryWaitForPlaytest()
    {
        if (!CanAct || choosingEchoDirection || kingsFreeMove) return false;
        CancelSelection();
        monsterSpawner.CloseKnightCounterWindows();
        BeginMonsterTurn();
        return true;
    }

    public void ConfigurePlaytestCombat(int health, int damage, Vector3Int startCell)
    {
        maxHealth = Mathf.Max(1, health);
        attackDamage = Mathf.Max(1, damage);
        ResetPlaytest(startCell);
    }

    // 체크메이트 생성기는 입력·점유를 바꾸지 않고 실제 이동술의 도달 규칙을 읽는다.
    public bool CanReachCellFrom(Vector3Int origin, Vector3Int destination)
    {
        ResolveReferences();
        if (gridManager == null || monsterSpawner == null || !gridManager.IsWalkableCell(destination)
            || monsterSpawner.IsOccupied(destination)) return false;
        Vector3Int delta = destination - origin;
        int x = Mathf.Abs(delta.x), y = Mathf.Abs(delta.y), distance = Mathf.Max(x, y);
        if (distance == 0) return false;
        if (distance == 1) return !cardinalOnly || x == 0 || y == 0;
        if (progressionSystem == null || !progressionSystem.HasMovementArt) return false;
        PlayerMovementArt art = progressionSystem.ActiveMovementArt;
        if (art == PlayerMovementArt.Knight) return x * y == 2;
        bool aligned = art == PlayerMovementArt.Bishop ? x == y : x == 0 || y == 0;
        int range = (art == PlayerMovementArt.Bishop ? 2 : 3) + progressionSystem.MovementArtLevel;
        if (!aligned || distance > range) return false;
        Vector3Int step = new Vector3Int(Math.Sign(delta.x), Math.Sign(delta.y), 0);
        for (Vector3Int cell = origin + step; cell != destination; cell += step)
            if (!gridManager.IsWalkableCell(cell) || monsterSpawner.IsOccupied(cell)) return false;
        return true;
    }

    // UI와 자동 검증 모두 실제 이동/공격 진입점을 사용한다.
    public bool TryPerformAction(PlayerActionSelectionMode mode, Vector3Int direction)
    {
        if (!CanAct || mode == PlayerActionSelectionMode.None)
            return false;
        if (kingsFreeMove && mode != PlayerActionSelectionMode.Move) return false;
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
            progressionSystem?.NotifyEchoDirectionSelected(direction != activeAttackDirection);
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
        movementArtInProgress = false;
        waitingForMonsters = false;
        kingsFreeMove = false;
        choosingEchoDirection = false;
        activeAttackUsedTrait = false;
        activeAttackKnightCounter = false;
        attackRecoveryPending = false;
        attackRecoveryRemaining = 0f;
        recoveryGrantsFreeMove = false;
        inputEnabled = true;
        enabled = true;
        gridPosition = startCell;
        gridPositionReady = true;
        targetPosition = gridManager.GetCellCenterWorld(startCell);
        transform.position = targetPosition;
        characterHealth.Initialize(maxHealth);
        GetComponent<PlayerDeathMarker>()?.ResetMarker();
        if (pendingAttackRelease != null)
        {
            StopCoroutine(pendingAttackRelease);
            pendingAttackRelease = null;
        }
        playerSprite.enabled = true;
        playerAnimator.enabled = true;
        playerAnimator.Rebind();
        SetMoving(false);
        CancelSelection();
    }

    private void BeginMonsterTurn()
    {
        if (characterHealth != null && characterHealth.IsDead) return;
        if (MoveCompleted == null)
        {
            waitingForMonsters = false;
            return;
        }

        waitingForMonsters = true;
        MoveCompleted.Invoke();
    }

    public void TakeDamage(int damage, PlayerDamageKind kind = PlayerDamageKind.Normal, Vector3Int incomingDirection = default)
    {
        if (progressionSystem != null)
            damage = progressionSystem.ModifyIncomingDamage(damage, kind);
        int healthBefore = characterHealth.CurrentHealth;
        characterHealth.TakeDamage(damage, incomingDirection);
        if (characterHealth.CurrentHealth < healthBefore)
            GameAudio.Play(CombatSound.PlayerHurt);
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
        CombatFeedback.ResetAttackOutcome();
        attackRecoveryPending = false;
        playerTraitSystem?.ResetCharge();
        kingsFreeMove = false;
        choosingEchoDirection = false;
        waitingForMonsters = true;
        CancelSelection();
        movementArtInProgress = false;
        SetMoving(false);
        enabled = false;
        Debug.Log("플레이어가 사망했습니다. 이동 입력을 중지합니다.", this);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        movementArtTravelSeconds = Mathf.Max(0.1f, movementArtTravelSeconds);
        maxHealth = Mathf.Max(1, maxHealth);
        attackDamage = Mathf.Max(1, attackDamage);
        blockedMoveSoundVolume = Mathf.Clamp01(blockedMoveSoundVolume);
    }

    private void OnDestroy()
    {
        CharacterHealth.AnyDamaged -= CollectAttackDamage;
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
