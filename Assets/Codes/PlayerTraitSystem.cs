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

    public string SelectedTraitId => selectedTraitId;

    public void SelectTrait(string traitId, int activationBagSize)
    {
        selectedTraitId = traitId ?? string.Empty;
        selectedBagSize = Mathf.Max(1, activationBagSize);
        RefillActivationBag();
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
