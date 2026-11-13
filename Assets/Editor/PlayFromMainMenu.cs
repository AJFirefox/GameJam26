#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pressing Play always starts the game from the main menu (the first scene in
/// File > Build Profiles / Scene List), no matter which scene is open.
/// Your open scene comes back when you stop playing.
/// Toggle it with the menu "Favor of Tyche > Play From Main Menu" (e.g. to test the arena directly).
/// </summary>
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    const string MenuPath = "Favor of Tyche/Play From Main Menu";
    const string PrefKey = "FavorOfTyche.PlayFromMainMenu";

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static PlayFromMainMenu()
    {
        EditorApplication.delayCall += Apply;   // wait until the asset database is ready
    }

    [MenuItem(MenuPath, false, 1)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    static void Apply()
    {
        if (!Enabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        string path = null;
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.enabled) { path = s.path; break; }

        SceneAsset menu = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        if (menu == null)
            Debug.LogWarning("PlayFromMainMenu: no enabled scene in the build list, Play will start the open scene.");
        EditorSceneManager.playModeStartScene = menu;
    }
}
#endif
