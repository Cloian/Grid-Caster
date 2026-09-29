using System;
using System.Collections.Generic;
using System.Linq;

public enum RelicRarity
{
    Common,
    Rare,
    Legendary
}

public enum PlayerDamageKind
{
    Normal,
    Hazard
}

public enum BasicActionType
{
    None,
    Move,
    Attack
}

public sealed class UpgradeDefinition
{
    public UpgradeDefinition(string id, string name, string description, int maxStacks,
        string requiredTrait = "", bool isAdvanced = false, int rewardTier = 0)
    {
        Id = id;
        Name = name;
        Description = description;
        MaxStacks = maxStacks;
        RequiredTrait = requiredTrait;
        IsAdvanced = isAdvanced;
        RewardTier = rewardTier;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int MaxStacks { get; }
    public string RequiredTrait { get; }
    public bool IsAdvanced { get; }
    public int RewardTier { get; }
    public bool IsTraitUpgrade => !string.IsNullOrEmpty(RequiredTrait);
}

public sealed class RelicDefinition
{
    public RelicDefinition(string id, string name, string description, RelicRarity rarity,
        string requiredTrait = "", int minimumWave = 1)
    {
        Id = id;
        Name = name;
        Description = description;
        Rarity = rarity;
        RequiredTrait = requiredTrait;
        MinimumWave = minimumWave;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public RelicRarity Rarity { get; }
    public string RequiredTrait { get; }
    public int MinimumWave { get; }
    public bool IsTraitRelic => !string.IsNullOrEmpty(RequiredTrait);
}

public static class RunProgressionCatalog
{
    private static readonly int[] StandardUpgradeWaves = { 2, 6, 10, 14, 18 };

    public static readonly IReadOnlyList<UpgradeDefinition> Upgrades = new[]
    {
        new UpgradeDefinition("life_tempering", "생명 연마", "최대 체력 +1 · 현재 체력 +1", 3),
        new UpgradeDefinition("mana_circulation", "마력 순환", "이동 게이지 +1 · 적중 게이지 +2", 3),
        new UpgradeDefinition("movement_breath", "이동술 호흡", "이동술 사용 후 게이지 +5", 2),
        new UpgradeDefinition("healing_breath", "회복 호흡", "웨이브 회복량 +5%p", 2),
        new UpgradeDefinition("quick_cast", "빠른 영창", "발동 백 크기 5→4→3", 2, "double_cast"),
        new UpgradeDefinition("echo_warhead", "메아리 탄두", "추가 공격 관통 +1", 2, "double_cast"),
        new UpgradeDefinition("echo_recovery", "반향 회수", "발동 공격 적중 시 게이지 +5", 2, "double_cast"),
        new UpgradeDefinition("thin_reload", "얇은 장전", "발동 백 크기 3→2", 1, "pierce"),
        new UpgradeDefinition("long_needle", "긴 바늘", "추가 관통 대상 +1", 2, "pierce"),
        new UpgradeDefinition("pierce_recovery", "관통 회수", "추가 대상마다 게이지 +3", 2, "pierce"),
        new UpgradeDefinition("dense_mana", "고밀도 마력", "발동 백 크기 4→3→2", 2, "damage_boost"),
        new UpgradeDefinition("overcharge", "과충전", "발동 추가 피해 +1", 2, "damage_boost"),
        new UpgradeDefinition("afterglow_recovery", "잔광 회수", "발동 공격 적중 시 게이지 +5", 2, "damage_boost"),
        new UpgradeDefinition("compressed_impact", "압축 충격", "발동 백 크기 4→3→2", 2, "knockback"),
        new UpgradeDefinition("long_impact", "긴 충격", "넉백 거리 +1", 1, "knockback"),
        new UpgradeDefinition("impact_recovery", "충격 회수", "실제 밀치기 성공 시 게이지 +5", 2, "knockback")
    };

    public static readonly IReadOnlyList<RelicDefinition> Relics = new[]
    {
        new RelicDefinition("alternating_gear", "교대의 톱니", "이동↔공격 교대 시 게이지 +5", RelicRarity.Common),
        new RelicDefinition("vanguard_shield", "선봉의 방패", "웨이브 첫 피해 1 감소", RelicRarity.Common),
        new RelicDefinition("ash_boots", "재의 장화", "웨이브 첫 위험 타일 피해 무효", RelicRarity.Common, minimumWave: 8),
        new RelicDefinition("hunters_mark", "사냥꾼의 인장", "이동 후 다음 적중 공격 피해 +1", RelicRarity.Rare),
        new RelicDefinition("blood_knot", "피의 매듭", "웨이브 첫 처치 시 체력 1 회복", RelicRarity.Rare),
        new RelicDefinition("landing_ward", "착지 결계", "이동술 후 다음 피해 1 감소", RelicRarity.Rare),
        new RelicDefinition("kings_turn", "왕의 차례", "웨이브당 1회 처치 후 무료 이동", RelicRarity.Legendary),
        new RelicDefinition("glass_heart", "유리 심장", "기본공격 피해 +1 · 최대 체력 -3", RelicRarity.Legendary),
        new RelicDefinition("ambush_crest", "기습 문장", "이동술 후 다음 기본공격 피해 +1", RelicRarity.Legendary),
        new RelicDefinition("twin_focus", "쌍둥이 초점", "첫 공격 처치 시 추가 공격 피해 +1", RelicRarity.Common, "double_cast"),
        new RelicDefinition("rotating_mirror", "회전 거울", "추가 공격 방향을 다시 선택", RelicRarity.Rare, "double_cast"),
        new RelicDefinition("triple_echo", "삼중 메아리", "발동 시 세 번째 공격 실행", RelicRarity.Legendary, "double_cast"),
        new RelicDefinition("thorn_needle", "가시 바늘", "첫 추가 대상 피해 +1", RelicRarity.Common, "pierce"),
        new RelicDefinition("suture_needle", "봉합 바늘", "2명 이상 관통 시 웨이브당 체력 1 회복", RelicRarity.Rare, "pierce"),
        new RelicDefinition("infinite_orbit", "무한 궤도", "발동 시 벽까지 모든 적 관통", RelicRarity.Legendary, "pierce"),
        new RelicDefinition("hot_afterglow", "뜨거운 잔광", "강화 공격 적중 시 게이지 +10", RelicRarity.Common, "damage_boost"),
        new RelicDefinition("excess_reservoir", "과잉 저장기", "강화 공격 처치 시 다음 공격 피해 +1", RelicRarity.Rare, "damage_boost"),
        new RelicDefinition("blast_core", "폭렬 핵", "강화 적중 주변 8칸에 피해 1", RelicRarity.Legendary, "damage_boost"),
        new RelicDefinition("iron_nail", "철벽 못", "넉백이 막히면 대상 피해 1", RelicRarity.Common, "knockback"),
        new RelicDefinition("domino_crest", "도미노 문장", "적과 충돌 시 두 적 모두 피해 1", RelicRarity.Rare, "knockback"),
        new RelicDefinition("battering_ram", "파성추", "최대 3칸 밀고 충돌한 적 행동 봉쇄", RelicRarity.Legendary, "knockback")
    };

    // 유물 웨이브를 실제 유물과 함께 클리어했을 때만 단계별 세 개를 모두 제시한다.
    public static readonly IReadOnlyList<UpgradeDefinition> AdvancedUpgrades = new[]
    {
        new UpgradeDefinition("movement_knight", "나이트 도약", "나이트의 L자 이동술을 습득", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("rapid_cycle", "고속 순환", "이동 게이지 +2 · 적중 게이지 +5", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("keen_magic", "예리한 마력", "기본공격 피해 +1 · 최대 체력 -1", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("movement_bishop", "비숍 활보", "비숍의 대각선 이동술을 습득 · 지나간 불길 제거", 1,
            isAdvanced: true, rewardTier: 2),
        new UpgradeDefinition("movement_training_2", "이동술 연마 II", "현재 이동술 강화 · 미보유 시 나이트 도약 습득", 1,
            isAdvanced: true, rewardTier: 2),
        new UpgradeDefinition("combat_regeneration", "전투 재생", "매 웨이브 첫 처치 시 체력 1 회복", 1,
            isAdvanced: true, rewardTier: 2),
        new UpgradeDefinition("movement_rook", "룩 돌진", "룩의 직선 이동술을 습득", 1,
            isAdvanced: true, rewardTier: 3),
        new UpgradeDefinition("movement_training_3", "이동술 연마 III", "현재 이동술 강화 · 미보유 시 비숍 활보 습득", 1,
            isAdvanced: true, rewardTier: 3),
        new UpgradeDefinition("alternating_overload", "교대 과충전", "이동↔공격 교대 시 게이지 +10", 1,
            isAdvanced: true, rewardTier: 3),
        new UpgradeDefinition("movement_training_4", "이동술 연마 IV", "현재 이동술 강화 · 미보유 시 룩 돌진 습득", 1,
            isAdvanced: true, rewardTier: 4),
        new UpgradeDefinition("immortal_cycle", "불멸 순환", "웨이브 첫 처치 체력 +2 · 클리어 회복 +10%p", 1,
            isAdvanced: true, rewardTier: 4),
        new UpgradeDefinition("trait_mastery", "특성 완성", "시작 특성 셔플 백 크기 -1 · 적중 게이지 +3", 1,
            isAdvanced: true, rewardTier: 4)
    };

    public static IEnumerable<UpgradeDefinition> AllUpgrades => Upgrades.Concat(AdvancedUpgrades);
    public static bool IsStandardUpgradeWave(int waveNumber) => StandardUpgradeWaves.Contains(waveNumber);

    public static UpgradeDefinition Upgrade(string id) => AllUpgrades.First(item => item.Id == id);
    public static RelicDefinition Relic(string id) => Relics.First(item => item.Id == id);
}
