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
        /// <summary>Only offered once this WeaponLoadout slot is unlocked (-1 = always). 0 sword, 1 hammer, 2 pistol, 3 coins.</summary>
        public int requiresWeapon = -1;
        public Action apply;
    }

    [Header("Ares' Bloodlust")]
    [Tooltip("Extra move + attack speed per Jackpot in a row.")]
    public float bloodlustSpeedPerStack = 0.12f;
    public int bloodlustMaxStacks = 4;

    [Header("Ambrosia")]
    [Tooltip("Max health added each time Ambrosia is taken. 0.15 = +15%, and it stacks.")]
    [Range(0f, 1f)] public float ambrosiaMaxHealthBonus = 0.15f;

    /// <summary>Fires when the player gains a boon.</summary>
    public event Action<Boon> OnBoonGained;

    public IReadOnlyList<Boon> Owned => owned;
    public int BloodlustStacks { get; private set; }

    private readonly List<Boon> allBoons = new List<Boon>();
    private readonly List<Boon> owned = new List<Boon>();
    private GameObject player;
    private Health playerHealth;
    private WeaponLoadout loadout;

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
            playerHealth = player.GetComponent<Health>();
            loadout = player.GetComponent<WeaponLoadout>();
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

        // ---------- Weapon boons (only offered once that weapon is unlocked) ----------
        allBoons.Add(new Boon
        {
            id = "let_it_ride", name = "Let It Ride", god = "Tyche",
            description = "Fortune's Edge: your combo carries from enemy to enemy instead of restarting.",
            requiresWeapon = 0,
            apply = () =>
            {
                if (player != null)
                    foreach (FortunesEdge s in player.GetComponentsInChildren<FortunesEdge>(true)) s.letItRide = true;
            },
        });

        allBoons.Add(new Boon
        {
            id = "rigged_machine", name = "Rigged Machine", god = "Hephaestus",
            description = "The Jackpot: all three reels always land on the same god. Triple effect, every time.",
            requiresWeapon = 1,
            apply = () => { },   // JackpotHammer checks for it
        });

        allBoons.Add(new Boon
        {
            id = "medusa_gaze", name = "Gaze", god = "Medusa",
            description = "Snake Eyes: any double turns enemies to stone, not just snake eyes.",
            requiresWeapon = 2,
            apply = () => { },   // SnakeEyesPistol checks for it
        });

        allBoons.Add(new Boon
        {
            id = "two_headed_coin", name = "Two-Headed Coin", god = "Janus",
            description = "Heads or Tails: every coin lands heads. Every coin explodes.",
            requiresWeapon = 3,
            apply = () => { },   // CoinProjectile checks for it
        });

        // Repeatable: can be offered at any altar, and stacks every time you take it
        allBoons.Add(new Boon
        {
            id = "ambrosia", name = "Ambrosia", god = "Olympus",
            description = "Food of the gods. +15% max health. Stacks every time you take it.",
            repeatable = true,
            apply = () =>
            {
                if (playerHealth != null) playerHealth.IncreaseMaxHealth(ambrosiaMaxHealthBonus);
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
        List<Boon> pool = allBoons.FindAll(b => !b.filler && (b.repeatable || !Has(b.id)) && WeaponAvailable(b));
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

    private bool WeaponAvailable(Boon b)
    {
        if (b.requiresWeapon < 0) return true;
        if (loadout == null && player != null) loadout = player.GetComponent<WeaponLoadout>();
        return loadout != null && loadout.IsUnlocked(b.requiresWeapon);
    }

    /// <summary>Gives the player a boon and applies it.</summary>
    public void Grant(Boon boon)
    {
        if (boon == null) return;
        boon.apply?.Invoke();
        owned.Add(boon);   // repeatable boons (Ambrosia) are listed once per time taken

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
        PlayerBuffs buffs = PlayerBuffs.Get(player);
        if (buffs != null) buffs.SetAresMultiplier(mult);
    }
}
