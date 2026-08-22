using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterHealth : MonoBehaviour
{
    public event Action<CharacterHealth> Died;

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
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        damageFlash?.Play();
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
