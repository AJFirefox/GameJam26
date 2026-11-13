using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Saved Master / Music / SFX volume (0-1) shared by every scene.
/// Nothing to set up: it runs automatically, finds any Slider whose name contains
/// "Master", "Music" or "SFX" (e.g. MasterVolumeSlider in the Settings menu), loads the
/// saved value into it and saves it again when it's dragged.
///   Master -> AudioListener.volume (everything)
///   Music  -> MusicManager
///   SFX    -> JackpotEffect and any other sound effects (multiply by VolumeSettings.Sfx)
/// </summary>
public class VolumeSettings : MonoBehaviour
{
    const string MasterKey = "MasterVolume", MusicKey = "MusicVolume", SfxKey = "SFXVolume";

    public static float Master { get; private set; } = 1f;
    public static float Music { get; private set; } = 1f;
    public static float Sfx { get; private set; } = 1f;

    static bool dirty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Master = PlayerPrefs.GetFloat(MasterKey, 1f);
        Music = PlayerPrefs.GetFloat(MusicKey, 1f);
        Sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
        AudioListener.volume = Master;

        var go = new GameObject("VolumeSettings");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<VolumeSettings>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    void Start() { HookSliders(); }   // the first scene loads before this object exists

    void OnSceneLoaded(Scene s, LoadSceneMode m) { HookSliders(); }
    void OnSceneUnloaded(Scene s) { Save(); }
    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationFocus(bool focus) { if (!focus) Save(); }
    void OnApplicationQuit() { Save(); }

    public static void SetMaster(float v) { Master = Mathf.Clamp01(v); AudioListener.volume = Master; dirty = true; }
    public static void SetMusic(float v) { Music = Mathf.Clamp01(v); dirty = true; }
    public static void SetSfx(float v) { Sfx = Mathf.Clamp01(v); dirty = true; }

    static void Save()
    {
        if (!dirty) return;
        PlayerPrefs.SetFloat(MasterKey, Master);
        PlayerPrefs.SetFloat(MusicKey, Music);
        PlayerPrefs.SetFloat(SfxKey, Sfx);
        PlayerPrefs.Save();
        dirty = false;
    }

    static void HookSliders()
    {
        // Include inactive ones: the Settings panel is usually hidden when the scene loads.
        Slider[] sliders = FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Slider s in sliders)
        {
            string n = s.gameObject.name.ToLowerInvariant();
            UnityAction<float> set; float value;
            if (n.Contains("master")) { set = SetMaster; value = Master; }
            else if (n.Contains("music")) { set = SetMusic; value = Music; }
            else if (n.Contains("sfx") || n.Contains("effect") || n.Contains("sound")) { set = SetSfx; value = Sfx; }
            else continue;

            // Switch off whatever was wired in the Inspector (it pointed at an empty AudioSource).
            for (int i = 0; i < s.onValueChanged.GetPersistentEventCount(); i++)
                s.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);

            s.minValue = 0f;
            s.maxValue = 1f;
            s.wholeNumbers = false;
            s.SetValueWithoutNotify(value);
            s.onValueChanged.RemoveListener(set);
            s.onValueChanged.AddListener(set);
        }
    }
}
