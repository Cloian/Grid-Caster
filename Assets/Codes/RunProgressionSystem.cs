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
    public const int MaxMovementArtLevel = 3;

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
    private int stitchHeals;
    private bool kingsTurnUsed;
    private bool movementLandingGuard;
    private bool attackHunterBonus;
    private int attackStoredSources;
    private int storedAttackSources;
    private bool firstCastKilled;
    private bool attackKilledAny;
    private bool recoveryGranted;
    private bool relicAcquiredThisWave;
    private bool starterSelectionPending;
    private int attackHitCount;
    private int alternatingBurstDamage;
    private bool initialized;
    private const int KnightCharge = 1;
    private const int AmbushCharge = 2;
    private const int ExcessCharge = 4;
    private bool overflowTransferred;
    private readonly HashSet<int> rewardedKills = new HashSet<int>();
    private readonly Dictionary<string, HashSet<int>> areaHitTargets =
        new Dictionary<string, HashSet<int>>();

    public PlayerMovementArt ActiveMovementArt { get; private set; }
    public int MovementArtLevel { get; private set; }
    public bool HasMovementArt => ActiveMovementArt != PlayerMovementArt.None;
    public string SelectedTraitId => traitSystem != null ? traitSystem.SelectedTraitId : string.Empty;
    public int TraitActivationInterval => traitSystem != null ? traitSystem.ActivationBagSize : 1;
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
    // 이전 게이지 컴포넌트와 저장 씬의 호환만 유지한다. 이동술은 이제 충전 자원을 쓰지 않는다.
    public int MoveGaugeBonus => 0;
    public int HitGaugeBonus => 0;
    public int WaveHealFlatBonus => Stack("healing_breath")
        + (Stack("immortal_cycle") > 0 ? 1 : 0);
    public bool HasRotatingMirror => HasRelic("rotating_mirror");

    private void Awake()
    {
        playerMovement = GetComponent<Move>();
        playerHealth = GetComponent<CharacterHealth>();
        traitSystem = GetComponent<PlayerTraitSystem>();
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
            .Where(CanSelectUpgrade)
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
            // 핵심 부품이 끝까지 등장하지 않는 런을 줄이되 선택은 강제하지 않는다.
            List<UpgradeDefinition> unowned = traitEligible.Where(item => Stack(item.Id) == 0).ToList();
            AddRandomAndRemove(unowned.Count > 0 ? unowned : traitEligible,
                eligible, currentUpgradeOptions);
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
            .Where(item => item.RewardTier == rewardTier && CanSelectUpgrade(item))
            .Select(item => item.Id == "trait_mastery"
                ? new UpgradeDefinition(item.Id, item.Name, TraitMasteryDescription(), 1,
                    isAdvanced: true, rewardTier: rewardTier) : item));
        while (currentUpgradeOptions.Count < 3)
        {
            List<UpgradeDefinition> fallback = RunProgressionCatalog.Upgrades
                .Where(item => !item.IsTraitUpgrade && CanSelectUpgrade(item)
                    && !currentUpgradeOptions.Any(option => option.Id == item.Id)).ToList();
            if (fallback.Count == 0) break;
            currentUpgradeOptions.Add(fallback[UnityEngine.Random.Range(0, fallback.Count)]);
        }
    }

    private string TraitMasteryDescription()
    {
        switch (traitSystem.SelectedTraitId)
        {
            case "double_cast": return "추가 시전의 모든 직격 피해 +1";
            case "pierce": return "추가 관통 대상의 직격 피해 +1";
            case "damage_boost": return "특성 발동 첫 직격 주변 8칸 피해 1";
            case "knockback": return "특성 발동 적중 지점의 생존 대상/주변 최대 2명 봉쇄";
            default: return "시작 특성에 맞는 완성 효과";
        }
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
        // 이미 최대인 카드나 효과가 없는 이동술 카드는 오래된 UI 호출로도 적용하지 않는다.
        if (!CanSelectUpgrade(selected)) return false;
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

    public bool CanSelectUpgrade(UpgradeDefinition item)
    {
        if (item == null || Stack(item.Id) >= item.MaxStacks) return false;
        if (item.IsTraitUpgrade && item.RequiredTrait != SelectedTraitId) return false;
        if (item.Id.StartsWith("movement_training_"))
            return !HasMovementArt || MovementArtLevel < MaxMovementArtLevel;
        if (item.Id == "movement_knight") return !HasMovementArt;
        if (item.Id == "movement_bishop") return ActiveMovementArt != PlayerMovementArt.Bishop;
        if (item.Id == "movement_rook") return ActiveMovementArt != PlayerMovementArt.Rook;
        if (item.Id == "movement_breath") return HasMovementArt;
        return true;
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
        MovementArtLevel = HasMovementArt ? Mathf.Max(1, MovementArtLevel) : 1;
        ActiveMovementArt = movementArt;
        MovementArtChanged?.Invoke(ActiveMovementArt, MovementArtLevel);
    }

    private void TrainMovementArt(PlayerMovementArt fallbackArt)
    {
        if (!HasMovementArt)
        {
            SetMovementArt(fallbackArt);
            return;
        }

        MovementArtLevel = Mathf.Min(MaxMovementArtLevel, MovementArtLevel + 1);
        MovementArtChanged?.Invoke(ActiveMovementArt, MovementArtLevel);
    }

    public void SetMovementArtForPlaytest(PlayerMovementArt movementArt, int level = 1)
    {
        ActiveMovementArt = movementArt;
        MovementArtLevel = movementArt == PlayerMovementArt.None ? 0 : Mathf.Clamp(level, 1, MaxMovementArtLevel);
        MovementArtChanged?.Invoke(ActiveMovementArt, MovementArtLevel);
    }

    public void NotifyMovementArtUsed()
    {
        int lockCount = Stack("movement_breath")
            + (Stack("rapid_cycle") > 0 ? 1 : 0);
        if (lockCount > 0)
            LockAdjacentEnemies(playerMovement.GridPosition, lockCount, null);
        if (HasRelic("landing_ward")) movementLandingGuard = true;
        if (HasRelic("ambush_crest")) storedAttackSources |= AmbushCharge;
        if (ActiveMovementArt == PlayerMovementArt.Knight && MovementArtLevel >= 3)
            storedAttackSources |= KnightCharge;
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
        reduction += Stack("trait_acceleration");
        traitSystem.SetActivationBagSize(Mathf.Max(2, baseSize - reduction));
    }

    private void HandleWaveStarted(int waveNumber)
    {
        firstDamageBlocked = false;
        firstHazardBlocked = false;
        firstKillHealed = false;
        stitchHeals = 0;
        kingsTurnUsed = false;
        movementLandingGuard = false;
        relicAcquiredThisWave = false;
        storedAttackSources = 0;
        lastBasicAction = BasicActionType.None;

        if (WaveTemplateCatalog.Get(waveNumber).IsRelicWave)
        {
            SpawnRelicAltars(waveNumber);
        }
    }

    private void HandleWaveCleared(int waveNumber)
    {
        storedAttackSources = 0;
        lastBasicAction = BasicActionType.None;
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
                && (waveNumber < 16 || item.Rarity >= RelicRarity.Rare)
                && (!item.IsTraitRelic || item.RequiredTrait == traitId)).ToList();
        if (eligible.Count == 0) return;

        RelicRarity rolled = RollRarity(waveNumber, risky);
        RelicRarity minimumRarity = waveNumber >= 16 ? RelicRarity.Rare : RelicRarity.Common;
        if (!risky)
        {
            // 안전 제단이 마지막 최고 등급을 소진하지 않게 위험 제단의 후보를 남긴다.
            eligible = eligible.Where(item => eligible.Any(other => other.Id != item.Id
                && other.Rarity >= item.Rarity)).ToList();
            if (eligible.Count == 0) return;
        }
        else
        {
            RelicOffer safeOffer = activeAltars.FirstOrDefault(item => !item.IsRisky);
            if (safeOffer != null) minimumRarity = safeOffer.Relic.Rarity;
            // 원칙적으로 한 단계 높은 등급을 보장하고, 후보 소진 시에만 같은 등급을 허용한다.
            int preferredGrade = Math.Min((int)RelicRarity.Legendary,
                Math.Max((int)RelicRarity.Rare, (int)minimumRarity + 1));
            rolled = (RelicRarity)Math.Max((int)rolled, preferredGrade);
            eligible = eligible.Where(item => item.Rarity >= minimumRarity).ToList();
            if (eligible.Count == 0) return;
            if (eligible.Any(item => (int)item.Rarity >= preferredGrade))
                eligible = eligible.Where(item => (int)item.Rarity >= preferredGrade).ToList();
        }
        List<RelicDefinition> rarityPool = eligible.Where(item => item.Rarity == rolled).ToList();
        if (rarityPool.Count == 0)
        {
            // 등급 후보가 소진돼도 위험 제단의 최소 품질을 낮추지 않는다.
            int nearestDistance = eligible.Min(item => Mathf.Abs((int)item.Rarity - (int)rolled));
            rarityPool = eligible.Where(item =>
                Mathf.Abs((int)item.Rarity - (int)rolled) == nearestDistance).ToList();
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
        if (relic.Id == "glass_heart") playerHealth.AdjustMaxHealth(-2);
    }

    private void RemoveRelicEffects(RelicDefinition relic)
    {
        if (relic.Id == "glass_heart") playerHealth.AdjustMaxHealth(2);
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
        lastBasicAction = BasicActionType.Move;
    }

    public void BeginAttack(PlayerAttackTraitRoll roll)
    {
        alternatingBurstDamage = lastBasicAction == BasicActionType.Move
            ? (HasRelic("alternating_gear") ? 1 : 0)
                + (Stack("alternating_overload") > 0 ? 1 : 0)
            : 0;
        attackHunterBonus = lastBasicAction == BasicActionType.Move
            && (HasRelic("hunters_mark") || Stack("hunter_instinct") > 0);
        attackStoredSources = storedAttackSources;
        storedAttackSources = 0;
        lastBasicAction = BasicActionType.Attack;
        firstCastKilled = false;
        attackKilledAny = false;
        recoveryGranted = false;
        attackHitCount = 0;
        overflowTransferred = false;
        rewardedKills.Clear();
        foreach (HashSet<int> targets in areaHitTargets.Values) targets.Clear();
    }

    public int ModifyAttackDamage(int baseDamage, PlayerAttackTraitRoll roll, int castIndex,
        int hitIndex = 0)
    {
        int damage = baseDamage;
        if (roll.DamageBoost) damage += 1 + Mathf.Min(1, Stack("overcharge"));
        if (roll.Knockback && Stack("long_impact") > 0) damage++;
        if (roll.DoubleCast && castIndex > 0 && Stack("echo_warhead") > 0) damage++;
        if (HasRelic("glass_heart")) damage++;
        if (Stack("arcane_overdrive") > 0) damage++;
        // 예약 보너스는 공격 행동의 첫 실제 직격에만 적용한다.
        if (attackHitCount == 0 && hitIndex == 0)
        {
            if (Stack("keen_magic") > 0
                && (roll.DoubleCast || roll.Pierce || roll.DamageBoost || roll.Knockback)) damage++;
            if (attackHunterBonus) damage++;
            if ((attackStoredSources & KnightCharge) != 0) damage++;
            if ((attackStoredSources & AmbushCharge) != 0) damage++;
            if ((attackStoredSources & ExcessCharge) != 0) damage++;
        }
        if (castIndex > 0 && firstCastKilled && HasRelic("twin_focus")) damage++;
        if (roll.DoubleCast && castIndex > 0 && Stack("trait_mastery") > 0) damage++;
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
        if (roll.Pierce && hitIndex > 0 && Stack("trait_mastery") > 0) damage++;
        return roll.Pierce && hitIndex == 1 && HasRelic("thorn_needle") ? damage + 1 : damage;
    }

    public void NotifyAttackHit(MonsterMovement target, PlayerAttackTraitRoll roll,
        int castIndex, int hitIndex)
    {
        attackHitCount++;
        if (target == null) return;
        if (target != null && alternatingBurstDamage > 0)
        {
            DamageAdjacentEnemies(target.GridPosition, alternatingBurstDamage, target, "alternating");
            alternatingBurstDamage = 0;
        }
        if (attackHitCount == 1 && (attackStoredSources & AmbushCharge) != 0)
            DamageAdjacentEnemies(target.GridPosition, 1, target, "ambush");

        if (!recoveryGranted && roll.DoubleCast && castIndex > 0
            && Stack("echo_recovery") > 0 && target != null)
        {
            LockAdjacentEnemies(target.GridPosition, Stack("echo_recovery") + 1, target);
            recoveryGranted = true;
        }
        if (roll.Pierce && hitIndex > 0 && Stack("pierce_recovery") > 0
            && target != null)
            DamageAdjacentEnemies(target.GridPosition, Stack("pierce_recovery"), target, "pierce_wave");
        if (!recoveryGranted && roll.DamageBoost && target != null)
        {
            int lockCount = Stack("afterglow_recovery")
                + (HasRelic("hot_afterglow") ? 1 : 0);
            if (lockCount > 0)
                LockAdjacentEnemies(target.GridPosition, lockCount, target);
            recoveryGranted = true;
        }

        if (roll.DamageBoost && attackHitCount == 1)
        {
            if (HasRelic("blast_core"))
                DamageAdjacentEnemies(target.GridPosition, 2, target, "blast_core");
            if (Stack("overcharge") >= 2)
                DamageAdjacentEnemies(target.GridPosition, 1, target, "overcharge");
            if (Stack("trait_mastery") > 0)
                DamageAdjacentEnemies(target.GridPosition, 1, target, "damage_mastery");
        }

        if (target.IsDead && HasRelic("glass_heart") && !overflowTransferred
            && target.LastDamageOverflow > 0)
        {
            MonsterMovement overflowTarget = monsterSpawner.ActiveMonsters
                .Where(other => other != null && other != target && !other.IsDead
                    && Chebyshev(other.GridPosition, target.GridPosition) == 1)
                .OrderBy(other => other.GetComponent<CharacterHealth>().CurrentHealth)
                .ThenBy(other => Chebyshev(other.GridPosition, playerMovement.GridPosition))
                .ThenBy(other => other.SpawnOrder).FirstOrDefault();
            if (overflowTarget != null)
            {
                overflowTransferred = true;
                overflowTarget.TakeDamage(Mathf.Min(2, target.LastDamageOverflow));
            }
        }
        NotifyDirectKill(target, roll, castIndex);
    }

    // 충돌까지 완료된 직접 처치만 알린다. 범위 처치는 이 진입점을 호출하지 않는다.
    public void NotifyDirectKill(MonsterMovement target, PlayerAttackTraitRoll roll, int castIndex)
    {
        if (target == null || !target.IsDead || !rewardedKills.Add(target.GetInstanceID())) return;
        int burstDamage = Stack("mana_circulation")
            + (Stack("rapid_cycle") > 0 ? 1 : 0);
        if (burstDamage > 0)
            DamageAdjacentEnemies(target.GridPosition, burstDamage, target, "kill_burst");
        attackKilledAny = true;
        if (castIndex == 0) firstCastKilled = true;
        if (!firstKillHealed)
        {
            firstKillHealed = true;
            int heal = (HasRelic("blood_knot") ? 2 : 0)
                + (Stack("combat_regeneration") > 0 ? 1 : 0)
                + (Stack("immortal_cycle") > 0 ? 2 : 0);
            if (heal > 0)
            {
                playerHealth.Heal(heal);
            }
        }
        if (roll.DamageBoost && HasRelic("excess_reservoir")) storedAttackSources |= ExcessCharge;
    }

    public bool CompleteAttack(PlayerAttackTraitRoll roll)
    {
        if (roll.Pierce && attackHitCount >= 2 && stitchHeals < 2
            && HasRelic("suture_needle"))
        {
            stitchHeals++;
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
        if (damage <= 0) return 0;
        if (kind == PlayerDamageKind.Hazard && !firstHazardBlocked
            && (HasRelic("ash_boots") || Stack("flame_adaptation") > 0))
        {
            firstHazardBlocked = true;
            return 0;
        }
        if (movementLandingGuard)
        {
            movementLandingGuard = false;
            damage = Mathf.Max(0, damage - 1);
        }
        if (damage > 0 && !firstDamageBlocked
            && (HasRelic("vanguard_shield") || Stack("barrier_shell") > 0))
        {
            firstDamageBlocked = true;
            damage = Mathf.Max(0, damage - 1);
        }
        return damage;
    }

    public int KnockbackDistance => HasRelic("battering_ram")
        ? 3 : 1 + Stack("long_impact");

    public void NotifyKnockbackSuccess(MonsterMovement target, MonsterMovement collision)
    {
        if (target != null) NotifyKnockbackResult(target, collision, target.GridPosition);
    }

    public void NotifyKnockbackResult(MonsterMovement target, MonsterMovement collision,
        Vector3Int impactCell)
    {
        int remaining = Stack("impact_recovery") > 0 ? Stack("impact_recovery") + 1 : 0;
        if (remaining > 0 && target != null && !target.IsDead && !target.IsActionBlocked)
        {
            target.SkipNextTurn();
            remaining--;
        }
        if (remaining > 0 && collision != null && !collision.IsDead && !collision.IsActionBlocked)
        {
            collision.SkipNextTurn();
            remaining--;
        }
        LockAdjacentEnemies(impactCell, remaining, null);
        if (HasRelic("battering_ram"))
        {
            if (target != null && !target.IsDead) target.SkipNextTurn();
            if (collision != null && !collision.IsDead) collision.SkipNextTurn();
        }
        if (Stack("trait_mastery") > 0)
        {
            int masteryTargets = 2;
            if (target != null && !target.IsDead && !target.IsActionBlocked)
            {
                target.SkipNextTurn();
                masteryTargets--;
            }
            LockAdjacentEnemies(impactCell, masteryTargets, null);
        }
    }

    private void DamageAdjacentEnemies(Vector3Int center, int damage,
        MonsterMovement excluded, string effectId)
    {
        if (damage <= 0 || monsterSpawner == null) return;
        if (!areaHitTargets.TryGetValue(effectId, out HashSet<int> affected))
        {
            affected = new HashSet<int>();
            areaHitTargets.Add(effectId, affected);
        }
        MonsterMovement[] snapshot = monsterSpawner.ActiveMonsters.ToArray();
        foreach (MonsterMovement monster in snapshot)
        {
            if (monster == null || monster == excluded || monster.IsDead) continue;
            if (Chebyshev(monster.GridPosition, center) == 1 && affected.Add(monster.GetInstanceID()))
                monster.TakeDamage(damage);
        }
    }

    private void LockAdjacentEnemies(Vector3Int center, int maximumTargets,
        MonsterMovement excluded)
    {
        if (maximumTargets <= 0 || monsterSpawner == null) return;
        foreach (MonsterMovement monster in monsterSpawner.ActiveMonsters
            .Where(item => item != null && item != excluded && !item.IsDead
                && !item.IsActionBlocked
                && Chebyshev(item.GridPosition, center) == 1)
            .OrderByDescending(ThreatensPlayer)
            .ThenBy(item => Chebyshev(item.GridPosition, playerMovement.GridPosition))
            .ThenBy(item => item.SpawnOrder)
            .Take(maximumTargets)
            .ToArray())
        {
            monster.SkipNextTurn();
        }
    }

    private bool ThreatensPlayer(MonsterMovement monster)
    {
        Vector3Int delta = monster.GridPosition - playerMovement.GridPosition;
        if (monster.MovementPattern == MonsterMovementPattern.EightDirection)
            return Chebyshev(monster.GridPosition, playerMovement.GridPosition) == 1;
        if (monster.MovementPattern == MonsterMovementPattern.CardinalFour)
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
        ChessMonsterBehaviour behaviour = monster.GetComponent<ChessMonsterBehaviour>();
        return behaviour != null && behaviour.IsRookCharged
            && behaviour.RookTargetColumn == playerMovement.GridPosition.x;
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
