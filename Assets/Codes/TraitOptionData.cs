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
    [Tooltip("기존 주기 저장값. 일반 적중 (이 값 - 1)회로 특성 공격 한 번을 준비합니다.")]
    [SerializeField, Min(1)] private int activationBagSize = 4;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public string Placeholder => placeholder;
    public int ActivationBagSize => Mathf.Max(1, activationBagSize);
    [System.Obsolete("수동 특성 공격은 확률로 발동하지 않습니다. 충전 요구량을 표시하세요.")]
    public float ActivationChance => 1f / ActivationBagSize;
}
