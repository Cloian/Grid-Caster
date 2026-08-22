using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public sealed class TurnProjectile : MonoBehaviour
{
    private GridManager gridManager;
    private MonsterSpawner monsterSpawner;
    private Vector3Int currentCell;
    private Vector3Int direction;
    private Vector3Int destinationCell;
    private Vector3 targetPosition;
    private float moveSpeed;
    private int damage;
    private bool moving;
    private bool travellingFullPath;
    private bool destroyAfterMove;
    private bool finished;
    private MonsterMovement pendingHitTarget;
    private Action<MonsterMovement> hitConfirmed;
    private Action<TurnProjectile> turnCompleted;

    public Vector3Int CurrentCell => currentCell;
    public Vector3Int Direction => direction;
    public bool IsFinished => finished;

    public void Initialize(
        GridManager targetGridManager,
        MonsterSpawner targetMonsterSpawner,
        Vector3Int spawnCell,
        Vector3Int moveDirection,
        float visualMoveSpeed,
        int projectileDamage,
        Action<MonsterMovement> onHitConfirmed
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
        hitConfirmed = onHitConfirmed;
        targetPosition = transform.position;
    }

    public void TravelFullPath(Action<TurnProjectile> onTravelCompleted)
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
        while (true)
        {
            Vector3Int nextCell = destinationCell + direction;

            if (!gridManager.IsInsideMapCell(nextCell))
            {
                break;
            }

            destinationCell = nextCell;

            // 바깥 경계 벽 타일을 목적지에 포함해 벽에 닿는 연출 뒤 제거한다.
            if (!gridManager.IsWalkableCell(destinationCell))
            {
                break;
            }

            if (monsterSpawner != null
                && monsterSpawner.TryGetMonsterAtCell(
                    destinationCell,
                    out MonsterMovement targetMonster
                ))
            {
                pendingHitTarget = targetMonster;
                break;
            }
        }

        if (destinationCell == currentCell)
        {
            FinishTurn(true);
            return;
        }

        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;
        travellingFullPath = true;
        destroyAfterMove = true;
        moving = true;
    }

    public void TakeTurn(Action<TurnProjectile> onTurnCompleted)
    {
        if (moving || finished)
            return;

        turnCompleted = onTurnCompleted;

        // 이전 턴에 몬스터가 투사체 타일로 들어온 경우 앞 칸으로 통과시키지 않는다.
        if (monsterSpawner != null
            && monsterSpawner.TryGetMonsterAtCell(
                currentCell,
                out MonsterMovement overlappingMonster
            ))
        {
            ResolveHit(overlappingMonster);
            return;
        }

        destinationCell = currentCell + direction;

        if (direction == Vector3Int.zero || gridManager == null
            || !gridManager.IsInsideMapCell(destinationCell))
        {
            FinishTurn(true);
            return;
        }

        if (!gridManager.IsWalkableCell(destinationCell))
        {
            // 보드에 남는 투사체도 경계 벽까지 이동한 뒤 제거한다.
            targetPosition = gridManager.GetCellCenterWorld(destinationCell);
            targetPosition.z = transform.position.z;
            destroyAfterMove = true;
            moving = true;
            return;
        }

        // 적과 투사체가 같은 시점에 행동하므로, 턴 시작 시점의 적 위치로 충돌을 확정한다.
        if (monsterSpawner != null
            && monsterSpawner.TryGetMonsterAtCell(
                destinationCell,
                out MonsterMovement targetMonster
            ))
        {
            ResolveHit(targetMonster);
            return;
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
        if (travellingFullPath || !moving || finished
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
            targetMonster.TakeDamage(damage);
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

        if ((transform.position - targetPosition).sqrMagnitude > 0.000001f)
            return;

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
        if (turnCompleted == null)
            return;

        moving = false;
        finished = true;
        Action<TurnProjectile> callback = turnCompleted;
        turnCompleted = null;
        callback.Invoke(this);
    }
}
