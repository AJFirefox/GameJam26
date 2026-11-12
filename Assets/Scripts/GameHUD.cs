using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-game HUD. Builds itself when the game starts, so the only setup is putting this
/// script on an empty GameObject in the scene.
///
/// Shows: health, stamina, wave number, reticle, dice readout by the crosshair, Favor of Tyche meter,
/// Lucky Number and roll bonus. Has hidden slots for ammo, boss bar/name and most recent boon,
/// which other scripts turn on through GameHUD.Instance.
///
/// Everything is a normal UI object under "HUD Canvas", so it can be restyled later.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    [Header("Colours")]
    public Color gold = new Color(1f, 0.82f, 0.25f);
    public Color marble = new Color(0.96f, 0.94f, 0.9f);
    public Color healthColor = new Color(0.85f, 0.2f, 0.2f);
    public Color staminaColor = new Color(0.35f, 0.8f, 0.45f);
    public Color barBackground = new Color(0f, 0f, 0f, 0.55f);
    public Color weakColor = new Color(0.65f, 0.65f, 0.65f);
    public Color strongColor = new Color(0.55f, 0.85f, 1f);
    public Color halfJackpotColor = new Color(1f, 0.6f, 0.25f);
    public Color curseColor = new Color(0.75f, 0.35f, 1f);

    [Header("Dice readout")]
    [Tooltip("Seconds a roll stays on screen before fading.")]
    public float rollHoldTime = 1.0f;
    public float rollFadeTime = 0.4f;

    [Header("Damage flash")]
    [Range(0f, 1f)] public float damageFlashAlpha = 0.3f;

    // ---------- Built UI ----------
    private Font font;
    private Canvas canvas;
    private Bar healthBar, staminaBar, favorBar, bossBar;
    private Text healthText, waveText, diceText, tierText, luckyText, bonusText, favorLabel;
    private Text ammoText, bossNameText, boonText, foresightText;
    private CanvasGroup diceGroup, bossGroup, boonGroup;
    private Image damageFlash;

    // ---------- Things we watch ----------
    private Health playerHealth;
    private PlayerStamina playerStamina;
    private SpawnManager spawnManager;
    private Health bossHealth;
    private int shownWave = -1;
    private Coroutine diceRoutine, boonRoutine, damageRoutine, wavePopRoutine;

    // ======================================================================
    // Setup
    // ======================================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildHUD();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        LuckSystem.OnRoll -= HandleRoll;
        LuckSystem.OnFavorChanged -= HandleFavor;
        LuckSystem.OnLuckyNumberChanged -= HandleLuckyNumber;
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealth;
            playerHealth.OnDamaged -= HandlePlayerDamaged;
        }
        if (playerStamina != null) playerStamina.OnStaminaChanged -= HandleStamina;
        if (bossHealth != null) bossHealth.OnHealthChanged -= HandleBossHealth;
    }

    void Start()
    {
        LuckSystem.OnRoll += HandleRoll;
        LuckSystem.OnFavorChanged += HandleFavor;
        LuckSystem.OnLuckyNumberChanged += HandleLuckyNumber;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<Health>();
            playerStamina = player.GetComponent<PlayerStamina>();
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealth;
            playerHealth.OnDamaged += HandlePlayerDamaged;
            HandleHealth(playerHealth.CurrentHealth, playerHealth.maxHealth);
        }
        else Debug.LogWarning("GameHUD: no Health found on the object tagged Player.");

        if (playerStamina != null)
        {
            playerStamina.OnStaminaChanged += HandleStamina;
            HandleStamina(playerStamina.CurrentStamina, playerStamina.maxStamina);
        }
        else staminaBar.root.gameObject.SetActive(false);

        spawnManager = FindAnyObjectByType<SpawnManager>();

        if (LuckSystem.Instance != null)
        {
            HandleFavor(LuckSystem.Instance.FavorNormalized);
            HandleLuckyNumber(LuckSystem.Instance.LuckyNumber);
        }
    }

    void Update()
    {
        // Wave number (polled, so it works with SpawnManager now and WaveManager later)
        int wave = spawnManager != null ? spawnManager.CurrentWave : 0;
        if (wave != shownWave)
        {
            shownWave = wave;
            waveText.text = wave > 0 ? $"WAVE {wave}" : "";
            if (wave > 0)
            {
                if (wavePopRoutine != null) StopCoroutine(wavePopRoutine);
                wavePopRoutine = StartCoroutine(Punch(waveText.rectTransform, 1.35f, 0.35f));
            }
        }

        // Roll bonus / curse (changes over time, e.g. Raven curse wearing off)
        if (LuckSystem.Instance != null)
        {
            int bonus = LuckSystem.Instance.CurrentRollBonus;
            bool cursed = LuckSystem.Instance.HasModifier("RavenCurse");
            if (bonus == 0 && !cursed) bonusText.text = "";
            else
            {
                bonusText.text = cursed ? $"CURSED {bonus:+0;-0;0}" : $"Rolls {bonus:+0;-0;0}";
                bonusText.color = cursed || bonus < 0 ? curseColor : staminaColor;
            }

            // Pulse the Favor meter when it's full
            if (LuckSystem.Instance.IsFavorFull)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
                favorBar.fill.color = Color.Lerp(gold, Color.white, pulse * 0.5f);
                favorLabel.text = "NEXT HIT: JACKPOT";
                favorLabel.color = gold;
            }
            else
            {
                favorBar.fill.color = gold;
                favorLabel.text = "FAVOR";
                favorLabel.color = marble;
            }
        }
    }

    // ======================================================================
    // Event handlers
    // ======================================================================

    private void HandleHealth(float current, float max)
    {
        healthBar.Set(max > 0f ? current / max : 0f);
        healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void HandleStamina(float current, float max) => staminaBar.Set(max > 0f ? current / max : 0f);

    private void HandleFavor(float normalized) => favorBar.Set(normalized);

    private void HandleLuckyNumber(int number) => luckyText.text = $"LUCKY  {number}";

    private void HandlePlayerDamaged(float amount, GameObject source)
    {
        if (damageRoutine != null) StopCoroutine(damageRoutine);
        damageRoutine = StartCoroutine(FadeImage(damageFlash, new Color(0.8f, 0f, 0f, damageFlashAlpha), 0.3f));
    }

    private void HandleRoll(RollResult roll)
    {
        // "3 + 4 +1 = 8"
        string dice = roll.dice != null && roll.dice.Length > 0 ? string.Join(" + ", roll.dice) : "?";
        string bonus = roll.bonus != 0 ? $"  {roll.bonus:+0;-0}" : "";
        diceText.text = $"{dice}{bonus} = {roll.total}";

        string label;
        Color color;
        if (roll.isJackpot)
        {
            label = roll.wasForced ? "TYCHE'S FAVOR!  JACKPOT" : "JACKPOT!";
            color = gold;
        }
        else if (roll.isHalfJackpot) { label = "CLOSE ENOUGH"; color = halfJackpotColor; }
        else if (roll.tier == RollTier.Weak) { label = "WEAK"; color = weakColor; }
        else if (roll.tier == RollTier.Strong) { label = "STRONG"; color = strongColor; }
        else { label = "NORMAL"; color = marble; }

        if (roll.isSnakeEyes) label += "  SNAKE EYES";
        tierText.text = $"{label}  ×{roll.multiplier:0.##}";
        tierText.color = color;
        diceText.color = roll.isJackpot ? gold : marble;
        tierText.fontSize = roll.isJackpot ? 34 : 24;

        if (diceRoutine != null) StopCoroutine(diceRoutine);
        diceRoutine = StartCoroutine(ShowRoll(roll.isJackpot));
    }

    private void HandleBossHealth(float current, float max) => bossBar.Set(max > 0f ? current / max : 0f);

    // ======================================================================
    // Public API for other scripts
    // ======================================================================

    /// <summary>Shows the ammo counter. Pass null to hide it (melee weapons, Snake Eyes' infinite ammo).</summary>
    public void SetAmmo(string ammo)
    {
        ammoText.gameObject.SetActive(!string.IsNullOrEmpty(ammo));
        ammoText.text = ammo ?? "";
    }

    /// <summary>Athena's foresight: shows upcoming rolls under the Lucky Number. Pass null to hide.</summary>
    public void SetForesight(string text) => foresightText.text = text ?? "";

    /// <summary>Shows the boss bar and name, and tracks the boss's Health.</summary>
    public void ShowBoss(Health boss, string bossName)
    {
        if (bossHealth != null) bossHealth.OnHealthChanged -= HandleBossHealth;
        bossHealth = boss;
        bossNameText.text = bossName.ToUpper();
        bossGroup.alpha = 1f;
        if (boss != null)
        {
            boss.OnHealthChanged += HandleBossHealth;
            HandleBossHealth(boss.CurrentHealth, boss.maxHealth);
            boss.OnDied += _ => HideBoss();
        }
    }

    public void HideBoss()
    {
        if (bossHealth != null) bossHealth.OnHealthChanged -= HandleBossHealth;
        bossHealth = null;
        bossGroup.alpha = 0f;
    }

    /// <summary>Shows the most recent boon for a few seconds.</summary>
    public void ShowBoon(string boonName, string description = "", float seconds = 4f)
    {
        boonText.text = string.IsNullOrEmpty(description) ? boonName : $"{boonName}\n<size=18>{description}</size>";
        if (boonRoutine != null) StopCoroutine(boonRoutine);
        boonRoutine = StartCoroutine(ShowThenFade(boonGroup, seconds, 0.5f));
    }

    // ======================================================================
    // Animations (unscaled time, so they still play during hitstop)
    // ======================================================================

    private IEnumerator ShowRoll(bool jackpot)
    {
        diceGroup.alpha = 1f;
        StartCoroutine(Punch(diceGroup.transform as RectTransform, jackpot ? 1.5f : 1.15f, jackpot ? 0.3f : 0.15f));
        yield return new WaitForSecondsRealtime(jackpot ? rollHoldTime * 1.5f : rollHoldTime);
        for (float t = 0f; t < rollFadeTime; t += Time.unscaledDeltaTime)
        {
            diceGroup.alpha = 1f - t / rollFadeTime;
            yield return null;
        }
        diceGroup.alpha = 0f;
    }

    private IEnumerator ShowThenFade(CanvasGroup group, float hold, float fade)
    {
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / fade;
            yield return null;
        }
        group.alpha = 0f;
    }

    private IEnumerator Punch(RectTransform rt, float peakScale, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            rt.localScale = Vector3.one * Mathf.Lerp(peakScale, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator FadeImage(Image img, Color from, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            img.color = new Color(from.r, from.g, from.b, Mathf.Lerp(from.a, 0f, t / duration));
            yield return null;
        }
        img.color = Color.clear;
    }

    // ======================================================================
    // Building the HUD
    // ======================================================================

    private class Bar
    {
        public RectTransform root;
        public RectTransform fillRect;
        public Image fill;
        public void Set(float t) => fillRect.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
    }

    private void BuildHUD()
    {
        // Canvas
        GameObject canvasGO = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGO.GetComponent<RectTransform>();

        // Damage flash (behind everything else)
        damageFlash = MakeImage("Damage Flash", root, Color.clear);
        Stretch(damageFlash.rectTransform);

        // ---- Bottom left: health + stamina ----
        RectTransform vitals = MakeRect("Vitals", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 40), new Vector2(460, 90));
        healthBar = MakeBar("Health Bar", vitals, new Vector2(0, 40), new Vector2(460, 30), healthColor);
        healthText = MakeText("Health Text", healthBar.root, "100 / 100", 20, marble, TextAnchor.MiddleCenter);
        Stretch(healthText.rectTransform);
        staminaBar = MakeBar("Stamina Bar", vitals, new Vector2(0, 14), new Vector2(340, 14), staminaColor);

        // ---- Top centre: wave ----
        waveText = MakeText("Wave Text", root, "", 48, marble, TextAnchor.MiddleCenter);
        Place(waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(500, 70));

        // ---- Boss bar (hidden until a boss appears) ----
        RectTransform boss = MakeRect("Boss", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(900, 70));
        bossGroup = boss.gameObject.AddComponent<CanvasGroup>();
        bossGroup.alpha = 0f;
        bossNameText = MakeText("Boss Name", boss, "", 30, gold, TextAnchor.MiddleCenter);
        Place(bossNameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -15), new Vector2(900, 36));
        bossBar = MakeBar("Boss Bar", boss, new Vector2(0, 8), new Vector2(900, 20), healthColor);

        // ---- Centre: reticle ----
        RectTransform reticle = MakeRect("Reticle", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
        MakeTick(reticle, new Vector2(0, 0), new Vector2(4, 4));       // dot
        MakeTick(reticle, new Vector2(0, 13), new Vector2(2, 10));     // up
        MakeTick(reticle, new Vector2(0, -13), new Vector2(2, 10));    // down
        MakeTick(reticle, new Vector2(13, 0), new Vector2(10, 2));     // right
        MakeTick(reticle, new Vector2(-13, 0), new Vector2(10, 2));    // left

        // ---- Dice readout, just right of the crosshair ----
        RectTransform dice = MakeRect("Dice Readout", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(45, 25), new Vector2(420, 90));
        diceGroup = dice.gameObject.AddComponent<CanvasGroup>();
        diceGroup.alpha = 0f;
        diceText = MakeText("Dice", dice, "", 32, marble, TextAnchor.LowerLeft);
        Place(diceText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), Vector2.zero, Vector2.zero, true);
        tierText = MakeText("Tier", dice, "", 24, marble, TextAnchor.UpperLeft);
        Place(tierText.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero, true);

        // ---- Favor meter + Lucky Number, under the crosshair ----
        RectTransform favor = MakeRect("Favor", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -45), new Vector2(240, 80));
        favorLabel = MakeText("Favor Label", favor, "FAVOR", 16, marble, TextAnchor.MiddleCenter);
        Place(favorLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -10), new Vector2(0, 20), true);
        favorBar = MakeBar("Favor Bar", favor, new Vector2(0, -26), new Vector2(240, 7), gold, topAnchored: true);
        luckyText = MakeText("Lucky Number", favor, "LUCKY  7", 20, gold, TextAnchor.MiddleCenter);
        Place(luckyText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(0, 24), true);
        bonusText = MakeText("Roll Bonus", favor, "", 18, staminaColor, TextAnchor.MiddleCenter);
        Place(bonusText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -72), new Vector2(0, 22), true);
        foresightText = MakeText("Foresight", favor, "", 18, strongColor, TextAnchor.MiddleCenter);
        Place(foresightText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), new Vector2(0, 22), true);

        // ---- Bottom right: ammo (hidden by default) ----
        ammoText = MakeText("Ammo", root, "", 44, marble, TextAnchor.LowerRight);
        Place(ammoText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(300, 60), false, new Vector2(1, 0));
        ammoText.gameObject.SetActive(false);

        // ---- Top right: most recent boon (hidden by default) ----
        RectTransform boon = MakeRect("Recent Boon", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(480, 90));
        boonGroup = boon.gameObject.AddComponent<CanvasGroup>();
        boonGroup.alpha = 0f;
        boonText = MakeText("Boon Text", boon, "", 26, gold, TextAnchor.UpperRight);
        Stretch(boonText.rectTransform);
    }

    // ---------- UI helpers ----------

    private RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image MakeImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Text MakeText(string name, Transform parent, string text, int size, Color color, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Outline o = go.GetComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.75f);
        o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private Bar MakeBar(string name, Transform parent, Vector2 pos, Vector2 size, Color color, bool topAnchored = false)
    {
        Vector2 anchor = topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0f, 0f);
        Vector2 pivot = topAnchored ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0f);
        RectTransform rt = MakeRect(name, parent, anchor, anchor, pivot, pos, size);

        Image back = rt.gameObject.AddComponent<Image>();
        back.color = barBackground;
        back.raycastTarget = false;

        Image fill = MakeImage("Fill", rt, color);
        RectTransform fr = fill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(2, 2);
        fr.offsetMax = new Vector2(-2, -2);

        return new Bar { root = rt, fillRect = fr, fill = fill };
    }

    private void MakeTick(RectTransform parent, Vector2 pos, Vector2 size)
    {
        Image img = MakeImage("Tick", parent, new Color(1f, 1f, 1f, 0.9f));
        Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        img.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
    }

    private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, bool stretch = false, Vector2? pivot = null)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        if (stretch && anchorMin != anchorMax)
        {
            // Stretch between the anchors, nudged by pos, with size as extra height when anchors share a Y.
            rt.offsetMin = new Vector2(0, pos.y - size.y * 0.5f) * (anchorMin.y == anchorMax.y ? 1f : 0f);
            rt.offsetMax = new Vector2(0, pos.y + size.y * 0.5f) * (anchorMin.y == anchorMax.y ? 1f : 0f);
        }
        else
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
