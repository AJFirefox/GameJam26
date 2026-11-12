using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Owns every boon: what they are, which ones the player has, and what they do.
/// Put this on any GameObject in the scene (e.g. the LuckSystem object).
///
/// To add a new boon: add an entry in BuildBoons() with an id, name, god, description and an Apply action.
/// </summary>
public class BoonManager : MonoBehaviour
{
    public static BoonManager Instance { get; private set; }

    public class Boon
    {
        public string id;
        public string name;
        public string god;
        public string description;
        /// <summary>Can be offered again after you own it (e.g. the heal).</summary>
        public bool repeatable;
        /// <summary>Only offered as a filler when there aren't enough real boons left.</summary>
        public bool filler;
        /// <summary>Needs the player to pick a Lucky Number after choosing it.</summary>
        public bool needsNumberPick;
        public Action apply;
    }

    [Header("Ares' Bloodlust")]
    [Tooltip("Extra move + attack speed per Jackpot in a row.")]
    public float bloodlustSpeedPerStack = 0.12f;
    public int bloodlustMaxStacks = 4;

    [Header("Ambrosia (filler heal)")]
    [Range(0f, 1f)] public float ambrosiaHealFraction = 0.4f;

    /// <summary>Fires when the player gains a boon.</summary>
    public event Action<Boon> OnBoonGained;

    public IReadOnlyList<Boon> Owned => owned;
    public int BloodlustStacks { get; private set; }

    private readonly List<Boon> allBoons = new List<Boon>();
    private readonly List<Boon> owned = new List<Boon>();
    private GameObject player;
    private PlayerMovement movement;
    private Health playerHealth;
    private float baseMoveSpeedMultiplier = 1f;

    // ======================================================================
    // Setup
    // ======================================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        BuildBoons();
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            movement = player.GetComponent<PlayerMovement>();
            playerHealth = player.GetComponent<Health>();
            if (movement != null) baseMoveSpeedMultiplier = movement.speedMultiplier;
        }
    }

    void OnEnable() => LuckSystem.OnRoll += HandleRoll;
    void OnDisable() => LuckSystem.OnRoll -= HandleRoll;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildBoons()
    {
        allBoons.Add(new Boon
        {
            id = "hermes_loaded_dice", name = "Loaded Dice", god = "Hermes",
            description = "+1 to every roll.",
            apply = () => LuckSystem.Instance?.AddPermanentBonus(1),
        });

        allBoons.Add(new Boon
        {
            id = "titans_gift", name = "Gift of the Titans", god = "The Titans",
            description = "Roll 3 dice instead of 2. Bigger numbers, and your Lucky Number can go up to 18.",
            apply = () => LuckSystem.Instance?.SetDiceCount(3),
        });

        allBoons.Add(new Boon
        {
            id = "oracle_decree", name = "Oracle's Decree", god = "The Oracle",
            description = "Choose your own Lucky Number.",
            needsNumberPick = true,
            apply = () => { },   // the Altar shows a number picker, then calls SetLuckyNumber
        });

        allBoons.Add(new Boon
        {
            id = "dionysus_close_enough", name = "Close Enough", god = "Dionysus",
            description = "Rolls 1 away from your Lucky Number count as half-Jackpots (x2).",
            apply = () => { if (LuckSystem.Instance != null) LuckSystem.Instance.closeEnoughEnabled = true; },
        });

        allBoons.Add(new Boon
        {
            id = "ares_bloodlust", name = "Bloodlust", god = "Ares",
            description = "Each Jackpot in a row makes you move and attack faster. Resets on a non-Jackpot.",
            apply = () => { },   // handled in HandleRoll
        });

        // Filler: offered when there aren't 3 real boons left to choose from
        allBoons.Add(new Boon
        {
            id = "ambrosia", name = "Ambrosia", god = "Olympus",
            description = "Restore 40% of your health.",
            repeatable = true, filler = true,
            apply = () =>
            {
                if (playerHealth != null) playerHealth.Heal(playerHealth.maxHealth * ambrosiaHealFraction);
            },
        });
    }

    // ======================================================================
    // Offering and granting
    // ======================================================================

    public bool Has(string id) => owned.Exists(b => b.id == id);

    /// <summary>Picks up to `count` different boons the player can still take. Fills with Ambrosia if needed.</summary>
    public List<Boon> GetOffer(int count)
    {
        List<Boon> pool = allBoons.FindAll(b => !b.filler && (b.repeatable || !Has(b.id)));
        List<Boon> offer = new List<Boon>();

        while (offer.Count < count && pool.Count > 0)
        {
            int i = Random.Range(0, pool.Count);
            offer.Add(pool[i]);
            pool.RemoveAt(i);
        }

        Boon filler = allBoons.Find(b => b.filler);
        if (offer.Count < count && filler != null) offer.Add(filler);
        return offer;
    }

    /// <summary>Gives the player a boon and applies it.</summary>
    public void Grant(Boon boon)
    {
        if (boon == null) return;
        boon.apply?.Invoke();
        if (!boon.filler) owned.Add(boon);

        OnBoonGained?.Invoke(boon);
        if (GameHUD.Instance != null) GameHUD.Instance.ShowBoon($"{boon.god}: {boon.name}", boon.description);
        Debug.Log($"[Boon] Gained {boon.god}: {boon.name}");
    }

    // ======================================================================
    // Ares' Bloodlust
    // ======================================================================

    private void HandleRoll(RollResult roll)
    {
        if (!Has("ares_bloodlust")) return;

        int stacks = roll.isJackpot ? Mathf.Min(BloodlustStacks + 1, bloodlustMaxStacks) : 0;
        if (stacks == BloodlustStacks) return;
        BloodlustStacks = stacks;

        float mult = 1f + bloodlustSpeedPerStack * BloodlustStacks;
        if (movement != null) movement.speedMultiplier = baseMoveSpeedMultiplier * mult;
        if (player != null)
            foreach (WeaponBase w in player.GetComponentsInChildren<WeaponBase>(true))
                w.attackSpeedMultiplier = mult;
    }
}
