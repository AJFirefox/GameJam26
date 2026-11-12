using UnityEngine;

/// <summary>
/// Cyclops: ranged enemy that keeps its distance and throws boulder dice at the player.
/// Uses CommonEnemy for health, death, chasing and the player reference.
/// </summary>
public class Cyclops : CommonEnemy
{
    public GameObject boulder;
    public float throwForce = 15f;
    public Transform bSpawnPos;

    [Tooltip("Throws when the player is within this distance.")]
    public float throwRange = 15f;
    [Tooltip("Stops walking closer once the player is this near, so it stays at range.")]
    public float preferredDistance = 10f;
    [Tooltip("Damage each boulder does to the player.")]
    public float boulderDamage = 15f;

    protected override void Start()
    {
        base.Start();   // sets up NavMeshAgent, Health, player and death handling
        if (boulder == null) Debug.LogWarning($"{name}: Cyclops has no boulder prefab assigned.");
    }

    public override void ApplyWaveScaling(float healthMultiplier, float damageMultiplier)
    {
        base.ApplyWaveScaling(healthMultiplier, damageMultiplier);
        boulderDamage *= damageMultiplier;
    }

    protected override void Update()
    {
        if (player == null || health == null || health.IsDead) return;
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (aI.isOnNavMesh) aI.isStopped = true;
            return;
        }

        float distance = Vector3.Distance(transform.position, player.transform.position);

        // Walk in until it's at a good throwing distance, then hold position
        if (aI.isOnNavMesh) aI.isStopped = distance <= preferredDistance;
        if (distance > preferredDistance) FollowPlayer();

        Attacking();
    }

    protected override void Attacking()
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance > throwRange) return;

        FacePlayer();

        if (canAttack && boulder != null)
        {
            Transform spawn = bSpawnPos != null ? bSpawnPos : transform;
            Vector3 spawnPos = spawn == transform ? transform.position + Vector3.up * 2f + transform.forward : spawn.position;

            // Aim at the player's chest, with a little arc
            Vector3 aim = (player.transform.position + Vector3.up - spawnPos).normalized + Vector3.up * 0.15f;

            GameObject projectile = Instantiate(boulder, spawnPos, Quaternion.identity);
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(aim.normalized * throwForce, ForceMode.Impulse);

            Boulder b = projectile.GetComponent<Boulder>();
            if (b != null) b.Init(gameObject, boulderDamage);

            canAttack = false;
            StartCoroutine(ResetAttack());
        }
    }
}
