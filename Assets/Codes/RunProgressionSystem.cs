using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class RelicOffer
{
    public RelicOffer(RelicDefinition relic, Vector3Int cell, bool risky)
    {
        Relic = relic;
        Cell = cell;
        IsRisky = risky;
    }

    public RelicDefinition Relic { get; }
    public Vector3Int Cell { get; }
    public bool IsRisky { get; }
}

// 한 런 안의 강화와 유물만 관리하며 사망/재시작 시 씬과 함께 초기화된다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Move))]
public sealed class RunProgressionSystem : MonoBehaviour
{
    public const int MaxRelicSlots = 4;

    public event Action<IReadOnlyList<UpgradeDefinition>> UpgradeOptionsReady;
    public event Action UpgradesChanged;
    public event Action<IReadOnlyList<RelicDefinition>, int> RelicsChanged;
    public event Action<IReadOnlyList<RelicOffer>> AltarsChanged;
    public event Action<RelicDefinition, IReadOnlyList<RelicDefinition>> RelicReplacementRequested;
    public event Action<PlayerMovementArt, int> MovementArtChanged;

    private static readonly Vector3Int[] EightDirections =
    {
        Vector3Int.up, new Vector3Int(1, 1, 0), Vector3Int.right,
        new Vector3Int(1, -1, 0), Vector3Int.down, new Vector3Int(-1, -1, 0),
        Vector3Int.left, new Vector3Int(-1, 1, 0)
    };

    private readonly Dictionary<string, int> upgradeStacks = new Dictionary<string, int>();
    private readonly List<RelicDefinition> relics = new List<RelicDefinition>();
    private readonly List<RelicOffer> activeAltars = new List<RelicOffer>();
    private readonly HashSet<string> shownRelics = new HashSet<string>();
    private readonly List<GameObject> altarMarkers = new List<GameObject>();
    private readonly List<UpgradeDefinition> currentUpgradeOptions = new List<UpgradeDefinition>();

    private Move playerMovement;
    private CharacterHealth playerHealth;
    private PlayerTraitSystem traitSystem;
    private UltimateGauge ultimateGauge;
    private MonsterSpawner monsterSpawner;
    private GridManager gridManager;
    private StageFlowManager stageFlow;
    private RelicDefinition pendingRelic;
    private Action pendingRelicResume;
    private Texture2D markerTexture;
    private Sprite markerSprite;
    private BasicActionType lastBasicAction;
    private bool firstDamageBlocked;
    private bool firstHazardBlocked;
    private bool firstKillHealed;
    private bool stitchHealed;
    private bool kingsTurnUsed;
    private bool movementLandingGuard;
    private bool attackHunterBonus;
    private bool attackStoredBonus;
    private bool storedAttackBonus;
    private bool firstCastKilled;
    private bool attackKilledAny;
    private bool recoveryGranted;
    private bool relicAcquiredThisWave;
    private bool starterSelectionPending;
    private int attackHitCount;
    private bool initialized;

    public PlayerMovementArt ActiveMovementArt { get; private set; }
    public int MovementArtLevel { get; private set; }
    public bool HasMovementArt => ActiveMovementArt != PlayerMovementArt.None;
    public string MovementArtName => ActiveMovementArt switch
    {
        PlayerMovementArt.Knight => "나이트 도약",
        PlayerMovementArt.Bishop => "비숍 활보",
        PlayerMovementArt.Rook => "룩 돌진",
        _ => "이동술 미보유"
    };

    public int Supply { get; private set; }
    public IReadOnlyList<RelicDefinition> Relics => relics;
    public IReadOnlyList<RelicOffer> ActiveAltars => activeAltars;
    public IReadOnlyList<UpgradeDefinition> CurrentUpgradeOptions => currentUpgradeOptions;
    public bool IsStarterUpgradeSelection => starterSelectionPending;
    public int MoveGaugeBonus => Stack("mana_circulation")
        + (Stack("rapid_cycle") > 0 ? 2 : 0);
    public int HitGaugeBonus => Stack("mana_circulation") * 2
        + (Stack("rapid_cycle") > 0 ? 5 : 0)
        + (Stack("trait_mastery") > 0 ? 3 : 0);
    public float WaveHealRatioBonus => Stack("healing_breath") * 0.05f
        + (Stack("immortal_cycle") > 0 ? 0.1f : 0f);
    public bool HasRotatingMirror => HasRelic("rotating_mirror");

    private void Awake()
    {
        playerMovement = GetComponent<Move>();
        playerHealth = GetComponent<CharacterHealth>();
        traitSystem = GetComponent<PlayerTraitSystem>();
        ultimateGauge = GetComponent<UltimateGauge>();
    }

    private void Start()
    {
        TryInitialize();
    }

    public void TryInitialize()
    {
        if (initialized)
            return;

        monsterSpawner = FindAnyObjectByType<MonsterSpawner>();
        gridManager = FindAnyObjectByType<GridManager>();
        stageFlow = monsterSpawner != null ? monsterSpawner.StageFlow : null;
        if (monsterSpawner == null || gridManager == null || stageFlow == null)
            return;

        stageFlow.UpgradeSelectionRequested += HandleUpgradeSelectionRequested;
        monsterSpawner.WaveStarted += HandleWaveStarted;
        monsterSpawner.WaveCleared += HandleWaveCleared;
        if (traitSystem != null) traitSystem.TraitSelected += HandleTraitSelected;
        initialized = true;
    }

    public int Stack(string upgradeId)
    {
        return upgradeStacks.TryGetValue(upgradeId, out int value) ? value : 0;
    }

    public bool HasRelic(string relicId)
    {
        return relics.Any(relic => relic.Id == relicId);
    }

    private void HandleUpgradeSelectionRequested(int clearedWave)
    {
        WaveTemplate clearedTemplate = WaveTemplateCatalog.Get(clearedWave);
        if (clearedTemplate.IsRelicWave)
        {
            if (relicAcquiredThisWave)
                BuildAdvancedUpgradeOptions(clearedWave / 4);
            else
                BuildUpgradeOptions(clearedWave);
        }
        else if (RunProgressionCatalog.IsStandardUpgradeWave(clearedWave))
        {
            BuildUpgradeOptions(clearedWave);
        }
        else
        {
            StartCoroutine(AdvanceWithoutReward());
            return;
        }

        if (UpgradeOptionsReady == null)
        {
            SelectUpgrade(0);
            return;
        }
        UpgradeOptionsReady.Invoke(currentUpgradeOptions);
    }

    private void BuildUpgradeOptions(int clearedWave)
    {
        currentUpgradeOptions.Clear();
        string traitId = traitSystem != null ? traitSystem.SelectedTraitId : string.Empty;
        List<UpgradeDefinition> eligible = RunProgressionCatalog.Upgrades
            .Where(item => (!item.IsTraitUpgrade || item.RequiredTrait == traitId)
                && Stack(item.Id) < item.MaxStacks)
            .ToList();
        List<UpgradeDefinition> traitEligible = eligible
            .Where(item => item.RequiredTrait == traitId).ToList();

        if (clearedWave == 2 && !HasMovementArt)
        {
            // 첫 특수 웨이브 전에 이동 선택지가 생기도록 2웨이브에서 반드시 제시한다.
            UpgradeDefinition movementUnlock = eligible
                .FirstOrDefault(item => item.Id == "movement_knight");
            if (movementUnlock != null)
            {
                currentUpgradeOptions.Add(movementUnlock);
                eligible.Remove(movementUnlock);
            }
        }

        if (traitEligible.Count > 0)
        {
            AddRandomAndRemove(traitEligible, eligible, currentUpgradeOptions);
        }
        while (currentUpgradeOptions.Count < 3 && eligible.Count > 0)
        {
            AddRandomAndRemove(eligible, eligible, currentUpgradeOptions);
        }
    }

    private void BuildAdvancedUpgradeOptions(int rewardTier)
    {
        currentUpgradeOptions.Clear();
        currentUpgradeOptions.AddRange(RunProgressionCatalog.AdvancedUpgrades
            .Where(item => item.RewardTier == rewardTier && Stack(item.Id) == 0));
    }

    private IEnumerator AdvanceWithoutReward()
    {
        // 이전 웨이브 제단 정리가 끝난 다음 새 웨이브를 시작해 이벤트 재진입을 피한다.
        yield return null;
        if (stageFlow != null && stageFlow.IsWaitingForUpgrade)
        {
            stageFlow.CompleteUpgradeSelection();
        }
    }

    private static void AddRandomAndRemove(List<UpgradeDefinition> source,
        List<UpgradeDefinition> all, List<UpgradeDefinition> destination)
    {
        UpgradeDefinition selected = source[UnityEngine.Random.Range(0, source.Count)];
        destination.Add(selected);
        source.Remove(selected);
        if (!ReferenceEquals(source, all)) all.Remove(selected);
    }

    public bool SelectUpgrade(int index)
    {
        bool isWaveReward = stageFlow != null && stageFlow.IsWaitingForUpgrade;
        if ((!starterSelectionPending && !isWaveReward)
            || index < 0 || index >= currentUpgradeOptions.Count)
            return false;

        UpgradeDefinition selected = currentUpgradeOptions[index];
        upgradeStacks[selected.Id] = Stack(selected.Id) + 1;
        ApplyImmediateUpgrade(selected.Id);
        UpgradesChanged?.Invoke();
        currentUpgradeOptions.Clear();

        if (starterSelectionPending)
        {
            starterSelectionPending = false;
            playerMovement.SetInputEnabled(true);
            return true;
        }

        return stageFlow.CompleteUpgradeSelection();
    }

    private void ApplyImmediateUpgrade(string upgradeId)
    {
        if (upgradeId == "movement_knight")
        {
            SetMovementArt(PlayerMovementArt.Knight);
        }
        else if (upgradeId == "movement_bishop")
        {
            SetMovementArt(PlayerMovementArt.Bishop);
        }
        else if (upgradeId == "movement_rook")
        {
            SetMovementArt(PlayerMovementArt.Rook);
        }
        else if (upgradeId == "movement_training_1")
        {
            TrainMovementArt(PlayerMovementArt.Knight);
        }
        else if (upgradeId == "movement_training_2")
        {
            TrainMovementArt(PlayerMovementArt.Knight);
        }
        else if (upgradeId == "movement_training_3")
        {
            TrainMovementArt(PlayerMovementArt.Bishop);
        }
        else if (upgradeId == "movement_training_4")
        {
            TrainMovementArt(PlayerMovementArt.Rook);
        }
        else if (upgradeId == "life_tempering")
        {
            playerHealth.IncreaseMaxHealth(1, true);
        }
        else if (upgradeId == "keen_magic")
        {
            playerHealth.AdjustMaxHealth(-1);
        }
        ApplyTraitBagSize();
    }

    private void SetMovementArt(PlayerMovementArt movementArt)
    {
        ActiveMovementArt = movementArt;
        MovementArtLevel = 1;
        MovementArtChanged?.Invoke(ActiveMovementArt, MovementArtLevel);
    }

    private void TrainMovementArt(PlayerMovementArt fallbackArt)
    {
        if (!HasMovementArt)
        {
            SetMovementArt(fallbackArt);
            return;
        }

        MovementArtLevel = Mathf.Min(3, MovementArtLevel + 1);
        MovementArtChanged?.Invoke(ActiveMovementArt, MovementArtLevel);
    }

    public void SetMovementArtForPlaytest(PlayerMovementArt movementArt, int level = 1)
    {
        ActiveMovementArt = movementArt;
        MovementArtLevel = movementArt == PlayerMovementArt.None ? 0 : Mathf.Clamp(level, 1, 3);
        MovementArtChanged?.Invoke(ActiveMovementArt, MovementArtLevel);
    }

    public void NotifyMovementArtUsed()
    {
        int returnedGauge = Stack("movement_breath") * 5;
        if (returnedGauge > 0) ultimateGauge?.AddGauge(returnedGauge);
        if (HasRelic("landing_ward")) movementLandingGuard = true;
        if (HasRelic("ambush_crest")
            || (ActiveMovementArt == PlayerMovementArt.Knight && MovementArtLevel >= 3))
            storedAttackBonus = true;
    }

    private void HandleTraitSelected(string traitId)
    {
        ApplyTraitBagSize();

        currentUpgradeOptions.Clear();
        currentUpgradeOptions.AddRange(RunProgressionCatalog.Upgrades
            .Where(item => item.RequiredTrait == traitId && Stack(item.Id) < item.MaxStacks)
            .Take(3));

        if (currentUpgradeOptions.Count == 0)
            return;

        // 첫 행동 전에 선택한 특성의 방향을 한 번 더 구체화한다.
        starterSelectionPending = true;
        playerMovement.SetInputEnabled(false);
        if (UpgradeOptionsReady == null)
        {
            SelectUpgrade(0);
            return;
        }
        UpgradeOptionsReady.Invoke(currentUpgradeOptions);
    }

    private void ApplyTraitBagSize()
    {
        if (traitSystem == null || string.IsNullOrEmpty(traitSystem.SelectedTraitId)) return;
        int baseSize;
        int reduction;
        switch (traitSystem.SelectedTraitId)
        {
            case "double_cast": baseSize = 5; reduction = Stack("quick_cast"); break;
            case "pierce": baseSize = 3; reduction = Stack("thin_reload"); break;
            case "damage_boost": baseSize = 4; reduction = Stack("dense_mana"); break;
            case "knockback": baseSize = 4; reduction = Stack("compressed_impact"); break;
            default: return;
        }
        reduction += Stack("trait_acceleration") + Stack("trait_mastery");
        traitSystem.SetActivationBagSize(Mathf.Max(2, baseSize - reduction));
    }

    private void HandleWaveStarted(int waveNumber)
    {
        firstDamageBlocked = false;
        firstHazardBlocked = false;
        firstKillHealed = false;
        stitchHealed = false;
        kingsTurnUsed = false;
        movementLandingGuard = false;
        relicAcquiredThisWave = false;

        if (WaveTemplateCatalog.Get(waveNumber).IsRelicWave)
        {
            SpawnRelicAltars(waveNumber);
        }
    }

    private void HandleWaveCleared(int waveNumber)
    {
        ClearAltars();
    }

    private void SpawnRelicAltars(int waveNumber)
    {
        ClearAltars();
        List<Vector3Int> candidates = ReachableEmptyCells();
        if (candidates.Count < 2) return;

        Vector3Int playerCell = playerMovement.GridPosition;
        Vector3Int safeCell = candidates
            .OrderBy(cell => Chebyshev(cell, playerCell))
            .ThenByDescending(DistanceFromNearestMonster)
            .First();
        candidates.Remove(safeCell);
        Vector3Int riskyCell = candidates
            .OrderBy(DistanceFromNearestMonster)
            .ThenByDescending(cell => Chebyshev(cell, playerCell))
            .First();

        AddAltarOffer(waveNumber, safeCell, false);
        AddAltarOffer(waveNumber, riskyCell, true);
        CreateAltarMarkers();
        AltarsChanged?.Invoke(activeAltars);
    }

    private void AddAltarOffer(int waveNumber, Vector3Int cell, bool risky)
    {
        string traitId = traitSystem != null ? traitSystem.SelectedTraitId : string.Empty;
        List<RelicDefinition> eligible = RunProgressionCatalog.Relics
            .Where(item => item.MinimumWave <= waveNumber && !shownRelics.Contains(item.Id)
                && (!item.IsTraitRelic || item.RequiredTrait == traitId)).ToList();
        if (eligible.Count == 0) return;

        RelicRarity rolled = RollRarity(waveNumber, risky);
        List<RelicDefinition> rarityPool = eligible.Where(item => item.Rarity == rolled).ToList();
        if (rarityPool.Count == 0)
        {
            rarityPool = eligible.OrderBy(item => Mathf.Abs((int)item.Rarity - (int)rolled)).ToList();
        }
        RelicDefinition selected = rarityPool[UnityEngine.Random.Range(0, rarityPool.Count)];
        shownRelics.Add(selected.Id);
        activeAltars.Add(new RelicOffer(selected, cell, risky));
    }

    private RelicRarity RollRarity(int waveNumber, bool risky)
    {
        if (waveNumber >= 16)
            return UnityEngine.Random.value < (risky ? 0.45f : 0.3f)
                ? RelicRarity.Legendary : RelicRarity.Rare;
        float legendary = waveNumber >= 12 ? 0.15f : waveNumber >= 8 ? 0.08f : 0.02f;
        float rare = waveNumber >= 12 ? 0.45f : waveNumber >= 8 ? 0.35f : 0.28f;
        if (risky) { legendary += 0.08f; rare += 0.12f; }
        float roll = UnityEngine.Random.value;
        if (roll < legendary) return RelicRarity.Legendary;
        return roll < legendary + rare ? RelicRarity.Rare : RelicRarity.Common;
    }

    private List<Vector3Int> ReachableEmptyCells()
    {
        List<Vector3Int> result = new List<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Vector3Int origin = playerMovement.GridPosition;
        queue.Enqueue(origin);
        visited.Add(origin);
        while (queue.Count > 0)
        {
            Vector3Int cell = queue.Dequeue();
            foreach (Vector3Int direction in EightDirections)
            {
                Vector3Int next = cell + direction;
                // 제단은 현재 배치에서 실제로 걸어갈 수 있는 빈 칸에만 놓는다.
                // 적이 있는 칸을 후보에서만 빼는 것이 아니라 BFS 통로로도 사용하지 않는다.
                if (visited.Contains(next)
                    || !gridManager.IsWalkableCell(next)
                    || monsterSpawner.TryGetMonsterAtCell(next, out _)) continue;
                visited.Add(next);
                queue.Enqueue(next);
                if (next != origin) result.Add(next);
            }
        }
        return result;
    }

    private int DistanceFromNearestMonster(Vector3Int cell)
    {
        int distance = int.MaxValue;
        foreach (MonsterMovement monster in monsterSpawner.ActiveMonsters)
            if (monster != null && !monster.IsDead)
                distance = Mathf.Min(distance, Chebyshev(cell, monster.GridPosition));
        return distance == int.MaxValue ? 99 : distance;
    }

    private static int Chebyshev(Vector3Int first, Vector3Int second)
    {
        return Mathf.Max(Mathf.Abs(first.x - second.x), Mathf.Abs(first.y - second.y));
    }

    private void CreateAltarMarkers()
    {
        if (markerSprite == null)
        {
            markerTexture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            markerTexture.SetPixel(0, 0, Color.white);
            markerTexture.Apply();
            markerSprite = Sprite.Create(markerTexture, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);
        }
        foreach (RelicOffer offer in activeAltars)
        {
            GameObject marker = new GameObject(offer.IsRisky ? "RiskyRelicAltar" : "SafeRelicAltar");
            marker.transform.position = gridManager.GetCellCenterWorld(offer.Cell);
            marker.transform.localScale = Vector3.one * 0.78f;
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.color = offer.IsRisky
                ? new Color(1f, 0.28f, 0.18f, 0.68f)
                : new Color(0.2f, 0.9f, 1f, 0.68f);
            renderer.sortingOrder = 4;
            altarMarkers.Add(marker);
        }
    }

    public bool TryHandleAltarAtCell(Vector3Int cell, Action resumeWorldTurn)
    {
        RelicOffer offer = activeAltars.FirstOrDefault(item => item.Cell == cell);
        if (offer == null) return false;
        ClearAltars();

        if (relics.Count < MaxRelicSlots)
        {
            relicAcquiredThisWave = true;
            AddRelic(offer.Relic);
            return false;
        }
        if (RelicReplacementRequested == null)
        {
            Supply++;
            NotifyRelicsChanged();
            return false;
        }
        pendingRelic = offer.Relic;
        pendingRelicResume = resumeWorldTurn;
        RelicReplacementRequested.Invoke(pendingRelic, relics);
        return true;
    }

    public bool ResolvePendingRelic(int replaceIndex)
    {
        if (pendingRelic == null) return false;
        if (replaceIndex >= 0 && replaceIndex < relics.Count)
        {
            RemoveRelicEffects(relics[replaceIndex]);
            relics[replaceIndex] = pendingRelic;
            ApplyRelicEffects(pendingRelic);
            relicAcquiredThisWave = true;
        }
        else
        {
            Supply++;
        }
        pendingRelic = null;
        NotifyRelicsChanged();
        Action resume = pendingRelicResume;
        pendingRelicResume = null;
        resume?.Invoke();
        return true;
    }

    private void AddRelic(RelicDefinition relic)
    {
        relics.Add(relic);
        ApplyRelicEffects(relic);
        NotifyRelicsChanged();
    }

    private void ApplyRelicEffects(RelicDefinition relic)
    {
        if (relic.Id == "glass_heart") playerHealth.AdjustMaxHealth(-3);
    }

    private void RemoveRelicEffects(RelicDefinition relic)
    {
        if (relic.Id == "glass_heart") playerHealth.AdjustMaxHealth(3);
    }

    private void NotifyRelicsChanged()
    {
        RelicsChanged?.Invoke(relics, Supply);
    }

    private void ClearAltars()
    {
        activeAltars.Clear();
        foreach (GameObject marker in altarMarkers) if (marker != null) Destroy(marker);
        altarMarkers.Clear();
        AltarsChanged?.Invoke(activeAltars);
    }

    public void NotifyMoveAction()
    {
        if (lastBasicAction == BasicActionType.Attack)
        {
            int alternatingGain = (HasRelic("alternating_gear") ? 5 : 0)
                + (Stack("alternating_overload") > 0 ? 10 : 0);
            if (alternatingGain > 0 && ultimateGauge != null)
            {
                ultimateGauge.AddGauge(alternatingGain);
            }
        }
        lastBasicAction = BasicActionType.Move;
    }

    public void BeginAttack(PlayerAttackTraitRoll roll)
    {
        if (lastBasicAction == BasicActionType.Move)
        {
            int alternatingGain = (HasRelic("alternating_gear") ? 5 : 0)
                + (Stack("alternating_overload") > 0 ? 10 : 0);
            if (alternatingGain > 0 && ultimateGauge != null)
            {
                ultimateGauge.AddGauge(alternatingGain);
            }
        }
        attackHunterBonus = lastBasicAction == BasicActionType.Move
            && (HasRelic("hunters_mark") || Stack("hunter_instinct") > 0);
        attackStoredBonus = storedAttackBonus;
        storedAttackBonus = false;
        lastBasicAction = BasicActionType.Attack;
        firstCastKilled = false;
        attackKilledAny = false;
        recoveryGranted = false;
        attackHitCount = 0;
    }

    public int ModifyAttackDamage(int baseDamage, PlayerAttackTraitRoll roll, int castIndex)
    {
        int damage = baseDamage;
        if (roll.DamageBoost) damage += 1 + Stack("overcharge");
        if (HasRelic("glass_heart")) damage++;
        if (Stack("keen_magic") > 0) damage++;
        if (Stack("arcane_overdrive") > 0) damage++;
        if (attackHunterBonus) damage++;
        if (attackStoredBonus) damage++;
        if (castIndex > 0 && firstCastKilled && HasRelic("twin_focus")) damage++;
        return damage;
    }

    public int ModifyPenetrations(PlayerAttackTraitRoll roll)
    {
        if (!roll.Pierce) return 0;
        if (HasRelic("infinite_orbit")) return 99;
        return 1 + Stack("long_needle");
    }

    public int AttackCastCount(PlayerAttackTraitRoll roll)
    {
        if (!roll.DoubleCast) return 1;
        return HasRelic("triple_echo") ? 3 : 2;
    }

    public int ModifyPenetrationDamage(int damage, PlayerAttackTraitRoll roll, int hitIndex)
    {
        return roll.Pierce && hitIndex == 1 && HasRelic("thorn_needle") ? damage + 1 : damage;
    }

    public void NotifyAttackHit(MonsterMovement target, PlayerAttackTraitRoll roll,
        int castIndex, int hitIndex)
    {
        attackHitCount++;
        if (!recoveryGranted && roll.DoubleCast && Stack("echo_recovery") > 0)
        {
            ultimateGauge.AddGauge(5 * Stack("echo_recovery"));
            recoveryGranted = true;
        }
        if (roll.Pierce && hitIndex > 0 && Stack("pierce_recovery") > 0)
            ultimateGauge.AddGauge(3 * Stack("pierce_recovery"));
        if (!recoveryGranted && roll.DamageBoost)
        {
            ultimateGauge.AddGauge(5 * Stack("afterglow_recovery")
                + (HasRelic("hot_afterglow") ? 10 : 0));
            recoveryGranted = true;
        }

        if (roll.DamageBoost && HasRelic("blast_core") && target != null)
        {
            MonsterMovement[] snapshot = monsterSpawner.ActiveMonsters.ToArray();
            foreach (MonsterMovement other in snapshot)
                if (other != null && other != target && !other.IsDead
                    && Chebyshev(other.GridPosition, target.GridPosition) == 1)
                    other.TakeDamage(1);
        }

        if (target == null || !target.IsDead) return;
        attackKilledAny = true;
        if (castIndex == 0) firstCastKilled = true;
        if (!firstKillHealed)
        {
            int heal = (HasRelic("blood_knot") ? 1 : 0)
                + (Stack("combat_regeneration") > 0 ? 1 : 0)
                + (Stack("immortal_cycle") > 0 ? 2 : 0);
            if (heal > 0)
            {
                firstKillHealed = true;
                playerHealth.Heal(heal);
            }
        }
        if (roll.DamageBoost && HasRelic("excess_reservoir")) storedAttackBonus = true;
    }

    public bool CompleteAttack(PlayerAttackTraitRoll roll)
    {
        if (roll.Pierce && attackHitCount >= 2 && !stitchHealed
            && HasRelic("suture_needle"))
        {
            stitchHealed = true;
            playerHealth.Heal(1);
        }
        bool livingEnemyRemains = monsterSpawner.ActiveMonsters.Any(
            monster => monster != null && !monster.IsDead);
        if (!kingsTurnUsed && attackKilledAny && livingEnemyRemains
            && HasRelic("kings_turn"))
        {
            kingsTurnUsed = true;
            return true;
        }
        return false;
    }

    public int ModifyIncomingDamage(int damage, PlayerDamageKind kind)
    {
        if (Stack("arcane_overdrive") > 0) damage++;
        if (movementLandingGuard)
        {
            movementLandingGuard = false;
            return Mathf.Max(0, damage - 1);
        }
        if (kind == PlayerDamageKind.Hazard && !firstHazardBlocked
            && (HasRelic("ash_boots") || Stack("flame_adaptation") > 0))
        {
            firstHazardBlocked = true;
            return 0;
        }
        if (!firstDamageBlocked
            && (HasRelic("vanguard_shield") || Stack("barrier_shell") > 0))
        {
            firstDamageBlocked = true;
            return Mathf.Max(0, damage - 1);
        }
        return damage;
    }

    public int KnockbackDistance => HasRelic("battering_ram")
        ? 3 : 1 + Stack("long_impact");

    public void NotifyKnockbackSuccess()
    {
        int stacks = Stack("impact_recovery");
        if (stacks > 0) ultimateGauge.AddGauge(5 * stacks);
    }

    private void OnDestroy()
    {
        if (stageFlow != null) stageFlow.UpgradeSelectionRequested -= HandleUpgradeSelectionRequested;
        if (monsterSpawner != null)
        {
            monsterSpawner.WaveStarted -= HandleWaveStarted;
            monsterSpawner.WaveCleared -= HandleWaveCleared;
        }
        if (traitSystem != null) traitSystem.TraitSelected -= HandleTraitSelected;
        ClearAltars();
        if (markerSprite != null) Destroy(markerSprite);
        if (markerTexture != null) Destroy(markerTexture);
    }
}
