using UnityEngine;

/// <summary>
/// Temporary and stacking buffs on the player, combined in one place so they don't fight each other:
///   Speed     = Ares' Bloodlust stacks x Nike's speed blessing   (move + attack speed)
///   Lifesteal = Hades: heal a fraction of all damage you deal
///   Foresight = Athena: see your next 3 rolls on the HUD
/// Added to the Player automatically the first time something needs it (PlayerBuffs.Get).
/// </summary>
public class PlayerBuffs : MonoBehaviour
{
    public float NikeMultiplier { get; private set; } = 1f;
    public float AresMultiplier { get; private set; } = 1f;
    public bool LifestealActive => Time.time < lifestealUntil;
    public bool ForesightActive => Time.time < foresightUntil;

    private PlayerMovement movement;
    private Health health;
    private float baseMoveMultiplier = 1f;
    private float nikeUntil, lifestealUntil, foresightUntil;
    private float lifestealFraction;
    private float lastAppliedSpeed = -1f;
    private bool foresightShown;

    /// <summary>Gets the PlayerBuffs on the player, adding one if needed.</summary>
    public static PlayerBuffs Get(GameObject player)
    {
        if (player == null) return null;
        PlayerBuffs b = player.GetComponent<PlayerBuffs>();
        return b != null ? b : player.AddComponent<PlayerBuffs>();
    }

    public static PlayerBuffs Find()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        return Get(p);
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        if (movement != null) baseMoveMultiplier = movement.speedMultiplier;
    }

    void OnEnable()
    {
        Health.OnAnyDamaged += HandleAnyDamaged;
        LuckSystem.OnRoll += HandleRoll;
    }

    void OnDisable()
    {
        Health.OnAnyDamaged -= HandleAnyDamaged;
        LuckSystem.OnRoll -= HandleRoll;
    }

    // ---------- Granting ----------

    public void SetAresMultiplier(float mult) => AresMultiplier = mult;

    public void GrantNike(float multiplier, float duration)
    {
        NikeMultiplier = Mathf.Max(multiplier, Time.time < nikeUntil ? NikeMultiplier : 1f);
        nikeUntil = Mathf.Max(nikeUntil, Time.time + duration);
    }

    public void GrantLifesteal(float fraction, float duration)
    {
        lifestealFraction = Mathf.Max(fraction, LifestealActive ? lifestealFraction : 0f);
        lifestealUntil = Mathf.Max(lifestealUntil, Time.time + duration);
    }

    public void GrantForesight(float duration)
    {
        foresightUntil = Mathf.Max(foresightUntil, Time.time + duration);
        RefreshForesight();
    }

    // ---------- Per-frame ----------

    void Update()
    {
        if (Time.time >= nikeUntil) NikeMultiplier = 1f;

        float speed = AresMultiplier * NikeMultiplier;
        if (!Mathf.Approximately(speed, lastAppliedSpeed))
        {
            lastAppliedSpeed = speed;
            if (movement != null) movement.speedMultiplier = baseMoveMultiplier * speed;
            foreach (WeaponBase w in GetComponentsInChildren<WeaponBase>(true)) w.attackSpeedMultiplier = speed;
        }

        if (foresightShown && !ForesightActive) RefreshForesight();
    }

    private void HandleAnyDamaged(Health target, float amount, GameObject source)
    {
        if (!LifestealActive || health == null || target == health) return;
        if (source == null || source.transform.root != transform) return;
        health.Heal(amount * lifestealFraction);
    }

    private void HandleRoll(RollResult roll)
    {
        if (ForesightActive) RefreshForesight();
    }

    private void RefreshForesight()
    {
        if (GameHUD.Instance == null) return;
        LuckSystem luck = LuckSystem.Instance;

        if (!ForesightActive || luck == null)
        {
            GameHUD.Instance.SetForesight(null);
            foresightShown = false;
            return;
        }

        int bonus = luck.CurrentRollBonus;
        string text = "ATHENA SEES:  ";
        foreach (int[] dice in luck.PeekUpcomingRolls(3))
        {
            int total = bonus;
            foreach (int d in dice) total += d;
            text += total == luck.LuckyNumber ? $"<color=#FFD040><b>{total}</b></color>  " : $"{total}  ";
        }
        GameHUD.Instance.SetForesight(text);
        foresightShown = true;
    }
}
