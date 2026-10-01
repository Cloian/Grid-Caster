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

    // 선택 카드에는 이번 선택 이후의 실제 중첩 수치를 표시한다.
    public string DescriptionAtStack(int stack)
    {
        int count = Math.Max(1, Math.Min(MaxStacks, stack));
        switch (Id)
        {
            case "mana_circulation":
                return $"기본공격으로 적을 처치하면 그 적 주변 8칸의 다른 적에게 피해 {count}을 줍니다. 폭발로 처치한 적은 다시 폭발하지 않습니다.";
            case "movement_breath":
                return $"이동술로 착지하면 착지 지점 주변 8칸의 적 최대 {count}명이 다음 적 턴에 행동하지 못합니다. 기본 한 칸 이동에는 적용되지 않습니다.";
            case "echo_recovery":
                return $"더블 캐스트의 추가 공격이 처음 적에게 맞으면 그 적 주변의 다른 적 최대 {count + 1}명이 다음 행동을 못합니다. 한 공격 행동에 한 번 적용됩니다.";
            case "pierce_recovery":
                return $"관통한 두 번째 적부터, 맞은 적 주변 8칸에 피해 {count}을 줍니다. 같은 적은 한 공격 행동에 이 파동 피해를 한 번만 받습니다.";
            case "afterglow_recovery":
                return $"데미지 강화가 발동한 공격이 처음 적에게 맞으면 주변의 다른 적 최대 {count}명이 다음 행동을 못합니다. 일반 공격에는 적용되지 않습니다.";
            case "impact_recovery":
                return $"넉백 공격을 맞은 적과 충돌한 적, 적중 지점 주변에서 최대 {count + 1}명의 다음 행동을 막습니다. 대상을 처치하거나 밀지 못해도 주변 적에게 적용됩니다.";
            case "overcharge":
                return count == 1 ? "데미지 강화가 발동한 공격의 직접 피해가 추가로 1 증가합니다. 기본 피해 1 기준으로 발동 시 피해 2 → 3이 됩니다."
                    : "데미지 강화가 발동한 공격의 추가 직접 피해 +1을 유지하고, 첫 적중 주변 8칸에 피해 1을 추가합니다. 2레벨은 직접 피해 대신 범위 피해를 더합니다.";
            case "healing_breath": return $"웨이브를 클리어할 때 기본 회복에 체력 {count}을 추가로 회복합니다. 최대 체력을 넘지는 않습니다.";
            case "quick_cast": return $"더블 캐스트 평균 발동 확률을 {RunProgressionDescriptions.Chance(5 - count)}로 높입니다. 추가 공격은 발동 확률을 다시 추첨하지 않습니다.";
            case "thin_reload": return "관통 평균 발동 확률을 50%로 높입니다. 발동하면 첫 적을 넘어 뒤쪽 적도 공격합니다.";
            case "dense_mana":
                return $"데미지 강화 평균 발동 확률을 {RunProgressionDescriptions.Chance(4 - count)}로 높입니다. 발동 공격의 피해량은 바꾸지 않습니다.";
            case "compressed_impact": return $"넉백 평균 발동 확률을 {RunProgressionDescriptions.Chance(4 - count)}로 높입니다. 밀기 거리와 피해량은 바꾸지 않습니다.";
            case "echo_warhead": return $"더블 캐스트의 추가 공격은 직접 피해 +1을 받고 뒤쪽 적 {count}명을 더 관통합니다. 첫 공격에는 적용되지 않으며, 2레벨은 관통 수만 증가합니다.";
            case "long_needle": return $"관통이 발동한 공격이 첫 대상을 포함해 최대 {2 + count}명의 적을 맞힙니다. 추가 대상들은 같은 공격 방향에 있어야 합니다.";
            case "life_tempering": return $"최대 체력과 현재 체력이 각각 {count} 증가합니다. 선택할 때마다 즉시 최대 체력 +1, 현재 체력 +1을 얻습니다.";
            case "long_impact": return "넉백이 발동한 공격의 직접 피해가 1 증가하고 밀기 거리가 1칸에서 2칸이 됩니다. 벽이나 다른 적을 통과해 밀지는 못합니다.";
            case "rapid_cycle": return "기본공격 처치 시 주변 8칸 폭발 피해가 1 증가하고, 이동술 착지 시 적 1명의 다음 행동을 막습니다. 연쇄 폭발·구속 착지와 합산되며 미보유 시에도 작동합니다.";
            case "keen_magic": return "시작 특성이 발동한 공격의 첫 직접 적중 피해가 1 증가합니다. 추가 공격마다 반복되지 않습니다. 선택 즉시 최대 체력이 1 감소합니다.";
            case "combat_regeneration": return "매 웨이브에서 기본공격으로 처음 적을 처치하면 체력 1을 회복합니다. 범위 피해 처치에는 적용되지 않습니다.";
            case "alternating_overload": return "일반 이동 또는 이동술 바로 다음 공격의 첫 적중 지점 주변 8칸에 피해 1을 줍니다. 한 공격 행동에 한 번만 적용됩니다.";
            case "immortal_cycle": return "매 웨이브 첫 직접 처치 시 체력 2를 회복하고, 웨이브 클리어 시 체력 1을 추가로 회복합니다. 다른 회복 효과와 합산됩니다.";
            default: return Description;
        }
    }
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
        new UpgradeDefinition("mana_circulation", "연쇄 폭발", "처치 시 주변 8칸의 적에게 피해 +1", 2),
        new UpgradeDefinition("movement_breath", "구속 착지", "이동술 착지 시 인접 적 1명의 다음 행동 봉쇄", 2),
        new UpgradeDefinition("healing_breath", "회복 호흡", "웨이브 기본 회복에 체력 +1", 2),
        new UpgradeDefinition("movement_knight", "나이트 도약", "나이트의 L자 이동술을 습득", 1),
        new UpgradeDefinition("quick_cast", "빠른 영창", "더블 캐스트 평균 발동 확률 20% → 25% → 33.3%", 2, "double_cast"),
        new UpgradeDefinition("echo_warhead", "메아리 탄두", "추가 시전 직격 피해 +1 · 추가 관통 1→2명", 2, "double_cast"),
        new UpgradeDefinition("echo_recovery", "반향 압박", "추가 시전 첫 적중 주변 2명 봉쇄 · 2중첩 시 3명", 2, "double_cast"),
        new UpgradeDefinition("thin_reload", "얇은 장전", "관통 평균 발동 확률 33.3% → 50%", 1, "pierce"),
        new UpgradeDefinition("long_needle", "긴 바늘", "추가 관통 대상 +1", 2, "pierce"),
        new UpgradeDefinition("pierce_recovery", "관통 파동", "추가 대상 적중 시 주변 8칸에 피해 +1", 2, "pierce"),
        new UpgradeDefinition("dense_mana", "고밀도 마력", "데미지 강화 평균 발동 확률 25% → 33.3% → 50%", 2, "damage_boost"),
        new UpgradeDefinition("overcharge", "과충전", "발동 직격 추가 피해 +1 · 2중첩 시 첫 적중 주변 피해 1", 2, "damage_boost"),
        new UpgradeDefinition("afterglow_recovery", "잔광 구속", "강화 공격 적중 시 주변 적 1명의 다음 행동 봉쇄", 2, "damage_boost"),
        new UpgradeDefinition("compressed_impact", "압축 충격", "넉백 평균 발동 확률 25% → 33.3% → 50%", 2, "knockback"),
        new UpgradeDefinition("long_impact", "긴 충격", "넉백 발동 직격 피해 +1 · 밀기 거리 +1", 1, "knockback"),
        new UpgradeDefinition("impact_recovery", "충격 봉쇄", "넉백 적중 후 최대 2명 봉쇄 · 2중첩 시 3명 · 처치/이동 불가도 발동", 2, "knockback")
    };

    public static readonly IReadOnlyList<RelicDefinition> Relics = new[]
    {
        new RelicDefinition("alternating_gear", "교대의 톱니", "일반 이동 또는 이동술 바로 다음 공격이 처음 적에게 맞으면, 그 적 주변 8칸의 다른 적에게 피해 1을 줍니다. 한 공격 행동에 한 번만 적용됩니다.", RelicRarity.Common),
        new RelicDefinition("vanguard_shield", "선봉의 방패", "매 웨이브에서 처음 받는 피해를 1 줄입니다. 피해가 0이 되면 체력을 잃지 않습니다. 다음 웨이브에서 다시 사용할 수 있습니다.", RelicRarity.Common),
        new RelicDefinition("ash_boots", "재의 장화", "매 웨이브에서 처음 받는 불길 피해를 전부 무시합니다. 적의 직접 공격과 투사체에는 적용되지 않습니다.", RelicRarity.Common, minimumWave: 8),
        new RelicDefinition("hunters_mark", "사냥꾼의 인장", "일반 이동 또는 이동술 바로 다음 공격의 첫 직접 적중 피해가 1 증가합니다. 추가 시전과 뒤쪽 관통 대상에 반복 적용되지 않습니다.", RelicRarity.Rare),
        new RelicDefinition("blood_knot", "피의 매듭", "매 웨이브에서 기본공격으로 처음 적을 처치하면 체력 2를 회복합니다. 범위 피해 처치는 제외하며 최대 체력을 넘지 않습니다.", RelicRarity.Rare),
        new RelicDefinition("landing_ward", "착지 결계", "이동술 착지 후 다음에 받는 피해를 1 줄입니다. 여러 번 착지해도 감소량은 중첩되지 않으며 웨이브 종료 시 해제됩니다.", RelicRarity.Rare),
        new RelicDefinition("kings_turn", "왕의 차례", "웨이브당 한 번, 기본공격으로 적을 처치하면 적 턴 전에 무료로 한 칸 이동하거나 생략할 수 있습니다. 이동술·추가 공격은 불가하며 이동 연계 효과도 발동하지 않습니다.", RelicRarity.Legendary),
        new RelicDefinition("glass_heart", "유리 심장", "모든 기본공격의 직접 피해 +1. 획득 즉시 최대 체력 -2. 직접 처치하고 남은 초과 피해를 최대 2까지 주변 적 1명에게 전달합니다. 한 공격 행동에 한 번, 재전달은 없습니다.", RelicRarity.Legendary),
        new RelicDefinition("ambush_crest", "기습 문장", "이동술 후 다음 공격의 첫 직접 적중 피해 +1, 적중 주변 8칸 피해 1. 여러 번 이동해도 중첩되지 않으며 빗나가도 소모됩니다.", RelicRarity.Legendary),
        new RelicDefinition("twin_focus", "쌍둥이 초점", "더블 캐스트의 첫 공격으로 적을 직접 처치하면, 같은 행동의 추가 공격 각각에 직접 피해 +1을 줍니다. 추가 공격만으로 처치한 경우에는 발동하지 않습니다.", RelicRarity.Common, "double_cast"),
        new RelicDefinition("rotating_mirror", "회전 거울", "더블 캐스트의 추가 공격 전에 8방향 중 공격 방향을 다시 고릅니다. 방향 선택은 턴을 소모하지 않으며 공격 시작 칸에서 발사합니다. 선택 중 이동은 불가합니다.", RelicRarity.Rare, "double_cast"),
        new RelicDefinition("triple_echo", "삼중 메아리", "더블 캐스트가 발동하면 추가 공격이 한 번 늘어 총 세 번 시전합니다. 새 특성을 추첨하지 않으며 모든 시전이 끝난 후 적이 한 번 행동합니다.", RelicRarity.Legendary, "double_cast"),
        new RelicDefinition("thorn_needle", "가시 바늘", "관통이 발동한 공격의 두 번째 적에게 직접 피해 +1을 줍니다. 첫 번째와 세 번째 이후 대상에는 적용되지 않습니다.", RelicRarity.Common, "pierce"),
        new RelicDefinition("suture_needle", "봉합 바늘", "관통이 발동한 한 공격으로 적 2명 이상을 직접 맞히면 체력 1을 회복합니다. 매 웨이브 최대 2번이며 범위 피해로 맞힌 적은 세지 않습니다.", RelicRarity.Rare, "pierce"),
        new RelicDefinition("infinite_orbit", "무한 궤도", "관통이 발동하면 같은 공격 방향의 모든 적을 벽까지 관통합니다. 발동하지 않은 공격에는 적용되지 않으며 관통 파동의 중복 피해 제한은 유지됩니다.", RelicRarity.Legendary, "pierce"),
        new RelicDefinition("hot_afterglow", "뜨거운 잔광", "데미지 강화가 발동한 공격의 첫 적중 주변에서 적 1명의 다음 행동을 막습니다. 잔광 구속을 함께 보유하면 다른 적을 선택합니다.", RelicRarity.Common, "damage_boost"),
        new RelicDefinition("excess_reservoir", "과잉 저장기", "데미지 강화가 발동한 공격으로 적을 직접 처치하면, 다음 공격의 첫 직접 적중 피해 +1을 저장합니다. 여러 처치로 중첩되지 않으며 빗나가도 소모됩니다.", RelicRarity.Rare, "damage_boost"),
        new RelicDefinition("blast_core", "폭렬 핵", "데미지 강화가 발동한 공격의 첫 적중 주변 8칸에 피해 2를 줍니다. 한 공격 행동에 한 번이며 과충전·특성 완성의 범위 피해와 합산됩니다.", RelicRarity.Legendary, "damage_boost"),
        new RelicDefinition("iron_nail", "철벽 못", "넉백이 발동했지만 벽·다른 적·룩의 이동 면역 때문에 한 칸도 밀지 못하면, 살아남은 대상에게 추가 피해 1을 줍니다.", RelicRarity.Common, "knockback"),
        new RelicDefinition("domino_crest", "도미노 문장", "넉백 경로가 처음 다른 적과 충돌하면 밀던 적과 충돌 상대에게 각각 피해 1을 줍니다. 이동 거리가 0이어도 적용되며 연쇄 충돌은 없습니다.", RelicRarity.Rare, "knockback"),
        new RelicDefinition("battering_ram", "파성추", "넉백이 발동하면 최대 3칸 밀고, 살아남은 대상과 처음 충돌한 적의 다음 행동을 막습니다. 밀기 불가여도 봉쇄는 적용되며 룩도 봉쇄할 수 있습니다.", RelicRarity.Legendary, "knockback")
    };

    // 유물 웨이브를 실제 유물과 함께 클리어했을 때만 단계별 세 개를 모두 제시한다.
    public static readonly IReadOnlyList<UpgradeDefinition> AdvancedUpgrades = new[]
    {
        new UpgradeDefinition("movement_training_1", "이동술 연마 I", "현재 이동술 강화 · 미보유 시 나이트 도약 습득", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("rapid_cycle", "폭발 순환", "직접 처치 폭발 피해 +1 · 구속 착지 대상 +1", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("keen_magic", "예리한 마력", "특성 발동 공격의 첫 직격 피해 +1 · 최대 체력 -1", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("movement_bishop", "비숍 활보", "비숍의 대각선 이동술을 습득 · 지나간 불길 제거", 1,
            isAdvanced: true, rewardTier: 2),
        new UpgradeDefinition("movement_training_2", "이동술 연마 II", "현재 이동술 강화 · 미보유 시 나이트 도약 습득", 1,
            isAdvanced: true, rewardTier: 2),
        new UpgradeDefinition("combat_regeneration", "전투 재생", "매 웨이브 첫 직접 처치 시 체력 1 회복", 1,
            isAdvanced: true, rewardTier: 2),
        new UpgradeDefinition("movement_rook", "룩 돌진", "룩의 직선 이동술을 습득", 1,
            isAdvanced: true, rewardTier: 3),
        new UpgradeDefinition("movement_training_3", "이동술 연마 III", "현재 이동술 강화 · 미보유 시 비숍 활보 습득", 1,
            isAdvanced: true, rewardTier: 3),
        new UpgradeDefinition("alternating_overload", "교대 과충전", "이동 후 첫 적중의 주변 피해 +1", 1,
            isAdvanced: true, rewardTier: 3),
        new UpgradeDefinition("movement_training_4", "이동술 연마 IV", "현재 이동술 강화 · 미보유 시 룩 돌진 습득", 1,
            isAdvanced: true, rewardTier: 4),
        new UpgradeDefinition("immortal_cycle", "불멸 순환", "웨이브 첫 직접 처치 체력 +2 · 클리어 추가 회복 1", 1,
            isAdvanced: true, rewardTier: 4),
        new UpgradeDefinition("trait_mastery", "특성 완성", "더블: 추가 시전 피해 +1 / 관통: 추가 대상 피해 +1 / 피해: 첫 적중 주변 피해 1 / 넉백: 최대 2명 봉쇄", 1,
            isAdvanced: true, rewardTier: 4)
    };

    public static IEnumerable<UpgradeDefinition> AllUpgrades => Upgrades.Concat(AdvancedUpgrades);
    public static bool IsStandardUpgradeWave(int waveNumber) => StandardUpgradeWaves.Contains(waveNumber);

    public static UpgradeDefinition Upgrade(string id) => AllUpgrades.First(item => item.Id == id);
    public static RelicDefinition Relic(string id) => Relics.First(item => item.Id == id);
}
