using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives a weapon model proper materials (gold, steel, gunmetal...) instead of the default grey.
/// Materials live in Assets/Resources/WeaponMaterials and are picked by name.
///
/// Each part of the model is matched against the rules (by the part's object name or its material name);
/// anything that doesn't match gets the Fallback material.
///
/// Added automatically to The Jackpot, Snake Eyes and Heads or Tails with sensible defaults.
/// To customise, add this component to the weapon's pivot yourself and edit the rules.
/// Available materials: Gold, GoldGlow, Steel, Gunmetal, RedLacquer, DarkWood, Ivory, Leather
/// </summary>
public class WeaponSkin : MonoBehaviour
{
    [Serializable]
    public class Rule
    {
        [Tooltip("Part of the object or material name to match, e.g. \"Cylinder\" or \"Material.003\". Not case sensitive.")]
        public string nameContains;
        [Tooltip("Material from Resources/WeaponMaterials, e.g. Gold")]
        public string material;
        public Rule() { }
        public Rule(string contains, string mat) { nameContains = contains; material = mat; }
    }

    [Tooltip("Used for any part no rule matches.")]
    public string fallbackMaterial = "Gold";
    public List<Rule> rules = new List<Rule>();
    [Tooltip("Log each part's name and the material it got, to help write rules.")]
    public bool logParts = false;

    private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    public static Material Load(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (cache.TryGetValue(name, out Material m) && m != null) return m;
        m = Resources.Load<Material>("WeaponMaterials/" + name);
        if (m == null) Debug.LogWarning($"WeaponSkin: no material named '{name}' in Resources/WeaponMaterials.");
        cache[name] = m;
        return m;
    }

    /// <summary>Sets up a skin with these rules on a weapon that doesn't already have one.</summary>
    public static WeaponSkin AddDefault(GameObject weapon, string fallback, params Rule[] rules)
    {
        WeaponSkin skin = weapon.GetComponent<WeaponSkin>();
        if (skin != null) return skin;   // the user's own settings win
        skin = weapon.AddComponent<WeaponSkin>();
        skin.fallbackMaterial = fallback;
        skin.rules.AddRange(rules);
        skin.Apply();
        return skin;
    }

    /// <summary>Gives every renderer under root the same material (e.g. fired coins).</summary>
    public static void ApplyAll(GameObject root, string materialName)
    {
        Material m = Load(materialName);
        if (m == null) return;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    void Awake() => Apply();

    [ContextMenu("Apply now")]
    public void Apply()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;

            Material[] current = r.sharedMaterials;
            Material[] mats = new Material[Mathf.Max(1, current.Length)];
            for (int i = 0; i < mats.Length; i++)
            {
                string matName = i < current.Length && current[i] != null ? current[i].name : "";
                string pick = Pick(r.gameObject.name, matName);
                mats[i] = Load(pick) ?? (i < current.Length ? current[i] : null);
                if (logParts) Debug.Log($"[WeaponSkin] {name}: part '{r.gameObject.name}' slot {i} ('{matName}') -> {pick}");
            }
            r.sharedMaterials = mats;
        }
    }

    private string Pick(string objectName, string materialName)
    {
        foreach (Rule rule in rules)
        {
            if (string.IsNullOrEmpty(rule.nameContains)) continue;
            if (Contains(materialName, rule.nameContains) || Contains(objectName, rule.nameContains)) return rule.material;
        }
        return fallbackMaterial;
    }

    private static bool Contains(string s, string part) =>
        !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
}
