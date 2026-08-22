using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProjectileManager : MonoBehaviour
{
    [Header("투사체 설정")]
    [Tooltip("플레이어 기본공격이 한 행동 안에서 전체 경로를 비행하는 연출 속도입니다.")]
    [SerializeField] private float moveSpeed = 16.8f;
    [SerializeField] private Color projectileColor = Color.white;
    [SerializeField] private int sortingOrder = 15;

    [Header("투사체 애니메이션")]
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private RuntimeAnimatorController projectileAnimatorController;

    private readonly List<TurnProjectile> activeProjectiles = new List<TurnProjectile>();
    private readonly List<TurnProjectile> turnProjectiles = new List<TurnProjectile>();
    private GridManager gridManager;
    private MonsterSpawner monsterSpawner;
    private Action allProjectilesCompleted;
    private int projectilesStillMoving;
    private int projectileSerial;

    public int ActiveProjectileCount
    {
        get
        {
            RemoveFinishedProjectiles();
            return activeProjectiles.Count;
        }
    }

    public bool IsTurnInProgress => projectilesStillMoving > 0;

    public void Initialize(GridManager targetGridManager, MonsterSpawner targetMonsterSpawner)
    {
        gridManager = targetGridManager;
        monsterSpawner = targetMonsterSpawner;
    }

    public bool SpawnPlayerProjectile(
        Vector3Int originCell,
        Vector3Int direction,
        int damage,
        int penetrationCount,
        Action<MonsterMovement> onHitConfirmed,
        Action onTravelCompleted
    )
    {
        ResolveReferences();

        Vector3Int normalizedDirection = new Vector3Int(
            Mathf.Clamp(direction.x, -1, 1),
            Mathf.Clamp(direction.y, -1, 1),
            0
        );

        if (gridManager == null || monsterSpawner == null
            || normalizedDirection == Vector3Int.zero)
        {
            Debug.LogError("투사체를 생성하려면 GridManager와 MonsterSpawner가 필요합니다.", this);
            return false;
        }

        if (!HasProjectileVisual())
            return false;

        return SpawnPlayerProjectileSegment(
            originCell,
            normalizedDirection,
            damage,
            Mathf.Max(0, penetrationCount),
            onHitConfirmed,
            onTravelCompleted
        );
    }

    private bool SpawnPlayerProjectileSegment(
        Vector3Int originCell,
        Vector3Int normalizedDirection,
        int damage,
        int remainingPenetrations,
        Action<MonsterMovement> onHitConfirmed,
        Action onTravelCompleted
    )
    {
        GameObject projectileObject = new GameObject("PlayerProjectile");
        projectileObject.transform.SetParent(transform, false);
        projectileObject.transform.position = gridManager.GetCellCenterWorld(originCell);
        projectileObject.transform.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg
        );

        SpriteRenderer spriteRenderer = projectileObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = projectileSprite;
        spriteRenderer.color = projectileColor;
        spriteRenderer.sortingOrder = sortingOrder;

        Animator animator = projectileObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = projectileAnimatorController;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);

        MonsterMovement hitMonster = null;
        TurnProjectile projectile = projectileObject.AddComponent<TurnProjectile>();
        projectile.Initialize(
            gridManager,
            monsterSpawner,
            originCell,
            normalizedDirection,
            moveSpeed,
            damage,
            targetMonster =>
            {
                hitMonster = targetMonster;
                onHitConfirmed?.Invoke(targetMonster);
            }
        );

        projectileSerial++;
        projectileObject.name = $"PlayerProjectile_{projectileSerial:00}";
        activeProjectiles.Add(projectile);

        // 플레이어 기본공격은 적 턴 전에 전체 경로를 한 번에 비행한다.
        projectile.TravelFullPath(finishedProjectile =>
        {
            activeProjectiles.Remove(finishedProjectile);

            if (hitMonster != null && remainingPenetrations > 0)
            {
                bool continuationCreated = SpawnPlayerProjectileSegment(
                    finishedProjectile.CurrentCell,
                    normalizedDirection,
                    damage,
                    remainingPenetrations - 1,
                    onHitConfirmed,
                    onTravelCompleted
                );

                if (continuationCreated)
                    return;
            }

            onTravelCompleted?.Invoke();
        });
        return true;
    }

    public void TakeTurn(Action onAllProjectilesCompleted)
    {
        if (IsTurnInProgress)
        {
            Debug.LogWarning("투사체 이동이 끝나기 전에 다음 투사체 턴이 요청되었습니다.", this);
            return;
        }

        RemoveFinishedProjectiles();
        allProjectilesCompleted = onAllProjectilesCompleted;
        turnProjectiles.Clear();
        turnProjectiles.AddRange(activeProjectiles);
        projectilesStillMoving = turnProjectiles.Count;

        if (projectilesStillMoving == 0)
        {
            CompleteProjectileTurn();
            return;
        }

        // 모든 투사체가 같은 프레임에 각자 한 타일 이동을 시작한다.
        TurnProjectile[] turnSnapshot = turnProjectiles.ToArray();

        foreach (TurnProjectile projectile in turnSnapshot)
        {
            if (projectile == null || projectile.IsFinished)
            {
                OnProjectileMoveCompleted(projectile);
                continue;
            }

            projectile.TakeTurn(OnProjectileMoveCompleted);
        }
    }

    public bool TryHitMonsterEnteringCell(
        MonsterMovement enteringMonster,
        Vector3Int enteringCell
    )
    {
        if (enteringMonster == null || enteringMonster.IsDead)
            return false;

        RemoveFinishedProjectiles();
        TurnProjectile[] projectileSnapshot = activeProjectiles.ToArray();
        bool hitConfirmed = false;

        foreach (TurnProjectile projectile in projectileSnapshot)
        {
            if (projectile == null
                || !projectile.TryHitMonsterEnteringCell(enteringMonster, enteringCell))
            {
                continue;
            }

            hitConfirmed = true;

            if (enteringMonster.IsDead)
                break;
        }

        return hitConfirmed;
    }

    private void OnProjectileMoveCompleted(TurnProjectile projectile)
    {
        projectilesStillMoving = Mathf.Max(0, projectilesStillMoving - 1);

        if (projectile == null || projectile.IsFinished)
        {
            activeProjectiles.Remove(projectile);
        }

        if (projectilesStillMoving == 0)
        {
            CompleteProjectileTurn();
        }
    }

    private void CompleteProjectileTurn()
    {
        projectilesStillMoving = 0;
        turnProjectiles.Clear();
        Action callback = allProjectilesCompleted;
        allProjectilesCompleted = null;
        callback?.Invoke();
    }

    private void RemoveFinishedProjectiles()
    {
        activeProjectiles.RemoveAll(
            projectile => projectile == null || projectile.IsFinished
        );
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
        {
            gridManager = GetComponent<GridManager>();

            if (gridManager == null)
            {
                gridManager = FindAnyObjectByType<GridManager>();
            }
        }

        if (monsterSpawner == null)
        {
            monsterSpawner = GetComponent<MonsterSpawner>();

            if (monsterSpawner == null)
            {
                monsterSpawner = FindAnyObjectByType<MonsterSpawner>();
            }
        }
    }

    private bool HasProjectileVisual()
    {
        if (projectileSprite != null && projectileAnimatorController != null)
            return true;

        Debug.LogError(
            "ProjectileManager에 투사체 Sprite와 Animator Controller가 설정되지 않았습니다.",
            this
        );
        return false;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
    }

}
