using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterHealth : MonoBehaviour
{
    public event Action<CharacterHealth> Died;
    // 모든 캐릭터의 실제 피해(피해 숫자 표시용). 두 번째 값은 실제로 깎인 체력이다(초과 피해 제외).
    public static event Action<CharacterHealth, int> AnyDamaged;

    [SerializeField] private int maxHealth = 1;
    [SerializeField] private int currentHealth = 1;

    private DamageFlash damageFlash;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        damageFlash = GetComponent<DamageFlash>();

        if (damageFlash == null)
        {
            damageFlash = gameObject.AddComponent<DamageFlash>();
        }
    }

    public void Initialize(int startingMaxHealth)
    {
        maxHealth = Mathf.Max(1, startingMaxHealth);
        currentHealth = maxHealth;
        CombatFeedback.ResetActor(this);
    }

    public void IncreaseMaxHealth(int amount, bool healSameAmount)
    {
        if (amount <= 0)
            return;

        maxHealth += amount;
        if (healSameAmount && !IsDead)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }
    }

    public void AdjustMaxHealth(int amount)
    {
        maxHealth = Mathf.Max(1, maxHealth + amount);
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    public void TakeDamage(int damage, Vector3Int incomingDirection = default)
    {
        if (IsDead || damage <= 0)
            return;

        int before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (CombatFeedback.DamageVisualDelay <= 0f) damageFlash?.Play();
        ActorMotion.Trigger(this, IsDead ? ActorMotion.Die : ActorMotion.Hit);
        CombatFeedback.PlayHit(this, incomingDirection, IsDead);
        AnyDamaged?.Invoke(this, before - currentHealth);
        Debug.Log($"{name}: 피해 {damage}, 체력 {currentHealth}/{maxHealth}", this);

        if (IsDead)
        {
            Died?.Invoke(this);
        }
    }

    public int Heal(int amount)
    {
        if (IsDead || amount <= 0 || currentHealth >= maxHealth)
            return 0;

        int healthBeforeHeal = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        int healedAmount = currentHealth - healthBeforeHeal;

        Debug.Log(
            $"{name}: 체력 회복 {healedAmount}, 체력 {currentHealth}/{maxHealth}",
            this
        );
        return healedAmount;
    }
}
