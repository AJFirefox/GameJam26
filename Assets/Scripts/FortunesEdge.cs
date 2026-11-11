using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Fortune's Edge: fast broadsword, 3-hit combo, lunge on hit 3.
/// Each hit rolls the dice. A Jackpot on hit 3 fires a cleave wave.
///
/// Swings are animated in code. Each hit is three poses the sword passes through:
///   Windup (cocked back) -> Impact (out in front) -> Follow Through (finished across the body)
/// The hand moves along a curve through them, fastest in the middle, with the edge leading the cut.
/// Select SwordPivot in the Scene view to see the poses drawn as coloured lines.
///
/// Setup: Main Camera > SwordPivot (this script, at the "hand") > sword model with its handle on the pivot,
/// blade pointing straight up at rest > blade has a trigger collider + Damage.
/// </summary>
public class FortunesEdge : WeaponBase
{
    [Serializable]
    public class SwingPose
    {
        [Tooltip("Where the hand is, relative to its rest position. X = right, Y = up, Z = forward (from the camera).")]
        public Vector3 handOffset;
        [Tooltip("Which way the blade points. X = right, Y = up, Z = forward. (0,1,0) = straight up.")]
        public Vector3 bladeDirection = Vector3.up;

        public SwingPose() { }
        public SwingPose(Vector3 hand, Vector3 blade) { handOffset = hand; bladeDirection = blade; }
    }

    [Serializable]
    public class SwingHit
    {
        public string label = "Hit";
        public float damage = 20f;
        public SwingPose windup = new SwingPose();
        public SwingPose impact = new SwingPose();
        public SwingPose followThrough = new SwingPose();
        [Tooltip("Seconds from windup to follow-through (the actual cut).")]
        public float swingTime = 0.2f;
        [Tooltip("Extra wind-up time for this hit only. A bigger wind-up makes a heavy hit read as heavy.")]
        public float extraWindup = 0f;
    }

    [Header("References")]
    [Tooltip("Damage component on the blade. Its collider must be a trigger. Target Tag = Enemy, Roll Luck = on.")]
    public Damage bladeHitbox;

    [Header("Swings")]
    public SwingHit[] swings = DefaultSwings();

    [Tooltip("Scales every hand movement. Above 1 = bigger, wider swings. Below 1 = tighter.")]
    public float swingSize = 1f;
    [Tooltip("Spins the blade around its own length. If the flat of the blade leads the cut, try 90, 180 or 270.")]
    public float edgeRoll = 0f;
    [Tooltip("Rough blade length in metres. Only used to work out which way the edge should face, and for the gizmos.")]
    public float bladeLength = 0.9f;

    [Header("Timing")]
    [Tooltip("Seconds to pull the sword back into the windup pose.")]
    public float windupTime = 0.1f;
    [Tooltip("Seconds to bring the sword back to rest after a swing.")]
    public float recoverTime = 0.2f;
    [Tooltip("If you don't attack again within this many seconds, the combo restarts at hit 1.")]
    public float comboResetTime = 0.9f;
    [Tooltip("Clicking during a swing queues the next hit if it's within this many seconds of the swing ending.")]
    public float inputBufferTime = 0.15f;

    [Header("Pacing (stops spamming)")]
    [Tooltip("Minimum pause after hits 1 and 2 before the next hit can start.")]
    public float delayBetweenSwings = 0.12f;
    [Tooltip("Longer pause after the finisher (hit 3) before you can attack again.")]
    public float finisherCooldown = 0.4f;

    [Header("Hit 3 lunge")]
    public float lungeSpeed = 16f;
    public float lungeDuration = 0.15f;

    [Header("Cleave wave (Jackpot on hit 3)")]
    [Tooltip("Optional. If empty, a simple golden wave is created in code.")]
    public GameObject cleaveWavePrefab;
    public float cleaveDamage = 40f;
    public float cleaveSpeed = 20f;
    public float cleaveLifetime = 1f;

    [Header("Boons")]
    [Tooltip("Let It Ride: the combo carries between enemies instead of restarting when you hit a new one.")]
    public bool letItRide = false;

    /// <summary>Fires when a combo hit starts (1, 2 or 3). Handy for sounds or a HUD combo counter.</summary>
    public event Action<int> OnComboHit;

    public int ComboStep => comboIndex + 1;
    public bool IsSwinging => isSwinging;

    private Quaternion restRotation;
    private Vector3 restPosition;
    private int comboIndex;            // 0, 1, 2
    private bool isSwinging;
    private bool attackBuffered;
    private float lastSwingEndTime = -999f;
    private float swingEndsAt;
    private float nextSwingAllowedAt;
    private Health comboTarget;        // the enemy the current combo is on
    private bool cleaveFiredThisSwing;
    private bool firstHitDoneThisSwing;
    private bool restartComboAfterSwing;
    private int currentSwingIndex;
    private Coroutine swingRoutine;

    // ======================================================================
    // Default swings
    // ======================================================================

    private static SwingHit[] DefaultSwings() => new[]
    {
        // Hit 1: forehand diagonal. Cocked over the right shoulder, cuts down through the middle, finishes low left.
        new SwingHit
        {
            label = "1: Diagonal slash", damage = 20f, swingTime = 0.17f,
            windup        = new SwingPose(new Vector3( 0.10f,  0.25f, -0.15f), new Vector3( 0.50f,  0.80f, -0.35f)),
            impact        = new SwingPose(new Vector3(-0.30f,  0.05f,  0.25f), new Vector3(-0.55f,  0.15f,  0.82f)),
            followThrough = new SwingPose(new Vector3(-0.60f, -0.15f,  0.05f), new Vector3(-0.75f, -0.55f,  0.35f)),
        },
        // Hit 2: backhand. Starts across the body on the left, sweeps flat through the front, finishes up and right.
        new SwingHit
        {
            label = "2: Backhand", damage = 20f, swingTime = 0.17f,
            windup        = new SwingPose(new Vector3(-0.55f, -0.05f,  0.00f), new Vector3(-0.85f,  0.20f,  0.45f)),
            impact        = new SwingPose(new Vector3(-0.10f,  0.10f,  0.30f), new Vector3( 0.10f,  0.20f,  0.97f)),
            followThrough = new SwingPose(new Vector3( 0.25f,  0.15f,  0.05f), new Vector3( 0.85f,  0.35f,  0.35f)),
        },
        // Hit 3: overhead slam. Raised high behind the head, chops down in front, finishes low.
        new SwingHit
        {
            label = "3: Overhead slam", damage = 35f, swingTime = 0.2f, extraWindup = 0.12f,
            windup        = new SwingPose(new Vector3(-0.15f,  0.40f, -0.20f), new Vector3( 0.05f,  0.60f, -0.80f)),
            impact        = new SwingPose(new Vector3(-0.20f,  0.15f,  0.35f), new Vector3( 0.00f,  0.10f,  1.00f)),
            followThrough = new SwingPose(new Vector3(-0.20f, -0.20f,  0.25f), new Vector3( 0.00f, -0.75f,  0.65f)),
        },
    };

    [ContextMenu("Reset swings to defaults")]
    private void ResetSwingsToDefaults()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Reset swings to defaults");
#endif
        swings = DefaultSwings();
        swingSize = 1f;
        windupTime = 0.1f;
        recoverTime = 0.2f;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // ======================================================================
    // Unity
    // ======================================================================

    protected override void Awake()
    {
        base.Awake();
        restRotation = transform.localRotation;
        restPosition = transform.localPosition;
    }

    void Start()
    {
        if (bladeHitbox == null)
        {
            Debug.LogError("FortunesEdge: assign the blade's Damage component in the Inspector.");
            enabled = false;
            return;
        }
        if (swings == null || swings.Length == 0) swings = DefaultSwings();
        bladeHitbox.rollLuck = true;   // every sword hit rolls the dice
        bladeHitbox.Deactivate();
    }

    protected override void OnEquip()
    {
        if (bladeHitbox != null) bladeHitbox.OnHitTarget += HandleBladeHit;
    }

    protected override void OnUnequip()
    {
        if (bladeHitbox != null)
        {
            bladeHitbox.OnHitTarget -= HandleBladeHit;
            bladeHitbox.Deactivate();
        }
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        isSwinging = false;
        attackBuffered = false;
        comboIndex = 0;
        transform.localRotation = restRotation;
        transform.localPosition = restPosition;
    }

    // ======================================================================
    // Input and combo
    // ======================================================================

    protected override void HandleAttackInput()
    {
        if (!AttackPressedThisFrame()) return;

        if (isSwinging)
        {
            // Queue the next hit if we're near the end of this swing (or in the pause after it).
            if (Time.time >= swingEndsAt - inputBufferTime) attackBuffered = true;
            return;
        }

        if (Time.time < nextSwingAllowedAt) return;
        StartNextSwing();
    }

    private void StartNextSwing()
    {
        // Too long since the last swing? Start the combo over.
        if (Time.time - lastSwingEndTime > comboResetTime) ResetCombo();

        swingRoutine = StartCoroutine(Swing(swings[comboIndex], comboIndex));
    }

    private void ResetCombo()
    {
        comboIndex = 0;
        comboTarget = null;
    }

    // ======================================================================
    // The swing
    // ======================================================================

    private IEnumerator Swing(SwingHit hit, int index)
    {
        isSwinging = true;
        attackBuffered = false;
        cleaveFiredThisSwing = false;
        firstHitDoneThisSwing = false;
        restartComboAfterSwing = false;
        currentSwingIndex = index;
        float speed = Mathf.Max(0.1f, attackSpeedMultiplier);
        bool isFinisher = index == swings.Length - 1;

        OnComboHit?.Invoke(index + 1);

        // Work out the three poses for this hit
        Vector3 slashDir = SlashDirection(hit);
        Vector3 wPos = PosePosition(hit.windup);
        Vector3 iPos = PosePosition(hit.impact);
        Vector3 fPos = PosePosition(hit.followThrough);
        Vector3 wDir = SafeDir(hit.windup.bladeDirection);
        Vector3 iDir = SafeDir(hit.impact.bladeDirection);
        Vector3 fDir = SafeDir(hit.followThrough.bladeDirection);
        Quaternion wRot = PoseRotation(wDir, slashDir);
        Quaternion fRot = PoseRotation(fDir, slashDir);

        // Curve control point so the hand passes exactly through the impact pose at the halfway point
        Vector3 control = 2f * iPos - 0.5f * (wPos + fPos);

        // 1. Wind up (smooth in and out, so it reads as a pull-back)
        Vector3 fromPos = transform.localPosition;
        Quaternion fromRot = transform.localRotation;
        float windup = (windupTime + hit.extraWindup) / speed;
        for (float t = 0f; t < 1f;)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, windup);
            float k = SmoothStep(Mathf.Clamp01(t));
            transform.localPosition = Vector3.Lerp(fromPos, wPos, k);
            transform.localRotation = Quaternion.Slerp(fromRot, wRot, k);
            yield return null;
        }

        // Lunge on the finisher, timed with the cut
        if (isFinisher && Movement != null) Movement.Dash(Owner.transform.forward, lungeSpeed, lungeDuration);

        // 2. The cut: hitbox on, fastest through the middle
        bladeHitbox.Activate(hit.damage);
        float swingDuration = hit.swingTime / speed;
        swingEndsAt = Time.time + swingDuration;
        for (float t = 0f; t < 1f;)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, swingDuration);
            float e = EaseInOutCubic(Mathf.Clamp01(t));

            // Hand along the curve
            float u = 1f - e;
            transform.localPosition = u * u * wPos + 2f * u * e * control + e * e * fPos;

            // Blade turns windup -> impact -> follow-through
            Vector3 dir = e < 0.5f
                ? Vector3.Slerp(wDir, iDir, e * 2f)
                : Vector3.Slerp(iDir, fDir, (e - 0.5f) * 2f);
            transform.localRotation = PoseRotation(dir, slashDir);
            yield return null;
        }
        transform.localPosition = fPos;
        transform.localRotation = fRot;
        bladeHitbox.Deactivate();

        // Advance the combo. Hitting a new enemy counts as hit 1 on them, so the next swing is hit 2.
        comboIndex = restartComboAfterSwing ? 1 % swings.Length : (index + 1) % swings.Length;
        lastSwingEndTime = Time.time;

        // 3. Pause, easing back toward rest. A queued click starts the next hit once the pause is over.
        float pause = (isFinisher ? finisherCooldown : delayBetweenSwings) / speed;
        nextSwingAllowedAt = Time.time + pause;

        float returnDuration = Mathf.Max(pause, recoverTime / speed);
        for (float elapsed = 0f; elapsed < returnDuration;)
        {
            if (attackBuffered && Time.time >= nextSwingAllowedAt)
            {
                swingRoutine = StartCoroutine(Swing(swings[comboIndex], comboIndex));
                yield break;
            }
            elapsed += Time.deltaTime;
            float k = SmoothStep(Mathf.Clamp01(elapsed / returnDuration));
            transform.localPosition = Vector3.Lerp(fPos, restPosition, k);
            transform.localRotation = Quaternion.Slerp(fRot, restRotation, k);
            yield return null;
        }

        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
        isSwinging = false;
        swingRoutine = null;

        // A click that came in right at the end still counts
        if (attackBuffered && Time.time >= nextSwingAllowedAt) StartNextSwing();
    }

    // ======================================================================
    // Pose maths
    // ======================================================================

    private Vector3 PosePosition(SwingPose pose) => RestPositionForMath() + pose.handOffset * swingSize;

    private Vector3 RestPositionForMath() => Application.isPlaying ? restPosition : transform.localPosition;

    /// <summary>The direction the blade tip travels across the whole cut. The edge faces this way.</summary>
    private Vector3 SlashDirection(SwingHit hit)
    {
        Vector3 tipStart = hit.windup.handOffset * swingSize + SafeDir(hit.windup.bladeDirection) * bladeLength;
        Vector3 tipEnd = hit.followThrough.handOffset * swingSize + SafeDir(hit.followThrough.bladeDirection) * bladeLength;
        Vector3 d = tipEnd - tipStart;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.down;
    }

    /// <summary>Rotation that points the blade (the model's up axis) along bladeDir, with the edge facing slashDir.</summary>
    private Quaternion PoseRotation(Vector3 bladeDir, Vector3 slashDir)
    {
        Vector3 edge = Vector3.ProjectOnPlane(slashDir, bladeDir);
        if (edge.sqrMagnitude < 0.0001f) edge = Vector3.ProjectOnPlane(Vector3.forward, bladeDir);
        if (edge.sqrMagnitude < 0.0001f) edge = Vector3.ProjectOnPlane(Vector3.right, bladeDir);

        // LookRotation points +Z down the blade; the 90° X turn makes +Y (the model's blade axis) point there instead.
        return Quaternion.LookRotation(bladeDir, edge) * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, edgeRoll, 0f);
    }

    private static Vector3 SafeDir(Vector3 v) => v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.up;
    private static float SmoothStep(float x) => x * x * (3f - 2f * x);
    private static float EaseInOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    // ======================================================================
    // Hits
    // ======================================================================

    private void HandleBladeHit(Health target, float damageDealt)
    {
        // Only the first enemy a swing touches decides the combo, so sweeping a crowd doesn't reset it.
        if (!firstHitDoneThisSwing)
        {
            firstHitDoneThisSwing = true;

            // Without Let It Ride, the combo only continues on the same enemy.
            if (!letItRide && comboTarget != null && target != comboTarget && currentSwingIndex > 0)
                restartComboAfterSwing = true;

            comboTarget = target;
        }

        // Jackpot on the finisher fires a cleave wave (once per swing)
        bool isFinisher = currentSwingIndex == swings.Length - 1;
        if (isFinisher && !cleaveFiredThisSwing && bladeHitbox.HasRolled && bladeHitbox.CurrentRoll.isJackpot)
        {
            cleaveFiredThisSwing = true;
            FireCleaveWave();
        }
    }

    private void FireCleaveWave()
    {
        Transform player = Owner.transform;
        Vector3 spawnPos = player.position + Vector3.up * 1f + player.forward * 1.5f;
        Quaternion rot = Quaternion.LookRotation(player.forward, Vector3.up);

        GameObject wave = cleaveWavePrefab != null
            ? Instantiate(cleaveWavePrefab, spawnPos, rot)
            : CleaveWave.CreateDefault(spawnPos, rot);

        CleaveWave cw = wave.GetComponent<CleaveWave>();
        if (cw == null) cw = wave.AddComponent<CleaveWave>();
        cw.Launch(Owner, cleaveDamage, cleaveSpeed, cleaveLifetime);
    }

    // ======================================================================
    // Scene view preview: select SwordPivot to see every pose
    // ======================================================================

    void OnDrawGizmosSelected()
    {
        if (swings == null || transform.parent == null) return;
        Transform space = transform.parent;
        Color[] hitColors = { new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.8f, 1f), new Color(1f, 0.35f, 0.35f) };

        for (int h = 0; h < swings.Length; h++)
        {
            SwingHit hit = swings[h];
            if (hit == null) continue;
            Color c = hitColors[h % hitColors.Length];

            Vector3 w = PosePosition(hit.windup), i = PosePosition(hit.impact), f = PosePosition(hit.followThrough);
            Vector3 control = 2f * i - 0.5f * (w + f);

            // Hand path
            Gizmos.color = c;
            Vector3 prev = space.TransformPoint(w);
            for (int s = 1; s <= 16; s++)
            {
                float e = s / 16f, u = 1f - e;
                Vector3 p = space.TransformPoint(u * u * w + 2f * u * e * control + e * e * f);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }

            // Blade at each pose: faint = windup, solid = impact, mid = follow-through
            DrawBlade(space, w, hit.windup.bladeDirection, new Color(c.r, c.g, c.b, 0.35f));
            DrawBlade(space, i, hit.impact.bladeDirection, c);
            DrawBlade(space, f, hit.followThrough.bladeDirection, new Color(c.r, c.g, c.b, 0.65f));
        }
    }

    private void DrawBlade(Transform space, Vector3 handLocal, Vector3 dirLocal, Color color)
    {
        Gizmos.color = color;
        Vector3 hand = space.TransformPoint(handLocal);
        Vector3 tip = space.TransformPoint(handLocal + SafeDir(dirLocal) * bladeLength);
        Gizmos.DrawLine(hand, tip);
        Gizmos.DrawSphere(hand, 0.02f);
    }
}
