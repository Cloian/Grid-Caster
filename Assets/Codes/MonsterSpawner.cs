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

    private readonly List<MonsterMovement> activeMonsters = new List<MonsterMovement>();
    private readonly List<Vector3Int> spawnCells = new List<Vector3Int>();
    private readonly List<MonsterMovementPattern> waveSpawnPlan =
        new List<MonsterMovementPattern>();

    private readonly HashSet<Vector3Int> blockedMonsterCells = new HashSet<Vector3Int>();

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

    public int CurrentWave => currentWave;

    private void Awake()
    {
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

        playerMovement.MoveCompleted += BeginMonsterTurn;

        BuildSpawnCells();
        SpawnNextWave();
    }

    private void Update()
    {
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

        if (monsterPrefabs == null || monsterPrefabs.Length == 0)
        {
            Debug.LogError("MonsterSpawner에 몬스터 프리팹이 필요합니다.", this);
            return false;
        }

        return true;
    }

    private void BeginMonsterTurn()
    {
        worldTurnInProgress = true;
        monsterTurnCompleted = false;
        projectileTurnCompleted = false;

        // 플레이어 기본 투사체는 이 시점 전에 전체 경로 처리가 끝난다.
        // 여기서는 보드에 남는 적/특수 투사체의 한 칸 행동만 처리한다.
        // 투사체 목적지와 피격을 먼저 확정한 뒤 살아남은 적만 행동한다.
        // 시각 이동은 같은 프레임 안에서 이어서 시작하므로 화면에서는 동시에 움직인다.
        projectileManager.TakeTurn(OnProjectileTurnCompleted);

        activeMonsters.RemoveAll(monster => monster == null || monster.IsDead);
        monstersStillMoving = activeMonsters.Count;
        blockedMonsterCells.Clear();

        foreach (MonsterMovement monster in activeMonsters)
        {
            blockedMonsterCells.Add(mapTilemap.WorldToCell(monster.transform.position));
        }

        if (monstersStillMoving == 0)
        {
            monsterTurnCompleted = true;
        }
        else
        {
            // 적과 투사체는 같은 프레임에 각각 한 타일 행동을 시작한다.
            foreach (MonsterMovement monster in activeMonsters)
            {
                monster.TakeTurn(
                    OnMonsterMoveCompleted,
                    destinationCell => TryReserveMonsterCell(monster, destinationCell)
                );
            }
        }

        TryCompleteWorldTurn();
    }

    private bool TryReserveMonsterCell(
        MonsterMovement movingMonster,
        Vector3Int destinationCell
    )
    {
        // 현재 다른 몬스터가 있거나 이번 턴에 이미 예약된 타일은 사용할 수 없다.
        if (blockedMonsterCells.Contains(destinationCell))
            return false;

        // 몬스터가 투사체의 도착 타일로 들어오면 실제 이동 전에 피격을 먼저 확정한다.
        projectileManager.TryHitMonsterEnteringCell(movingMonster, destinationCell);

        if (movingMonster == null || movingMonster.IsDead)
            return true;

        return blockedMonsterCells.Add(destinationCell);
    }

    public bool TryGetMonsterAtCell(Vector3Int cell, out MonsterMovement targetMonster)
    {
        activeMonsters.RemoveAll(monster => monster == null || monster.IsDead);

        foreach (MonsterMovement monster in activeMonsters)
        {
            if (gridManager.WorldToCell(monster.transform.position) == cell)
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

        Vector3Int currentCell = gridManager.WorldToCell(targetMonster.transform.position);
        Vector3Int destinationCell = currentCell + normalizedDirection;

        if (!gridManager.IsWalkableCell(destinationCell))
            return false;

        if (player != null
            && gridManager.WorldToCell(player.position) == destinationCell)
        {
            return false;
        }

        if (TryGetMonsterAtCell(destinationCell, out MonsterMovement occupyingMonster)
            && occupyingMonster != targetMonster)
        {
            return false;
        }

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
        if (!worldTurnInProgress || !monsterTurnCompleted || !projectileTurnCompleted)
            return;

        worldTurnInProgress = false;
        playerMovement.CompleteMonsterTurn();
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
        int spawnedCount = 0;
        int spawnedBatCount = 0;

        foreach (MonsterMovementPattern movementPattern in waveSpawnPlan)
        {
            if (SpawnMonster(movementPattern))
            {
                spawnedCount++;

                if (movementPattern == MonsterMovementPattern.EightDirection)
                {
                    spawnedBatCount++;
                }
            }
        }

        waveActive = spawnedCount > 0;

        int spawnedSnakeCount = spawnedCount - spawnedBatCount;
        Debug.Log(
            $"웨이브 {currentWave}: Snake {spawnedSnakeCount} / Bat {spawnedBatCount} "
            + $"(총 {spawnedCount}마리)",
            this
        );
        WaveStarted?.Invoke(currentWave);

        if (!waveActive)
        {
            nextWaveTime = Time.time + nextWaveDelay;
        }
    }

    private void CompleteCurrentWave()
    {
        waveActive = false;

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
        MonsterMovement prefab = GetNextPrefab(movementPattern);

        if (prefab == null || !TryGetSpawnCell(out Vector3Int spawnCell))
            return false;

        Vector3 spawnPosition = mapTilemap.GetCellCenterWorld(spawnCell);
        MonsterMovement monster = Instantiate(prefab, spawnPosition, Quaternion.identity, transform);

        spawnSerial++;
        monster.name = $"{prefab.name}_{spawnSerial:00}";
        monster.Initialize(player, gridManager);
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

        Vector3Int playerCell = mapTilemap.WorldToCell(player.position);
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

    private bool IsOccupied(Vector3Int cell)
    {
        foreach (MonsterMovement monster in activeMonsters)
        {
            if (monster != null
                && !monster.IsDead
                && mapTilemap.WorldToCell(monster.transform.position) == cell)
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
