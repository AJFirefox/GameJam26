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
        if (player == null || health == null || health.IsDead) return;

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

    protected virtual void HandleDeath(GameObject killer)
    {
        StopAllCoroutines();
        if (hitBox != null) hitBox.enabled = false;
        if (aI != null && aI.isOnNavMesh) aI.isStopped = true;
    }
}
