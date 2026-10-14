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
            case "movement_breath":
                return $"이동술로 착지하면 착지 지점 주변 8칸의 적 최대 {count}명이 다음 적 턴에 행동하지 못합니다. 기본 한 칸 이동에는 적용되지 않습니다.";
            case "echo_recovery":
                return "더블 캐스트의 추가 공격 전에 8방향 중 방향을 다시 고릅니다. 직전 시전과 다른 방향이면 그 추가 공격의 직접 피해 +1. 선택 중 이동은 불가하며 모든 공격이 끝난 뒤 적이 한 번 행동합니다.";
            case "pierce_recovery":
                return $"관통이 발동한 공격의 두 번째 대상부터 직접 피해가 {count} 증가합니다. 첫 대상이나 공격선 밖의 적에게는 적용되지 않습니다.";
            case "afterglow_recovery":
                return count == 1 ? "기본 주변 8칸에 2칸 거리의 상하좌우 4칸을 더해 총 12칸을 타격합니다. 피해량은 유지합니다. 중심과 플레이어는 제외하며 범위 피해는 추가 폭발·충전·직접 처치 보상을 만들지 않습니다."
                    : "주변 8칸과 바깥 십자 4칸에 2칸 거리의 대각선 모서리 4칸을 더해 총 16칸을 타격합니다. 바깥 테두리의 나머지 8칸은 비워 두며 피해량은 유지합니다. 중심과 플레이어는 제외하며 추가 폭발·충전·직접 처치 보상은 없습니다.";
            case "impact_recovery":
                return count == 1 ? "살아남은 직접 대상의 다음 적 행동을 2회 막습니다. 중복 봉쇄는 기간을 갱신하며 합산하지 않고 주변 적은 막지 않습니다."
                    : "살아남은 직접 대상은 다음 적 행동 2회, 처음 충돌한 적은 1회 막습니다. 중복 봉쇄는 기간을 갱신하며 합산하지 않고 주변 적은 막지 않습니다.";
            case "overcharge":
                return $"범위 공격의 직격 피해가 추가로 {count} 증가합니다. 기본 피해 1 기준으로 발동 시 피해 {2 + count}입니다. 주변 피해량과 폭발 범위는 바꾸지 않습니다. 특성 심화의 직접 피해 +1과 합산합니다.";
            case "healing_breath": return $"웨이브를 클리어할 때 기본 회복에 체력 {count}을 추가로 회복합니다. 최대 체력을 넘지는 않습니다.";
            case "quick_cast": return $"더블 캐스트 준비에 필요한 일반 적중을 {4 - count}회로 줄입니다. 추가 시전은 충전하지 않습니다.";
            case "thin_reload": return "관통 준비에 필요한 일반 적중을 1회로 줄입니다. 특성 공격의 피해나 관통 수는 바꾸지 않습니다.";
            case "dense_mana":
                return $"범위 공격 준비에 필요한 일반 적중을 {3 - count}회로 줄입니다. 특성 공격의 피해량은 바꾸지 않습니다.";
            case "compressed_impact": return $"넉백 준비에 필요한 일반 적중을 {3 - count}회로 줄입니다. 밀기 거리와 피해량은 바꾸지 않습니다.";
            case "echo_warhead": return $"더블 캐스트의 추가 공격 각각의 직접 피해가 {count} 증가합니다. 첫 공격에는 적용되지 않으며 관통 효과는 주지 않습니다.";
            case "long_needle": return $"관통이 발동한 공격의 추가 대상이 {count}명 늘어, 기본 최대 {2 + count}명을 맞힙니다. 특성 심화는 여기에 1명을 더합니다. 모두 같은 공격 방향에 있어야 합니다.";
            case "life_tempering": return $"최대 체력과 현재 체력이 각각 {count} 증가합니다. 선택할 때마다 즉시 최대 체력 +1, 현재 체력 +1을 얻습니다.";
            case "long_impact": return "넉백 적중 지점의 주변 8칸에 있는 적도 중심에서 바깥으로 1칸 밉니다. 벽 충돌은 밀린 적에게 피해 1, 적 충돌은 양쪽에 피해 1을 줍니다. 같은 행동의 충돌 피해는 적마다 한 번이며 플레이어와 이동 면역 룩은 밀지 않습니다. 주변 밀기에는 봉쇄·직접 처치 보상이 적용되지 않습니다.";
            case "keen_magic": return "시작 특성이 발동한 공격의 첫 직접 적중 피해가 1 증가합니다. 추가 공격마다 반복되지 않습니다. 선택 즉시 최대 체력이 1 감소합니다.";
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
        new UpgradeDefinition("movement_breath", "구속 착지", "이동술 착지 시 인접 적 1명의 다음 행동 봉쇄", 2),
        new UpgradeDefinition("healing_breath", "회복 호흡", "웨이브 기본 회복에 체력 +1", 2),
        new UpgradeDefinition("movement_knight", "나이트 도약", "나이트의 L자 이동술을 습득", 1),
        new UpgradeDefinition("movement_bishop", "비숍 활보", "비숍의 대각선 이동술을 습득 · 지나간 불길 제거", 1),
        new UpgradeDefinition("movement_rook", "룩 돌진", "룩의 직선 이동술을 습득", 1),
        new UpgradeDefinition("quick_cast", "빠른 영창", "더블 캐스트 필요 일반 적중 4회 → 3회 → 2회", 2, "double_cast"),
        new UpgradeDefinition("echo_warhead", "메아리 탄두", "추가 시전 직접 피해 +1→2 · 관통 없음", 2, "double_cast"),
        new UpgradeDefinition("echo_recovery", "회전 영창", "추가 시전 재조준 · 다른 방향 직접 피해 +1", 1, "double_cast"),
        new UpgradeDefinition("thin_reload", "얇은 장전", "관통 필요 일반 적중 2회 → 1회", 1, "pierce"),
        new UpgradeDefinition("long_needle", "긴 바늘", "추가 관통 대상 +1", 2, "pierce"),
        new UpgradeDefinition("pierce_recovery", "관통 가속", "두 번째 대상부터 직접 피해 +1→2", 2, "pierce"),
        new UpgradeDefinition("dense_mana", "고밀도 마력", "범위 공격 필요 일반 적중 3회 → 2회 → 1회", 2, "damage_boost"),
        new UpgradeDefinition("overcharge", "과충전", "발동 직접 피해 추가 +1→2", 2, "damage_boost"),
        new UpgradeDefinition("afterglow_recovery", "폭발 증폭", "바깥 십자 → X자 추가 · 범위 8→12→16칸", 2, "damage_boost"),
        new UpgradeDefinition("compressed_impact", "압축 충격", "넉백 필요 일반 적중 3회 → 2회 → 1회", 2, "knockback"),
        new UpgradeDefinition("long_impact", "충격 확산", "적중 지점 주변 8칸의 적도 바깥으로 1칸 밀기", 1, "knockback"),
        new UpgradeDefinition("impact_recovery", "충격 고정", "직접 대상 2턴 봉쇄 · Lv.2 첫 충돌 상대 1턴 봉쇄", 2, "knockback")
    };

    public static readonly IReadOnlyList<RelicDefinition> Relics = new[]
    {
        new RelicDefinition("alternating_gear", "교대의 톱니", "일반 이동 또는 이동술 바로 다음 공격이 처음 적에게 맞으면, 그 적 주변 8칸의 다른 적에게 피해 1을 줍니다. 한 공격 행동에 한 번만 적용됩니다.", RelicRarity.Common),
        new RelicDefinition("vanguard_shield", "선봉의 방패", "매 웨이브에서 처음 받는 피해를 1 줄입니다. 피해가 0이 되면 체력을 잃지 않습니다. 다음 웨이브에서 다시 사용할 수 있습니다.", RelicRarity.Common),
        new RelicDefinition("ash_boots", "재의 장화", "매 웨이브에서 처음 받는 불길 피해를 전부 무시합니다. 적의 직접 공격과 투사체에는 적용되지 않습니다.", RelicRarity.Common, minimumWave: 8),
        new RelicDefinition("hunters_mark", "사냥꾼의 인장", "일반 이동 또는 이동술 바로 다음 공격의 첫 직접 적중 피해가 1 증가합니다. 추가 시전과 뒤쪽 관통 대상에 반복 적용되지 않습니다.", RelicRarity.Rare),
        new RelicDefinition("blood_knot", "피의 매듭", "매 웨이브에서 기본공격으로 처음 적을 처치하면 체력 2를 회복합니다. 범위 피해 처치는 제외하며 최대 체력을 넘지 않습니다.", RelicRarity.Rare),
        new RelicDefinition("landing_ward", "착지 결계", "이동술 착지 후 다음에 받는 피해를 1 줄입니다. 여러 번 착지해도 감소량은 중첩되지 않으며 웨이브 종료 시 해제됩니다.", RelicRarity.Rare),
        new RelicDefinition("kings_turn", "왕의 차례", "웨이브당 한 번, 직접 공격으로 적을 처치하면 적 행동 전에 기본 1칸을 추가로 이동합니다. 우클릭으로 생략할 수 있습니다. 이동술·추가 공격은 불가하며 이동 연계 효과도 발동하지 않습니다. 적 행동은 없어지는 것이 아니라 무료 이동 뒤 한 번 진행됩니다.", RelicRarity.Legendary),
        new RelicDefinition("glass_heart", "유리 심장", "모든 기본공격의 직접 피해 +1. 획득 즉시 최대 체력 -2. 직접 처치하고 남은 초과 피해를 최대 2까지 주변 적 1명에게 전달합니다. 한 공격 행동에 한 번, 재전달은 없습니다.", RelicRarity.Legendary),
        new RelicDefinition("ambush_crest", "기습 문장", "이동술 후 다음 공격의 첫 직접 적중 피해 +1, 적중 주변 8칸 피해 1. 여러 번 이동해도 중첩되지 않으며 빗나가도 소모됩니다.", RelicRarity.Legendary),
        new RelicDefinition("twin_focus", "쌍둥이 초점", "더블 캐스트의 첫 공격으로 적을 직접 처치하면, 같은 행동의 추가 공격 각각에 직접 피해 +1을 줍니다. 추가 공격만으로 처치한 경우에는 발동하지 않습니다.", RelicRarity.Common, "double_cast"),
        new RelicDefinition("rotating_mirror", "회전 거울", "추가 시전마다 8방향 재조준이 가능합니다. 회전 영창을 이미 보유하면 추가 시전의 직접 피해도 1 증가합니다. 선택 중 이동은 불가하며 적 턴은 추가로 발생하지 않습니다.", RelicRarity.Rare, "double_cast"),
        new RelicDefinition("triple_echo", "삼중 메아리", "더블 캐스트가 발동하면 추가 공격이 한 번 늘어 총 세 번 시전합니다. 새 특성을 추첨하지 않으며 모든 시전이 끝난 후 적이 한 번 행동합니다.", RelicRarity.Legendary, "double_cast"),
        new RelicDefinition("thorn_needle", "가시 바늘", "관통이 발동한 공격의 두 번째 적에게 직접 피해 +1을 줍니다. 첫 번째와 세 번째 이후 대상에는 적용되지 않습니다.", RelicRarity.Common, "pierce"),
        new RelicDefinition("suture_needle", "봉합 바늘", "관통이 발동한 한 공격으로 적 2명 이상을 직접 맞히면 체력 1을 회복합니다. 매 웨이브 최대 2번이며 범위 피해로 맞힌 적은 세지 않습니다.", RelicRarity.Rare, "pierce"),
        new RelicDefinition("infinite_orbit", "무한 궤도", "관통이 발동하면 같은 공격 방향의 모든 적을 벽까지 관통합니다. 발동하지 않은 공격에는 적용되지 않습니다.", RelicRarity.Legendary, "pierce"),
        new RelicDefinition("hot_afterglow", "잔광 회수", "범위 공격의 직접 대상이 살아남으면 다음 공격의 첫 직접 적중 피해 +1을 저장합니다. 과잉 저장기와 같은 저장 칸을 쓰므로 중첩되지 않고 빗나가도 소모됩니다.", RelicRarity.Common, "damage_boost"),
        new RelicDefinition("excess_reservoir", "과잉 저장기", "범위 공격으로 적을 직접 처치하면, 다음 공격의 첫 직접 적중 피해 +1을 저장합니다. 여러 처치로 중첩되지 않으며 빗나가도 소모됩니다.", RelicRarity.Rare, "damage_boost"),
        new RelicDefinition("blast_core", "폭발 핵", "범위 공격으로 중심 대상을 직접 처치하면 그 공격의 파편 피해 +1. 현재 폭발 범위를 사용하며 직접 피해와 범위는 늘리지 않습니다. 중심 대상이 살아남거나 주변 적만 죽으면 보너스가 없습니다. 추가 폭발·충전·직접 처치 보상은 없습니다.", RelicRarity.Legendary, "damage_boost"),
        new RelicDefinition("iron_nail", "철벽 못", "넉백이 발동했지만 벽·다른 적·룩의 이동 면역 때문에 한 칸도 밀지 못하면, 살아남은 대상에게 추가 피해 1을 줍니다.", RelicRarity.Common, "knockback"),
        new RelicDefinition("domino_crest", "도미노 문장", "넉백 경로가 처음 다른 적과 충돌하면 밀던 적과 충돌 상대에게 각각 피해 1을 줍니다. 이동 거리가 0이어도 적용되며 연쇄 충돌은 없습니다.", RelicRarity.Rare, "knockback"),
        new RelicDefinition("battering_ram", "파성추", "넉백이 발동하면 최대 3칸 밀고, 살아남은 대상과 처음 충돌한 적의 다음 행동을 막습니다. 밀기 불가여도 봉쇄는 적용되며 룩도 봉쇄할 수 있습니다.", RelicRarity.Legendary, "knockback")
    };

    // 유물 획득 보상에서 해금 단계 이하의 미보유 상급 후보를 가중 추첨한다.
    public static readonly IReadOnlyList<UpgradeDefinition> AdvancedUpgrades = new[]
    {
        new UpgradeDefinition("movement_training_1", "이동술 연마", "현재 이동술 강화 · 미보유 시 나이트 도약 습득", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("rapid_cycle", "특성 심화", "선택한 시작 특성의 고유 효과 강화", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("keen_magic", "예리한 마력", "특성 발동 공격의 첫 직격 피해 +1 · 최대 체력 -1", 1,
            isAdvanced: true, rewardTier: 1),
        new UpgradeDefinition("movement_training_2", "이동술 연마", "현재 이동술 강화 · 미보유 시 나이트 도약 습득", 1,
            isAdvanced: true, rewardTier: 2),
        new UpgradeDefinition("movement_training_3", "이동술 연마", "현재 이동술 강화 · 미보유 시 비숍 활보 습득", 1,
            isAdvanced: true, rewardTier: 3),
        new UpgradeDefinition("movement_training_4", "이동술 연마", "현재 이동술 강화 · 미보유 시 룩 돌진 습득", 1,
            isAdvanced: true, rewardTier: 4),
        new UpgradeDefinition("trait_mastery", "특성 완성", "더블: 추가 시전 피해 +1 / 관통: 추가 대상 피해 +1 / 피해: 주변 피해 +1 / 넉백: 직격·충돌 피해 +1", 1,
            isAdvanced: true, rewardTier: 4)
    };

    public static IEnumerable<UpgradeDefinition> AllUpgrades => Upgrades.Concat(AdvancedUpgrades);
    public static bool IsStandardUpgradeWave(int waveNumber) => StandardUpgradeWaves.Contains(waveNumber);

    public static UpgradeDefinition Upgrade(string id) => AllUpgrades.First(item => item.Id == id);
    public static RelicDefinition Relic(string id) => Relics.First(item => item.Id == id);
}
