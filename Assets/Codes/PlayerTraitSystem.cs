using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Move))]
public sealed class PlayerTraitSystem : MonoBehaviour
{
    private readonly List<bool> activationBag = new List<bool>();
    private string selectedTraitId = string.Empty;
    private int selectedBagSize = 1;
    private int nextBagIndex;

    public event Action<string> TraitActivated;
    public event Action<string> TraitSelected;

    public string SelectedTraitId => selectedTraitId;
    public int ActivationBagSize => selectedBagSize;

    public void SelectTrait(string traitId, int activationBagSize)
    {
        selectedTraitId = traitId ?? string.Empty;
        selectedBagSize = Mathf.Max(1, activationBagSize);
        RefillActivationBag();
        TraitSelected?.Invoke(selectedTraitId);
    }

    public void SetActivationBagSize(int bagSize)
    {
        int requestedSize = Mathf.Max(1, bagSize);
        if (selectedBagSize == requestedSize) return;
        selectedBagSize = requestedSize;
        // 이미 소비한 백은 보존한다. 주기 변경은 다음 백부터 적용한다.
        // 첫 행동 전의 시작 강화만 미사용 백에 즉시 반영한다.
        if (nextBagIndex == 0) RefillActivationBag();
    }

    public PlayerAttackTraitRoll RollBasicAttack()
    {
        if (string.IsNullOrEmpty(selectedTraitId))
            return default;

        if (activationBag.Count == 0 || nextBagIndex >= activationBag.Count)
        {
            RefillActivationBag();
        }

        bool activated = activationBag[nextBagIndex];
        nextBagIndex++;

        if (!activated)
            return default;

        TraitActivated?.Invoke(selectedTraitId);
        Debug.Log($"특성 발동: {selectedTraitId}", this);

        return new PlayerAttackTraitRoll(
            selectedTraitId == "double_cast",
            selectedTraitId == "pierce",
            selectedTraitId == "damage_boost",
            selectedTraitId == "knockback"
        );
    }

    private void RefillActivationBag()
    {
        activationBag.Clear();
        nextBagIndex = 0;

        if (string.IsNullOrEmpty(selectedTraitId))
            return;

        // 셔플 백 하나마다 정확히 한 번 발동시켜 긴 연속 실패를 방지한다.
        activationBag.Add(true);

        for (int i = 1; i < selectedBagSize; i++)
        {
            activationBag.Add(false);
        }

        for (int i = activationBag.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            bool temporary = activationBag[i];
            activationBag[i] = activationBag[swapIndex];
            activationBag[swapIndex] = temporary;
        }
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
