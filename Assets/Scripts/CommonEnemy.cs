using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class CommonEnemy : MonoBehaviour
{
    protected NavMeshAgent aI;
    protected GameObject player;
    protected Health health;
    protected Health playerHealth;
    protected bool gameOver;

    [Tooltip("Trigger collider with a Damage script on it (Target Tag = Player).")]
    public SphereCollider hitBox;

    [Tooltip("How close the player has to be before this enemy swings.")]
    public float attackRange = 2f;
    [Tooltip("Seconds between attacks.")]
    public float attackTime = 1.5f;
    [Tooltip("How long the hitbox stays on during a swing.")]
    public float attackActiveTime = 0.2f;

    protected bool canAttack = true;

    /// <summary>Turned to stone (Snake Eyes). Can't move or attack.</summary>
    public bool IsPetrified { get; private set; }
    private Coroutine knockbackRoutine;


    protected virtual void Start()
    {
        aI = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<Health>();

        if (hitBox != null) hitBox.enabled = false;
        if (health != null) health.OnDied += HandleDeath;
        else Debug.LogWarning($"{name}: enemy has no Health component, so it can't be killed.");
    }

    protected virtual void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDeath;
    }

    protected virtual void Update()
    {
        if (player == null || health == null || health.IsDead || IsPetrified) return;

        // Stop chasing once the player is dead.
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (aI.isOnNavMesh) aI.isStopped = true;
            return;
        }

        FollowPlayer();
        Attacking();
    }

    /// <summary>Turns to face the player without tilting up or down.</summary>
    protected void FacePlayer()
    {
        Vector3 target = player.transform.position;
        target.y = transform.position.y;
        transform.LookAt(target);
    }

    protected void FollowPlayer()
    {
        if (aI.isOnNavMesh) aI.SetDestination(player.transform.position);
    }

    protected virtual void Attacking()
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance <= attackRange && canAttack)
        {
            canAttack = false;
            StartCoroutine(Swing());
        }
    }


    IEnumerator Swing()

    {
        // Hitbox on for a short window, then off. The Damage script on the hitbox deals the damage.
        if (hitBox != null) hitBox.enabled = true;
        yield return new WaitForSeconds(attackActiveTime);
        if (hitBox != null) hitBox.enabled = false;
        StartCoroutine(ResetAttack());
    }

    protected IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, attackTime - attackActiveTime));
        canAttack = true;
    }

    /// <summary>Turns the enemy to stone (or back). Stops all attacks and movement while on.</summary>
    public virtual void SetPetrified(bool on)
    {
        if (IsPetrified == on) return;
        IsPetrified = on;
        if (aI == null) aI = GetComponent<NavMeshAgent>();

        if (on)
        {
            StopAllCoroutines();
            knockbackRoutine = null;
            if (hitBox != null) hitBox.enabled = false;
            foreach (Damage d in GetComponentsInChildren<Damage>(true)) d.Deactivate();
            if (aI != null && aI.isOnNavMesh) { aI.isStopped = true; aI.velocity = Vector3.zero; }
        }
        else
        {
            canAttack = true;
            if (aI != null && aI.isOnNavMesh && (health == null || !health.IsDead)) aI.isStopped = false;
        }
    }

    /// <summary>Shoves the enemy along the ground (stays on the NavMesh, stops at walls).</summary>
    public void Knockback(Vector3 velocity, float duration)
    {
        if (!isActiveAndEnabled || IsPetrified) return;
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(KnockbackRoutine(velocity, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 velocity, float duration)
    {
        if (aI == null) aI = GetComponent<NavMeshAgent>();
        velocity.y = 0f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = 1f - t / duration;
            if (aI != null && aI.isOnNavMesh) aI.Move(velocity * k * Time.deltaTime);
            yield return null;
        }
        knockbackRoutine = null;
    }

    /// <summary>
    /// Called by the SpawnManager right after spawning, to make later waves tougher.
    /// 1 = normal. Scales max health and every Damage hitbox on this enemy.
    /// </summary>
    public virtual void ApplyWaveScaling(float healthMultiplier, float damageMultiplier)
    {
        Health h = GetComponent<Health>();
        if (h != null) h.ResetHealth(h.maxHealth * healthMultiplier);

        foreach (Damage d in GetComponentsInChildren<Damage>(true))
            d.damageAmount *= damageMultiplier;
    }

    protected virtual void HandleDeath(GameObject killer)
    {
        StopAllCoroutines();
        if (hitBox != null) hitBox.enabled = false;
        if (aI != null && aI.isOnNavMesh) aI.isStopped = true;
    }
}
