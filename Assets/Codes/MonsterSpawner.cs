using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    [Tooltip("비숍 전용 외형입니다. 비워 두면 기존 체스 몬스터 Sprite를 사용합니다.")]
    [SerializeField] private Sprite bishopMonsterSprite;
    [Tooltip("비숍 전용 반복 애니메이션입니다.")]
    [SerializeField] private RuntimeAnimatorController bishopMonsterAnimatorController;
    [Tooltip("비워 두면 기존 체스 몬스터 스프라이트를 임시로 사용합니다.")]
    [SerializeField] private Sprite rookMonsterSprite;
    [SerializeField, Min(1)] private int chessMonsterHealth = 3;
    [Tooltip("일반 웨이브의 나이트는 최소 2턴 간격으로 도약합니다. 독립 패턴 테스트는 별도 값을 사용합니다.")]
    [SerializeField, Min(2)] private int knightActionInterval = 2;
    [SerializeField, Min(1)] private int bishopMoveDistance = 4;
    [SerializeField, Min(1)] private int bishopOrbitDistance = 2;
    [SerializeField, Min(1)] private int bishopFireTurns = 3;
    [SerializeField, Min(1)] private int bishopFireDamage = 1;
    public BishopFireTrail FireTrail { get; private set; }

    private bool UsesChessTurns => chessPlaytest != null || enableChessWaves;
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
    private bool worldTurnInProgress;
    private bool monsterTurnCompleted;
    private bool projectileTurnCompleted;
    private bool planningMonsterTurn;
    private bool projectileTurnStarted;
    private ChessPlaytest chessPlaytest;
    private StageFlowManager stageFlowManager;

    public int CurrentWave => currentWave;
    public WaveTemplate CurrentWaveTemplate { get; private set; }
    public StageFlowManager StageFlow => stageFlowManager;
    public bool IsWorldTurnInProgress => worldTurnInProgress;
    public IReadOnlyList<MonsterMovement> ActiveMonsters => activeMonsters;

    private void Awake()
    {
        chessPlaytest = GetComponent<ChessPlaytest>();
        if (chessPlaytest == null)
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
        if (chessPlaytest != null)
            return;
        if (playerHealth != null && playerHealth.IsDead)
            return;

        activeMonsters.RemoveAll(monster => monster == null || monster.IsDead);

        if (waveActive && activeMonsters.Count == 0
            && monstersStillMoving == 0 && !worldTurnInProgress && !playerMovement.IsActionInProgress)
        {
            CompleteCurrentWave();
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

        if (chessPlaytest == null && (monsterPrefabs == null || monsterPrefabs.Length == 0))
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

    private void BeginMonsterTurn()
    {
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
                        || reservedTransitCells.Contains(destinationCell),
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
        if (blockedMonsterCells.Contains(destinationCell) || reservedTransitCells.Contains(destinationCell))
            return false;

        Vector3Int step = Vector3Int.zero;
        if (movingMonster.MovementPattern == MonsterMovementPattern.Bishop)
        {
            Vector3Int delta = destinationCell - currentCell;
            if (Mathf.Abs(delta.x) != Mathf.Abs(delta.y) || delta == Vector3Int.zero) return false;
            step = new Vector3Int(Math.Sign(delta.x), Math.Sign(delta.y), 0);
            for (Vector3Int cell = currentCell + step; cell != destinationCell; cell += step)
                if (blockedMonsterCells.Contains(cell) || reservedTransitCells.Contains(cell)) return false;
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
        activeMonsters.RemoveAll(monster => monster == null || monster.IsDead);

        foreach (MonsterMovement monster in activeMonsters)
        {
            if (monster.GridPosition == cell)
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
        collisionMonster = null;
        if (targetMonster == null || targetMonster.IsDead || gridManager == null)
            return false;

        // 성벽에 고정된 쇠뇌 룩은 지상 넉백의 대상이 아니다.
        if (targetMonster.MovementPattern == MonsterMovementPattern.Rook)
            return false;

        Vector3Int normalizedDirection = new Vector3Int(
            Mathf.Clamp(attackDirection.x, -1, 1),
            Mathf.Clamp(attackDirection.y, -1, 1),
            0
        );

        if (normalizedDirection == Vector3Int.zero)
            return false;

        Vector3Int originCell = targetMonster.GridPosition;
        Vector3Int destinationCell = originCell;
        int steps = Mathf.Max(1, distance);
        for (int step = 0; step < steps; step++)
        {
            Vector3Int candidate = destinationCell + normalizedDirection;
            if (!gridManager.IsWalkableCell(candidate)
                || (player != null && playerMovement.GridPosition == candidate)) break;
            if (TryGetMonsterAtCell(candidate, out MonsterMovement occupyingMonster)
                && occupyingMonster != targetMonster)
            {
                collisionMonster = occupyingMonster;
                break;
            }
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

        worldTurnInProgress = false;
        playerMovement.CompleteMonsterTurn();
        WorldTurnCompleted?.Invoke();
    }

    public void ResetPlaytestEncounter()
    {
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
        int health, int order, int knightInterval, int bishopTravel, int bishopOrbit)
    {
        bool wallMountedRook = pattern == MonsterMovementPattern.Rook
            && gridManager.IsTopBoundaryWallCell(cell);
        if ((!gridManager.IsWalkableCell(cell) && !wallMountedRook)
            || cell == playerMovement.GridPosition
            || TryGetMonsterAtCell(cell, out _))
            throw new InvalidOperationException($"테스트 스폰 타일이 막혀 있습니다: {cell}");

        bool usesBishopVisual = pattern == MonsterMovementPattern.Bishop
            && bishopMonsterSprite != null;
        if (usesBishopVisual)
        {
            sprite = bishopMonsterSprite;
        }

        // 새 몬스터는 프리팹 없이 독립 Sprite와 행동 컴포넌트로 구성한다.
        GameObject actor = new GameObject(pattern.ToString());
        actor.transform.SetParent(transform, false);
        actor.transform.position = gridManager.GetCellCenterWorld(cell);
        SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = usesBishopVisual ? Color.white : color;
        renderer.sortingOrder = 10;
        if (usesBishopVisual)
        {
            FitSpriteInsideTile(actor.transform, sprite);
            if (bishopMonsterAnimatorController != null)
            {
                Animator animator = actor.AddComponent<Animator>();
                animator.runtimeAnimatorController = bishopMonsterAnimatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
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

    private static void FitSpriteInsideTile(Transform visualTransform, Sprite sprite)
    {
        if (visualTransform == null || sprite == null)
            return;

        const float MaximumTileSpan = 0.95f;
        float largestDimension = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        if (largestDimension <= MaximumTileSpan)
            return;

        float scale = MaximumTileSpan / largestDimension;
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
        CurrentWaveTemplate = WaveTemplateCatalog.Get(currentWave);
        BuildWaveSpawnPlan(CurrentWaveTemplate);
        // 같은 웨이브는 같은 외곽 배치 순서에서 시작한다. 플레이어와 점유 상태 때문에
        // 사용할 수 없는 칸만 순서대로 건너뛴다.
        waveSpawnCellCursor = spawnCells.Count > 0
            ? (currentWave * 7) % spawnCells.Count
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

    private bool SpawnMonster(MonsterMovementPattern movementPattern)
    {
        if (enableChessWaves && movementPattern >= MonsterMovementPattern.Knight)
        {
            Vector3Int chessCell;
            bool foundCell = movementPattern == MonsterMovementPattern.Rook
                ? TryGetRookWallSpawnCell(out chessCell)
                : TryGetSpawnCell(out chessCell);
            if (!foundCell) return false;
            Sprite monsterSprite = movementPattern == MonsterMovementPattern.Rook
                && rookMonsterSprite != null
                ? rookMonsterSprite
                : chessMonsterSprite;
            MonsterMovement chessMonster = SpawnPlaytestMonster(movementPattern, chessCell,
                monsterSprite, ChessWaveDisplay.ColorFor(movementPattern), chessMonsterHealth,
                ++spawnSerial, Mathf.Max(2, knightActionInterval), bishopMoveDistance, bishopOrbitDistance);
            return true;
        }
        MonsterMovement prefab = GetNextPrefab(movementPattern);

        if (prefab == null || !TryGetSpawnCell(out Vector3Int spawnCell))
            return false;

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
