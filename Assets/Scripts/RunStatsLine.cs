using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows the finished run's stats as one small line at the bottom of the game over scene
/// ("Waves 7 · Jackpots 12 · Score 820 · Best 900"). Created by GameOverManager just before it
/// loads that scene, so the scene itself doesn't need changing. Removes itself when you leave.
/// </summary>
public class RunStatsLine : MonoBehaviour
{
    private string targetScene;
    private string line;
    private bool shown;

    /// <summary>best = -1 means this run is a new best.</summary>
    public static void ShowAfterLoad(string sceneName, int waves, int jackpots, int score, int best)
    {
        GameObject go = new GameObject("Run Stats");
        DontDestroyOnLoad(go);
        RunStatsLine r = go.AddComponent<RunStatsLine>();
        r.targetScene = sceneName;
        string bestText = best < 0 ? "<color=#FFD040>New best!</color>" : $"Best {best}";
        r.line = $"Waves {waves}   ·   Jackpots {jackpots}   ·   <color=#FFD040>Score {score}</color>   ·   {bestText}";
    }

    void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != targetScene)
        {
            // Left the game over screen: clean up
            if (shown) Destroy(gameObject);
            return;
        }
        if (shown) return;
        shown = true;

        RectTransform canvas = UIBuilder.MakeCanvas("Run Stats Canvas", transform, 100);
        Text t = UIBuilder.Label("Stats", canvas, line, 28, UIBuilder.Marble, TextAnchor.MiddleCenter, new Vector2(0f, 45f), new Vector2(1600f, 50f));
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;   // never blocks the Reawaken button
    }
}
