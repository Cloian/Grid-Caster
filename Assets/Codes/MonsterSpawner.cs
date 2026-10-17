using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum KnockbackBlockKind { None, Wall, Monster, Player, Immovable, Invalid }

[DisallowMultipleComponent]
[RequireComponent(typeof(GridManager))]
[RequireComponent(typeof(ProjectileManager))]
public sealed class MonsterSpawner : MonoBehaviour
{
    public event Action<int> WaveStarted;
    public event Action<int> WaveCleared;
    public event Action WorldTurnCompleted;

    [Header("참조")]
    [SerializeField] private MonsterMovement[] monsterPrefabs;
    [SerializeField] private Transform player;
    [SerializeField] private Tilemap mapTilemap;

    [Header("스폰 설정")]
    [SerializeField] private int edgeInsetTiles = 1;
    [SerializeField] private int minimumPlayerDistance = 4;

    [Header("체스 몬스터 웨이브")]
    [SerializeField] private bool enableChessWaves;
    [SerializeField] private Sprite chessMonsterSprite;
    [Tooltip("기본 체스 몬스터 Sprite를 쓰는 몬스터의 대기·이동·공격·피격·사망 모션입니다.")]
    [SerializeField] private RuntimeAnimatorController chessMonsterAnimatorController;
    [Tooltip("비숍 전용 외형입니다. 비워 두면 기존 체스 몬스터 Sprite를 사용합니다.")]
    [SerializeField] private Sprite bishopMonsterSprite;
    [Tooltip("비숍 전용 반복 애니메이션입니다.")]
    [SerializeField] private RuntimeAnimatorController bishopMonsterAnimatorController;
    [Tooltip("나이트(기갑 병마) 전용 외형입니다. 비워 두면 기존 체스 몬스터 Sprite를 사용합니다.")]
    [SerializeField] private Sprite knightMonsterSprite;
    [SerializeField] private RuntimeAnimatorController knightMonsterAnimatorController;
    [Tooltip("룩(쇠뇌를 문 성벽 괴물) 전용 외형입니다. 비워 두면 기존 체스 몬스터 Sprite를 사용합니다.")]
    [SerializeField] private Sprite rookMonsterSprite;
    [SerializeField] private RuntimeAnimatorController rookMonsterAnimatorController;
    [Tooltip("원거리 폭탄병 외형입니다. 비워 두면 기존 체스 몬스터 Sprite를 사용합니다.")]
    [SerializeField] private Sprite bomberMonsterSprite;
    [SerializeField] private RuntimeAnimatorController bomberMonsterAnimatorController;
    [Tooltip("폭탄병은 멀리서 위협하는 대신 약하다. 다가가 잡을 이유를 준다.")]
    [SerializeField, Min(1)] private int bomberHealth = 2;
    [SerializeField, Min(1)] private int chessMonsterHealth = 3;

    [Header("여왕의 시계 (프로토타입)")]
    [Tooltip("웨이브마다 공개된 턴 예산. 넘기면 폰 증원이 오고, 안에 정리하면 주도권 보너스.")]
    [SerializeField] private bool enableQueenClock = true;
    [SerializeField, Min(1)] private int clockBaseTurns = 9;
    [SerializeField, Min(0f)] private float clockTurnsPerMonster = 0.75f;
    [SerializeField, Min(1)] private int overtimeReinforceInterval = 3;
    [SerializeField, Min(0)] private int maxReinforcementsPerWave = 2;
    [Tooltip("증원은 플레이어 근처(체비쇼프 거리)에서 소환해 직접 다가오게 한다. 멀리 쫓아가는 악순환을 막는다.")]
    [SerializeField, Min(1)] private int reinforcementMinDistance = 2;
    [SerializeField, Min(1)] private int reinforcementMaxDistance = 3;
    [Tooltip("원래 웨이브 적을 모두 잡으면 남은 증원은 소환 해제되고 웨이브가 끝난다.")]
    [SerializeField] private bool dismissReinforcementsOnClear = true;
    [Tooltip("일반 웨이브의 나이트는 최소 2턴 간격으로 도약합니다. 독립 패턴 테스트는 별도 값을 사용합니다.")]
    [SerializeField, Min(2)] private int knightActionInterval = 2;
    [SerializeField, Min(1)] private int bishopMoveDistance = 4;
    [SerializeField, Min(1)] private int bishopOrbitDistance = 2;
    [SerializeField, Min(1)] private int bishopFireTurns = 2;
    [SerializeField, Min(1)] private int bishopFireDamage = 1;
    public BishopFireTrail FireTrail { get; private set; }

    private bool UsesChessTurns => chessPlaytest != null || bossPlaytest != null || enableChessWaves;
    // 비숍 불길은 이동만 막는 벽이다(투사체는 통과). 플레이어·적의 이동 판정에서 함께 쓴다.
    public bool IsFireCell(Vector3Int cell) => FireTrail != null && FireTrail.HasFire(cell);
    public bool ChessWavesEnabled => enableChessWaves;

    private readonly List<MonsterMovement> activeMonsters = new List<MonsterMovement>();
    private readonly List<Vector3Int> spawnCells = new List<Vector3Int>();
    private readonly List<Vector3Int> rookWallSpawnCells = new List<Vector3Int>();
    private readonly List<MonsterMovementPattern> waveSpawnPlan =
        new List<MonsterMovementPattern>();
    private readonly List<MonsterMovement> monsterTurnOrder =
        new List<MonsterMovement>();

    private readonly HashSet<Vector3Int> blockedMonsterCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> reservedTransitCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> recentlyDefeatedCells = new HashSet<Vector3Int>();

    private int nextPrefabIndex;
    private int spawnSerial;
    private int waveSpawnCellCursor;
    private int waveSpawnStride = 1;
    private int rookSpawnCellCursor;
    private int rookSpawnStride = 1;
    private GridManager gridManager;
    private ProjectileManager projectileManager;
    private Move playerMovement;
    private CharacterHealth playerHealth;
    private int monstersStillMoving;
    private int currentWave;
    private bool waveActive;
    private int reinforcementsThisWave;
    private readonly HashSet<MonsterMovement> reinforcements = new HashSet<MonsterMovement>();
    // 원래 웨이브 적(증원 제외) 중 살아 있는 수
    private int OriginalMonstersAlive
    {
        get
        {
            int count = 0;
            foreach (MonsterMovement monster in activeMonsters)
                if (monster != null && !monster.IsDead && !reinforcements.Contains(monster)) count++;
            return count;
        }
    }
    public bool QueenClockActive => enableQueenClock && waveActive && chessPlaytest == null && bossPlaytest == null;
    public int ClockTurn { get; private set; }
    public int ClockBudget { get; private set; }
    public bool IsClockOvertime => ClockTurn > ClockBudget;
    // 다음 증원까지 남은 턴(초과 전에는 예산까지 남은 턴 + 1)
    public int TurnsUntilReinforcement => !IsClockOvertime ? ClockBudget - ClockTurn + 1
        : overtimeReinforceInterval - (ClockTurn - ClockBudget - 1) % overtimeReinforceInterval;
    private bool worldTurnInProgress;
    private bool monsterTurnCompleted;
    private bool projectileTurnCompleted;
    private bool planningMonsterTurn;
    private bool projectileTurnStarted;
    private ChessPlaytest chessPlaytest;
    private QueenBossPlaytest bossPlaytest;
    private StageFlowManager stageFlowManager;

    public int CurrentWave => currentWave;
    public WaveTemplate CurrentWaveTemplate { get; private set; }
    public StageFlowManager StageFlow => stageFlowManager;
    public bool IsWorldTurnInProgress => worldTurnInProgress;
    public IReadOnlyList<MonsterMovement> ActiveMonsters => activeMonsters;

    // 일반 적은 한 칸, 큰 보스는 몸 전체를 범위 판정에 사용하되 피해는 적당 한 번이다.
    public IEnumerable<Vector3Int> OccupiedCellsOf(MonsterMovement actor)
    {
        if (bossPlaytest != null && bossPlaytest.IsQueen(actor))
        {
            for (int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
                yield return QueenBossBehaviour.QueenCenter + new Vector3Int(x,y,0);
        }
        else if (actor != null) yield return actor.GridPosition;
    }
    public Vector3Int ImpactCellOf(MonsterMovement actor, Vector3Int origin, Vector3Int direction)
    {
        if (bossPlaytest == null || !bossPlaytest.IsQueen(actor) || direction == Vector3Int.zero) return actor.GridPosition;
        foreach (Vector3Int cell in OccupiedCellsOf(actor))
        {
            Vector3Int delta=cell-origin;
            int distance=Mathf.Max(Mathf.Abs(delta.x),Mathf.Abs(delta.y));
            if(distance>0&&origin+direction*distance==cell)
            {
                for(int step=1;step<distance;step++)
                {
                    Vector3Int earlier=origin+direction*step;
                    if(Mathf.Abs(earlier.x-QueenBossBehaviour.QueenCenter.x)<=1&&Mathf.Abs(earlier.y-QueenBossBehaviour.QueenCenter.y)<=1)return earlier;
                }
                return cell;
            }
        }
        return actor.GridPosition;
    }

    private void Awake()
    {
        chessPlaytest = GetComponent<ChessPlaytest>();
        bossPlaytest = GetComponent<QueenBossPlaytest>();
        if (chessPlaytest == null && bossPlaytest == null)
        {
            stageFlowManager = GetComponent<StageFlowManager>();
            if (stageFlowManager == null)
            {
                stageFlowManager = gameObject.AddComponent<StageFlowManager>();
            }
        }
        if (mapTilemap == null)
        {
            mapTilemap = FindAnyObjectByType<Tilemap>();
        }

        gridManager = GetComponent<GridManager>();
        projectileManager = GetComponent<ProjectileManager>();

        if (gridManager == null)
        {
            gridManager = gameObject.AddComponent<GridManager>();
        }

        if (projectileManager == null)
        {
            projectileManager = gameObject.AddComponent<ProjectileManager>();
        }

        gridManager.Initialize(mapTilemap, edgeInsetTiles);
        projectileManager.Initialize(gridManager, this);
    }

    private void Start()
    {
        ResolveReferences();

        if (!CanSpawn())
        {
            enabled = false;
            return;
        }

        if (UsesChessTurns)
        {
            FireTrail = gameObject.AddComponent<BishopFireTrail>();
            ConfigureFire(bishopFireTurns, bishopFireDamage);
        }
        playerMovement.MoveCompleted += BeginMonsterTurn;
        // 모든 전투 씬에서 몬스터 체력 칸·예상 피해·행동 아이콘·피해 숫자를 보여 준다.
        MonsterStatusHud.Attach(this, playerMovement);

        if (bossPlaytest != null)
        {
            bossPlaytest.Initialize(this, playerMovement, gridManager, projectileManager);
            return;
        }

        if (chessPlaytest != null)
        {
            chessPlaytest.Initialize(this, playerMovement, gridManager, projectileManager);
            return;
        }

        if (enableChessWaves)
        {
            ChessWaveDisplay display = gameObject.AddComponent<ChessWaveDisplay>();
            display.Initialize(this, playerMovement);
        }
        BuildSpawnCells();
        stageFlowManager.Initialize(this, playerMovement, playerHealth);
        stageFlowManager.BeginRun();
    }

    private void Update()
    {
        if (chessPlaytest != null || bossPlaytest != null)
            return;
        if (playerHealth != null && playerHealth.IsDead)
            return;

        activeMonsters.RemoveAll(monster => monster == null || monster.IsDead);

        if (waveActive && monstersStillMoving == 0 && !worldTurnInProgress && !playerMovement.IsActionInProgress)
        {
            // 원래 적을 모두 잡으면 남은 증원은 소환 해제한다(증원 때문에 웨이브가 늘어지지 않게).
            if (dismissReinforcementsOnClear && reinforcements.Count > 0 && activeMonsters.Count > 0
                && OriginalMonstersAlive == 0)
                DismissReinforcements();
            if (activeMonsters.Count == 0) CompleteCurrentWave();
        }

    }

    private void ResolveReferences()
    {
        if (player != null)
        {
            playerMovement = player.GetComponent<Move>();
        }

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<Move>();

            if (playerMovement != null)
            {
                player = playerMovement.transform;
            }
        }

        if (playerHealth == null && playerMovement != null)
        {
            playerHealth = playerMovement.GetComponent<CharacterHealth>();
        }

        if (mapTilemap == null)
        {
            mapTilemap = FindAnyObjectByType<Tilemap>();
        }

        if (gridManager == null)
        {
            gridManager = GetComponent<GridManager>();
        }

        if (projectileManager == null)
        {
            projectileManager = GetComponent<ProjectileManager>();
        }

        if (gridManager != null)
        {
            gridManager.Initialize(mapTilemap, edgeInsetTiles);
        }

        if (projectileManager != null)
        {
            projectileManager.Initialize(gridManager, this);
        }
    }

    private bool CanSpawn()
    {
        if (playerMovement == null || player == null || mapTilemap == null
            || gridManager == null || projectileManager == null)
        {
            Debug.LogError(
                "MonsterSpawner에 플레이어, GridManager, ProjectileManager와 맵 Tilemap 참조가 필요합니다.",
                this
            );
            return false;
        }

        if (chessPlaytest == null && bossPlaytest == null && (monsterPrefabs == null || monsterPrefabs.Length == 0))
        {
            Debug.LogError("MonsterSpawner에 몬스터 프리팹이 필요합니다.", this);
            return false;
        }

        if (enableChessWaves && chessMonsterSprite == null)
        {
            Debug.LogError("체스 웨이브의 독립 Sprite를 연결하세요.", this);
            return false;
        }
        return true;
    }

    public void ConfigureFire(int turns, int damage)
    {
        FireTrail.Initialize(gridManager, playerMovement, turns, damage);
    }

    public void CloseKnightCounterWindows()
    {
        foreach (MonsterMovement monster in activeMonsters)
            if (monster != null && monster.TryGetComponent(out ChessMonsterBehaviour pattern))
                pattern.CloseKnightCounterWindow();
    }

    private void BeginMonsterTurn()
    {
        if (bossPlaytest != null)
        {
            // 플레이어 전체 공격 경로가 끝난 완료 알림에서만 보스의 한 행동을 진행한다.
            worldTurnInProgress = true;
            bossPlaytest.TakeWorldTurn(() =>
            {
                worldTurnInProgress = false;
                playerMovement.CompleteMonsterTurn();
                WorldTurnCompleted?.Invoke();
            });
            return;
        }
        // 봉쇄로 자체 행동이 생략된 나이트에도 이전 기회가 남지 않는다.
        CloseKnightCounterWindows();
        worldTurnInProgress = true;
        monsterTurnCompleted = false;
        projectileTurnCompleted = false;
        projectileTurnStarted = false;
        planningMonsterTurn = true;
        if (playerMovement.CurrentHealth > 0) FireTrail?.AdvanceTurn();

        if (UsesChessTurns)
            projectileManager.TryHitPlayerEnteringCell(playerMovement.GridPosition);

        // 플레이어 기본 투사체는 이 시점 전에 전체 경로 처리가 끝난다.
        // 여기서는 보드에 남는 적/특수 투사체의 한 칸 행동만 처리한다.
        // 투사체 목적지와 피격을 먼저 확정한 뒤 살아남은 적만 행동한다.
        // 시각 이동은 같은 프레임 안에서 이어서 시작하므로 화면에서는 동시에 움직인다.
        if (!UsesChessTurns)
        {
            projectileTurnStarted = true;
            projectileManager.TakeTurn(OnProjectileTurnCompleted);
        }

        activeMonsters.RemoveAll(monster => monster == null || monster.IsDead);
        monstersStillMoving = activeMonsters.Count;
        blockedMonsterCells.Clear();
        reservedTransitCells.Clear();
        monsterTurnOrder.Clear();
        monsterTurnOrder.AddRange(activeMonsters);

        foreach (MonsterMovement monster in activeMonsters)
        {
            blockedMonsterCells.Add(monster.GridPosition);
        }

        Vector3Int playerCell = playerMovement.GridPosition;
        monsterTurnOrder.Sort(
            (first, second) => CompareMonsterTurnOrder(first, second, playerCell)
        );

        if (monstersStillMoving == 0)
        {
            monsterTurnCompleted = true;
        }
        else
        {
            // 가까운 적부터 경로와 목적지를 예약하지만 이동 연출은 다음 Update에 함께 시작한다.
            foreach (MonsterMovement monster in monsterTurnOrder)
            {
                if (UsesChessTurns && playerHealth.IsDead)
                {
                    OnMonsterMoveCompleted();
                    continue;
                }
                monster.TakeTurn(
                    OnMonsterMoveCompleted,
                    (currentCell, destinationCell) =>
                        TryReserveMonsterCell(monster, currentCell, destinationCell),
                    destinationCell => blockedMonsterCells.Contains(destinationCell)
                        || reservedTransitCells.Contains(destinationCell)
                        || IsFireCell(destinationCell),
                    destinationCell => monster.MovementPattern
                            == MonsterMovementPattern.CardinalFour
                        && recentlyDefeatedCells.Contains(destinationCell)
                );
            }
        }

        // 사망 칸 우선권은 바로 다음 적 행동 계획에서만 사용한다.
        recentlyDefeatedCells.Clear();
        planningMonsterTurn = false;
        TryCompleteWorldTurn();
    }

    private bool TryReserveMonsterCell(
        MonsterMovement movingMonster,
        Vector3Int currentCell,
        Vector3Int destinationCell
    )
    {
        // 현재 다른 몬스터가 있거나 이번 턴에 이미 예약된 타일은 사용할 수 없다.
        if (blockedMonsterCells.Contains(destinationCell) || reservedTransitCells.Contains(destinationCell)
            || IsFireCell(destinationCell))
            return false;

        Vector3Int step = Vector3Int.zero;
        if (movingMonster.MovementPattern == MonsterMovementPattern.Bishop)
        {
            Vector3Int delta = destinationCell - currentCell;
            if (Mathf.Abs(delta.x) != Mathf.Abs(delta.y) || delta == Vector3Int.zero) return false;
            step = new Vector3Int(Math.Sign(delta.x), Math.Sign(delta.y), 0);
            for (Vector3Int cell = currentCell + step; cell != destinationCell; cell += step)
                if (blockedMonsterCells.Contains(cell) || reservedTransitCells.Contains(cell) || IsFireCell(cell)) return false;
        }

        // 몬스터가 투사체의 도착 타일로 들어오면 실제 이동 전에 피격을 먼저 확정한다.
        projectileManager.TryHitMonsterEnteringCell(movingMonster, destinationCell);

        if (movingMonster == null || movingMonster.IsDead)
        {
            blockedMonsterCells.Remove(currentCell);
            return true;
        }

        // 앞 몬스터가 비운 칸을 뒤 몬스터가 같은 턴에 예약할 수 있게 즉시 점유를 갱신한다.
        blockedMonsterCells.Remove(currentCell);

        if (blockedMonsterCells.Add(destinationCell))
        {
            // 비숍의 통과 칸도 이번 계획 동안 예약한다. 출발 칸은 뒤 적이 사용할 수 있다.
            if (step != Vector3Int.zero)
                for (Vector3Int cell = currentCell + step; cell != destinationCell; cell += step)
                    reservedTransitCells.Add(cell);
            return true;
        }

        blockedMonsterCells.Add(currentCell);
        return false;
    }

    private int CompareMonsterTurnOrder(
        MonsterMovement first,
        MonsterMovement second,
        Vector3Int playerCell
    )
    {
        int firstDistance = GetMonsterDistanceToPlayer(first, playerCell);
        int secondDistance = GetMonsterDistanceToPlayer(second, playerCell);
        int distanceComparison = firstDistance.CompareTo(secondDistance);

        if (distanceComparison != 0)
            return distanceComparison;

        // 일반 몬스터도 생성 순서를 부여한다. 동일 거리의 예약 순서를 고정한다.
        int order = first.SpawnOrder.CompareTo(second.SpawnOrder);
        return order != 0 ? order : first.GetInstanceID().CompareTo(second.GetInstanceID());
    }

    private int GetMonsterDistanceToPlayer(
        MonsterMovement monster,
        Vector3Int playerCell
    )
    {
        if (monster == null)
            return int.MaxValue;

        Vector3Int monsterCell = monster.GridPosition;
        int horizontalDistance = Mathf.Abs(playerCell.x - monsterCell.x);
        int verticalDistance = Mathf.Abs(playerCell.y - monsterCell.y);

        return monster.MovementPattern == MonsterMovementPattern.EightDirection
            ? Mathf.Max(horizontalDistance, verticalDistance)
            : horizontalDistance + verticalDistance;
    }

    public bool TryGetMonsterAtCell(Vector3Int cell, out MonsterMovement targetMonster)
    {
        if (bossPlaytest != null && bossPlaytest.TryGetQueenAtCell(cell, out targetMonster)) return true;
        foreach (MonsterMovement monster in activeMonsters)
        {
            if (monster != null && !monster.IsDead && monster.GridPosition == cell)
            {
                targetMonster = monster;
                return true;
            }
        }

        targetMonster = null;
        return false;
    }

    public bool TryKnockbackMonster(
        MonsterMovement targetMonster,
        Vector3Int attackDirection
    )
    {
        return TryKnockbackMonster(targetMonster, attackDirection, 1, out _);
    }

    public bool TryKnockbackMonster(
        MonsterMovement targetMonster,
        Vector3Int attackDirection,
        int distance,
        out MonsterMovement collisionMonster
    )
    {
        return TryKnockbackMonster(targetMonster, attackDirection, distance, out collisionMonster, out _);
    }

    public bool TryKnockbackMonster(MonsterMovement targetMonster, Vector3Int attackDirection,
        int distance, out MonsterMovement collisionMonster, out KnockbackBlockKind block)
    {
        collisionMonster = null;
        block = KnockbackBlockKind.Invalid;
        if (targetMonster == null || targetMonster.IsDead || gridManager == null)
            return false;

        // 성벽에 고정된 쇠뇌 룩은 지상 넉백의 대상이 아니다.
        if (targetMonster.MovementPattern == MonsterMovementPattern.Rook
            || (bossPlaytest != null && bossPlaytest.IsQueen(targetMonster)))
        {
            block = KnockbackBlockKind.Immovable;
            return false;
        }

        Vector3Int normalizedDirection = new Vector3Int(
            Mathf.Clamp(attackDirection.x, -1, 1),
            Mathf.Clamp(attackDirection.y, -1, 1),
            0
        );

        if (normalizedDirection == Vector3Int.zero)
            return false;

        block = KnockbackBlockKind.None;
        Vector3Int originCell = targetMonster.GridPosition;
        Vector3Int destinationCell = originCell;
        int steps = Mathf.Max(1, distance);
        for (int step = 0; step < steps; step++)
        {
            Vector3Int candidate = destinationCell + normalizedDirection;
            if (player != null && playerMovement.GridPosition == candidate)
            { block = KnockbackBlockKind.Player; break; }
            if (TryGetMonsterAtCell(candidate, out MonsterMovement occupyingMonster)
                && occupyingMonster != targetMonster)
            {
                collisionMonster = occupyingMonster;
                block = KnockbackBlockKind.Monster;
                break;
            }
            if (!gridManager.IsWalkableCell(candidate))
            { block = KnockbackBlockKind.Wall; break; }
            destinationCell = candidate;
        }
        return destinationCell != originCell && targetMonster.ApplyKnockback(destinationCell);
    }

    private void OnMonsterMoveCompleted()
    {
        monstersStillMoving--;

        if (monstersStillMoving <= 0)
        {
            monstersStillMoving = 0;
            monsterTurnCompleted = true;
            TryCompleteWorldTurn();
        }
    }

    private void OnProjectileTurnCompleted()
    {
        projectileTurnCompleted = true;
        TryCompleteWorldTurn();
    }

    private void TryCompleteWorldTurn()
    {
        if (!worldTurnInProgress || planningMonsterTurn || !monsterTurnCompleted)
            return;

        if (!projectileTurnStarted)
        {
            // 체스 전투에서는 모든 적의 이동이 끝난 뒤 새로 발사된 탄까지 한 칸씩 처리한다.
            projectileTurnStarted = true;
            if (playerHealth.IsDead)
                projectileTurnCompleted = true;
            else
            {
                projectileManager.TakeTurn(OnProjectileTurnCompleted);
                return;
            }
        }
        if (!projectileTurnCompleted)
            return;

        AdvanceQueenClock();
        worldTurnInProgress = false;
        playerMovement.CompleteMonsterTurn();
        WorldTurnCompleted?.Invoke();
    }

    // 시계와 무관한 규칙을 검사하는 검증이 증원 영향을 빼고 확인할 때 쓴다.
    public void SetQueenClockForValidation(bool enabled) => enableQueenClock = enabled;

    // 여왕의 시계: 적 턴이 끝날 때마다 한 칸. 예산을 넘기면 정해진 간격으로 폰 증원이 온다.
    private void AdvanceQueenClock()
    {
        if (!QueenClockActive || playerHealth.IsDead) return;
        activeMonsters.RemoveAll(monster => monster == null || monster.IsDead);
        if (OriginalMonstersAlive == 0) return;
        ClockTurn++;
        if (!IsClockOvertime || reinforcementsThisWave >= maxReinforcementsPerWave) return;
        if ((ClockTurn - ClockBudget - 1) % overtimeReinforceInterval != 0) return;
        if (!TryGetReinforcementCell(out Vector3Int cell)) return;
        int before = activeMonsters.Count;
        if (SpawnMonster(MonsterMovementPattern.CardinalFour, cell) && activeMonsters.Count > before)
        {
            MonsterMovement summoned = activeMonsters[activeMonsters.Count - 1];
            reinforcements.Add(summoned);
            summoned.gameObject.AddComponent<SummonedGhost>();
            reinforcementsThisWave++;
            Debug.Log($"여왕의 시계 초과: 웨이브 {currentWave} {ClockTurn}/{ClockBudget}턴, 폰 증원 {reinforcementsThisWave} at {cell}", this);
        }
    }

    // 플레이어에게서 2~3칸 떨어진 빈 바닥. 가까운 거리부터 고르되 같은 거리 안에서는 무작위.
    private bool TryGetReinforcementCell(out Vector3Int cell)
    {
        cell = default;
        Vector3Int center = playerMovement.GridPosition;
        for (int distance = reinforcementMinDistance; distance <= reinforcementMaxDistance; distance++)
        {
            var candidates = new List<Vector3Int>();
            for (int y = -distance; y <= distance; y++)
                for (int x = -distance; x <= distance; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != distance) continue;
                    Vector3Int candidate = center + new Vector3Int(x, y, 0);
                    if (gridManager.IsWalkableCell(candidate) && !IsOccupied(candidate)) candidates.Add(candidate);
                }
            if (candidates.Count == 0) continue;
            cell = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return true;
        }
        return false;
    }

    private void DismissReinforcements()
    {
        var cells = new List<Vector3Int>();
        foreach (MonsterMovement monster in reinforcements)
        {
            if (monster == null || monster.IsDead) continue;
            cells.Add(monster.GridPosition);
            activeMonsters.Remove(monster);
            monster.gameObject.SetActive(false);
            Destroy(monster.gameObject);
        }
        reinforcements.Clear();
        KnightQuakeEffect.PlayDust(gridManager, cells);
        if (cells.Count > 0) Debug.Log($"여왕의 시계: 웨이브 {currentWave} 원래 적 정리 → 증원 {cells.Count}마리 소환 해제", this);
    }

    public void ResetPlaytestEncounter()
    {
        CombatFeedback.ClearAll();
        worldTurnInProgress = false;
        activeMonsters.RemoveAll(monster => monster == null);
        foreach (MonsterMovement monster in activeMonsters)
        {
            monster.gameObject.SetActive(false);
            Destroy(monster.gameObject);
        }
        activeMonsters.Clear();
        blockedMonsterCells.Clear();
        reservedTransitCells.Clear();
        recentlyDefeatedCells.Clear();
        monstersStillMoving = 0;
        projectileManager.ClearProjectiles();
        FireTrail?.Clear();
    }

    public MonsterMovement SpawnPlaytestMonster(
        MonsterMovementPattern pattern, Vector3Int cell, Sprite sprite, Color color,
        int health, int order, int knightInterval, int bishopTravel, int bishopOrbit,
        RuntimeAnimatorController motionOverride = null)
    {
        bool wallMountedRook = pattern == MonsterMovementPattern.Rook
            && gridManager.IsTopBoundaryWallCell(cell);
        if ((!gridManager.IsWalkableCell(cell) && !wallMountedRook)
            || cell == playerMovement.GridPosition
            || TryGetMonsterAtCell(cell, out _))
            throw new InvalidOperationException($"테스트 스폰 타일이 막혀 있습니다: {cell}");

        // 전용 외형(64px 시트)이 있는 패턴은 색을 입히지 않고 한 칸 크기로 맞춘다.
        bool usesOwnVisual = TryGetOwnVisual(pattern, out Sprite ownSprite, out RuntimeAnimatorController ownMotion);
        if (usesOwnVisual)
            sprite = ownSprite;

        // 새 몬스터는 프리팹 없이 독립 Sprite와 행동 컴포넌트로 구성한다.
        GameObject actor = new GameObject(pattern.ToString());
        actor.transform.SetParent(transform, false);
        actor.transform.position = gridManager.GetCellCenterWorld(cell);
        SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        // 보스전처럼 호출자가 그림과 모션을 함께 넘기면 색을 입히지 않고 그 모션을 쓴다(크기는 호출자가 정한다).
        renderer.color = usesOwnVisual || motionOverride != null ? Color.white : color;
        renderer.sortingOrder = 10;
        RuntimeAnimatorController motion = null;
        if (usesOwnVisual)
        {
            FitSpriteInsideTile(actor.transform, sprite);
            motion = ownMotion;
        }
        else if (motionOverride != null)
        {
            motion = motionOverride;
        }
        else if (sprite == chessMonsterSprite)
        {
            motion = chessMonsterAnimatorController;
        }
        if (motion != null)
        {
            Animator animator = actor.AddComponent<Animator>();
            animator.runtimeAnimatorController = motion;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        MonsterMovement monster = actor.AddComponent<MonsterMovement>();
        ChessMonsterBehaviour behaviour = actor.AddComponent<ChessMonsterBehaviour>();
        monster.Initialize(player, gridManager);
        monster.ConfigurePlaytest(pattern, health, order);
        behaviour.Initialize(monster, gridManager, projectileManager, playerMovement,
            knightInterval, bishopTravel, bishopOrbit, FireTrail);
        RegisterActiveMonster(monster);
        return monster;
    }

    private void RegisterActiveMonster(MonsterMovement monster)
    {
        if (monster == null)
            return;

        monster.Defeated -= HandleMonsterDefeated;
        monster.Defeated += HandleMonsterDefeated;
        activeMonsters.Add(monster);
    }

    private void HandleMonsterDefeated(MonsterMovement monster, Vector3Int defeatedCell)
    {
        // 플레이어 행동 중 앞 적이 죽어 생긴 빈칸을 다음 뱀이 자연스럽게 메우도록 기억한다.
        if (gridManager != null && gridManager.IsWalkableCell(defeatedCell))
            recentlyDefeatedCells.Add(defeatedCell);
        blockedMonsterCells.Remove(defeatedCell);
    }

    private bool TryGetOwnVisual(MonsterMovementPattern pattern, out Sprite sprite,
        out RuntimeAnimatorController motion)
    {
        (sprite, motion) = pattern switch
        {
            MonsterMovementPattern.Bishop => (bishopMonsterSprite, bishopMonsterAnimatorController),
            MonsterMovementPattern.Knight => (knightMonsterSprite, knightMonsterAnimatorController),
            MonsterMovementPattern.Rook => (rookMonsterSprite, rookMonsterAnimatorController),
            MonsterMovementPattern.Bomber => (bomberMonsterSprite, bomberMonsterAnimatorController),
            _ => ((Sprite)null, (RuntimeAnimatorController)null)
        };
        return sprite != null;
    }

    private static void FitSpriteInsideTile(Transform visualTransform, Sprite sprite)
    {
        if (visualTransform == null || sprite == null)
            return;

        const float MaximumTileSpan = 0.95f;
        float largestDimension = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        if (largestDimension <= MaximumTileSpan)
            return;

        // 64px 프레임(2칸 크기)은 정확히 1/2로 줄여 타일과 같은 화면 픽셀 격자에 맞춘다.
        // 가장자리 1px 여백이 있어 그림은 한 칸 안에 들어간다.
        int divisor = Mathf.CeilToInt(largestDimension - 0.001f);
        float scale = divisor >= 2 ? 1f / divisor : MaximumTileSpan / largestDimension;
        visualTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private void BuildSpawnCells()
    {
        spawnCells.Clear();
        rookWallSpawnCells.Clear();

        BoundsInt bounds = mapTilemap.cellBounds;
        int minimumX = bounds.xMin + edgeInsetTiles;
        int maximumX = bounds.xMax - edgeInsetTiles - 1;
        int minimumY = bounds.yMin + edgeInsetTiles;
        int maximumY = bounds.yMax - edgeInsetTiles - 1;

        if (minimumX > maximumX || minimumY > maximumY)
        {
            Debug.LogError("맵이 너무 작아 설정된 안쪽 둘레에 몬스터를 스폰할 수 없습니다.", this);
            return;
        }

        for (int x = minimumX; x <= maximumX; x++)
        {
            AddSpawnCellIfWalkable(new Vector3Int(x, minimumY, 0));

            if (maximumY != minimumY)
            {
                AddSpawnCellIfWalkable(new Vector3Int(x, maximumY, 0));
            }
        }

        for (int y = minimumY + 1; y < maximumY; y++)
        {
            AddSpawnCellIfWalkable(new Vector3Int(minimumX, y, 0));

            if (maximumX != minimumX)
            {
                AddSpawnCellIfWalkable(new Vector3Int(maximumX, y, 0));
            }
        }

        // 룩은 지상 외곽이 아니라 실제 상단 경계 벽의 안쪽 면에만 장착한다.
        int topWallY = bounds.yMax - edgeInsetTiles;
        for (int x = minimumX; x <= maximumX; x++)
        {
            Vector3Int wallCell = new Vector3Int(x, topWallY, 0);
            if (gridManager.IsTopBoundaryWallCell(wallCell))
                rookWallSpawnCells.Add(wallCell);
        }
    }

    private void AddSpawnCellIfWalkable(Vector3Int cell)
    {
        if (gridManager.IsWalkableCell(cell))
        {
            spawnCells.Add(cell);
        }
    }

    public bool StartWave(int waveNumber)
    {
        if (chessPlaytest != null || waveActive || worldTurnInProgress)
            return false;

        currentWave = Mathf.Max(1, waveNumber);
        recentlyDefeatedCells.Clear();
        CurrentWaveTemplate = WaveTemplateCatalog.Roll(currentWave);
        BuildWaveSpawnPlan(CurrentWaveTemplate);
        bool variableWave = currentWave != 10 && currentWave != WaveTemplateCatalog.FinalWave;
        if (variableWave)
            for (int i = waveSpawnPlan.Count - 1; i > 0; i--)
            {
                int other = UnityEngine.Random.Range(0, i + 1);
                (waveSpawnPlan[i], waveSpawnPlan[other]) = (waveSpawnPlan[other], waveSpawnPlan[i]);
            }
        // 외곽 간격과 플레이어 최소 거리는 보존하고 시작점/유형 배치를 변주한다.
        waveSpawnCellCursor = spawnCells.Count > 0
            ? variableWave ? UnityEngine.Random.Range(0, spawnCells.Count) : (currentWave * 7) % spawnCells.Count
            : 0;
        waveSpawnStride = spawnCells.Count > 0
            ? Mathf.Max(1, spawnCells.Count / Mathf.Max(1, waveSpawnPlan.Count))
            : 1;
        int rookCount = 0;
        foreach (MonsterMovementPattern pattern in waveSpawnPlan)
            if (pattern == MonsterMovementPattern.Rook) rookCount++;
        rookSpawnCellCursor = rookWallSpawnCells.Count > 0
            ? UnityEngine.Random.Range(0, rookWallSpawnCells.Count)
            : 0;
        rookSpawnStride = rookWallSpawnCells.Count > 0
            ? Mathf.Max(1, rookWallSpawnCells.Count / Mathf.Max(1, rookCount))
            : 1;
        int spawnedCount = 0;

        foreach (MonsterMovementPattern movementPattern in waveSpawnPlan)
        {
            if (SpawnMonster(movementPattern))
            {
                spawnedCount++;

            }
        }

        waveActive = spawnedCount > 0;
        ClockTurn = 0;
        reinforcementsThisWave = 0;
        reinforcements.Clear();
        ClockBudget = clockBaseTurns + Mathf.CeilToInt(spawnedCount * clockTurnsPerMonster);

        Debug.Log(
            $"웨이브 {currentWave} · {CurrentWaveTemplate.DisplayName}: "
            + $"난이도 {CurrentWaveTemplate.DifficultyTier}단계, 총 {spawnedCount}마리 "
            + $"(특수: {CurrentWaveTemplate.IsSpecialWave}, 유물: {CurrentWaveTemplate.IsRelicWave}, "
            + $"중간보스: {CurrentWaveTemplate.IsMidBossWave})",
            this
        );
        WaveStarted?.Invoke(currentWave);

        return waveActive;
    }

    private void CompleteCurrentWave()
    {
        // 여왕의 시계 안에 정리하면 주도권 보너스(실험 지표도 남긴다).
        if (QueenClockActive)
        {
            bool fast = !IsClockOvertime;
            if (fast) playerMovement.Initiative?.Grant(playerMovement.Initiative.FastClearBonus, "빠른 정리");
            InitiativeTracker tempo = playerMovement.Initiative;
            Debug.Log($"TEMPO_WAVE wave={currentWave} turns={ClockTurn}/{ClockBudget} fast={fast} reinforcements={reinforcementsThisWave}"
                + (tempo != null ? $" initiative={tempo.Current}/{tempo.Max} arts={tempo.MovementArtsUsed} attacks={tempo.AttacksResolved} multi={tempo.MultiHitAttacks} adjacent={tempo.AdjacentHits}" : string.Empty), this);
        }
        waveActive = false;
        if (enableChessWaves)
        {
            projectileManager.ClearProjectiles();
            FireTrail?.Clear();
        }

        WaveCleared?.Invoke(currentWave);
    }

    private void BuildWaveSpawnPlan(WaveTemplate template)
    {
        waveSpawnPlan.Clear();
        waveSpawnPlan.AddRange(template.Monsters);
    }

    private bool SpawnMonster(MonsterMovementPattern movementPattern, Vector3Int? forcedCell = null)
    {
        if (enableChessWaves && movementPattern >= MonsterMovementPattern.Knight)
        {
            Vector3Int chessCell;
            bool foundCell = movementPattern == MonsterMovementPattern.Rook
                ? TryGetRookWallSpawnCell(out chessCell)
                : TryGetSpawnCell(out chessCell);
            if (!foundCell) return false;
            MonsterMovement chessMonster = SpawnPlaytestMonster(movementPattern, chessCell,
                chessMonsterSprite, ChessWaveDisplay.ColorFor(movementPattern),
                movementPattern == MonsterMovementPattern.Bomber ? bomberHealth : chessMonsterHealth,
                ++spawnSerial, Mathf.Max(2, knightActionInterval), bishopMoveDistance, bishopOrbitDistance);
            return true;
        }
        MonsterMovement prefab = GetNextPrefab(movementPattern);

        Vector3Int spawnCell = default;
        if (prefab == null || (forcedCell.HasValue ? IsOccupied(forcedCell.Value) : !TryGetSpawnCell(out spawnCell)))
            return false;
        if (forcedCell.HasValue) spawnCell = forcedCell.Value;

        Vector3 spawnPosition = mapTilemap.GetCellCenterWorld(spawnCell);
        MonsterMovement monster = Instantiate(prefab, spawnPosition, Quaternion.identity, transform);

        spawnSerial++;
        monster.name = $"{prefab.name}_{spawnSerial:00}";
        monster.Initialize(player, gridManager, spawnSerial);
        RegisterActiveMonster(monster);
        return true;
    }

    private MonsterMovement GetNextPrefab(MonsterMovementPattern movementPattern)
    {
        for (int i = 0; i < monsterPrefabs.Length; i++)
        {
            MonsterMovement prefab = monsterPrefabs[nextPrefabIndex % monsterPrefabs.Length];
            nextPrefabIndex++;

            if (prefab != null && prefab.MovementPattern == movementPattern)
                return prefab;
        }

        Debug.LogError($"{movementPattern} 이동 방식의 몬스터가 스포너에 없습니다.", this);
        return null;
    }

    private bool TryGetSpawnCell(out Vector3Int spawnCell)
    {
        spawnCell = default;

        if (spawnCells.Count == 0)
            return false;

        Vector3Int playerCell = playerMovement.GridPosition;
        int startIndex = waveSpawnCellCursor % spawnCells.Count;

        for (int i = 0; i < spawnCells.Count; i++)
        {
            int candidateIndex = (startIndex + i) % spawnCells.Count;
            Vector3Int candidate = spawnCells[candidateIndex];
            int distanceFromPlayer = Mathf.Abs(candidate.x - playerCell.x)
                + Mathf.Abs(candidate.y - playerCell.y);

            if (candidate == playerCell
                || distanceFromPlayer < minimumPlayerDistance
                || IsOccupied(candidate))
                continue;

            spawnCell = candidate;
            waveSpawnCellCursor = (candidateIndex + waveSpawnStride) % spawnCells.Count;
            return true;
        }

        return false;
    }

    private bool TryGetRookWallSpawnCell(out Vector3Int spawnCell)
    {
        spawnCell = default;
        if (rookWallSpawnCells.Count == 0)
            return false;

        int startIndex = rookSpawnCellCursor % rookWallSpawnCells.Count;
        for (int i = 0; i < rookWallSpawnCells.Count; i++)
        {
            int candidateIndex = (startIndex + i) % rookWallSpawnCells.Count;
            Vector3Int candidate = rookWallSpawnCells[candidateIndex];
            if (IsOccupied(candidate)) continue;

            spawnCell = candidate;
            rookSpawnCellCursor = (candidateIndex + rookSpawnStride) % rookWallSpawnCells.Count;
            return true;
        }

        return false;
    }

    public bool IsOccupied(Vector3Int cell)
    {
        if (bossPlaytest != null && bossPlaytest.TryGetQueenAtCell(cell, out _)) return true;
        foreach (MonsterMovement monster in activeMonsters)
        {
            if (monster != null
                && !monster.IsDead
                && monster.GridPosition == cell)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        edgeInsetTiles = Mathf.Max(1, edgeInsetTiles);
        minimumPlayerDistance = Mathf.Max(0, minimumPlayerDistance);

        if (Application.isPlaying && gridManager != null)
        {
            gridManager.Initialize(mapTilemap, edgeInsetTiles);
        }
    }

    private void OnDestroy()
    {
        foreach (MonsterMovement monster in activeMonsters)
            if (monster != null)
                monster.Defeated -= HandleMonsterDefeated;

        if (playerMovement != null)
        {
            playerMovement.MoveCompleted -= BeginMonsterTurn;
        }
    }
}
