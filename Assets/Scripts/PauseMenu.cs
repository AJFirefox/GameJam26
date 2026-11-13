using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause menu: Esc (or gamepad Start) pauses during a fight, with Resume, Restart, Main Menu and Quit.
/// Also pauses automatically if the mouse gets freed (Esc in a browser) or the window loses focus,
/// so WebGL players can always get back in by clicking Resume.
/// Added automatically by GameHUD; nothing to set up.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Tooltip("Scene loaded by the Main Menu button.")]
    public string mainMenuScene = "DrakesAWESOMEScene";
    [Tooltip("Pause when the mouse stops being captured (e.g. Esc in a browser).")]
    public bool pauseWhenMouseFreed = true;
    [Tooltip("Pause when the game window loses focus (alt-tab, clicking another tab).")]
    public bool pauseOnFocusLost = true;

    private RectTransform canvas;
    private Health playerHealth;
    private bool hadMouse;

    void Awake()
    {
        IsOpen = false;
        UIBuilder.EnsureEventSystem();
        Build();
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerHealth = p.GetComponent<Health>();
    }

    void OnDestroy()
    {
        if (IsOpen) Time.timeScale = 1f;
        IsOpen = false;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        bool togglePressed = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);

        if (IsOpen)
        {
            if (togglePressed) Resume();
            return;
        }

        // Only pause during normal play: not while the Altar menu or game over has time stopped
        bool playing = Time.timeScale > 0f && (playerHealth == null || !playerHealth.IsDead);
        if (!playing) { hadMouse = false; return; }

        bool mouseCaptured = Cursor.lockState == CursorLockMode.Locked;
        bool lostMouse = pauseWhenMouseFreed && hadMouse && !mouseCaptured;
        hadMouse = mouseCaptured;

        if (togglePressed || lostMouse) Pause();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused && pauseOnFocusLost && !IsOpen && Time.timeScale > 0f && (playerHealth == null || !playerHealth.IsDead))
            Pause();
    }

    public void Pause()
    {
        IsOpen = true;
        hadMouse = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        canvas.gameObject.SetActive(true);
    }

    public void Resume()
    {
        canvas.gameObject.SetActive(false);
        Time.timeScale = 1f;
        IsOpen = false;
        // In a browser this only works from a click; otherwise the next click in the game captures the mouse
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PlayerMovement.LastRecaptureFrame = Time.frameCount;   // the Resume click shouldn't also swing
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        IsOpen = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        IsOpen = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Build()
    {
        canvas = UIBuilder.MakeCanvas("Pause Menu", transform, 45);

        Image dim = UIBuilder.Panel("Dim", canvas, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.65f));
        UIBuilder.Stretch(dim.rectTransform);

        Image panel = UIBuilder.Panel("Panel", canvas, Vector2.zero, new Vector2(560, 620), UIBuilder.PanelColor);
        UIBuilder.SetBorder(panel, UIBuilder.Gold, 3f);
        Transform p = panel.transform;

        UIBuilder.Label("Title", p, "PAUSED", 56, UIBuilder.Gold, TextAnchor.MiddleCenter, new Vector2(0, 240), new Vector2(500, 70));

        bool canQuit = Application.platform != RuntimePlatform.WebGLPlayer;
        float y = 130f;
        UIBuilder.Button("Resume", p, "RESUME", 28, new Vector2(0, y), new Vector2(380, 70), Resume); y -= 90f;
        UIBuilder.Button("Restart", p, "RESTART", 28, new Vector2(0, y), new Vector2(380, 70), Restart); y -= 90f;
        UIBuilder.Button("Main Menu", p, "MAIN MENU", 28, new Vector2(0, y), new Vector2(380, 70), MainMenu); y -= 90f;
        if (canQuit) { UIBuilder.Button("Quit", p, "QUIT GAME", 28, new Vector2(0, y), new Vector2(380, 70), Quit); y -= 90f; }

        UIBuilder.Label("Controls", p,
            "WASD move · Mouse look · Click attack · Space jump · Shift roll\nR reload · E altar · Tab blessings · Esc pause",
            16, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, new Vector2(0, -265), new Vector2(520, 50));

        canvas.gameObject.SetActive(false);
    }
}
