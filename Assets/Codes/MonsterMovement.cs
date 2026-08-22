using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CharacterHealth))]
public sealed class MonsterMovement : MonoBehaviour
{
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
    private bool preferHorizontalOnTie;
    private Action moveCompleted;
    private Func<Vector3Int, bool> tryReserveCell;

    public bool IsDead => characterHealth != null && characterHealth.IsDead;
    public MonsterMovementPattern MovementPattern => movementPattern;

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
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (moving)
        {
            MoveOneTile();
        }
    }

    public void Initialize(Transform targetPlayer, GridManager targetGridManager)
    {
        player = targetPlayer;
        playerMovement = player != null ? player.GetComponent<Move>() : null;
        gridManager = targetGridManager;
    }

    public void TakeTurn(
        Action onMoveCompleted,
        Func<Vector3Int, bool> reserveDestination
    )
    {
        if (moving)
        {
            return;
        }

        moveCompleted = onMoveCompleted;
        tryReserveCell = reserveDestination;

        if (IsDead || playerMovement == null || gridManager == null || !gridManager.IsReady)
        {
            FinishTurn();
            return;
        }

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
    }

    private void TryBeginChaseStep()
    {
        Vector3Int currentCell = gridManager.WorldToCell(transform.position);
        Vector3Int playerCell = gridManager.WorldToCell(player.position);
        Vector3Int difference = playerCell - currentCell;

        if (IsPlayerInAttackRange(difference))
        {
            AttackPlayer();
            return;
        }

        if (movementPattern == MonsterMovementPattern.EightDirection)
        {
            TryBeginEightDirectionStep(currentCell, difference);
            return;
        }

        TryBeginCardinalStep(currentCell, difference);
    }

    private bool IsPlayerInAttackRange(Vector3Int difference)
    {
        int horizontalDistance = Mathf.Abs(difference.x);
        int verticalDistance = Mathf.Abs(difference.y);

        if (movementPattern == MonsterMovementPattern.EightDirection)
        {
            // 비행형은 대각선으로 맞닿은 플레이어도 공격할 수 있다.
            return Mathf.Max(horizontalDistance, verticalDistance) <= 1;
        }

        return horizontalDistance + verticalDistance <= 1;
    }

    private void TryBeginCardinalStep(Vector3Int currentCell, Vector3Int difference)
    {
        GetShortestDirections(difference, out Vector3Int firstDirection, out Vector3Int secondDirection);

        if (TryBeginMove(currentCell, firstDirection))
            return;

        if (TryBeginMove(currentCell, secondDirection))
            return;

        FinishTurn();
    }

    private void TryBeginEightDirectionStep(Vector3Int currentCell, Vector3Int difference)
    {
        Vector3Int horizontal = difference.x == 0
            ? Vector3Int.zero
            : new Vector3Int((int)Mathf.Sign(difference.x), 0, 0);
        Vector3Int vertical = difference.y == 0
            ? Vector3Int.zero
            : new Vector3Int(0, (int)Mathf.Sign(difference.y), 0);
        Vector3Int diagonal = horizontal + vertical;

        // 박쥐 같은 비행형은 대각선을 우선하고, 막혀 있으면 가까워지는 직선 방향을 시도한다.
        if (horizontal != Vector3Int.zero
            && vertical != Vector3Int.zero
            && TryBeginMove(currentCell, diagonal))
        {
            return;
        }

        GetShortestDirections(difference, out Vector3Int firstDirection, out Vector3Int secondDirection);

        if (TryBeginMove(currentCell, firstDirection))
            return;

        if (TryBeginMove(currentCell, secondDirection))
            return;

        FinishTurn();
    }

    private void GetShortestDirections(
        Vector3Int difference,
        out Vector3Int firstDirection,
        out Vector3Int secondDirection
    )
    {
        Vector3Int horizontal = difference.x == 0
            ? Vector3Int.zero
            : new Vector3Int((int)Mathf.Sign(difference.x), 0, 0);
        Vector3Int vertical = difference.y == 0
            ? Vector3Int.zero
            : new Vector3Int(0, (int)Mathf.Sign(difference.y), 0);

        bool chooseHorizontalFirst = Mathf.Abs(difference.x) > Mathf.Abs(difference.y);

        if (Mathf.Abs(difference.x) == Mathf.Abs(difference.y))
        {
            chooseHorizontalFirst = preferHorizontalOnTie;
            preferHorizontalOnTie = !preferHorizontalOnTie;
        }

        firstDirection = chooseHorizontalFirst ? horizontal : vertical;
        secondDirection = chooseHorizontalFirst ? vertical : horizontal;
    }

    private bool TryBeginMove(Vector3Int currentCell, Vector3Int direction)
    {
        if (direction == Vector3Int.zero)
            return false;

        Vector3Int destinationCell = currentCell + direction;
        Vector3Int playerCell = gridManager.WorldToCell(player.position);

        if (destinationCell == playerCell)
        {
            AttackPlayer();
            return true;
        }

        if (!gridManager.IsWalkableCell(destinationCell) || tryReserveCell == null)
        {
            return false;
        }

        if (!tryReserveCell(destinationCell))
            return false;

        // 목적지로 들어오는 투사체에 의해 사망했다면 이동 연출을 시작하지 않는다.
        if (IsDead)
            return true;

        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        moving = true;

        if (direction.x < 0)
        {
            monsterSprite.flipX = spriteFacesRight;
        }
        else if (direction.x > 0)
        {
            monsterSprite.flipX = !spriteFacesRight;
        }

        return true;
    }

    private void AttackPlayer()
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
        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        transform.position = targetPosition;
        return true;
    }

    private void MoveOneTile()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if ((transform.position - targetPosition).sqrMagnitude > 0.000001f)
            return;

        transform.position = targetPosition;
        moving = false;
        FinishTurn();
    }

    private void FinishTurn()
    {
        Action callback = moveCompleted;
        moveCompleted = null;
        tryReserveCell = null;
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
