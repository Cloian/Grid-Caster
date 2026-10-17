using System;
using UnityEngine;

// 주도권(프로토타입): 이동술은 주도권을 소모하고, 주도권은 적극적인 공격으로만 찬다.
// 멀리서 저격하고 위험하면 이동술로 도망치는 반복을 줄이려는 실험이다. 기본 한 칸 이동은 항상 공짜다.
// 이 컴포넌트가 없는 씬(체스·보스 테스트)은 기존처럼 이동술에 제한이 없다.
[DisallowMultipleComponent]
public sealed class InitiativeTracker : MonoBehaviour
{
    [Tooltip("끄면 이동술 비용이 사라져 기존 규칙과 비교할 수 있습니다.")]
    [SerializeField] private bool enableInitiative = true;
    [SerializeField, Min(1)] private int maxInitiative = 3;
    [SerializeField, Min(0)] private int startInitiative = 1;
    [SerializeField, Min(1)] private int movementArtCost = 1;
    [Header("공격 한 번에 얻는 주도권")]
    [Tooltip("이번 공격으로 처치한 적 하나마다")]
    [SerializeField, Min(0)] private int gainPerKill = 1;
    [Tooltip("한 공격(연속 시전·범위 포함)으로 서로 다른 적 2명 이상에게 피해")]
    [SerializeField, Min(0)] private int gainMultiHit = 1;
    [Tooltip("8방향으로 붙어 있는 적을 직접 맞힘")]
    [SerializeField, Min(0)] private int gainAdjacentHit = 1;
    [Tooltip("공격 한 번으로 얻을 수 있는 최대치")]
    [SerializeField, Min(1)] private int maxGainPerAttack = 2;
    [Tooltip("여왕의 시계 안에 웨이브를 정리하면")]
    [SerializeField, Min(0)] private int fastClearBonus = 1;

    public event Action Changed;
    public bool IsEnabled => enableInitiative;
    public int Current { get; private set; }
    public int Max => maxInitiative;
    public int MovementArtCost => movementArtCost;
    public string LastGainReason { get; private set; } = string.Empty;
    public int FastClearBonus => fastClearBonus;

    // 실험 지표(웨이브 단위로 비교): 이동술 사용, 주도권 부족으로 못 쓴 횟수, 공격 결과
    public int MovementArtsUsed { get; private set; }
    public int AttacksResolved { get; private set; }
    public int MultiHitAttacks { get; private set; }
    public int AdjacentHits { get; private set; }

    public bool CanAffordMovementArt => !enableInitiative || Current >= movementArtCost;

    private void Awake()
    {
        Current = Mathf.Clamp(startInitiative, 0, maxInitiative);
    }

    // 이동술·시계와 무관한 규칙을 검사하는 검증이 주도권 영향을 빼고 확인할 때 쓴다.
    public void SetEnabledForValidation(bool enabled)
    {
        enableInitiative = enabled;
        Changed?.Invoke();
    }

    public bool TrySpendMovementArt()
    {
        if (!enableInitiative) { MovementArtsUsed++; return true; }
        if (Current < movementArtCost) return false;
        Current -= movementArtCost;
        MovementArtsUsed++;
        Changed?.Invoke();
        return true;
    }

    public void RegisterAttack(int distinctTargetsHit, int kills, bool adjacentHit)
    {
        AttacksResolved++;
        if (distinctTargetsHit >= 2) MultiHitAttacks++;
        if (adjacentHit) AdjacentHits++;
        int gain = kills * gainPerKill
            + (distinctTargetsHit >= 2 ? gainMultiHit : 0)
            + (adjacentHit ? gainAdjacentHit : 0);
        gain = Mathf.Min(gain, maxGainPerAttack);
        if (gain <= 0) return;
        string reason = kills > 0 ? "처치" : distinctTargetsHit >= 2 ? "다수 적중" : "근접 적중";
        Grant(gain, reason);
    }

    public void Grant(int amount, string reason)
    {
        if (!enableInitiative || amount <= 0) return;
        int before = Current;
        Current = Mathf.Min(maxInitiative, Current + amount);
        if (Current == before) return;
        LastGainReason = reason;
        Changed?.Invoke();
    }

    private void OnValidate()
    {
        startInitiative = Mathf.Clamp(startInitiative, 0, maxInitiative);
        maxGainPerAttack = Mathf.Max(1, maxGainPerAttack);
    }
}
