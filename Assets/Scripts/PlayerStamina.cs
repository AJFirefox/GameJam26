using System;
using UnityEngine;

/// <summary>
/// Stamina for the player. Rolling costs stamina; it refills after a short delay.
/// Put this on the Player next to PlayerMovement and Health.
/// </summary>
public class PlayerStamina : MonoBehaviour
{
    public float maxStamina = 100f;
    [Tooltip("Stamina refilled per second.")]
    public float regenPerSecond = 35f;
    [Tooltip("Seconds after spending stamina before it starts refilling.")]
    public float regenDelay = 0.6f;
    [Tooltip("Stamina cost of one dodge roll.")]
    public float rollCost = 30f;

    /// <summary>(current, max). The HUD stamina bar listens to this.</summary>
    public event Action<float, float> OnStaminaChanged;

    public float CurrentStamina => current;
    public float Normalized => maxStamina > 0f ? current / maxStamina : 0f;

    private float current;
    private float regenAllowedAt;

    void Awake()
    {
        current = maxStamina;
    }

    void Start()
    {
        OnStaminaChanged?.Invoke(current, maxStamina);
    }

    void Update()
    {
        if (current >= maxStamina || Time.time < regenAllowedAt) return;

        current = Mathf.Min(maxStamina, current + regenPerSecond * Time.deltaTime);
        OnStaminaChanged?.Invoke(current, maxStamina);
    }

    public bool CanAfford(float amount) => current >= amount;

    /// <summary>Spends stamina if there's enough. Returns false (and spends nothing) if not.</summary>
    public bool TrySpend(float amount)
    {
        if (current < amount) return false;
        current -= amount;
        regenAllowedAt = Time.time + regenDelay;
        OnStaminaChanged?.Invoke(current, maxStamina);
        return true;
    }

    public void Restore(float amount)
    {
        current = Mathf.Clamp(current + amount, 0f, maxStamina);
        OnStaminaChanged?.Invoke(current, maxStamina);
    }
}
