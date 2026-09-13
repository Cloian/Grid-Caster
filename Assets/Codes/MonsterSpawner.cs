using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(GridManager))]
[RequireComponent(typeof(ProjectileManager))]
public sealed class MonsterSpawner : MonoBehaviour
{
    public event Action<int> WaveStarted;
    public event Action<int> WaveCleared;
    public event Action WorldTurnCompleted;

    private const float WaveClearHealRatio = 0.2f;

    [Header("참조")]
    [SerializeField] private MonsterMovement[] monsterPrefabs;
    [SerializeField] private Transform player;
    [SerializeField] private Tilemap mapTilemap;

    [Header("스폰 설정")]
    [SerializeField] private int firstWaveMonsterCount = 4;
    [SerializeField] private int monsterIncreasePerWave = 1;
    [SerializeField] private float nextWaveDelay = 1f;
    [SerializeField] private int edgeInsetTiles = 1;
    [SerializeField] private int minimumPlayerDistance = 4;

    [Header("웨이브 몬스터 구성")]
    [SerializeField, Range(0f, 1f)] private float batRatioWave4To5 = 0.2f;
    [SerializeField, Range(0f, 1f)] private float batRatioWave6To8 = 0.3f;
    [SerializeField, Range(0f, 1f)] private float batRatioWave9AndLater = 0.4f;

    [Header("체스 몬스터 웨이브")]
    [SerializeField] private bool enableChessWaves;
    [SerializeField] private Sprite chessMonsterSprite;
    [SerializeField, Min(1)] private int chessMonsterHealth = 3;
    [SerializeField, Min(1)] private int knightActionInterval = 1;
    [SerializeField, Min(1)] private int bishopMoveDistance = 2;
    [SerializeField, Min(1)] private int bishopOrbitDistance = 3;
    [SerializeField, Min(1)] private int bishopFireTurns = 3;
    [SerializeField, Min(1)] private int bishopFireDamage = 1;
    public BishopFireTrail FireTrail { get; private set; }

    private bool UsesChessTurns => chessPlaytest != null || enableChessWaves;
    public bool ChessWavesEnabled => enableChessWaves;

    private readonly List<MonsterMovement> activeMonsters = new List<MonsterMovement>();
    private readonly List<Vector3Int> spawnCells = new List<Vector3Int>();
    private readonly List<MonsterMovementPattern> waveSpawnPlan =
        new List<MonsterMovementPattern>();
    private readonly List<MonsterMovement> monsterTurnOrder =
        new List<MonsterMovement>();

    private readonly HashSet<Vector3Int> blockedMonsterCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> reservedTransitCells = new HashSet<Vector3Int>();

    private float nextWaveTime;
    private int nextPrefabIndex;
    private int spawnSerial;
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

    public int CurrentWave => currentWave;
    public bool IsWorldTurnInProgress => worldTurnInProgress;
    public IReadOnlyList<MonsterMovement> ActiveMonsters => activeMonsters;

    private void Awake()
    {
        chessPlaytest = GetComponent<ChessPlaytest>();
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
            display.Initialize(this, gridManager, playerMovement);
        }
        BuildSpawnCells();
        SpawnNextWave();
    }

    private void Update()
    {
        if (chessPlaytest != null)
            return;
        if (playerHealth != null && playerHealth.IsDead)
            return;

        activeMonsters.RemoveAll(monster => monster == null || monster.IsDead);

        if (waveActive && activeMonsters.Count == 0
            && monstersStillMoving == 0 && !worldTurnInProgress)
        {
            CompleteCurrentWave();
        }

        if (!waveActive && Time.time >= nextWaveTime)
        {
            SpawnNextWave();
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
                        || reservedTransitCells.Contains(destinationCell)
                );
            }
        }

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
        if (targetMonster == null || targetMonster.IsDead || gridManager == null)
            return false;

        Vector3Int normalizedDirection = new Vector3Int(
            Mathf.Clamp(attackDirection.x, -1, 1),
            Mathf.Clamp(attackDirection.y, -1, 1),
            0
        );

        if (normalizedDirection == Vector3Int.zero)
            return false;

        Vector3Int currentCell = targetMonster.GridPosition;
        Vector3Int destinationCell = currentCell + normalizedDirection;

        if (!gridManager.IsWalkableCell(destinationCell))
            return false;

        if (player != null
            && playerMovement.GridPosition == destinationCell)
        {
            return false;
        }

        if (TryGetMonsterAtCell(destinationCell, out MonsterMovement occupyingMonster)
            && occupyingMonster != targetMonster)
        {
            return false;
        }

        // 룩은 넉백으로도 테두리 밖에 놓이지 않게 한다.
        if (targetMonster.MovementPattern == MonsterMovementPattern.Rook
            && targetMonster.TryGetComponent(out ChessMonsterBehaviour rook)
            && !rook.IsRookBoundaryCell(destinationCell)) return false;
        return targetMonster.ApplyKnockback(destinationCell);
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
        monstersStillMoving = 0;
        projectileManager.ClearProjectiles();
        FireTrail?.Clear();
    }

    public MonsterMovement SpawnPlaytestMonster(
        MonsterMovementPattern pattern, Vector3Int cell, Sprite sprite, Color color,
        int health, int order, int knightInterval, int bishopTravel, int bishopOrbit)
    {
        if (!gridManager.IsWalkableCell(cell) || cell == playerMovement.GridPosition
            || TryGetMonsterAtCell(cell, out _))
            throw new InvalidOperationException($"테스트 스폰 타일이 막혀 있습니다: {cell}");

        // 새 몬스터는 프리팹 없이 독립 Sprite와 행동 컴포넌트로 구성한다.
        GameObject actor = new GameObject(pattern.ToString());
        actor.transform.SetParent(transform, false);
        actor.transform.position = gridManager.GetCellCenterWorld(cell);
        SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 10;
        MonsterMovement monster = actor.AddComponent<MonsterMovement>();
        ChessMonsterBehaviour behaviour = actor.AddComponent<ChessMonsterBehaviour>();
        monster.Initialize(player, gridManager);
        monster.ConfigurePlaytest(pattern, health, order);
        behaviour.Initialize(monster, gridManager, projectileManager, playerMovement,
            knightInterval, bishopTravel, bishopOrbit, FireTrail);
        activeMonsters.Add(monster);
        return monster;
    }

    private void BuildSpawnCells()
    {
        spawnCells.Clear();

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
    }

    private void AddSpawnCellIfWalkable(Vector3Int cell)
    {
        if (gridManager.IsWalkableCell(cell))
        {
            spawnCells.Add(cell);
        }
    }

    private void SpawnNextWave()
    {
        currentWave++;
        int requestedCount = firstWaveMonsterCount
            + (currentWave - 1) * monsterIncreasePerWave;
        int plannedBatCount = CalculateBatCount(currentWave, requestedCount);
        BuildWaveSpawnPlan(requestedCount, plannedBatCount);
        if (enableChessWaves)
        {
            // 총 마릿수를 유지하며 해금된 패턴을 한 마리씩 우선 배치한다.
            List<MonsterMovementPattern> unlocked = new List<MonsterMovementPattern>();
            if (currentWave >= 3) unlocked.Add(MonsterMovementPattern.Knight);
            if (currentWave >= 5)
            {
                unlocked.Add(MonsterMovementPattern.Bishop);
                unlocked.Add(MonsterMovementPattern.Rook);
            }
            foreach (MonsterMovementPattern pattern in unlocked)
            {
                int index = waveSpawnPlan.LastIndexOf(MonsterMovementPattern.CardinalFour);
                if (index < 0) index = waveSpawnPlan.LastIndexOf(MonsterMovementPattern.EightDirection);
                if (index >= 0) waveSpawnPlan.RemoveAt(index);
            }
            waveSpawnPlan.InsertRange(0, unlocked);
        }
        int spawnedCount = 0;

        foreach (MonsterMovementPattern movementPattern in waveSpawnPlan)
        {
            if (SpawnMonster(movementPattern))
            {
                spawnedCount++;

            }
        }

        waveActive = spawnedCount > 0;

        Debug.Log($"웨이브 {currentWave}: 총 {spawnedCount}마리 (체스 패턴 포함: {enableChessWaves})", this);
        WaveStarted?.Invoke(currentWave);

        if (!waveActive)
        {
            nextWaveTime = Time.time + nextWaveDelay;
        }
    }

    private void CompleteCurrentWave()
    {
        waveActive = false;
        if (enableChessWaves)
        {
            projectileManager.ClearProjectiles();
            FireTrail?.Clear();
        }

        int requestedHeal = playerHealth == null
            ? 0
            : Mathf.Max(1, Mathf.CeilToInt(playerHealth.MaxHealth * WaveClearHealRatio));
        int healedAmount = playerHealth != null ? playerHealth.Heal(requestedHeal) : 0;

        Debug.Log(
            $"웨이브 {currentWave} 클리어: 최대 체력의 20% 회복 "
            + $"({healedAmount}/{requestedHeal})",
            this
        );
        WaveCleared?.Invoke(currentWave);
        nextWaveTime = Time.time + nextWaveDelay;
    }

    public float GetBatRatioForWave(int waveNumber)
    {
        if (waveNumber <= 3)
            return 0f;

        if (waveNumber <= 5)
            return batRatioWave4To5;

        if (waveNumber <= 8)
            return batRatioWave6To8;

        return batRatioWave9AndLater;
    }

    private int CalculateBatCount(int waveNumber, int totalMonsterCount)
    {
        float batRatio = GetBatRatioForWave(waveNumber);

        if (batRatio <= 0f || totalMonsterCount <= 0)
            return 0;

        // 박쥐가 해금된 웨이브에는 최소 한 마리가 등장하도록 보장한다.
        return Mathf.Clamp(
            Mathf.Max(1, Mathf.RoundToInt(totalMonsterCount * batRatio)),
            0,
            totalMonsterCount
        );
    }

    private void BuildWaveSpawnPlan(int totalMonsterCount, int batCount)
    {
        waveSpawnPlan.Clear();

        for (int i = 0; i < batCount; i++)
        {
            waveSpawnPlan.Add(MonsterMovementPattern.EightDirection);
        }

        for (int i = batCount; i < totalMonsterCount; i++)
        {
            waveSpawnPlan.Add(MonsterMovementPattern.CardinalFour);
        }

        // 목표 마릿수는 유지하면서 스폰 순서만 섞는다.
        for (int i = waveSpawnPlan.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            MonsterMovementPattern temporary = waveSpawnPlan[i];
            waveSpawnPlan[i] = waveSpawnPlan[swapIndex];
            waveSpawnPlan[swapIndex] = temporary;
        }
    }

    private bool SpawnMonster(MonsterMovementPattern movementPattern)
    {
        if (enableChessWaves && movementPattern >= MonsterMovementPattern.Knight)
        {
            if (!TryGetSpawnCell(out Vector3Int chessCell)) return false;
            MonsterMovement chessMonster = SpawnPlaytestMonster(movementPattern, chessCell,
                chessMonsterSprite, ChessWaveDisplay.ColorFor(movementPattern), chessMonsterHealth,
                ++spawnSerial, knightActionInterval, bishopMoveDistance, bishopOrbitDistance);
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
        activeMonsters.Add(monster);
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
        int startIndex = Random.Range(0, spawnCells.Count);

        for (int i = 0; i < spawnCells.Count; i++)
        {
            Vector3Int candidate = spawnCells[(startIndex + i) % spawnCells.Count];
            int distanceFromPlayer = Mathf.Abs(candidate.x - playerCell.x)
                + Mathf.Abs(candidate.y - playerCell.y);

            if (candidate == playerCell
                || distanceFromPlayer < minimumPlayerDistance
                || IsOccupied(candidate))
                continue;

            spawnCell = candidate;
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
        firstWaveMonsterCount = Mathf.Max(1, firstWaveMonsterCount);
        monsterIncreasePerWave = Mathf.Max(1, monsterIncreasePerWave);
        nextWaveDelay = Mathf.Max(0f, nextWaveDelay);
        edgeInsetTiles = Mathf.Max(1, edgeInsetTiles);
        minimumPlayerDistance = Mathf.Max(0, minimumPlayerDistance);
        batRatioWave4To5 = Mathf.Clamp01(batRatioWave4To5);
        batRatioWave6To8 = Mathf.Clamp01(batRatioWave6To8);
        batRatioWave9AndLater = Mathf.Clamp01(batRatioWave9AndLater);

        if (Application.isPlaying && gridManager != null)
        {
            gridManager.Initialize(mapTilemap, edgeInsetTiles);
        }
    }

    private void OnDestroy()
    {
        if (playerMovement != null)
        {
            playerMovement.MoveCompleted -= BeginMonsterTurn;
        }
    }
}
