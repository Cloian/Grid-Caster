using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Move))]
public sealed class UltimateGauge : MonoBehaviour
{
    public event Action<int, int> GaugeChanged;
    public event Action<bool> ReadyStateChanged;

    [Header("이동술 게이지")]
    [SerializeField, Min(1)] private int maxGauge = 100;
    [SerializeField, Min(0)] private int currentGauge;
    [SerializeField, Min(0)] private int moveGain = 5;
    [SerializeField, Min(0)] private int attackHitGain = 15;

    private Move playerMovement;
    private RunProgressionSystem progressionSystem;

    public int MaxGauge => maxGauge;
    public int CurrentGauge => currentGauge;
    public int MoveGain => moveGain;
    public int AttackHitGain => attackHitGain;
    public bool IsReady => currentGauge >= maxGauge;
    public float NormalizedGauge => maxGauge > 0
        ? (float)currentGauge / maxGauge
        : 0f;

    private void Awake()
    {
        playerMovement = GetComponent<Move>();
        progressionSystem = GetComponent<RunProgressionSystem>();
        currentGauge = Mathf.Clamp(currentGauge, 0, maxGauge);
    }

    private void OnEnable()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponent<Move>();
        }

        playerMovement.PlayerMoved += HandlePlayerMoved;
        playerMovement.AttackHit += HandleAttackHit;
    }

    private void Start()
    {
        NotifyGaugeChanged();
    }

    private void OnDisable()
    {
        if (playerMovement == null)
            return;

        playerMovement.PlayerMoved -= HandlePlayerMoved;
        playerMovement.AttackHit -= HandleAttackHit;
    }

    public void AddGauge(int amount)
    {
        if (amount <= 0 || IsReady)
            return;

        bool wasReady = IsReady;
        currentGauge = Mathf.Min(maxGauge, currentGauge + amount);
        NotifyGaugeChanged();

        if (wasReady != IsReady)
        {
            ReadyStateChanged?.Invoke(IsReady);
        }
    }

    public bool TryConsumeFullGauge(int retainedGauge = 0)
    {
        if (!IsReady)
            return false;

        currentGauge = Mathf.Clamp(retainedGauge, 0, maxGauge - 1);
        NotifyGaugeChanged();
        ReadyStateChanged?.Invoke(false);
        return true;
    }

    private void HandlePlayerMoved()
    {
        if (progressionSystem == null) progressionSystem = GetComponent<RunProgressionSystem>();
        // 한 타일 이동을 완료한 시점에만 게이지를 획득한다.
        AddGauge(moveGain + (progressionSystem != null ? progressionSystem.MoveGaugeBonus : 0));
    }

    private void HandleAttackHit()
    {
        if (progressionSystem == null) progressionSystem = GetComponent<RunProgressionSystem>();
        // 빈 칸 공격이 아니라 실제 적에게 적중했을 때만 게이지를 획득한다.
        AddGauge(attackHitGain + (progressionSystem != null ? progressionSystem.HitGaugeBonus : 0));
    }

    private void NotifyGaugeChanged()
    {
        GaugeChanged?.Invoke(currentGauge, maxGauge);
    }

    private void OnValidate()
    {
        maxGauge = Mathf.Max(1, maxGauge);
        currentGauge = Mathf.Clamp(currentGauge, 0, maxGauge);
        moveGain = Mathf.Max(0, moveGain);
        attackHitGain = Mathf.Max(0, attackHitGain);
    }
}
