using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The heart of Favor of Tyche. Every attack asks this for a roll.
///
/// Usage from a weapon:
///     RollResult roll = LuckSystem.Instance.Roll();
///     float damage = baseDamage * roll.multiplier;
///
/// Other scripts can listen for rolls without needing a reference:
///     LuckSystem.OnRoll += MyHandler;   (remember to -= in OnDisable)
/// </summary>
public class LuckSystem : MonoBehaviour
{
    public static LuckSystem Instance { get; private set; }

    // ---------- Events (HUD, JackpotEffect, BoonManager, GameManager listen to these) ----------
    public static event Action<RollResult> OnRoll;          // every roll, jackpot or not
    public static event Action<RollResult> OnJackpot;       // full Jackpots only
    public static event Action<float> OnFavorChanged;       // 0..1 fill of the Favor meter
    public static event Action<int> OnLuckyNumberChanged;
    public static event Action<int> OnDiceCountChanged;

    [Header("Dice")]
    [Tooltip("2 by default. Gift of the Titans sets this to 3.")]
    [SerializeField] private int diceCount = 2;
    private const int Sides = 6;

    [Header("Lucky Number")]
    [SerializeField] private int luckyNumber = 7;

    [Header("Damage multipliers")]
    public float weakMultiplier = 0.5f;
    public float normalMultiplier = 1f;
    public float strongMultiplier = 1.5f;
    public float jackpotMultiplier = 3f;
    [Tooltip("Used by Dionysus' Close Enough (rolls within ±1 of the Lucky Number).")]
    public float halfJackpotMultiplier = 2f;

    [Header("Favor of Tyche meter")]
    [Tooltip("How many non-jackpot rolls it takes to fill the meter.")]
    public int rollsToFillFavor = 10;

    [Header("Boon flags (set by BoonManager)")]
    [Tooltip("Dionysus' Close Enough: rolls within ±1 of the Lucky Number are half-Jackpots.")]
    public bool closeEnoughEnabled = false;

    [Header("Debug")]
    public bool logRolls = false;
    [Tooltip("Press this in Play mode to make a test roll (only while Log Rolls is on).")]
    public KeyCode debugRollKey = KeyCode.F1;

    // ---------- Runtime state ----------
    private int permanentBonus;                                   // Hermes' Loaded Dice etc.
    private readonly List<TimedModifier> timedModifiers = new List<TimedModifier>(); // Raven curse etc.
    private readonly List<int[]> upcomingRolls = new List<int[]>(); // pre-rolled dice for Athena's preview
    private int favorRolls;

    public int DiceCount => diceCount;
    public int LuckyNumber => luckyNumber;
    public int MinLuckyNumber => diceCount;
    public int MaxLuckyNumber => diceCount * Sides;
    public float FavorNormalized => Mathf.Clamp01((float)favorRolls / rollsToFillFavor);
    public bool IsFavorFull => favorRolls >= rollsToFillFavor;
    public int TotalJackpots { get; private set; }               // for the score
    public int ConsecutiveJackpots { get; private set; }         // for Ares' Bloodlust
    public RollResult LastRoll { get; private set; }

    /// <summary>Sum of every bonus/penalty currently applied to rolls (e.g. +1 Hermes, -2 curse).</summary>
    public int CurrentRollBonus
    {
        get
        {
            RemoveExpiredModifiers();
            int total = permanentBonus;
            foreach (TimedModifier m in timedModifiers) total += m.amount;
            return total;
        }
    }

    private struct TimedModifier
    {
        public string id;
        public int amount;
        public float expiresAt;
    }

    // ======================================================================
    // Unity
    // ======================================================================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("LuckSystem: a second LuckSystem was found and destroyed. Keep only one in the scene.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        luckyNumber = Mathf.Clamp(luckyNumber, MinLuckyNumber, MaxLuckyNumber);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Let the HUD draw its starting values.
        OnFavorChanged?.Invoke(FavorNormalized);
        OnLuckyNumberChanged?.Invoke(luckyNumber);
        OnDiceCountChanged?.Invoke(diceCount);
    }

    void Update()
    {
        if (logRolls && Input.GetKeyDown(debugRollKey)) Roll();
    }

    // ======================================================================
    // Rolling
    // ======================================================================

    /// <summary>Roll the dice for one attack. Call this once per attack (or once per sword hit).</summary>
    public RollResult Roll()
    {
        int bonus = CurrentRollBonus;
        bool forced = IsFavorFull;
        int[] dice;

        if (forced)
        {
            // Full Favor meter: the next attack is a guaranteed Jackpot.
            // The Athena preview queue is left alone, so previewed rolls still come next.
            dice = MakeDiceForTotal(luckyNumber - bonus);
            favorRolls = 0;
        }
        else if (upcomingRolls.Count > 0)
        {
            dice = upcomingRolls[0];
            upcomingRolls.RemoveAt(0);
        }
        else
        {
            dice = RollRawDice();
        }

        RollResult result = Evaluate(dice, bonus, forced);

        // Bookkeeping
        if (result.isJackpot)
        {
            TotalJackpots++;
            ConsecutiveJackpots++;
        }
        else
        {
            ConsecutiveJackpots = 0;
            favorRolls = Mathf.Min(favorRolls + 1, rollsToFillFavor);
        }

        LastRoll = result;

        if (logRolls) Debug.Log($"[Luck] {result}");

        OnRoll?.Invoke(result);
        if (result.isJackpot) OnJackpot?.Invoke(result);
        OnFavorChanged?.Invoke(FavorNormalized);

        return result;
    }

    private RollResult Evaluate(int[] dice, int bonus, bool forced)
    {
        int rawTotal = 0;
        foreach (int d in dice) rawTotal += d;
        int total = rawTotal + bonus;

        RollResult r = new RollResult
        {
            dice = dice,
            bonus = bonus,
            total = total,
            luckyNumber = luckyNumber,
            wasForced = forced,
            isDouble = HasMatchingPair(dice),
            isSnakeEyes = CountOf(dice, 1) >= 2,
        };

        if (total == luckyNumber || forced)
        {
            r.tier = RollTier.Jackpot;
            r.isJackpot = true;
            r.multiplier = jackpotMultiplier;
            return r;
        }

        // Weak / Normal / Strong. For 2d6 this gives 2-4 / 5-9 / 10-12 like the GDD.
        // For 3d6 it scales to 3-6 / 7-14 / 15-18.
        int min = diceCount;
        int max = diceCount * Sides;
        int band = Mathf.RoundToInt((max - min) * 0.2f);

        if (total <= min + band) { r.tier = RollTier.Weak; r.multiplier = weakMultiplier; }
        else if (total >= max - band) { r.tier = RollTier.Strong; r.multiplier = strongMultiplier; }
        else { r.tier = RollTier.Normal; r.multiplier = normalMultiplier; }

        if (closeEnoughEnabled && Mathf.Abs(total - luckyNumber) == 1)
        {
            r.isHalfJackpot = true;
            r.multiplier = Mathf.Max(r.multiplier, halfJackpotMultiplier);
        }

        return r;
    }

    private int[] RollRawDice()
    {
        int[] dice = new int[diceCount];
        for (int i = 0; i < diceCount; i++) dice[i] = UnityEngine.Random.Range(1, Sides + 1);
        return dice;
    }

    /// <summary>Builds dice that add up to rawTarget, so a forced Jackpot still shows believable dice.</summary>
    private int[] MakeDiceForTotal(int rawTarget)
    {
        int[] dice = new int[diceCount];
        for (int i = 0; i < diceCount; i++) dice[i] = 1;

        int remaining = Mathf.Clamp(rawTarget, diceCount, diceCount * Sides) - diceCount;
        while (remaining > 0)
        {
            int i = UnityEngine.Random.Range(0, diceCount);
            if (dice[i] < Sides) { dice[i]++; remaining--; }
        }
        return dice;
    }

    // ======================================================================
    // Athena: see your next rolls
    // ======================================================================

    /// <summary>
    /// Returns the next `count` rolls' dice without using them up. Those exact dice are what
    /// the next rolls will be. The current bonus is applied when each one is actually rolled.
    /// </summary>
    public List<int[]> PeekUpcomingRolls(int count)
    {
        while (upcomingRolls.Count < count) upcomingRolls.Add(RollRawDice());
        return upcomingRolls.GetRange(0, count);
    }

    // ======================================================================
    // Modifiers (called by boons, curses, etc.)
    // ======================================================================

    /// <summary>Permanent +/- to every roll. Hermes' Loaded Dice = AddPermanentBonus(1).</summary>
    public void AddPermanentBonus(int amount)
    {
        permanentBonus += amount;
    }

    /// <summary>
    /// Temporary +/- to rolls. Using the same id refreshes it instead of stacking.
    /// Raven curse = ApplyTimedModifier("RavenCurse", -2, 10f).
    /// </summary>
    public void ApplyTimedModifier(string id, int amount, float duration)
    {
        timedModifiers.RemoveAll(m => m.id == id);
        timedModifiers.Add(new TimedModifier { id = id, amount = amount, expiresAt = Time.time + duration });
    }

    public bool HasModifier(string id)
    {
        RemoveExpiredModifiers();
        return timedModifiers.Exists(m => m.id == id);
    }

    private void RemoveExpiredModifiers()
    {
        timedModifiers.RemoveAll(m => Time.time >= m.expiresAt);
    }

    /// <summary>Oracle's Decree. Clamped to what the dice can actually roll.</summary>
    public void SetLuckyNumber(int number)
    {
        luckyNumber = Mathf.Clamp(number, MinLuckyNumber, MaxLuckyNumber);
        OnLuckyNumberChanged?.Invoke(luckyNumber);
    }

    /// <summary>Gift of the Titans = SetDiceCount(3).</summary>
    public void SetDiceCount(int count)
    {
        diceCount = Mathf.Max(1, count);
        upcomingRolls.Clear(); // old previews have the wrong number of dice
        luckyNumber = Mathf.Clamp(luckyNumber, MinLuckyNumber, MaxLuckyNumber);
        OnDiceCountChanged?.Invoke(diceCount);
        OnLuckyNumberChanged?.Invoke(luckyNumber);
    }

    /// <summary>Adds favor directly, e.g. from a pickup. amount is in "rolls".</summary>
    public void AddFavor(int amount)
    {
        favorRolls = Mathf.Clamp(favorRolls + amount, 0, rollsToFillFavor);
        OnFavorChanged?.Invoke(FavorNormalized);
    }

    /// <summary>Reset everything for a new run.</summary>
    public void ResetForNewRun()
    {
        permanentBonus = 0;
        timedModifiers.Clear();
        upcomingRolls.Clear();
        favorRolls = 0;
        TotalJackpots = 0;
        ConsecutiveJackpots = 0;
        closeEnoughEnabled = false;
        SetDiceCount(2);
        SetLuckyNumber(7);
        OnFavorChanged?.Invoke(FavorNormalized);
    }

    // ======================================================================
    // Odds (for UI like Oracle's Decree: "7 = 17% chance")
    // ======================================================================

    /// <summary>Chance (0..1) that a roll's total, including the current bonus, equals `number`.</summary>
    public float ChanceOfTotal(int number)
    {
        int raw = number - CurrentRollBonus;
        if (raw < diceCount || raw > diceCount * Sides) return 0f;

        // Count the ways to make each raw total with diceCount dice.
        long[] ways = { 1 };
        for (int d = 0; d < diceCount; d++)
        {
            long[] next = new long[ways.Length + Sides];
            for (int t = 0; t < ways.Length; t++)
                for (int face = 1; face <= Sides; face++)
                    next[t + face] += ways[t];
            ways = next;
        }
        return raw < ways.Length ? ways[raw] / Mathf.Pow(Sides, diceCount) : 0f;
    }

    public float JackpotChance => ChanceOfTotal(luckyNumber);

    // ======================================================================
    // Helpers
    // ======================================================================

    private static bool HasMatchingPair(int[] dice)
    {
        for (int i = 0; i < dice.Length; i++)
            for (int j = i + 1; j < dice.Length; j++)
                if (dice[i] == dice[j]) return true;
        return false;
    }

    private static int CountOf(int[] dice, int value)
    {
        int c = 0;
        foreach (int d in dice) if (d == value) c++;
        return c;
    }

    [ContextMenu("Test: roll 10,000 times and log the results")]
    private void TestDistribution()
    {
        int weak = 0, normal = 0, strong = 0, jackpot = 0;
        for (int i = 0; i < 10000; i++)
        {
            int[] dice = RollRawDice();
            RollResult r = Evaluate(dice, CurrentRollBonus, false);
            switch (r.tier)
            {
                case RollTier.Weak: weak++; break;
                case RollTier.Normal: normal++; break;
                case RollTier.Strong: strong++; break;
                case RollTier.Jackpot: jackpot++; break;
            }
        }
        Debug.Log($"[Luck] 10,000 rolls of {diceCount}d6 (bonus {CurrentRollBonus:+0;-0;0}), Lucky Number {luckyNumber}: " +
                  $"Weak {weak / 100f}%  Normal {normal / 100f}%  Strong {strong / 100f}%  Jackpot {jackpot / 100f}% " +
                  $"(expected jackpot {JackpotChance * 100f:0.0}%)");
    }
}

public enum RollTier { Weak, Normal, Strong, Jackpot }

/// <summary>Everything about one roll. Weapons, the HUD and effects all read from this.</summary>
public struct RollResult
{
    public int[] dice;          // the individual dice faces, e.g. {3, 4}
    public int bonus;           // total of all modifiers applied (+1 Hermes, -2 curse...)
    public int total;           // sum of dice + bonus
    public int luckyNumber;     // the Lucky Number at the time of the roll
    public RollTier tier;
    public float multiplier;    // multiply your weapon's base damage by this
    public bool isJackpot;
    public bool isHalfJackpot;  // Dionysus' Close Enough
    public bool wasForced;      // came from a full Favor meter
    public bool isDouble;       // any two dice match (Medusa's Gaze)
    public bool isSnakeEyes;    // two or more 1s (Medusa's Snake Eyes pistol)

    public override string ToString()
    {
        string diceText = dice != null ? string.Join(" + ", dice) : "?";
        string bonusText = bonus != 0 ? $" {bonus:+0;-0}" : "";
        string tags = (isHalfJackpot ? " HALF-JACKPOT" : "") + (wasForced ? " (Favor)" : "") +
                      (isSnakeEyes ? " SNAKE EYES" : isDouble ? " DOUBLE" : "");
        return $"{diceText}{bonusText} = {total} → {tier} x{multiplier}{tags}";
    }
}
