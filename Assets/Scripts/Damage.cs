using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hitbox. Put it on a GameObject with a trigger collider (sword blade, enemy fist, explosion).
/// While the collider is enabled, it damages anything with the target tag that has a Health,
/// hitting each target only once per activation (one swing = one hit per enemy).
///
/// Two ways to drive it:
///   1. Just enable/disable the collider (what CommonEnemy does). The hit list resets each time it turns on.
///   2. Call Activate() / Deactivate() from a weapon script.
///
/// With "Roll Luck" on, it asks the LuckSystem for one roll per activation and multiplies
/// the damage by it. Player weapons use that; enemy hitboxes leave it off.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Damage : MonoBehaviour
{
    [Header("Damage")]
    public float damageAmount = 10f;

    [Tooltip("Only objects with this tag get hurt. Player weapons: \"Enemy\". Enemy attacks: \"Player\".")]
    public string targetTag = "Enemy";

    [Tooltip("Who gets credit for the hit. Leave empty to use the root of this object (e.g. the Player).")]
    public GameObject owner;

    [Header("Luck")]
    [Tooltip("Roll the LuckSystem once per activation and multiply damage by the result.")]
    public bool rollLuck = false;

    [Header("Debug")]
    public bool logHits = false;

    /// <summary>(target hit, damage dealt). Weapons listen to this for lunges, cleave waves, etc.</summary>
    public event Action<Health, float> OnHitTarget;

    /// <summary>The roll used for the current activation (only meaningful when rollLuck is on).</summary>
    public RollResult CurrentRoll { get; private set; }
    public bool HasRolled { get; private set; }

    private Collider col;
    private bool wasEnabled;
    private readonly HashSet<Health> alreadyHit = new HashSet<Health>();

    void Awake()
    {
        col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"Damage on '{name}': the collider should be a trigger. Ticking Is Trigger for you.");
            col.isTrigger = true;
        }
        if (owner == null) owner = transform.root.gameObject;
        wasEnabled = col.enabled;
    }

    void FixedUpdate()
    {
        // If someone turned the collider back on (way 1), start a fresh activation.
        if (col.enabled && !wasEnabled) BeginActivation();
        wasEnabled = col.enabled;
    }

    /// <summary>Turns the hitbox on for a new attack. Optionally overrides the damage.</summary>
    public void Activate(float newDamage = -1f)
    {
        if (newDamage >= 0f) damageAmount = newDamage;
        BeginActivation();
        col.enabled = true;
        wasEnabled = true;
    }

    public void Deactivate()
    {
        col.enabled = false;
        wasEnabled = false;
    }

    private void BeginActivation()
    {
        alreadyHit.Clear();
        HasRolled = false;
    }

    void OnTriggerEnter(Collider other) => TryHit(other);
    void OnTriggerStay(Collider other) => TryHit(other);   // catches targets already inside when it turns on

    private void TryHit(Collider other)
    {
        if (!col.enabled) return;

        Health target = other.GetComponentInParent<Health>();
        if (target == null || target.IsDead) return;
        if (!target.CompareTag(targetTag) && !other.CompareTag(targetTag)) return;
        if (target.gameObject == owner) return;          // never hit yourself
        if (!alreadyHit.Add(target)) return;             // already hit this activation

        float amount = damageAmount;

        if (rollLuck && LuckSystem.Instance != null)
        {
            // One roll per swing, shared by every enemy the swing hits.
            if (!HasRolled)
            {
                CurrentRoll = LuckSystem.Instance.Roll();
                HasRolled = true;
            }
            amount *= CurrentRoll.multiplier;
        }

        float dealt = target.TakeDamage(amount, owner);

        if (logHits && Application.isEditor) Debug.Log($"[Damage] {owner.name} hit {target.name} for {dealt:0.#}" +
                               (rollLuck && HasRolled ? $" ({CurrentRoll})" : ""));

        OnHitTarget?.Invoke(target, dealt);
    }
}
