using System;
using System.Globalization;

// 카드와 보유 상세창이 같은 실제 효과와 레벨을 설명하도록 문구를 한 곳에서 관리한다.
public static class RunProgressionDescriptions
{
    public static string Chance(int attacksPerActivation)
    {
        return (100f / Math.Max(1, attacksPerActivation)).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    public static string MovementName(PlayerMovementArt art)
    {
        return art switch
        {
            PlayerMovementArt.Knight => "나이트 도약",
            PlayerMovementArt.Bishop => "비숍 활보",
            PlayerMovementArt.Rook => "룩 돌진",
            _ => "이동술 미보유"
        };
    }

    public static bool IsMovementUpgrade(string id)
    {
        return id == "movement_knight" || id == "movement_bishop" || id == "movement_rook"
            || id.StartsWith("movement_training_", StringComparison.Ordinal);
    }

    public static PlayerMovementArt GrantedMovementArt(string id)
    {
        return id switch
        {
            "movement_bishop" or "movement_training_3" => PlayerMovementArt.Bishop,
            "movement_rook" or "movement_training_4" => PlayerMovementArt.Rook,
            _ => PlayerMovementArt.Knight
        };
    }

    public static string MovementEffect(PlayerMovementArt art, int level)
    {
        level = Math.Max(1, Math.Min(RunProgressionSystem.MaxMovementArtLevel, level));
        string shared = "S를 누르고 표시된 빈 칸을 클릭합니다. 사용할 때마다 행동 1회를 소비합니다.";
        switch (art)
        {
            case PlayerMovementArt.Knight:
                string knight = "가로 2칸·세로 1칸 또는 가로 1칸·세로 2칸의 L자로 도약합니다. 중간 벽과 적을 넘을 수 있지만 착지 칸은 비어 있어야 합니다.";
                if (level >= 2) knight += "\n착지 칸과 상하좌우 4칸의 불길을 제거합니다.";
                if (level >= 3) knight += "\n도약 후 다음 공격의 첫 직접 적중 피해 +1. 여러 번 도약해도 이 보너스는 중첩되지 않고, 빗나가도 소모됩니다.";
                return knight + "\n\n" + shared;
            case PlayerMovementArt.Bishop:
                return $"대각선으로 최대 {2 + level}칸 이동하며 지나간 불길을 제거합니다. 중간 벽이나 적은 통과하지 못합니다.\n인접한 1칸은 기본 이동으로 처리되어 불길 제거·이동술 착지 효과가 없습니다.\n\n{shared}";
            case PlayerMovementArt.Rook:
                return $"상하좌우 직선으로 최대 {3 + level}칸 이동합니다. 중간 벽이나 적을 통과하지 못하고 불길도 제거하지 않습니다.\n인접한 1칸은 기본 이동으로 처리되어 이동술 착지 효과가 없습니다.\n\n{shared}";
            default:
                return "현재 이동술이 없습니다. 이동술을 습득하면 S의 기본 이동 선택지에 특수 이동 칸이 함께 표시됩니다.";
        }
    }

    public static string ChoiceName(UpgradeDefinition item, RunProgressionSystem run)
    {
        if (!IsMovementUpgrade(item.Id)) return item.Name;
        if (item.Id.StartsWith("movement_training_", StringComparison.Ordinal))
            return run.HasMovementArt ? run.MovementArtName + " 연마" : MovementName(GrantedMovementArt(item.Id)) + " 습득";
        return item.Name + (run.HasMovementArt ? "로 교체" : " 습득");
    }

    public static string ChoiceDescription(UpgradeDefinition item, RunProgressionSystem run)
    {
        if (IsMovementUpgrade(item.Id))
        {
            bool training = item.Id.StartsWith("movement_training_", StringComparison.Ordinal);
            PlayerMovementArt nextArt = training && run.HasMovementArt ? run.ActiveMovementArt : GrantedMovementArt(item.Id);
            int nextLevel = !run.HasMovementArt ? 1 : training ? Math.Min(RunProgressionSystem.MaxMovementArtLevel, run.MovementArtLevel + 1) : run.MovementArtLevel;
            string current = run.HasMovementArt ? run.MovementArtName + " Lv." + run.MovementArtLevel : "미보유";
            string change = training && run.HasMovementArt ? MovementLevelBenefit(nextArt, nextLevel)
                : run.HasMovementArt ? "기존 이동술을 교체합니다. 레벨은 유지하며 동시에 하나만 보유합니다." : "새 이동술을 Lv.1로 습득합니다.";
            return $"현재: {current}\n선택 후: {MovementName(nextArt)} Lv.{nextLevel}\n최대 레벨: Lv.3\n\n이번 선택: {change}\n\n{MovementSummary(nextArt, nextLevel)}";
        }

        int currentStack = run.Stack(item.Id);
        int nextStack = currentStack + 1;
        string currentLevel = currentStack == 0 ? "미보유" : $"Lv.{currentStack}";
        string result = $"현재: {currentLevel}\n선택 후: Lv.{nextStack} (최대 Lv.{item.MaxStacks})";
        string changeText = UpgradeBenefit(item.Id, currentStack, nextStack);
        string effect = Effect(item, nextStack, run);
        return result + "\n\n이번 선택: " + changeText + "\n\n" + effect;
    }

    // 선택 카드에는 변화와 핵심 효과만, 예외/중복/판정 조건은 ChoiceDetails에 둔다.
    public static string ChoiceSummary(UpgradeDefinition item, RunProgressionSystem run)
    {
        if (IsMovementUpgrade(item.Id))
        {
            bool training = item.Id.StartsWith("movement_training_", StringComparison.Ordinal);
            PlayerMovementArt art = training && run.HasMovementArt ? run.ActiveMovementArt : GrantedMovementArt(item.Id);
            int level = !run.HasMovementArt ? 1 : training
                ? Math.Min(RunProgressionSystem.MaxMovementArtLevel, run.MovementArtLevel + 1) : run.MovementArtLevel;
            string current = run.HasMovementArt ? $"{run.MovementArtName} Lv.{run.MovementArtLevel}" : "미보유";
            string effect = training && run.HasMovementArt ? art switch
            {
                PlayerMovementArt.Knight => level == 2 ? "착지 주변 불길 제거 추가" : "도약 후 다음 공격 피해 +1 추가",
                PlayerMovementArt.Bishop => $"대각선 이동 {1 + level} → {2 + level}칸",
                _ => $"직선 이동 {2 + level} → {3 + level}칸"
            } : art switch
            {
                PlayerMovementArt.Knight => "L자 도약 · 중간 벽과 적 넘기",
                PlayerMovementArt.Bishop => $"대각선 최대 {2 + level}칸 · 경로 불길 제거",
                _ => $"직선 최대 {3 + level}칸 이동"
            };
            return $"현재: {current}\n선택 후: {MovementName(art)} Lv.{level}\n\n{effect}"
                + (run.HasMovementArt && !training ? "\n기존 이동술 교체 · 레벨 유지" : "");
        }

        int stack = run.Stack(item.Id);
        string currentLevel = stack == 0 ? "미보유" : $"Lv.{stack}";
        string summary = $"현재: {currentLevel}\n선택 후: Lv.{stack + 1} (최대 Lv.{item.MaxStacks})\n\n"
            + CoreEffect(item.Id, stack + 1, run);
        if (IsFrequencyUpgrade(item.Id))
            summary += $"\n필요한 일반 적중 {run.TraitChargeRequired}회 → {Math.Max(1, run.TraitChargeRequired - 1)}회";
        else if (item.IsTraitUpgrade || item.Id == "keen_magic" || item.Id == "trait_mastery" || item.Id == "rapid_cycle")
            summary += $"\n일반 적중 {run.TraitChargeRequired}회로 준비 · 수동 사용";
        return summary;
    }

    private static string CoreEffect(string id, int level, RunProgressionSystem run)
    {
        return id switch
        {
            "life_tempering" => "최대 체력 +1 · 즉시 체력 +1",
            "movement_breath" => $"이동술 착지 시 주변 적 {level}명 행동 봉쇄",
            "healing_breath" => $"웨이브 클리어 시 추가 체력 회복 {level}",
            "quick_cast" => "더블 캐스트 충전에 필요한 일반 적중 -1회",
            "thin_reload" => "관통 충전에 필요한 일반 적중 -1회",
            "dense_mana" => "범위 공격 충전에 필요한 일반 적중 -1회",
            "compressed_impact" => "넉백 충전에 필요한 일반 적중 -1회",
            "echo_warhead" => $"추가 시전 직접 피해 +{level}\n관통 효과 없음",
            "echo_recovery" => "추가 시전 재조준\n다른 방향이면 직접 피해 +1",
            "long_needle" => $"관통 발동 시 추가 직격 대상 +{level}명",
            "pierce_recovery" => $"관통 두 번째 대상부터 직접 피해 +{level}",
            "overcharge" => $"강화 발동 시 추가 직접 피해 +{level}",
            "afterglow_recovery" => level == 1 ? "바깥 십자 4칸 추가\n폭발 범위 8 → 12칸"
                : "바깥 X자 4칸 추가\n폭발 범위 12 → 16칸",
            "long_impact" => "적중 지점 주변 8칸의 적\n바깥으로 1칸 밀기",
            "impact_recovery" => "직접 대상 2턴 봉쇄" + (level >= 2 ? "\n첫 충돌 상대 1턴 봉쇄" : ""),
            "rapid_cycle" => DeepeningSummary(run.SelectedTraitId),
            "keen_magic" => "특성 발동 첫 직격 피해 +1\n최대 체력 -1",
            "trait_mastery" => run.SelectedTraitId switch
            {
                "double_cast" => "더블 캐스트 추가 시전 피해 +1",
                "pierce" => "관통 발동 시 추가 대상 피해 +1",
                "damage_boost" => "특성 공격 주변 피해 +1",
                "knockback" => "넉백 직접 피해 +1\n첫 충돌 상대 피해 +1",
                _ => "시작 특성의 효과 강화"
            },
            _ => "효과는 상세에서 확인"
        };
    }

    public static string ChoiceDetails(UpgradeDefinition item, RunProgressionSystem run)
    {
        string description = ChoiceDescription(item, run);
        if (!IsMovementUpgrade(item.Id)) return description;
        bool training = item.Id.StartsWith("movement_training_", StringComparison.Ordinal);
        PlayerMovementArt art = training && run.HasMovementArt ? run.ActiveMovementArt : GrantedMovementArt(item.Id);
        int level = !run.HasMovementArt ? 1 : training ? Math.Min(RunProgressionSystem.MaxMovementArtLevel, run.MovementArtLevel + 1) : run.MovementArtLevel;
        return description.Substring(0, description.LastIndexOf("\n\n", StringComparison.Ordinal))
            + "\n\n" + MovementEffect(art, level);
    }

    private static string MovementSummary(PlayerMovementArt art, int level)
    {
        string effect = art switch
        {
            PlayerMovementArt.Knight => "L자 도약으로 중간 벽·적을 넘습니다. 빈 칸에만 착지합니다.",
            PlayerMovementArt.Bishop => $"대각선 최대 {2 + level}칸. 지나간 불길 제거. 중간 벽·적 통과 불가.",
            PlayerMovementArt.Rook => $"직선 최대 {3 + level}칸. 중간 벽·적 통과 및 불길 제거 불가.",
            _ => string.Empty
        };
        return effect + "\nS로 선택 · 행동 1회 소비\n세부 조건은 [상세]에서 확인";
    }

    public static string OwnedDescription(UpgradeDefinition item, RunProgressionSystem run)
    {
        if (IsMovementUpgrade(item.Id))
            return "이 보상은 이미 적용되었습니다. 현재 보유 이동술과 실제 레벨은 아래와 같습니다.\n\n현재: "
                + run.MovementArtName + $" Lv.{run.MovementArtLevel} (최대 Lv.3)\n\n" + MovementEffect(run.ActiveMovementArt, run.MovementArtLevel);
        int stack = run.Stack(item.Id);
        return $"현재 Lv.{stack} / 최대 Lv.{item.MaxStacks}" + (stack >= item.MaxStacks ? " · 최대 강화 완료" : "")
            + "\n\n" + Effect(item, stack, run);
    }

    private static string Effect(UpgradeDefinition item, int stack, RunProgressionSystem run)
    {
        if (item.Id == "trait_mastery") return TraitMastery(run.SelectedTraitId);
        if (item.Id == "rapid_cycle") return TraitDeepening(run.SelectedTraitId);
        string effect = item.DescriptionAtStack(stack);
        if (IsFrequencyUpgrade(item.Id))
            effect += "\n\n요구량은 즉시 줄어듭니다. 현재 충전은 유지하며 이미 준비한 한 번을 추가로 늘리지는 않습니다.";
        return effect;
    }

    public static bool IsFrequencyUpgrade(string id)
    {
        return id == "quick_cast" || id == "thin_reload" || id == "dense_mana" || id == "compressed_impact";
    }

    private static string UpgradeBenefit(string id, int current, int next)
    {
        switch (id)
        {
            case "quick_cast": return $"필요한 일반 적중 {4 - current}회 → {4 - next}회";
            case "thin_reload": return "필요한 일반 적중 2회 → 1회";
            case "dense_mana":
            case "compressed_impact": return $"필요한 일반 적중 {3 - current}회 → {3 - next}회";
            case "movement_breath":
                return $"행동을 막는 적 수 {current}명 → {next}명";
            case "afterglow_recovery": return $"폭발 범위 {8 + current * 4} → {8 + next * 4}칸 · "
                + (next == 1 ? "바깥 십자 추가" : "바깥 X자 추가") + " · 피해 유지";
            case "echo_recovery": return "추가 시전 8방향 재조준 해금";
            case "impact_recovery": return current == 0 ? "직접 대상 2턴 봉쇄 · 막힌 경로 충격 피해 1" : "첫 충돌 상대 1턴 봉쇄 추가";
            case "pierce_recovery": return $"추가 관통 대상 피해 보너스 {current} → {next}";
            case "healing_breath": return $"클리어 추가 회복량 {current} → {next}";
            case "echo_warhead": return $"추가 공격 직접 피해 보너스 {current} → {next}";
            case "long_needle": return $"발동 공격의 최대 직격 대상 {2 + current}명 → {2 + next}명";
            case "overcharge": return $"발동 직접 피해 보너스 {current} → {next}";
            case "life_tempering": return "최대 체력 +1, 현재 체력 +1";
            default: return "아래 효과를 새로 얻습니다.";
        }
    }

    private static string MovementLevelBenefit(PlayerMovementArt art, int level)
    {
        switch (art)
        {
            case PlayerMovementArt.Knight: return level == 2 ? "착지 칸과 상하좌우 4칸의 불길 제거 추가. 도약 거리는 유지됩니다." : "도약 후 다음 공격의 첫 직접 적중 피해 +1 추가. 불길 제거와 도약 거리는 유지됩니다.";
            case PlayerMovementArt.Bishop: return $"대각선 이동 거리 {1 + level}칸 → {2 + level}칸. 경로 불길 제거는 유지됩니다.";
            case PlayerMovementArt.Rook: return $"직선 이동 거리 {2 + level}칸 → {3 + level}칸. 불길 제거 효과는 없습니다.";
            default: return string.Empty;
        }
    }

    public static string TraitEffect(string id)
    {
        return id switch
        {
            "double_cast" => "특성 공격은 같은 방향으로 한 번 더 시전합니다. 추가 시전은 충전하지 않고, 적은 모든 시전이 끝난 뒤 한 번 행동합니다.",
            "pierce" => "발동하면 첫 적을 넘어 같은 방향의 다음 적 1명까지 맞힙니다. 공격 방향에 적이 줄지어 있어야 합니다.",
            "damage_boost" => "특성 공격의 적중 위치 주변 기본 8칸에 파편 피해 1을 줍니다. 폭발 증폭으로 바깥 십자·X자를 더해 12·16칸까지 확장합니다. 직접 대상은 피해 +1을 받아 기본 피해 1 기준 직격 피해 2입니다. 직접 대상은 주변 피해에서 제외하며 플레이어는 피해를 받지 않습니다. 직접 대상을 처치하지 않아도 폭발하고, 빗나가면 폭발하지 않습니다. 한 행동에 한 번이며 주변 피해는 추가 폭발·충전·직접 처치 보상을 만들지 않습니다.",
            "knockback" => "살아남은 적을 공격 방향으로 1칸 밉니다. 벽 충돌은 밀린 적에게 피해 1, 적 충돌은 양쪽에 피해 1. 충돌 피해는 적마다 행동당 한 번이며 플레이어에게는 적용하지 않습니다. 성벽의 룩은 밀리지 않습니다.",
            _ => "아직 시작 특성을 선택하지 않았습니다."
        };
    }

    public static string TraitMastery(string id)
    {
        return id switch
        {
            "double_cast" => "더블 캐스트의 추가 공격 각각에 직접 피해 +1을 줍니다. 첫 공격에는 적용되지 않습니다. 충전 요구량은 유지됩니다.",
            "pierce" => "관통이 발동한 공격에서 두 번째 적부터 직접 피해 +1을 줍니다. 첫 대상의 피해와 충전 요구량은 유지됩니다.",
            "damage_boost" => "특성 공격의 현재 폭발 범위에 들어간 적의 파편 피해 +1. 폭발 증폭은 범위를 늘리고, 폭발 핵은 중심 직접 처치 시에만 추가 피해 +1을 줍니다. 직접 피해와 범위는 유지하며 추가 폭발·충전·직접 처치 보상은 없습니다.",
            "knockback" => "넉백 발동 직접 피해가 1 증가하고 처음 충돌한 적에게 추가 피해 1을 줍니다. 주변의 무관한 적에게는 적용되지 않으며 충전 요구량은 유지됩니다.",
            _ => "선택한 시작 특성의 효과를 강화합니다."
        };
    }

    private static string DeepeningSummary(string id)
    {
        return id switch
        {
            "double_cast" => "추가 시전 직접 피해 +1",
            "pierce" => "관통 발동 시 추가 직격 대상 +1명",
            "damage_boost" => "특성 공격 직접 피해 +1",
            "knockback" => "넉백 발동 밀기 거리 +1칸",
            _ => "선택한 시작 특성의 고유 효과 강화"
        };
    }

    public static string TraitDeepening(string id)
    {
        return DeepeningSummary(id) + (id switch
        {
            "double_cast" => ". 첫 공격에는 적용되지 않고 관통이나 범위 피해는 주지 않습니다.",
            "pierce" => ". 같은 공격선의 적만 맞히며 피해량과 충전 요구량은 유지됩니다.",
            "damage_boost" => ". 과충전과 합산하며 주변 피해량·폭발 범위·충전 요구량은 유지합니다.",
            "knockback" => ". 벽·적을 통과하지 못하고 성벽 룩은 움직이지 않습니다. 다른 밀기 강화와 합산됩니다.",
            _ => ". 시작 특성을 먼저 선택해야 합니다."
        });
    }
}
