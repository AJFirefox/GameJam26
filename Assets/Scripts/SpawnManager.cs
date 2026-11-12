using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>
/// Runs the waves: more enemies each wave, tougher enemies over time, a mix of enemy types,
/// a boss every Nth wave, and a pause between waves (where the Altar of Tyche will go).
///
/// Other scripts can listen to:
///   OnWaveStarted(wave, isBossWave), OnWaveCleared(wave), OnBossSpawned(bossHealth)
/// The Altar can hold the next wave with RequestHold() and let it start with ReleaseHold().
/// </summary>
public class SpawnManager : MonoBehaviour
{
    [Serializable]
    public class EnemyOption
    {
        public string label = "Enemy";
        public GameObject prefab;
        [Tooltip("First wave this enemy can show up in.")]
        public int firstWave = 1;
        [Tooltip("How common it is compared with the others. The basic enemy is 1.")]
        public float weight = 1f;
    }

    [Header("Setup")]
    [Tooltip("The basic enemy (Satyr). Always in the mix.")]
    public GameObject enemyPrefab;
    [Tooltip("Other enemy types (Cyclops, Raven...) and the wave they start appearing.")]
    public List<EnemyOption> extraEnemies = new List<EnemyOption>();

    [Tooltip("Empty GameObjects placed where enemies should appear.")]
    public Transform[] spawnPoints;
    [Tooltip("Spawn points closer than this to the player are skipped (if there are others).")]
    public float minSpawnDistanceFromPlayer = 8f;
    [Tooltip("How far around each spawn point to look for floor at the player's level.")]
    public float groundSearchRadius = 4f;
    [Tooltip("A spawn spot more than this far above/below the player's floor is ignored (e.g. the top of a wall).")]
    public float maxHeightDifference = 1.5f;
    [Tooltip("If a spawn point is cut off from the arena, step inward this far at a time looking for connected floor.")]
    public float inwardStep = 1f;
    [Tooltip("The furthest inward a spawn can be moved.")]
    public float maxInwardDistance = 15f;

    [Header("Wave Settings")]
    [Tooltip("How many enemies spawn in wave 1.")]
    public int startingEnemyCount = 3;
    [Tooltip("How many extra enemies are added each wave.")]
    public int enemiesAddedPerWave = 2;
    [Tooltip("Cap on enemies per wave, so late waves stay playable.")]
    public int maxEnemiesPerWave = 30;
    [Tooltip("Seconds between each individual enemy spawn.")]
    public float timeBetweenSpawns = 0.5f;
    [Tooltip("Seconds to wait after a wave is cleared before the next one starts.")]
    public float timeBetweenWaves = 3f;
    [Tooltip("Seconds before wave 1 starts.")]
    public float firstWaveDelay = 2f;

    [Header("Difficulty Scaling")]
    [Tooltip("Extra enemy health per wave. 0.08 = +8% per wave (wave 10 = +72%).")]
    public float healthIncreasePerWave = 0.08f;
    [Tooltip("Extra enemy damage per wave. 0.05 = +5% per wave.")]
    public float damageIncreasePerWave = 0.05f;

    [Header("Boss")]
    public GameObject bossPrefab;
    public string bossName = "The Minotaur";
    [Tooltip("A boss wave happens every this many waves (5 = waves 5, 10, 15...).")]
    public int bossEvery = 5;
    [Tooltip("Optional. Where the boss appears. Empty = the spawn point furthest from the player.")]
    public Transform bossSpawnPoint;
    [Tooltip("Regular enemies that spawn alongside the boss.")]
    public int bossWaveExtraEnemies = 0;
    [Tooltip("Extra boss health each time it comes back. 0.5 = +50% on its 2nd appearance, +100% on its 3rd.")]
    public float bossHealthIncreasePerAppearance = 0.5f;

    // ---------- Events ----------
    public event Action<int, bool> OnWaveStarted;     // (wave, isBossWave)
    public event Action<int> OnWaveCleared;           // (wave)
    public event Action<Health> OnBossSpawned;

    // ---------- State ----------
    public int CurrentWave { get; private set; } = 0;
    public int WavesCleared { get; private set; } = 0;
    public bool IsBossWave { get; private set; }
    public bool IsBetweenWaves { get; private set; } = true;
    public int EnemiesAlive
    {
        get
        {
            aliveEnemies.RemoveAll(h => h == null || h.IsDead);
            return aliveEnemies.Count;
        }
    }

    private readonly List<Health> aliveEnemies = new List<Health>();
    private Health playerHealth;
    private Transform player;
    private int holds;
    private int bossAppearances;

    void Start()
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("SpawnManager: assign an enemy prefab and at least one spawn point.");
            enabled = false;
            return;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<Health>();
        }

        if (bossPrefab == null) Debug.LogWarning("SpawnManager: no boss prefab assigned, boss waves will be normal waves.");

        StartCoroutine(RunWaves());
    }

    // ======================================================================
    // Holding between waves (for the Altar of Tyche)
    // ======================================================================

    /// <summary>Stops the next wave from starting until ReleaseHold() is called. Call during OnWaveCleared.</summary>
    public void RequestHold() => holds++;

    /// <summary>Lets the next wave start (after the normal timeBetweenWaves).</summary>
    public void ReleaseHold() => holds = Mathf.Max(0, holds - 1);

    // ======================================================================
    // Wave loop
    // ======================================================================

    IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(firstWaveDelay);

        while (!PlayerDead())
        {
            CurrentWave++;
            IsBossWave = bossPrefab != null && bossEvery > 0 && CurrentWave % bossEvery == 0;
            IsBetweenWaves = false;

            float healthMult = 1f + healthIncreasePerWave * (CurrentWave - 1);
            float damageMult = 1f + damageIncreasePerWave * (CurrentWave - 1);

            int regularCount = IsBossWave
                ? bossWaveExtraEnemies
                : Mathf.Min(maxEnemiesPerWave, startingEnemyCount + (CurrentWave - 1) * enemiesAddedPerWave);

            Debug.Log($"Wave {CurrentWave} starting: {(IsBossWave ? "BOSS + " : "")}{regularCount} enemies " +
                      $"(health x{healthMult:0.##}, damage x{damageMult:0.##})");
            OnWaveStarted?.Invoke(CurrentWave, IsBossWave);

            if (IsBossWave) SpawnBoss(damageMult);

            for (int i = 0; i < regularCount; i++)
            {
                if (PlayerDead()) yield break;
                SpawnEnemy(PickEnemy(), healthMult, damageMult);
                yield return new WaitForSeconds(timeBetweenSpawns);
            }

            // Wait until everything from this wave is dead
            yield return new WaitUntil(() => EnemiesAlive == 0 || PlayerDead());
            if (PlayerDead()) yield break;

            WavesCleared++;
            IsBetweenWaves = true;
            Debug.Log($"Wave {CurrentWave} cleared!");
            OnWaveCleared?.Invoke(CurrentWave);

            // Wait for the Altar (if it asked us to), then the normal breather
            yield return new WaitUntil(() => holds == 0);
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    // ======================================================================
    // Spawning
    // ======================================================================

    private GameObject PickEnemy()
    {
        float total = 1f;   // the basic enemy always has weight 1
        foreach (EnemyOption o in extraEnemies)
            if (o.prefab != null && CurrentWave >= o.firstWave) total += o.weight;

        float roll = Random.value * total;
        if (roll < 1f) return enemyPrefab;
        roll -= 1f;

        foreach (EnemyOption o in extraEnemies)
        {
            if (o.prefab == null || CurrentWave < o.firstWave) continue;
            if (roll < o.weight) return o.prefab;
            roll -= o.weight;
        }
        return enemyPrefab;
    }

    private GameObject SpawnEnemy(GameObject prefab, float healthMult, float damageMult, Transform at = null)
    {
        Transform point = at != null ? at : PickSpawnPoint(false);
        Vector3 pos = point.position;

        pos = GroundLevelSpawn(pos, point.name);

        Quaternion facing = player != null
            ? Quaternion.LookRotation(Vector3.ProjectOnPlane(player.position - pos, Vector3.up).normalized + Vector3.forward * 0.0001f)
            : point.rotation;

        GameObject enemy = Instantiate(prefab, pos, facing);

        CommonEnemy ce = enemy.GetComponent<CommonEnemy>();
        if (ce != null) ce.ApplyWaveScaling(healthMult, damageMult);

        Health h = enemy.GetComponent<Health>();
        if (h != null) aliveEnemies.Add(h);
        else Debug.LogWarning($"SpawnManager: {prefab.name} has no Health, so the wave can't tell when it dies.");

        return enemy;
    }

    private void SpawnBoss(float damageMult)
    {
        bossAppearances++;
        float bossHealthMult = 1f + bossHealthIncreasePerAppearance * (bossAppearances - 1);
        Transform point = bossSpawnPoint != null ? bossSpawnPoint : PickSpawnPoint(true);

        GameObject boss = SpawnEnemy(bossPrefab, bossHealthMult, damageMult, point);
        Health bossHealth = boss.GetComponent<Health>();

        // Name from the boss script if it has one
        string displayName = bossName;
        Minotaur m = boss.GetComponent<Minotaur>();
        if (m != null && !string.IsNullOrEmpty(m.bossName)) displayName = m.bossName;

        if (GameHUD.Instance != null && bossHealth != null) GameHUD.Instance.ShowBoss(bossHealth, displayName);
        OnBossSpawned?.Invoke(bossHealth);
    }

    /// <summary>Random spawn point that isn't on top of the player. furthest = pick the furthest one instead.</summary>
    private Transform PickSpawnPoint(bool furthest)
    {
        if (player == null) return spawnPoints[Random.Range(0, spawnPoints.Length)];

        if (furthest)
        {
            Transform best = spawnPoints[0];
            float bestDist = -1f;
            foreach (Transform t in spawnPoints)
            {
                float d = (t.position - player.position).sqrMagnitude;
                if (d > bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        List<Transform> farEnough = new List<Transform>();
        foreach (Transform t in spawnPoints)
            if (Vector3.Distance(t.position, player.position) >= minSpawnDistanceFromPlayer) farEnough.Add(t);

        if (farEnough.Count == 0) return spawnPoints[Random.Range(0, spawnPoints.Length)];
        return farEnough[Random.Range(0, farEnough.Count)];
    }

    /// <summary>
    /// Finds the floor under a spawn point at the same height as the player's floor, so enemies
    /// appear in the doorway and not on top of the wall above it.
    /// </summary>
    private Vector3 GroundLevelSpawn(Vector3 spawnPos, string pointName)
    {
        float floorY = PlayerFloorY(spawnPos.y);
        Vector3 center = ArenaCenter();
        Vector3 start = new Vector3(spawnPos.x, floorY, spawnPos.z);
        Vector3 inwardTarget = new Vector3(center.x, floorY, center.z);

        // Try the spawn point, then step inward toward the middle of the arena until the floor
        // there is at the player's level AND connected to where the player is.
        for (float inward = 0f; inward <= maxInwardDistance; inward += inwardStep)
        {
            Vector3 probe = Vector3.MoveTowards(start, inwardTarget, inward);
            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, groundSearchRadius, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - floorY) <= maxHeightDifference
                && CanReachPlayer(hit.position))
            {
                if (inward > 0f && !warnedPoints.Contains(pointName))
                {
                    warnedPoints.Add(pointName);
                    Debug.Log($"SpawnManager: {pointName} was cut off from the arena, so enemies spawn {inward:0.#} m further in. " +
                              "Move it inward in the scene if you want them to appear right in the doorway.");
                }
                return hit.position;
            }
        }

        // Last resort: anywhere near, still preferring the player's level
        if (NavMesh.SamplePosition(start, out NavMeshHit any, 25f, NavMesh.AllAreas)) return any.position;

        Debug.LogWarning($"SpawnManager: no NavMesh near {pointName}. Bake the NavMesh, or move the spawn point into the doorway.");
        return spawnPos;
    }

    /// <summary>Height of the floor the player is standing on (ignores the player's own colliders).</summary>
    private float PlayerFloorY(float fallback)
    {
        if (player == null) return fallback;
        RaycastHit[] hits = Physics.RaycastAll(player.position + Vector3.up * 0.5f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = lastPlayerFloorY ?? player.position.y;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.root == player.root) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; }
        }
        lastPlayerFloorY = y;
        return y;
    }

    private float? lastPlayerFloorY;
    private NavMeshPath reachPath;
    private readonly HashSet<string> warnedPoints = new HashSet<string>();

    /// <summary>True if an enemy standing here could walk to the player.</summary>
    private bool CanReachPlayer(Vector3 from)
    {
        if (player == null) return true;
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas)) return true;  // can't tell, allow
        if (reachPath == null) reachPath = new NavMeshPath();
        return NavMesh.CalculatePath(from, playerHit.position, NavMesh.AllAreas, reachPath)
               && reachPath.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>Middle of the arena: the average of the spawn points.</summary>
    private Vector3 ArenaCenter()
    {
        Vector3 sum = Vector3.zero;
        foreach (Transform t in spawnPoints) sum += t.position;
        return sum / spawnPoints.Length;
    }

    private bool PlayerDead() => playerHealth != null && playerHealth.IsDead;
}
