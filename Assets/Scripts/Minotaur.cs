using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The Minotaur: boss with heavy axe swings and a charging dash.
///
/// Up close:   a 2-swing axe combo. Each swing has a clear wind-up (leans back) before it lands.
/// At range:   stops, paws the ground (crouches and shakes), locks on, then charges in a straight line.
///             After a charge it's winded for a moment. That's the player's window to punish it.
/// Below 50%:  enrages. Faster wind-ups, faster movement, shorter cooldowns.
///
/// Uses CommonEnemy for health, death, chasing and the player reference.
/// The axe uses CommonEnemy's Hit Box; the charge has its own Dash Hit Box.
/// When the real model arrives, hook animations up through the Animator triggers or the events below.
/// </summary>
public class Minotaur : CommonEnemy
{
    [Header("Boss")]
    public string bossName = "The Minotaur";

    [Header("Axe swing (uses Hit Box above)")]
    public float swingRange = 3.5f;
    [Tooltip("Swings per combo.")]
    public int swingsPerCombo = 2;
    [Tooltip("Lean-back before each swing. This is the player's warning.")]
    public float swingWindup = 0.55f;
    public float swingActiveTime = 0.25f;
    [Tooltip("Pause between swings in a combo.")]
    public float timeBetweenSwings = 0.3f;
    [Tooltip("Pause after the combo before it moves again.")]
    public float swingRecovery = 0.7f;
    public float swingCooldown = 1.2f;

    [Header("Dash attack")]
    [Tooltip("Trigger collider around the body with a Damage script (Target Tag = Player).")]
    public Collider dashHitBox;
    public float dashMinRange = 6f;
    public float dashMaxRange = 20f;
    [Tooltip("Paws the ground for this long before charging. It tracks the player for most of it, then locks direction.")]
    public float dashWindup = 0.9f;
    [Tooltip("Fraction of the wind-up spent tracking the player before locking on (so the player can dodge).")]
    [Range(0f, 1f)] public float dashTrackFraction = 0.7f;
    public float dashSpeed = 20f;
    public float dashDuration = 0.7f;
    [Tooltip("Winded after a charge. Free hits for the player.")]
    public float dashRecovery = 1.3f;
    public float dashCooldown = 6f;
    [Tooltip("How hard a charge knocks the player back.")]
    public float dashKnockbackSpeed = 14f;

    [Header("Enrage")]
    [Range(0f, 1f)] public float enrageAtHealth = 0.5f;
    [Tooltip("Speeds up wind-ups, movement and cooldowns once enraged.")]
    public float enrageSpeedMultiplier = 1.3f;

    [Header("Placeholder animation (until the real model)")]
    public float swingLeanAngle = 18f;
    public float dashCrouchAngle = 12f;

    [Header("Hooks for sounds / VFX / animations")]
    public UnityEvent onSwingWindup;
    public UnityEvent onSwing;
    public UnityEvent onDashWindup;
    public UnityEvent onDash;
    public UnityEvent onDashEnd;
    public UnityEvent onEnrage;

    public bool IsEnraged { get; private set; }

    private bool busy;
    private float nextSwingTime;
    private float nextDashTime;
    private float baseAgentSpeed;
    private Damage axeDamage;
    private Damage dashDamage;
    private Animator animator;
    private PlayerMovement playerMovement;
    private float lean;               // current placeholder lean angle (X tilt)

    private float Speed => IsEnraged ? enrageSpeedMultiplier : 1f;

    // ======================================================================
    // Setup
    // ======================================================================

    protected override void Start()
    {
        base.Start();

        baseAgentSpeed = aI.speed;
        animator = GetComponentInChildren<Animator>();
        if (player != null) playerMovement = player.GetComponent<PlayerMovement>();

        if (hitBox != null) axeDamage = hitBox.GetComponent<Damage>();
        if (dashHitBox != null)
        {
            dashDamage = dashHitBox.GetComponent<Damage>();
            dashHitBox.enabled = false;
            if (dashDamage != null) dashDamage.OnHitTarget += HandleDashHit;
        }
        else Debug.LogWarning($"{name}: Minotaur has no Dash Hit Box assigned. The charge won't hurt.");

        if (health != null) health.OnHealthChanged += CheckEnrage;

        // Don't open with a charge the instant it spawns
        nextDashTime = Time.time + 2f;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (dashDamage != null) dashDamage.OnHitTarget -= HandleDashHit;
        if (health != null) health.OnHealthChanged -= CheckEnrage;
    }

    // ======================================================================
    // Brain
    // ======================================================================

    protected override void Update()
    {
        if (player == null || health == null || health.IsDead || IsPetrified) return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            if (aI.isOnNavMesh) aI.isStopped = true;
            return;
        }

        if (busy) return;

        float distance = FlatDistanceToPlayer();

        if (Time.time >= nextDashTime && distance >= dashMinRange && distance <= dashMaxRange)
        {
            StartCoroutine(DashAttack());
        }
        else if (Time.time >= nextSwingTime && distance <= swingRange)
        {
            StartCoroutine(SwingCombo());
        }
        else
        {
            aI.updateRotation = true;
            if (aI.isOnNavMesh) aI.isStopped = false;
            FollowPlayer();
        }
    }

    // ======================================================================
    // Axe swings
    // ======================================================================

    private IEnumerator SwingCombo()
    {
        busy = true;
        StopMoving();

        for (int i = 0; i < swingsPerCombo; i++)
        {
            if (health.IsDead) yield break;

            // Wind up: lean back while turning to face the player
            onSwingWindup?.Invoke();
            Trigger("SwingWindup");
            float windup = swingWindup / Speed;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                TurnTowardsPlayer(10f);
                SetLean(Mathf.Lerp(0f, -swingLeanAngle, t / windup));
                yield return null;
            }

            // Swing: snap forward, hitbox on
            onSwing?.Invoke();
            Trigger("Swing");
            SetHitbox(axeDamage, hitBox, true);
            float active = swingActiveTime;
            for (float t = 0f; t < active; t += Time.deltaTime)
            {
                SetLean(Mathf.Lerp(-swingLeanAngle, swingLeanAngle * 0.6f, t / active));
                yield return null;
            }
            SetHitbox(axeDamage, hitBox, false);

            // Settle before the next swing
            float gap = timeBetweenSwings / Speed;
            for (float t = 0f; t < gap; t += Time.deltaTime)
            {
                SetLean(Mathf.Lerp(swingLeanAngle * 0.6f, 0f, t / gap));
                yield return null;
            }
        }

        SetLean(0f);
        yield return new WaitForSeconds(swingRecovery / Speed);

        nextSwingTime = Time.time + swingCooldown / Speed;
        busy = false;
    }

    // ======================================================================
    // Dash attack
    // ======================================================================

    private IEnumerator DashAttack()
    {
        busy = true;
        StopMoving();
        onDashWindup?.Invoke();
        Trigger("DashWindup");

        // Wind up: crouch and shake, tracking the player, then lock direction
        float windup = dashWindup / Speed;
        float trackUntil = windup * dashTrackFraction;
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            if (t < trackUntil) TurnTowardsPlayer(12f);
            // Crouch, with a pawing-the-ground shake that gets stronger near the end
            float shake = 4f * (t / windup);
            SetLean(Mathf.Lerp(0f, -dashCrouchAngle, t / windup) + Random.Range(-shake, shake));
            yield return null;
        }
        SetLean(dashCrouchAngle * 0.5f);   // lunge forward

        // Charge in a straight line. NavMeshAgent.Move stops it at walls and edges.
        onDash?.Invoke();
        Trigger("Dash");
        Vector3 dir = transform.forward;
        dir.y = 0f;
        dir.Normalize();
        SetHitbox(dashDamage, dashHitBox, true);
        chargeHitPlayer = false;

        float speed = dashSpeed * Speed;
        for (float t = 0f; t < dashDuration; t += Time.deltaTime)
        {
            Vector3 before = transform.position;
            if (aI.isOnNavMesh) aI.Move(dir * speed * Time.deltaTime);
            else transform.position += dir * speed * Time.deltaTime;

            // Stop early if it hit the player or slammed into a wall
            bool blocked = (transform.position - before).sqrMagnitude < (speed * Time.deltaTime * 0.2f) * (speed * Time.deltaTime * 0.2f);
            if (chargeHitPlayer || (blocked && t > 0.05f)) break;
            yield return null;
        }

        SetHitbox(dashDamage, dashHitBox, false);
        onDashEnd?.Invoke();

        // Winded: free hits for the player
        for (float t = 0f; t < dashRecovery; t += Time.deltaTime)
        {
            SetLean(Mathf.Lerp(dashCrouchAngle * 0.5f, 0f, t / 0.3f));
            yield return null;
        }
        SetLean(0f);

        nextDashTime = Time.time + dashCooldown / Speed;
        nextSwingTime = Time.time + 0.5f;
        busy = false;
    }

    private bool chargeHitPlayer;

    private void HandleDashHit(Health target, float damage)
    {
        chargeHitPlayer = true;

        // Knock the player back along the charge
        if (playerMovement != null && target != null && target.transform.root == player.transform.root)
        {
            Vector3 away = player.transform.position - transform.position;
            away.y = 0f;
            playerMovement.Dash(away.sqrMagnitude > 0.01f ? away : transform.forward, dashKnockbackSpeed, 0.2f);
        }
    }

    // ======================================================================
    // Enrage
    // ======================================================================

    private void CheckEnrage(float current, float max)
    {
        if (IsEnraged || max <= 0f || current <= 0f) return;
        if (current / max > enrageAtHealth) return;

        IsEnraged = true;
        aI.speed = baseAgentSpeed * enrageSpeedMultiplier;
        onEnrage?.Invoke();
        Trigger("Enrage");
        Debug.Log($"{bossName} is enraged!");
    }

    // ======================================================================
    // Stone (Snake Eyes)
    // ======================================================================

    public override void SetPetrified(bool on)
    {
        base.SetPetrified(on);   // stops coroutines, hitboxes off, agent stopped
        if (dashHitBox != null) dashHitBox.enabled = false;
        SetLean(0f);
        busy = false;
        if (!on && aI != null) aI.updateRotation = true;
    }

    // ======================================================================
    // Death
    // ======================================================================

    protected override void HandleDeath(GameObject killer)
    {
        base.HandleDeath(killer);   // stops coroutines, axe hitbox off, agent stopped
        if (dashHitBox != null) dashHitBox.enabled = false;
        if (dashDamage != null) dashDamage.Deactivate();
        SetLean(0f);
        busy = true;
    }

    // ======================================================================
    // Helpers
    // ======================================================================

    private void StopMoving()
    {
        aI.updateRotation = false;   // we turn it ourselves during attacks
        if (aI.isOnNavMesh)
        {
            aI.isStopped = true;
            aI.velocity = Vector3.zero;
        }
    }

    private float FlatDistanceToPlayer()
    {
        Vector3 d = player.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private void TurnTowardsPlayer(float turnSpeed)
    {
        Vector3 d = player.transform.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        float targetYaw = Quaternion.LookRotation(d).eulerAngles.y;
        float yaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, turnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(lean, yaw, 0f);
    }

    /// <summary>Placeholder animation: tilts the whole body forward/back. Positive = forward.</summary>
    private void SetLean(float angle)
    {
        lean = angle;
        transform.rotation = Quaternion.Euler(lean, transform.eulerAngles.y, 0f);
    }

    private static void SetHitbox(Damage dmg, Collider col, bool on)
    {
        if (dmg != null)
        {
            if (on) dmg.Activate();
            else dmg.Deactivate();
        }
        else if (col != null) col.enabled = on;
    }

    /// <summary>Fires an Animator trigger if the model has one with that name. Safe to call without an Animator.</summary>
    private void Trigger(string param)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == param && p.type == AnimatorControllerParameterType.Trigger) { animator.SetTrigger(param); return; }
    }
}
