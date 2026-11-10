using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SpawnManager : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("The enemy prefab to spawn.")]
    public GameObject enemyPrefab;

    [Tooltip("Empty GameObjects placed where enemies should appear.")]
    public Transform[] spawnPoints;

    [Header("Wave Settings")]
    [Tooltip("How many enemies spawn in wave 1.")]
    public int startingEnemyCount = 3;

    [Tooltip("How many extra enemies are added each wave.")]
    public int enemiesAddedPerWave = 2;

    [Tooltip("Seconds between each individual enemy spawn.")]
    public float timeBetweenSpawns = 0.5f;

    [Tooltip("Seconds to wait after a wave is cleared before the next one starts.")]
    public float timeBetweenWaves = 3f;

    public int CurrentWave { get; private set; } = 0;

    private readonly List<GameObject> aliveEnemies = new List<GameObject>();

    void Start()
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("WaveSpawner: assign an enemy prefab and at least one spawn point.");
            enabled = false;
            return;
        }

        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        while (true)
        {
            CurrentWave++;
            int enemiesThisWave = startingEnemyCount + (CurrentWave - 1) * enemiesAddedPerWave;
            Debug.Log($"Wave {CurrentWave} starting: {enemiesThisWave} enemies");

            // Spawn this wave's enemies one at a time
            for (int i = 0; i < enemiesThisWave; i++)
            {
                SpawnEnemy();
                yield return new WaitForSeconds(timeBetweenSpawns);
            }

            // Wait until every enemy from this wave has been destroyed
            yield return new WaitUntil(() =>
            {
                aliveEnemies.RemoveAll(e => e == null);
                return aliveEnemies.Count == 0;
            });

            Debug.Log($"Wave {CurrentWave} cleared!");
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    void SpawnEnemy()
    {
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject enemy = Instantiate(enemyPrefab, point.position, point.rotation);
        aliveEnemies.Add(enemy);
    }
}