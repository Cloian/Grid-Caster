using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public sealed class TurnProjectile : MonoBehaviour
{
    // 실행과 예고가 같은 경계/벽 장착 적 검사 순서를 사용한다. 호출 자체는 보드를 바꾸지 않는다.
    public static Vector3Int FindImpact(GridManager grid, MonsterSpawner spawner,
        Vector3Int origin, Vector3Int step, out MonsterMovement target,
        Action<Vector3Int> onFloorEntered = null)
    {
        target = null;
        Vector3Int destination = origin;
        if (grid == null || step == Vector3Int.zero) return destination;
        // 관통의 다음 구간이 큰 적 내부에서 시작해도 같은 적을 다시 직격하지 않는다.
        MonsterMovement originOccupant = null;
        if (spawner != null) spawner.TryGetMonsterAtCell(origin, out originOccupant);
        while (grid.IsInsideMapCell(destination + step))
        {
            destination += step;
            // 적이 서 있는 적중 칸도 공격이 지나간 바닥이다. 외벽은 제외한다.
            // 미리보기는 콜백을 전달하지 않으므로 불길이나 점유를 바꾸지 않는다.
            if (grid.IsWalkableCell(destination)) onFloorEntered?.Invoke(destination);
            if (spawner != null && spawner.TryGetMonsterAtCell(destination, out MonsterMovement candidate)
                && candidate != originOccupant)
            { target = candidate; break; }
            if (!grid.IsWalkableCell(destination)) break;
        }
        return destination;
    }
    private GridManager gridManager;
    private MonsterSpawner monsterSpawner;
    private Vector3Int currentCell;
    private Vector3Int direction;
    private Vector3Int destinationCell;
    private Vector3 targetPosition;
    private float moveSpeed;
    private int damage;
    private int tilesPerTurn = 1;
    private bool moving;
    private bool travellingFullPath;
    private float fullPathDeadline = -1f;
    private bool destroyAfterMove;
    private bool finished;
    private MonsterMovement pendingHitTarget;
    private Action<MonsterMovement> hitConfirmed;
    private Action<Vector3Int> pathCellEntered;
    private Action<TurnProjectile> turnCompleted;
    private Move enemyTarget;

    public Vector3Int CurrentCell => currentCell;
    public Vector3Int Direction => direction;
    public bool IsFinished => finished;
    public bool IsEnemyProjectile => enemyTarget != null;

    public void SetEnemyOwner(Move targetPlayer) => enemyTarget = targetPlayer;
    public void Cancel()
    {
        // 정리·재시작은 공격 완료가 아니다. 관통 후속탄과 적 턴을 시작하지 않는다.
        turnCompleted = null;
        FinishTurn(true);
    }

    public bool TryHitPlayerEnteringCell(Vector3Int cell)
    {
        if (finished || enemyTarget == null || currentCell != cell)
            return false;
        ResolvePlayerHit();
        return true;
    }

    private void ResolvePlayerHit()
    {
        // 피해 직전에 숨겨 적중한 탄이 한 프레임 더 보이지 않도록 한다.
        GetComponent<SpriteRenderer>().enabled = false;
        try { enemyTarget.TakeDamage(damage, PlayerDamageKind.Normal, direction); }
        finally { FinishTurn(true); }
    }

    public void Initialize(
        GridManager targetGridManager,
        MonsterSpawner targetMonsterSpawner,
        Vector3Int spawnCell,
        Vector3Int moveDirection,
        float visualMoveSpeed,
        int projectileDamage,
        Action<MonsterMovement> onHitConfirmed,
        int turnMoveDistance = 1,
        Action<Vector3Int> onPathCellEntered = null
    )
    {
        gridManager = targetGridManager;
        monsterSpawner = targetMonsterSpawner;
        currentCell = spawnCell;
        direction = new Vector3Int(
            Mathf.Clamp(moveDirection.x, -1, 1),
            Mathf.Clamp(moveDirection.y, -1, 1),
            0
        );
        moveSpeed = Mathf.Max(0.01f, visualMoveSpeed);
        damage = Mathf.Max(1, projectileDamage);
        tilesPerTurn = Mathf.Max(1, turnMoveDistance);
        hitConfirmed = onHitConfirmed;
        pathCellEntered = onPathCellEntered;
        targetPosition = transform.position;
    }

    public void TravelFullPath(Action<TurnProjectile> onTravelCompleted, float completionDeadline = -1f)
    {
        if (moving || finished)
            return;

        turnCompleted = onTravelCompleted;
        destinationCell = currentCell;
        pendingHitTarget = null;

        if (direction == Vector3Int.zero || gridManager == null)
        {
            FinishTurn(true);
            return;
        }

        // 판정은 발사 순간 타일 경로로 확정하고 Transform은 결과를 빠르게 연출만 한다.
        // 사거리 제한 없이 첫 대상 또는 맵 끝의 경계 벽까지 탐색한다.
        destinationCell = FindImpact(gridManager, monsterSpawner, currentCell, direction,
            out pendingHitTarget, pathCellEntered);

        if (destinationCell == currentCell)
        {
            FinishTurn(true);
            return;
        }

        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        travellingFullPath = true;
        fullPathDeadline = completionDeadline;
        destroyAfterMove = true;
        moving = true;
        // 여러 관통 구간은 같은 시전의 시간 상한을 공유한다. 남은 구간도 같은 프레임에 종료한다.
        if (fullPathDeadline >= 0f && Time.time >= fullPathDeadline)
            CompleteVisualMove();
    }

    public void TakeTurn(Action<TurnProjectile> onTurnCompleted)
    {
        if (moving || finished)
            return;

        turnCompleted = onTurnCompleted;

        if (enemyTarget != null && enemyTarget.CurrentHealth <= 0)
        {
            FinishTurn(false);
            return;
        }

        // 이전 턴에 몬스터가 투사체 타일로 들어온 경우 앞 칸으로 통과시키지 않는다.
        if (enemyTarget != null && TryHitPlayerEnteringCell(enemyTarget.GridPosition))
            return;

        if (enemyTarget == null && monsterSpawner != null
            && monsterSpawner.TryGetMonsterAtCell(
                currentCell,
                out MonsterMovement overlappingMonster
            ))
        {
            ResolveHit(overlappingMonster);
            return;
        }

        destinationCell = currentCell;

        if (direction == Vector3Int.zero || gridManager == null)
        {
            FinishTurn(true);
            return;
        }

        for (int step = 0; step < tilesPerTurn; step++)
        {
            Vector3Int nextCell = destinationCell + direction;
            if (!gridManager.IsInsideMapCell(nextCell))
            {
                FinishTurn(true);
                return;
            }

            destinationCell = nextCell;
            if (!gridManager.IsWalkableCell(destinationCell))
            {
                // 보드에 남는 투사체도 경계 벽까지 이동한 뒤 제거한다.
                destroyAfterMove = true;
                break;
            }

            // 한 턴에 여러 칸 움직이는 적 탄도 중간 칸의 플레이어를 건너뛰지 않는다.
            if (enemyTarget != null && destinationCell == enemyTarget.GridPosition)
            {
                ResolvePlayerHit();
                return;
            }

            if (enemyTarget == null && monsterSpawner != null
                && monsterSpawner.TryGetMonsterAtCell(destinationCell,
                    out MonsterMovement targetMonster))
            {
                ResolveHit(targetMonster);
                return;
            }
        }

        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        moving = true;
    }

    public bool TryHitMonsterEnteringCell(
        MonsterMovement enteringMonster,
        Vector3Int enteringCell
    )
    {
        if (enemyTarget != null || travellingFullPath || !moving || finished
            || enteringMonster == null || enteringMonster.IsDead
            || destinationCell != enteringCell)
        {
            return false;
        }

        ResolveHit(enteringMonster);
        return true;
    }

    private void ResolveHit(MonsterMovement targetMonster)
    {
        if (finished)
            return;

        if (targetMonster == null || targetMonster.IsDead)
        {
            FinishTurn(true);
            return;
        }

        try
        {
            targetMonster.TakeDamage(damage, direction);
            hitConfirmed?.Invoke(targetMonster);
        }
        finally
        {
            // 피해 이벤트에서 다른 로직이 실행되거나 예외가 발생해도 투사체는 반드시 종료한다.
            FinishTurn(true);
        }
    }

    private void Update()
    {
        if (!moving)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if (travellingFullPath && fullPathDeadline >= 0f && Time.time >= fullPathDeadline)
            transform.position = targetPosition;

        if ((transform.position - targetPosition).sqrMagnitude > 0.000001f)
            return;

        CompleteVisualMove();
    }

    private void CompleteVisualMove()
    {

        transform.position = targetPosition;
        currentCell = destinationCell;
        moving = false;

        if (travellingFullPath)
        {
            travellingFullPath = false;
            MonsterMovement targetMonster = pendingHitTarget;
            pendingHitTarget = null;

            if (targetMonster != null && !targetMonster.IsDead)
            {
                ResolveHit(targetMonster);
            }
            else
            {
                FinishTurn(true);
            }

            return;
        }

        FinishTurn(destroyAfterMove);
    }

    private void FinishTurn(bool destroyProjectile)
    {
        Action<TurnProjectile> callback = turnCompleted;
        turnCompleted = null;

        if (destroyProjectile)
        {
            moving = false;
            travellingFullPath = false;
            pendingHitTarget = null;
            finished = true;

            // Destroy는 프레임 끝에 처리되므로 먼저 비활성화해 적중 후 잔상이 통과하지 않게 한다.
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        try
        {
            callback?.Invoke(this);
        }
        finally
        {
            if (destroyProjectile)
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnDisable()
    {
        // 정상 도착만 FinishTurn에서 완료를 알린다. 씬 종료 중에는 새 행동을 만들지 않는다.
        moving = false;
        travellingFullPath = false;
        pendingHitTarget = null;
        finished = true;
        turnCompleted = null;
    }
}
