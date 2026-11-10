using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health for anything that can be hurt: the player, enemies, bosses.
///
/// Deal damage:      target.GetComponent<Health>().TakeDamage(10f, gameObject);
/// Heal:             health.Heal(25f);
/// Listen in code:   health.OnHealthChanged += (current, max) => { ... };
/// Any death:        Health.OnAnyDeath += (died, killer) => { ... };   // score, Hades, Raven curse
/// </summary>
public class Health : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Taking damage")]
    [Tooltip("Seconds of invulnerability after being hit. Give the player ~0.5, leave enemies at 0.")]
    public float invulnerabilityAfterHit = 0f;

    [Tooltip("Multiplies all incoming damage. Stone (Snake Eyes) sets this to 2.")]
    public float damageTakenMultiplier = 1f;

    [Header("Death")]
    [Tooltip("Enemies: on. Player: off (the GameManager will handle game over).")]
    public bool destroyOnDeath = true;
    [Tooltip("Seconds to wait before destroying, e.g. to let a death animation play.")]
    public float destroyDelay = 0f;

    [Header("Inspector events (optional: hook up sounds, particles, animations)")]
    public UnityEvent onDamaged;
    public UnityEvent onHealed;
    public UnityEvent onDeath;

    // ---------- Code events ----------
    /// <summary>(current, max). Fires on damage, heal and reset. Good for health bars.</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>(amount actually taken, who dealt it, may be null).</summary>
    public event Action<float, GameObject> OnDamaged;
    /// <summary>(who killed it, may be null).</summary>
    public event Action<GameObject> OnDied;
    /// <summary>Fires when anything dies. (what died, who killed it, may be null).</summary>
    public static event Action<Health, GameObject> OnAnyDeath;

    /// <summary>Set true while rolling, during cutscenes, etc.</summary>
    public bool IsInvulnerable { get; set; }

    public float CurrentHealth => currentHealth;
    public float Normalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public bool IsDead { get; private set; }
    public GameObject LastAttacker { get; private set; }

    private float invulnerableUntil;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Deals damage. Returns how much was actually taken (0 if dead, invulnerable, etc.).
    /// </summary>
    public float TakeDamage(float amount, GameObject source = null)
    {
        if (IsDead || amount <= 0f) return 0f;
        if (IsInvulnerable || Time.time < invulnerableUntil) return 0f;

        float taken = Mathf.Min(amount * damageTakenMultiplier, currentHealth);
        currentHealth -= taken;
        LastAttacker = source;

        if (invulnerabilityAfterHit > 0f) invulnerableUntil = Time.time + invulnerabilityAfterHit;

        OnDamaged?.Invoke(taken, source);
        onDamaged?.Invoke();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f) Die(source);
        return taken;
    }

    /// <summary>Heals up to max. Returns how much was actually healed.</summary>
    public float Heal(float amount)
    {
        if (IsDead || amount <= 0f) return 0f;

        float healed = Mathf.Min(amount, maxHealth - currentHealth);
        if (healed <= 0f) return 0f;

        currentHealth += healed;
        onHealed?.Invoke();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        return healed;
    }

    /// <summary>Kills instantly, ignoring invulnerability.</summary>
    public void Kill(GameObject source = null)
    {
        if (IsDead) return;
        currentHealth = 0f;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Die(source);
    }

    /// <summary>Back to full and alive. Use for restarting a run, or scaling enemies per wave.</summary>
    public void ResetHealth(float newMaxHealth = -1f)
    {
        if (newMaxHealth > 0f) maxHealth = newMaxHealth;
        currentHealth = maxHealth;
        IsDead = false;
        invulnerableUntil = 0f;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die(GameObject killer)
    {
        IsDead = true;

        OnDied?.Invoke(killer);
        onDeath?.Invoke();
        OnAnyDeath?.Invoke(this, killer);

        if (destroyOnDeath) Destroy(gameObject, destroyDelay);
    }
}
