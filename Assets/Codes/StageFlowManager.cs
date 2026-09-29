using System;
using System.Collections;
using UnityEngine;

public enum StageFlowState
{
    Idle,
    PreparingWave,
    Combat,
    WaveCleared,
    UpgradeSelection,
    StageBoss,
    StageCleared,
    GameOver
}

// 스테이지 진행 순서만 관리한다. 몬스터 생성과 전투 판정은 MonsterSpawner에 위임한다.
[DisallowMultipleComponent]
public sealed class StageFlowManager : MonoBehaviour
{
    public event Action<StageFlowState> StateChanged;
    public event Action<int> UpgradeSelectionRequested;
    public event Action StageCleared;

    [Header("스테이지 흐름")]
    [SerializeField, Min(1)] private int finalWave = WaveTemplateCatalog.FinalWave;
    [SerializeField, Min(0f)] private float fallbackAdvanceDelay = 1f;
    [SerializeField, Range(0f, 1f)] private float waveClearHealRatio = 0.1f;

    private MonsterSpawner monsterSpawner;
    private Move playerMovement;
    private CharacterHealth playerHealth;
    private Coroutine fallbackAdvanceRoutine;
    private bool initialized;

    public StageFlowState State { get; private set; } = StageFlowState.Idle;
    public int CurrentWave { get; private set; }
    public int FinalWave => finalWave;
    public bool IsWaitingForUpgrade => State == StageFlowState.UpgradeSelection;

    public void Initialize(
        MonsterSpawner spawner,
        Move controlledPlayer,
        CharacterHealth controlledPlayerHealth
    )
    {
        if (initialized)
            return;

        monsterSpawner = spawner;
        playerMovement = controlledPlayer;
        playerHealth = controlledPlayerHealth;

        if (monsterSpawner == null || playerMovement == null || playerHealth == null)
        {
            Debug.LogError("StageFlowManager 초기화에 스포너와 플레이어 참조가 필요합니다.", this);
            enabled = false;
            return;
        }

        monsterSpawner.WaveCleared += HandleWaveCleared;
        playerHealth.Died += HandlePlayerDied;
        initialized = true;
    }

    public void BeginRun()
    {
        if (!initialized || State != StageFlowState.Idle)
            return;

        CurrentWave = 0;
        StartNextWave();
    }

    // 강화 UI가 실제 선택을 적용한 뒤 호출한다.
    public bool CompleteUpgradeSelection()
    {
        if (State != StageFlowState.UpgradeSelection)
            return false;

        StopFallbackAdvance();
        StartNextWave();
        return true;
    }

    private void StartNextWave()
    {
        if (CurrentWave >= finalWave)
            return;

        CurrentWave++;
        ChangeState(StageFlowState.PreparingWave);

        bool isBossWave = CurrentWave == finalWave;
        if (isBossWave)
        {
            ChangeState(StageFlowState.StageBoss);
        }

        if (!monsterSpawner.StartWave(CurrentWave))
        {
            Debug.LogError($"웨이브 {CurrentWave}에 몬스터를 생성하지 못해 진행을 중단합니다.", this);
            ChangeState(StageFlowState.Idle);
            return;
        }

        // 첫 웨이브의 입력 잠금은 시작 특성 UI가 관리한다.
        if (CurrentWave > 1 && !playerHealth.IsDead)
        {
            playerMovement.SetInputEnabled(true);
        }

        if (!isBossWave)
        {
            ChangeState(StageFlowState.Combat);
        }
    }

    private void HandleWaveCleared(int waveNumber)
    {
        if (waveNumber != CurrentWave
            || (State != StageFlowState.Combat && State != StageFlowState.StageBoss))
        {
            return;
        }

        ChangeState(StageFlowState.WaveCleared);

        if (CurrentWave >= finalWave)
        {
            playerMovement.SetInputEnabled(false);
            ChangeState(StageFlowState.StageCleared);
            StageCleared?.Invoke();
            Debug.Log($"스테이지 클리어: 웨이브 {CurrentWave}", this);
            return;
        }

        HealAfterWave();
        playerMovement.SetInputEnabled(false);
        ChangeState(StageFlowState.UpgradeSelection);

        if (UpgradeSelectionRequested != null)
        {
            UpgradeSelectionRequested.Invoke(CurrentWave);
            return;
        }

        // 강화 UI가 붙기 전 단계에서도 기존 전투 루프를 계속 시험할 수 있게 한다.
        fallbackAdvanceRoutine = StartCoroutine(AdvanceWithoutUpgradeSelection());
    }

    private void HealAfterWave()
    {
        RunProgressionSystem progression = playerMovement.GetComponent<RunProgressionSystem>();
        float healRatio = waveClearHealRatio
            + (progression != null ? progression.WaveHealRatioBonus : 0f);
        int requestedHeal = Mathf.CeilToInt(playerHealth.MaxHealth * healRatio);
        int healedAmount = playerHealth.Heal(requestedHeal);

        Debug.Log(
            $"웨이브 {CurrentWave} 클리어: 최대 체력의 {healRatio * 100f:0.#}% 회복 "
            + $"({healedAmount}/{requestedHeal})",
            this
        );
    }

    private IEnumerator AdvanceWithoutUpgradeSelection()
    {
        if (fallbackAdvanceDelay > 0f)
        {
            yield return new WaitForSeconds(fallbackAdvanceDelay);
        }

        fallbackAdvanceRoutine = null;
        if (State == StageFlowState.UpgradeSelection)
        {
            StartNextWave();
        }
    }

    private void HandlePlayerDied(CharacterHealth defeatedPlayer)
    {
        StopFallbackAdvance();
        ChangeState(StageFlowState.GameOver);
    }

    private void ChangeState(StageFlowState nextState)
    {
        if (State == nextState)
            return;

        State = nextState;
        StateChanged?.Invoke(State);
    }

    private void StopFallbackAdvance()
    {
        if (fallbackAdvanceRoutine == null)
            return;

        StopCoroutine(fallbackAdvanceRoutine);
        fallbackAdvanceRoutine = null;
    }

    private void OnValidate()
    {
        finalWave = Mathf.Max(1, finalWave);
        fallbackAdvanceDelay = Mathf.Max(0f, fallbackAdvanceDelay);
        waveClearHealRatio = Mathf.Clamp01(waveClearHealRatio);
    }

    private void OnDestroy()
    {
        StopFallbackAdvance();

        if (monsterSpawner != null)
        {
            monsterSpawner.WaveCleared -= HandleWaveCleared;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
        }
    }
}
