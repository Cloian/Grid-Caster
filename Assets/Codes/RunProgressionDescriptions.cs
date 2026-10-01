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
            summary += $"\n평균 발동 확률 {Chance(run.TraitActivationInterval)} → {Chance(Math.Max(2, run.TraitActivationInterval - 1))}";
        else if (item.IsTraitUpgrade || item.Id == "keen_magic" || item.Id == "trait_mastery")
            summary += "\n평균 발동 확률 " + Chance(run.TraitActivationInterval);
        return summary;
    }

    private static string CoreEffect(string id, int level, RunProgressionSystem run)
    {
        return id switch
        {
            "life_tempering" => "최대 체력 +1 · 즉시 체력 +1",
            "mana_circulation" => $"공격 처치 시 주변 8칸 폭발\n폭발 피해 {level}",
            "movement_breath" => $"이동술 착지 시 주변 적 {level}명 행동 봉쇄",
            "healing_breath" => $"웨이브 클리어 시 추가 체력 회복 {level}",
            "quick_cast" => "더블 캐스트가 더 자주 발동",
            "thin_reload" => "관통이 더 자주 발동",
            "dense_mana" => "데미지 강화가 더 자주 발동",
            "compressed_impact" => "넉백이 더 자주 발동",
            "echo_warhead" => $"추가 시전 피해 +1 · 추가 관통 {level}명",
            "echo_recovery" => $"추가 시전 적중 시 주변 적 {level + 1}명 행동 봉쇄",
            "long_needle" => $"관통 발동 시 최대 {2 + level}명 직격",
            "pierce_recovery" => $"관통 추가 대상 주변 8칸에 피해 {level}",
            "overcharge" => level == 1 ? "강화 발동 시 추가 직접 피해 +1" : "강화 발동 직접 피해 +1\n첫 적중 주변 8칸에 피해 1 추가",
            "afterglow_recovery" => $"강화 공격 적중 시 주변 적 {level}명 행동 봉쇄",
            "long_impact" => "넉백 발동 시 피해 +1 · 밀기 2칸",
            "impact_recovery" => $"넉백 적중 시 최대 {level + 1}명 행동 봉쇄",
            "rapid_cycle" => "처치 폭발 피해 +1\n이동술 착지 시 적 1명 행동 봉쇄",
            "keen_magic" => "특성 발동 첫 직격 피해 +1\n최대 체력 -1",
            "combat_regeneration" => "웨이브 첫 직접 처치 시 체력 1 회복",
            "alternating_overload" => "이동 직후 공격 적중 시 주변 8칸에 피해 1",
            "immortal_cycle" => "웨이브 첫 직접 처치 시 체력 2 회복\n클리어 시 추가 회복 1",
            "trait_mastery" => run.SelectedTraitId switch
            {
                "double_cast" => "더블 캐스트 추가 시전 피해 +1",
                "pierce" => "관통 발동 시 추가 대상 피해 +1",
                "damage_boost" => "강화 발동 첫 적중 주변 8칸에 피해 1",
                "knockback" => "넉백 발동 시 최대 2명 행동 봉쇄",
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
            return "이 보상은 이미 적용되었습니다. 보상 이름의 I~IV는 획득 단계이며 이동술 레벨이 아닙니다.\n\n현재: "
                + run.MovementArtName + $" Lv.{run.MovementArtLevel} (최대 Lv.3)\n\n" + MovementEffect(run.ActiveMovementArt, run.MovementArtLevel);
        int stack = run.Stack(item.Id);
        return $"현재 Lv.{stack} / 최대 Lv.{item.MaxStacks}" + (stack >= item.MaxStacks ? " · 최대 강화 완료" : "")
            + "\n\n" + Effect(item, stack, run);
    }

    private static string Effect(UpgradeDefinition item, int stack, RunProgressionSystem run)
    {
        if (item.Id == "trait_mastery") return TraitMastery(run.SelectedTraitId);
        string effect = item.DescriptionAtStack(stack);
        if (IsFrequencyUpgrade(item.Id))
            effect += "\n\n발동은 매번 독립 추첨이 아니라 일정 공격 묶음 안에서 한 번 보장됩니다. 진행 중인 묶음이 끝나면 새 확률이 적용됩니다.";
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
            case "quick_cast": return $"평균 발동 확률 {Chance(5 - current)} → {Chance(5 - next)}";
            case "thin_reload": return "평균 발동 확률 33.3% → 50%";
            case "dense_mana":
            case "compressed_impact": return $"평균 발동 확률 {Chance(4 - current)} → {Chance(4 - next)}";
            case "mana_circulation": return $"처치 폭발 피해 {current} → {next}";
            case "movement_breath":
            case "afterglow_recovery": return $"행동을 막는 적 수 {current}명 → {next}명";
            case "echo_recovery":
            case "impact_recovery": return $"행동을 막는 적 수 {(current == 0 ? 0 : current + 1)}명 → {next + 1}명";
            case "pierce_recovery": return $"관통 파동 피해 {current} → {next}";
            case "healing_breath": return $"클리어 추가 회복량 {current} → {next}";
            case "echo_warhead": return current == 0 ? "추가 공격 피해 +1, 추가 관통 1명" : "추가 관통 1명 → 2명 (피해 보너스 유지)";
            case "long_needle": return $"발동 공격의 최대 직격 대상 {2 + current}명 → {2 + next}명";
            case "overcharge": return current == 0 ? "발동 직접 피해 +1" : "첫 적중 주변 8칸에 피해 1 추가";
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
            "double_cast" => "발동하면 같은 방향으로 기본공격을 한 번 더 합니다. 추가 공격은 특성을 다시 추첨하지 않고, 적은 모든 공격이 끝난 뒤 한 번 행동합니다.",
            "pierce" => "발동하면 첫 적을 넘어 같은 방향의 다음 적 1명까지 맞힙니다. 공격 방향에 적이 줄지어 있어야 합니다.",
            "damage_boost" => "발동한 기본공격의 직접 피해가 1 증가합니다. 기본 피해 1 기준으로 피해 2를 줍니다.",
            "knockback" => "발동하면 살아남은 적을 공격 방향으로 1칸 밉니다. 벽·다른 캐릭터가 막으면 밀지 못하며 성벽의 룩은 이동하지 않습니다.",
            _ => "아직 시작 특성을 선택하지 않았습니다."
        };
    }

    public static string TraitMastery(string id)
    {
        return id switch
        {
            "double_cast" => "더블 캐스트의 추가 공격 각각에 직접 피해 +1을 줍니다. 첫 공격에는 적용되지 않습니다. 발동 확률은 유지됩니다.",
            "pierce" => "관통이 발동한 공격에서 두 번째 적부터 직접 피해 +1을 줍니다. 첫 대상의 피해와 발동 확률은 유지됩니다.",
            "damage_boost" => "데미지 강화가 발동한 공격의 첫 적중 주변 8칸에 피해 1을 줍니다. 다른 범위 피해와 합산되며 발동 확률은 유지됩니다.",
            "knockback" => "넉백이 발동하면 살아남은 대상과 적중 지점 주변에서 최대 2명의 다음 행동을 막습니다. 다른 봉쇄 효과와 대상이 겹치지 않으며 발동 확률은 유지됩니다.",
            _ => "선택한 시작 특성의 효과를 강화합니다."
        };
    }
}
