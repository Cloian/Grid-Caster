using System;
using UnityEngine;

[Serializable]
public sealed class TraitOptionData
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private string placeholder;
    [Tooltip("이 횟수의 공격마다 정확히 한 번 발동하도록 셔플 백을 구성합니다.")]
    [SerializeField, Min(1)] private int activationBagSize = 4;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public string Placeholder => placeholder;
    public int ActivationBagSize => Mathf.Max(1, activationBagSize);
    public float ActivationChance => 1f / ActivationBagSize;
}
