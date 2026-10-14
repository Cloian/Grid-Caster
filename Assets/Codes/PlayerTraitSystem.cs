using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Move))]
public sealed class PlayerTraitSystem : MonoBehaviour
{
    private string selectedTraitId = string.Empty;
    private int selectedBagSize = 1;
    private int chargeProgress;

    public event Action<string> TraitActivated;
    public event Action<string> TraitSelected;
    public event Action ChargeChanged;

    public string SelectedTraitId => selectedTraitId;
    public int ActivationBagSize => selectedBagSize;
    // 기존 씬의 주기 값을 보존한다. 일반 적중 N-1회 + 특성 공격 1회가 한 주기다.
    public int ChargeRequired => Mathf.Max(1, selectedBagSize - 1);
    public int ChargeProgress => chargeProgress;
    public bool IsReady => !string.IsNullOrEmpty(selectedTraitId) && chargeProgress >= ChargeRequired;

    public void SelectTrait(string traitId, int activationBagSize)
    {
        selectedTraitId = traitId ?? string.Empty;
        selectedBagSize = Mathf.Max(1, activationBagSize);
        ResetCharge();
        TraitSelected?.Invoke(selectedTraitId);
    }

    public void SetActivationBagSize(int bagSize)
    {
        int requestedSize = Mathf.Max(1, bagSize);
        if (selectedBagSize == requestedSize) return;
        selectedBagSize = requestedSize;
        chargeProgress = Mathf.Min(chargeProgress, ChargeRequired);
        ChargeChanged?.Invoke();
    }

    [Obsolete("자동 추첨은 제거되었습니다. 공격 확정은 TryBeginAttack을 사용하세요.")]
    public PlayerAttackTraitRoll RollBasicAttack()
    {
        // 호환 진입점도 일반 공격이다. 자동 추첨은 더 이상 하지 않는다.
        return default;
    }

    public PlayerAttackTraitRoll PreviewAttack(bool useTrait)
    {
        if (!useTrait || !IsReady) return default;
        return new PlayerAttackTraitRoll(
            selectedTraitId == "double_cast",
            selectedTraitId == "pierce",
            selectedTraitId == "damage_boost",
            selectedTraitId == "knockback"
        );
    }

    public bool TryBeginAttack(bool useTrait, out PlayerAttackTraitRoll effect)
    {
        effect = PreviewAttack(useTrait);
        if (!useTrait) return true;
        if (!IsReady) return false;
        ResetCharge();
        TraitActivated?.Invoke(selectedTraitId);
        return true;
    }

    public void RegisterCompletedAttack(bool hitEnemy, bool usedTrait)
    {
        // 전체 공격 행동이 끝난 뒤 한 번만 호출한다. 추가 영창/범위 피해는 중복 충전하지 않는다.
        if (!hitEnemy || usedTrait || IsReady || string.IsNullOrEmpty(selectedTraitId)) return;
        chargeProgress = Mathf.Min(ChargeRequired, chargeProgress + 1);
        ChargeChanged?.Invoke();
    }

    public void ResetCharge()
    {
        chargeProgress = 0;
        ChargeChanged?.Invoke();
    }
}

public readonly struct PlayerAttackTraitRoll
{
    public PlayerAttackTraitRoll(
        bool doubleCast,
        bool pierce,
        bool damageBoost,
        bool knockback
    )
    {
        DoubleCast = doubleCast;
        Pierce = pierce;
        DamageBoost = damageBoost;
        Knockback = knockback;
    }

    public bool DoubleCast { get; }
    public bool Pierce { get; }
    public bool DamageBoost { get; }
    public bool Knockback { get; }
}
