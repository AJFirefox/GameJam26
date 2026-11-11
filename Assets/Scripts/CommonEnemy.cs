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


    void Start()
    {
        aI = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<Health>();

        if (hitBox != null) hitBox.enabled = false;
        health.OnDied += HandleDeath;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDeath;
    }

    void Update()
    {
        if (player == null || health.IsDead) return;

        // Stop chasing once the player is dead.
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (aI.isOnNavMesh) aI.isStopped = true;
            return;
        }

        FollowPlayer();
        Attacking();
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

    }

    protected IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, attackTime - attackActiveTime));
        canAttack = true;
    }

    void HandleDeath(GameObject killer)
    {
        StopAllCoroutines();
        if (hitBox != null) hitBox.enabled = false;
        if (aI != null && aI.isOnNavMesh) aI.isStopped = true;
    }
}
