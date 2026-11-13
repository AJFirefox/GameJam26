using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Game over: when the player dies, saves the score (waves cleared + Jackpots landed) and loads the
/// game over scene (GameOverBrah). The run's stats are shown as a small line on that screen.
/// Untick "Use Game Over Scene" to use the built-in overlay with Restart / Main Menu instead.
/// Put this on an empty GameObject in the game scene.
/// </summary>
public class GameOverManager : MonoBehaviour
{
    [Header("Game over scene")]
    [Tooltip("Load a separate game over scene instead of showing the overlay.")]
    public bool useGameOverScene = true;
    [Tooltip("Must be in File > Build Profiles > Scene List.")]
    public string gameOverScene = "GameOverBrah";
    [Tooltip("Show waves, Jackpots and score as a small line on the game over scene.")]
    public bool showStatsOnGameOverScene = true;

    [Header("Overlay (when not using the scene)")]
    [Tooltip("Scene loaded by the Main Menu button.")]
    public string mainMenuScene = "DrakesAWESOMEScene";
    [Tooltip("Seconds after death before the screen appears.")]
    public float delay = 1.2f;

    [Header("Score")]
    public int pointsPerWave = 100;
    public int pointsPerJackpot = 10;

    private Health playerHealth;
    private SpawnManager spawner;
    private RectTransform canvas;
    private Text statsText;
    private bool shown;

    void Awake()
    {
        UIBuilder.EnsureEventSystem();
        BuildScreen();
    }

    void Start()
    {
        Time.timeScale = 1f;   // in case we came back from a paused scene
        spawner = FindAnyObjectByType<SpawnManager>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerHealth = p.GetComponent<Health>();
        if (playerHealth != null) playerHealth.OnDied += HandlePlayerDied;
        else Debug.LogWarning("GameOverManager: no Health on the Player, so game over can't trigger.");
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
    }

    private void HandlePlayerDied(GameObject killer)
    {
        if (!shown) StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        shown = true;
        yield return new WaitForSecondsRealtime(delay);

        int waves = spawner != null ? spawner.WavesCleared : 0;
        int jackpots = LuckSystem.Instance != null ? LuckSystem.Instance.TotalJackpots : 0;
        int score = waves * pointsPerWave + jackpots * pointsPerJackpot;
        int bestKey = PlayerPrefs.GetInt("BestScore", 0);
        bool newBest = score > bestKey;
        if (newBest) { PlayerPrefs.SetInt("BestScore", score); PlayerPrefs.Save(); }

        // Save the run so the game over scene (or the menu) can show it
        PlayerPrefs.SetInt("LastWaves", waves);
        PlayerPrefs.SetInt("LastJackpots", jackpots);
        PlayerPrefs.SetInt("LastScore", score);
        PlayerPrefs.Save();

        if (useGameOverScene && Application.CanStreamedLevelBeLoaded(gameOverScene))
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (showStatsOnGameOverScene)
                RunStatsLine.ShowAfterLoad(gameOverScene, waves, jackpots, score, newBest ? -1 : bestKey);
            SceneManager.LoadScene(gameOverScene);
            yield break;
        }
        if (useGameOverScene)
            Debug.LogWarning($"GameOverManager: scene '{gameOverScene}' isn't in the build's scene list, so showing the overlay instead.");

        statsText.text =
            $"<size=26>Waves cleared</size>\n<size=44><b>{waves}</b></size>\n\n" +
            $"<size=26>Jackpots landed</size>\n<size=44><b>{jackpots}</b></size>\n\n" +
            $"<size=30><color=#FFD040>SCORE  {score}</color></size>\n" +
            (newBest ? "<size=22><color=#FFD040>New best!</color></size>" : $"<size=20>Best: {bestKey}</size>");

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        canvas.gameObject.SetActive(true);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    private void BuildScreen()
    {
        canvas = UIBuilder.MakeCanvas("Game Over", transform, 60);

        Image dim = UIBuilder.Panel("Dim", canvas, Vector2.zero, Vector2.zero, new Color(0.05f, 0f, 0f, 0.8f));
        UIBuilder.Stretch(dim.rectTransform);

        UIBuilder.Label("Title", canvas, "THE FATES HAVE SPOKEN", 64, new Color(0.9f, 0.25f, 0.2f), TextAnchor.MiddleCenter, new Vector2(0, 330), new Vector2(1400, 90));
        statsText = UIBuilder.Label("Stats", canvas, "", 26, UIBuilder.Marble, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(800, 460));

        UIBuilder.Button("Restart", canvas, "RESTART", 30, new Vector2(-180, -320), new Vector2(320, 76), Restart);
        UIBuilder.Button("Main Menu", canvas, "MAIN MENU", 30, new Vector2(180, -320), new Vector2(320, 76), MainMenu);

        canvas.gameObject.SetActive(false);
    }
}
