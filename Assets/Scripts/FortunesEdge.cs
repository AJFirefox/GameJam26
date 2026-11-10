using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Fortune's Edge: fast broadsword, 3-hit combo, lunge on hit 3.
/// Each hit rolls the dice. A Jackpot on hit 3 fires a cleave wave.
///
/// Swings are animated in code by rotating this object (the sword's pivot).
/// Setup: Main Camera > SwordPivot (this script) > your sword model > blade with a trigger collider + Damage.
/// </summary>
public class FortunesEdge : WeaponBase
{
    [Serializable]
    public class ComboHit
    {
        public float damage = 20f;
        [Tooltip("Pivot rotation at the start of the swing (local Euler angles).")]
        public Vector3 startRotation;
        [Tooltip("Pivot rotation at the end of the swing.")]
        public Vector3 endRotation;
        [Tooltip("Seconds the swing itself takes.")]
        public float swingTime = 0.2f;
    }

    [Header("References")]
    [Tooltip("Damage component on the blade. Its collider must be a trigger. Target Tag = Enemy, Roll Luck = on.")]
    public Damage bladeHitbox;

    [Header("Combo")]
    public ComboHit[] combo =
    {
        // Hit 1: right to left
        new ComboHit { damage = 20f, startRotation = new Vector3(10f, 70f, -30f),  endRotation = new Vector3(10f, -80f, -30f), swingTime = 0.18f },
        // Hit 2: left to right
        new ComboHit { damage = 20f, startRotation = new Vector3(10f, -80f, 30f),  endRotation = new Vector3(10f, 70f, 30f),   swingTime = 0.18f },
        // Hit 3: overhead slam
        new ComboHit { damage = 35f, startRotation = new Vector3(-70f, 0f, 0f),    endRotation = new Vector3(70f, 0f, 0f),     swingTime = 0.22f },
    };

    [Tooltip("Seconds to raise the sword into the starting pose before each swing.")]
    public float windupTime = 0.06f;
    [Tooltip("Seconds to bring the sword back to rest after a swing.")]
    public float recoverTime = 0.15f;
    [Tooltip("If you don't attack again within this many seconds, the combo restarts at hit 1.")]
    public float comboResetTime = 0.8f;
    [Tooltip("Clicking during a swing queues the next hit if it's within this many seconds of the swing ending.")]
    public float inputBufferTime = 0.25f;

    [Header("Hit 3 lunge")]
    public float lungeSpeed = 16f;
    public float lungeDuration = 0.15f;

    [Header("Cleave wave (Jackpot on hit 3)")]
    [Tooltip("Optional. If empty, a simple golden wave is created in code.")]
    public GameObject cleaveWavePrefab;
    public float cleaveDamage = 40f;
    public float cleaveSpeed = 20f;
    public float cleaveLifetime = 1f;

    [Header("Boons")]
    [Tooltip("Let It Ride: the combo carries between enemies instead of restarting when you hit a new one.")]
    public bool letItRide = false;

    /// <summary>Fires when a combo hit starts (1, 2 or 3). Handy for sounds or a HUD combo counter.</summary>
    public event Action<int> OnComboHit;

    public int ComboStep => comboIndex + 1;
    public bool IsSwinging => isSwinging;

    private Quaternion restRotation;
    private int comboIndex;            // 0, 1, 2
    private bool isSwinging;
    private bool attackBuffered;
    private float lastSwingEndTime = -999f;
    private float swingEndsAt;
    private Health comboTarget;        // the enemy the current combo is on
    private bool cleaveFiredThisSwing;
    private bool firstHitDoneThisSwing;
    private bool restartComboAfterSwing;
    private int currentSwingIndex;
    private Coroutine swingRoutine;

    protected override void Awake()
    {
        base.Awake();
        restRotation = transform.localRotation;
    }

    void Start()
    {
        if (bladeHitbox == null)
        {
            Debug.LogError("FortunesEdge: assign the blade's Damage component in the Inspector.");
            enabled = false;
            return;
        }
        bladeHitbox.rollLuck = true;   // every sword hit rolls the dice
        bladeHitbox.Deactivate();
    }

    protected override void OnEquip()
    {
        if (bladeHitbox != null) bladeHitbox.OnHitTarget += HandleBladeHit;
    }

    protected override void OnUnequip()
    {
        if (bladeHitbox != null)
        {
            bladeHitbox.OnHitTarget -= HandleBladeHit;
            bladeHitbox.Deactivate();
        }
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        isSwinging = false;
        attackBuffered = false;
        comboIndex = 0;
        transform.localRotation = restRotation;
    }

    protected override void HandleAttackInput()
    {
        if (!AttackPressedThisFrame()) return;

        if (isSwinging)
        {
            // Queue the next hit if we're near the end of this swing.
            if (Time.time >= swingEndsAt - inputBufferTime) attackBuffered = true;
            return;
        }

        StartNextSwing();
    }

    private void StartNextSwing()
    {
        // Too long since the last swing? Start the combo over.
        if (Time.time - lastSwingEndTime > comboResetTime) ResetCombo();

        swingRoutine = StartCoroutine(Swing(combo[comboIndex], comboIndex));
    }

    private IEnumerator Swing(ComboHit hit, int index)
    {
        isSwinging = true;
        attackBuffered = false;
        cleaveFiredThisSwing = false;
        firstHitDoneThisSwing = false;
        restartComboAfterSwing = false;
        currentSwingIndex = index;
        float speed = Mathf.Max(0.1f, attackSpeedMultiplier);

        OnComboHit?.Invoke(index + 1);

        Quaternion start = Quaternion.Euler(hit.startRotation);
        Quaternion end = Quaternion.Euler(hit.endRotation);

        // Wind up
        yield return RotateTo(transform.localRotation, start, windupTime / speed, false);

        // Lunge on the last hit
        bool isFinisher = index == combo.Length - 1;
        if (isFinisher && Movement != null)
        {
            Vector3 forward = Owner.transform.forward;
            Movement.Dash(forward, lungeSpeed, lungeDuration);
        }

        // Swing with the hitbox on
        bladeHitbox.Activate(hit.damage);
        float swingDuration = hit.swingTime / speed;
        swingEndsAt = Time.time + swingDuration;
        yield return RotateTo(start, end, swingDuration, true);
        bladeHitbox.Deactivate();

        // Advance the combo
        // Hitting a new enemy counts as hit 1 on them, so the next swing is hit 2.
        comboIndex = restartComboAfterSwing ? 1 % combo.Length : (index + 1) % combo.Length;
        lastSwingEndTime = Time.time;

        // Chain straight into the next hit if the player clicked during the swing
        if (attackBuffered)
        {
            swingRoutine = StartCoroutine(Swing(combo[comboIndex], comboIndex));
            yield break;
        }

        // Otherwise return to rest
        yield return RotateTo(end, restRotation, recoverTime / speed, false);
        isSwinging = false;
        swingRoutine = null;
    }

    private IEnumerator RotateTo(Quaternion from, Quaternion to, float duration, bool easeOut)
    {
        if (duration <= 0f) { transform.localRotation = to; yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            k = easeOut ? 1f - (1f - k) * (1f - k) : k * k * (3f - 2f * k); // fast start for swings, smooth for the rest
            transform.localRotation = Quaternion.Slerp(from, to, k);
            yield return null;
        }
        transform.localRotation = to;
    }

    private void HandleBladeHit(Health target, float damageDealt)
    {
        // Only the first enemy a swing touches decides the combo, so sweeping a crowd doesn't reset it.
        if (!firstHitDoneThisSwing)
        {
            firstHitDoneThisSwing = true;

            // Without Let It Ride, the combo only continues on the same enemy.
            if (!letItRide && comboTarget != null && target != comboTarget && currentSwingIndex > 0)
                restartComboAfterSwing = true;

            comboTarget = target;
        }

        // Jackpot on the finisher fires a cleave wave (once per swing)
        bool isFinisher = currentSwingIndex == combo.Length - 1;
        if (isFinisher && !cleaveFiredThisSwing && bladeHitbox.HasRolled && bladeHitbox.CurrentRoll.isJackpot)
        {
            cleaveFiredThisSwing = true;
            FireCleaveWave();
        }
    }

    private void ResetCombo()
    {
        comboIndex = 0;
        comboTarget = null;
    }

    private void FireCleaveWave()
    {
        Transform player = Owner.transform;
        Vector3 spawnPos = player.position + Vector3.up * 1f + player.forward * 1.5f;
        Quaternion rot = Quaternion.LookRotation(player.forward, Vector3.up);

        GameObject wave = cleaveWavePrefab != null
            ? Instantiate(cleaveWavePrefab, spawnPos, rot)
            : CleaveWave.CreateDefault(spawnPos, rot);

        CleaveWave cw = wave.GetComponent<CleaveWave>();
        if (cw == null) cw = wave.AddComponent<CleaveWave>();
        cw.Launch(Owner, cleaveDamage, cleaveSpeed, cleaveLifetime);
    }
}
