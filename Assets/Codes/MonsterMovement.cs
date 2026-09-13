using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CharacterHealth))]
public sealed class MonsterMovement : MonoBehaviour
{
    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down
    };

    private static readonly Vector3Int[] EightDirections =
    {
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(-1, -1, 0),
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down
    };

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool spriteFacesRight = true;
    [SerializeField] private MonsterMovementPattern movementPattern =
        MonsterMovementPattern.CardinalFour;

    [Header("전투 설정")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int attackDamage = 1;

    private Transform player;
    private Move playerMovement;
    private GridManager gridManager;
    private SpriteRenderer monsterSprite;
    private CharacterHealth characterHealth;
    private Vector3 targetPosition;
    private bool moving;
    private Action moveCompleted;
    private Action landingAction;
    private Func<Vector3Int, Vector3Int, bool> tryReserveCell;
    private Func<Vector3Int, bool> isCellBlocked;
    private Vector3Int gridPosition;
    private Vector3Int lastStep;
    private ChessMonsterBehaviour chessBehaviour;
    private Vector3 jumpOrigin;
    private float jumpProgress;
    private const float KnightJumpSeconds = 0.24f;

    public bool IsDead => characterHealth != null && characterHealth.IsDead;
    public MonsterMovementPattern MovementPattern => movementPattern;
    public Vector3Int GridPosition => gridPosition;
    public int SpawnOrder { get; private set; }

    private void Awake()
    {
        monsterSprite = GetComponent<SpriteRenderer>();
        characterHealth = GetComponent<CharacterHealth>();

        if (characterHealth == null)
        {
            characterHealth = gameObject.AddComponent<CharacterHealth>();
        }

        characterHealth.Initialize(maxHealth);
        characterHealth.Died += HandleDeath;
        targetPosition = transform.position;
        chessBehaviour = GetComponent<ChessMonsterBehaviour>();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        UpdateFacingToPlayer();

        if (moving)
        {
            MoveOneTile();
        }
    }

    public void Initialize(Transform targetPlayer, GridManager targetGridManager, int spawnOrder = 0)
    {
        SpawnOrder = spawnOrder;
        player = targetPlayer;
        playerMovement = player != null ? player.GetComponent<Move>() : null;
        gridManager = targetGridManager;
        gridPosition = gridManager.WorldToCell(transform.position);
        UpdateFacingToPlayer();
    }

    public void ConfigurePlaytest(MonsterMovementPattern pattern, int health, int order)
    {
        movementPattern = pattern;
        maxHealth = Mathf.Max(1, health);
        attackDamage = 1;
        moveSpeed = pattern == MonsterMovementPattern.Bishop ? 18f : 9f;
        SpawnOrder = order;
        characterHealth.Initialize(maxHealth);
        chessBehaviour = GetComponent<ChessMonsterBehaviour>();
    }

    public void TakeTurn(
        Action onMoveCompleted,
        Func<Vector3Int, Vector3Int, bool> reserveDestination,
        Func<Vector3Int, bool> checkCellBlocked
    )
    {
        if (moving)
        {
            return;
        }

        moveCompleted = onMoveCompleted;
        tryReserveCell = reserveDestination;
        isCellBlocked = checkCellBlocked;

        if (IsDead || playerMovement == null || gridManager == null || !gridManager.IsReady)
        {
            FinishTurn();
            return;
        }

        UpdateFacingToPlayer();
        if (chessBehaviour != null)
            chessBehaviour.TakeTurn(playerMovement.GridPosition, checkCellBlocked);
        else
            TryBeginChaseStep();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            Move playerController = FindAnyObjectByType<Move>();

            if (playerController != null)
            {
                player = playerController.transform;
            }
        }

        if (playerMovement == null && player != null)
        {
            playerMovement = player.GetComponent<Move>();
        }

        if (gridManager == null)
        {
            gridManager = FindAnyObjectByType<GridManager>();
        }

        UpdateFacingToPlayer();
    }

    private void TryBeginChaseStep()
    {
        Vector3Int currentCell = gridPosition;
        Vector3Int playerCell = playerMovement.GridPosition;
        Vector3Int difference = playerCell - currentCell;

        if (IsPlayerInAttackRange(difference))
        {
            AttackPlayer();
            return;
        }

        if (!TryFindNextPathCell(currentCell, playerCell, out Vector3Int nextCell))
        {
            FinishTurn();
            return;
        }

        if (!TryBeginMove(currentCell, nextCell))
        {
            FinishTurn();
        }
    }

    private bool IsPlayerInAttackRange(Vector3Int difference)
    {
        int horizontalDistance = Mathf.Abs(difference.x);
        int verticalDistance = Mathf.Abs(difference.y);

        if (movementPattern == MonsterMovementPattern.EightDirection)
        {
            // 비행형은 대각선으로 맞닿은 플레이어도 공격할 수 있다.
            return Mathf.Max(horizontalDistance, verticalDistance) <= 1
                && CanCrossCorner(gridPosition, difference);
        }

        return horizontalDistance + verticalDistance <= 1;
    }

    private bool TryFindNextPathCell(
        Vector3Int startCell,
        Vector3Int playerCell,
        out Vector3Int nextCell
    )
    {
        nextCell = startCell;
        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom =
            new Dictionary<Vector3Int, Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> firstSteps = new Dictionary<Vector3Int, Vector3Int>();

        frontier.Enqueue(startCell);
        cameFrom.Add(startCell, startCell);

        Vector3Int[] directions = movementPattern == MonsterMovementPattern.EightDirection
            ? (Vector3Int[])EightDirections.Clone()
            : (Vector3Int[])CardinalDirections.Clone();
        // BFS의 최단 거리 보장은 유지하고, 같은 길이일 때만 진행 방향을 유지한다.
        Array.Sort(directions, (a, b) =>
        {
            int comparison = PathDistance(startCell + a, playerCell)
                .CompareTo(PathDistance(startCell + b, playerCell));
            if (comparison != 0) return comparison;
            comparison = (b == lastStep).CompareTo(a == lastStep);
            if (comparison != 0) return comparison;
            comparison = a.x.CompareTo(b.x);
            return comparison != 0 ? comparison : a.y.CompareTo(b.y);
        });
        Vector3Int closestCell = startCell;
        int closestDistance = PathDistance(startCell, playerCell);

        while (frontier.Count > 0)
        {
            Vector3Int currentCell = frontier.Dequeue();

            foreach (Vector3Int direction in directions)
            {
                Vector3Int candidateCell = currentCell + direction;

                if (cameFrom.ContainsKey(candidateCell)
                    || !gridManager.IsWalkableCell(candidateCell)
                    || !CanCrossCorner(currentCell, direction))
                {
                    continue;
                }

                // 플레이어 타일은 탐색 목표이므로 몬스터 점유 검사에서 제외한다.
                if (candidateCell != playerCell
                    && isCellBlocked != null
                    && isCellBlocked(candidateCell))
                {
                    continue;
                }

                cameFrom.Add(candidateCell, currentCell);
                firstSteps[candidateCell] = currentCell == startCell ? candidateCell : firstSteps[currentCell];
                int distance = PathDistance(candidateCell, playerCell);
                if (distance < closestDistance
                    && PathDistance(firstSteps[candidateCell], playerCell) < PathDistance(startCell, playerCell))
                {
                    closestDistance = distance;
                    closestCell = candidateCell;
                }

                if (candidateCell == playerCell)
                {
                    nextCell = candidateCell;

                    // 플레이어까지의 경로를 역추적해 이번 턴의 첫 한 칸만 선택한다.
                    while (cameFrom[nextCell] != startCell)
                    {
                        nextCell = cameFrom[nextCell];
                    }

                    return true;
                }

                frontier.Enqueue(candidateCell);
            }
        }

        // 완전한 경로가 막혔어도 더 가까운 대기 위치까지 접근한다.
        // 같은 거리의 칸으로 배회하지 않고, 더 접근할 수 없으면 기다린다.
        if (closestCell == startCell) return false;
        nextCell = closestCell;
        while (cameFrom[nextCell] != startCell) nextCell = cameFrom[nextCell];
        // 경로가 끊긴 상태의 임시 접근은 매 턴 거리가 줄어들 때만 허용한다.
        return PathDistance(nextCell, playerCell) < PathDistance(startCell, playerCell);
    }

    private int PathDistance(Vector3Int from, Vector3Int to)
    {
        int x = Mathf.Abs(from.x - to.x);
        int y = Mathf.Abs(from.y - to.y);
        return movementPattern == MonsterMovementPattern.EightDirection
            ? Mathf.Max(x, y) : x + y;
    }

    private bool CanCrossCorner(Vector3Int origin, Vector3Int step)
    {
        if (step.x == 0 || step.y == 0) return true;
        // 비행형의 8방향 이동은 유지하되 벽 모서리를 뚫지는 않는다.
        return gridManager.IsWalkableCell(origin + new Vector3Int(step.x, 0, 0))
            && gridManager.IsWalkableCell(origin + new Vector3Int(0, step.y, 0));
    }

    internal bool TryBeginMove(Vector3Int currentCell, Vector3Int destinationCell, Action onLanding = null)
    {
        if (destinationCell == currentCell)
            return false;

        if (chessBehaviour != null && movementPattern == MonsterMovementPattern.Knight)
        {
            Vector3Int step = destinationCell - currentCell;
            if (Mathf.Abs(step.x) * Mathf.Abs(step.y) != 2) return false;
        }
        Vector3Int playerCell = playerMovement.GridPosition;

        if (destinationCell == playerCell)
        {
            AttackPlayer();
            return true;
        }

        if (!gridManager.IsWalkableCell(destinationCell) || tryReserveCell == null)
        {
            return false;
        }

        if (!tryReserveCell(currentCell, destinationCell))
            return false;

        // 목적지로 들어오는 투사체에 의해 사망했다면 이동 연출을 시작하지 않는다.
        if (IsDead)
            return true;

        // 목적지를 예약한 즉시 논리 좌표를 확정하며 이후 Transform은 연출에만 사용한다.
        lastStep = destinationCell - currentCell;
        gridPosition = destinationCell;
        jumpOrigin = transform.position;
        jumpProgress = 0f;
        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        landingAction = onLanding;
        moving = true;

        return true;
    }

    private void UpdateFacingToPlayer()
    {
        if (player == null || monsterSprite == null)
            return;

        float horizontalDifference = player.position.x - transform.position.x;

        if (Mathf.Abs(horizontalDifference) <= 0.001f)
            return;

        bool shouldFaceLeft = horizontalDifference < 0f;
        monsterSprite.flipX = shouldFaceLeft ? spriteFacesRight : !spriteFacesRight;
    }

    internal void AttackPlayer()
    {
        playerMovement.TakeDamage(attackDamage);
        FinishTurn();
    }

    public void TakeDamage(int damage)
    {
        characterHealth.TakeDamage(damage);
    }

    public bool ApplyKnockback(Vector3Int destinationCell)
    {
        if (IsDead || moving || gridManager == null)
            return false;

        // 점유 검사가 끝난 타일을 즉시 논리 위치와 화면 위치에 함께 반영한다.
        lastStep = Vector3Int.zero;
        gridPosition = destinationCell;
        jumpOrigin = transform.position;
        jumpProgress = 0f;
        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        transform.position = targetPosition;
        return true;
    }

    private void MoveOneTile()
    {
        if (chessBehaviour != null && movementPattern == MonsterMovementPattern.Knight)
        {
            // 중간 칸 판정 없이 예약한 L자 착지 칸으로 도약하는 연출이다.
            jumpProgress = Mathf.Min(1f, jumpProgress + Time.deltaTime / KnightJumpSeconds);
            transform.position = Vector3.Lerp(jumpOrigin, targetPosition, jumpProgress)
                + Vector3.up * (Mathf.Sin(jumpProgress * Mathf.PI) * 0.6f);
            if (jumpProgress < 1f) return;
        }
        else
        {
            transform.position = Vector3.MoveTowards(
                transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }

        if ((transform.position - targetPosition).sqrMagnitude > 0.000001f)
            return;

        transform.position = targetPosition;
        moving = false;
        Action landed = landingAction;
        landingAction = null;
        landed?.Invoke();
        FinishTurn();
    }

    internal void FinishTurn()
    {
        landingAction = null;
        Action callback = moveCompleted;
        moveCompleted = null;
        tryReserveCell = null;
        isCellBlocked = null;
        callback?.Invoke();
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        maxHealth = Mathf.Max(1, maxHealth);
        attackDamage = Mathf.Max(1, attackDamage);
    }

    private void HandleDeath(CharacterHealth defeatedCharacter)
    {
        moving = false;
        FinishTurn();
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (moveCompleted != null)
        {
            moving = false;
            FinishTurn();
        }
    }

    private void OnDestroy()
    {
        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDeath;
        }
    }
}
