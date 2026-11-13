using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Jackpot: slow, heavy war hammer with big sweeping swings that alternate right-to-left and left-to-right,
/// hitting everything in the arc. Each swing rolls the dice. A Jackpot spins 3 god reels:
/// Zeus (chain lightning), Poseidon (shockwave), Hades (lifesteal), Nike (speed), Athena (see your next rolls).
/// The same god on several reels = a stronger effect.
///
/// Swings are animated in code: each is three poses (windup, impact, follow-through) the hammer passes through.
/// Select HammerPivot in the Scene view to see them drawn as coloured lines.
///
/// Setup: Main Camera > HammerPivot (this script, at the hand) > slot hammer model, handle on the pivot,
/// head pointing up at rest > hammer head has a trigger collider + Damage (Target Tag = Enemy).
/// </summary>
public class JackpotHammer : WeaponBase
{
    [Serializable]
    public class SwingPose
    {
        [Tooltip("Where the hand is, relative to rest. X = right, Y = up, Z = forward (from the camera).")]
        public Vector3 handOffset;
        [Tooltip("Which way the hammer points from the handle to the head. (0,1,0) = straight up.")]
        public Vector3 headDirection = Vector3.up;
        public SwingPose() { }
        public SwingPose(Vector3 hand, Vector3 head) { handOffset = hand; headDirection = head; }
    }

    [Serializable]
    public class Sweep
    {
        public string label = "Sweep";
        public float damage = 45f;
        public SwingPose windup = new SwingPose();
        public SwingPose impact = new SwingPose();
        public SwingPose followThrough = new SwingPose();
        [Tooltip("Seconds from windup to follow-through.")]
        public float swingTime = 0.28f;
    }

    [Header("References")]
    [Tooltip("Damage on the hammer head. Trigger collider, Target Tag = Enemy. Roll Luck is turned on for you.")]
    public Damage headHitbox;

    [Header("Sweeps (they alternate)")]
    public Sweep[] sweeps = DefaultSweeps();
    [Tooltip("Scales every hand movement. Above 1 = wider sweeps.")]
    public float swingSize = 1f;
    [Tooltip("Spins the hammer around its handle, if the head's face isn't leading the swing. Try 90, 180 or 270.")]
    public float faceRoll = 0f;
    [Tooltip("Rough hammer length (handle to head) in metres. Used to aim the head and for the gizmos.")]
    public float hammerLength = 0.9f;

    [Header("Timing")]
    [Tooltip("Hauling the hammer back before a sweep. Heavier than the sword.")]
    public float windupTime = 0.3f;
    [Tooltip("Bringing the hammer back to rest.")]
    public float recoverTime = 0.3f;
    [Tooltip("Pause after a sweep before the next one can start.")]
    public float cooldown = 0.2f;
    [Tooltip("If you don't swing again within this many seconds, it starts again from the first sweep.")]
    public float comboResetTime = 1.2f;
    [Tooltip("Clicking within this many seconds of a sweep ending queues the next one.")]
    public float inputBufferTime = 0.25f;

    [Header("Feel")]
    public float swingShake = 0.03f;
    public float hitShake = 0.1f;

    private Quaternion restRotation;
    private Vector3 restPosition;
    private int sweepIndex;
    private bool swinging;
    private bool buffered;
    private float nextAllowed;
    private float lastSweepEnd = -999f;
    private float sweepEndsAt;
    private bool reelsTriggeredThisSwing;
    private bool hitSomethingThisSwing;
    private SlotReels reels;
    private Coroutine swingRoutine;
    private JackpotEffect fx;

    private static Sweep[] DefaultSweeps() => new[]
    {
        // Sweep 1: hauled back over the right shoulder, swept flat through the front, finishing far left
        new Sweep
        {
            label = "1: Right to left", damage = 45f, swingTime = 0.28f,
            windup        = new SwingPose(new Vector3( 0.30f,  0.10f, -0.25f), new Vector3( 0.85f,  0.35f, -0.45f)),
            impact        = new SwingPose(new Vector3(-0.15f, -0.02f,  0.35f), new Vector3(-0.15f,  0.10f,  1.00f)),
            followThrough = new SwingPose(new Vector3(-0.70f, -0.08f,  0.05f), new Vector3(-0.90f,  0.00f,  0.30f)),
        },
        // Sweep 2: backhand. Starts wound across the body on the left, sweeps back to the right
        new Sweep
        {
            label = "2: Left to right", damage = 45f, swingTime = 0.3f,
            windup        = new SwingPose(new Vector3(-0.60f,  0.05f, -0.10f), new Vector3(-0.85f,  0.30f, -0.25f)),
            impact        = new SwingPose(new Vector3(-0.05f, -0.02f,  0.38f), new Vector3( 0.10f,  0.10f,  1.00f)),
            followThrough = new SwingPose(new Vector3( 0.40f, -0.05f,  0.05f), new Vector3( 0.90f,  0.10f,  0.25f)),
        },
    };

    [ContextMenu("Reset sweeps to defaults")]
    private void ResetSweeps()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Reset sweeps");
#endif
        sweeps = DefaultSweeps();
        swingSize = 1f;
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
        reels = GetComponent<SlotReels>();
        if (reels == null) reels = gameObject.AddComponent<SlotReels>();

        // Slot-machine look: red lacquer body, ivory reel windows, gold trim, wooden handle, glowing lettering
        WeaponSkin.AddDefault(gameObject, "Gold",
            new WeaponSkin.Rule("text", "GoldGlow"),
            new WeaponSkin.Rule("Cube", "RedLacquer"),
            new WeaponSkin.Rule("Plane", "Ivory"),
            new WeaponSkin.Rule("Cylinder", "DarkWood"),
            new WeaponSkin.Rule("Torus", "Gold"),
            new WeaponSkin.Rule("Cone", "Gold"));
    }

    void Start()
    {
        fx = FindAnyObjectByType<JackpotEffect>();
        if (sweeps == null || sweeps.Length == 0) sweeps = DefaultSweeps();
        if (headHitbox == null)
        {
            Debug.LogError("JackpotHammer: assign the hammer head's Damage component in the Inspector.");
            enabled = false;
            return;
        }
        headHitbox.rollLuck = true;
        headHitbox.Deactivate();
    }

    protected override void OnEquip()
    {
        if (headHitbox != null) headHitbox.OnHitTarget += HandleHit;
    }

    protected override void OnUnequip()
    {
        if (headHitbox != null)
        {
            headHitbox.OnHitTarget -= HandleHit;
            headHitbox.Deactivate();
        }
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        swinging = false;
        buffered = false;
        sweepIndex = 0;
        transform.localRotation = restRotation;
        transform.localPosition = restPosition;
    }

    // ======================================================================
    // Input
    // ======================================================================

    protected override void HandleAttackInput()
    {
        if (!AttackPressedThisFrame()) return;
        if (swinging)
        {
            if (Time.time >= sweepEndsAt - inputBufferTime) buffered = true;
            return;
        }
        if (Time.time < nextAllowed) return;
        StartSweep();
    }

    private void StartSweep()
    {
        if (Time.time - lastSweepEnd > comboResetTime) sweepIndex = 0;
        swingRoutine = StartCoroutine(SweepRoutine(sweeps[sweepIndex]));
    }

    // ======================================================================
    // The sweep
    // ======================================================================

    private IEnumerator SweepRoutine(Sweep s)
    {
        swinging = true;
        buffered = false;
        reelsTriggeredThisSwing = false;
        hitSomethingThisSwing = false;
        float speed = Mathf.Max(0.1f, attackSpeedMultiplier);

        Vector3 swingDir = SwingDirection(s);
        Vector3 wPos = PosePosition(s.windup), iPos = PosePosition(s.impact), fPos = PosePosition(s.followThrough);
        Vector3 wDir = SafeDir(s.windup.headDirection), iDir = SafeDir(s.impact.headDirection), fDir = SafeDir(s.followThrough.headDirection);
        Quaternion wRot = PoseRotation(wDir, swingDir), fRot = PoseRotation(fDir, swingDir);
        Vector3 control = 2f * iPos - 0.5f * (wPos + fPos);   // curve passes through the impact pose halfway

        // 1. Haul it back (slow, heavy)
        Vector3 fromPos = transform.localPosition;
        Quaternion fromRot = transform.localRotation;
        float windup = windupTime / speed;
        for (float t = 0f; t < 1f;)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, windup);
            float k = SmoothStep(Mathf.Clamp01(t));
            transform.localPosition = Vector3.Lerp(fromPos, wPos, k);
            transform.localRotation = Quaternion.Slerp(fromRot, wRot, k);
            yield return null;
        }

        // 2. The sweep: hitbox on, fastest through the middle
        headHitbox.Activate(s.damage);
        if (fx != null) fx.ShakeOnly(swingShake);
        float duration = s.swingTime / speed;
        sweepEndsAt = Time.time + duration;
        for (float t = 0f; t < 1f;)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float e = EaseInOutCubic(Mathf.Clamp01(t));
            float u = 1f - e;
            transform.localPosition = u * u * wPos + 2f * u * e * control + e * e * fPos;
            Vector3 dir = e < 0.5f ? Vector3.Slerp(wDir, iDir, e * 2f) : Vector3.Slerp(iDir, fDir, (e - 0.5f) * 2f);
            transform.localRotation = PoseRotation(dir, swingDir);
            yield return null;
        }
        transform.localPosition = fPos;
        transform.localRotation = fRot;
        headHitbox.Deactivate();

        sweepIndex = (sweepIndex + 1) % sweeps.Length;
        lastSweepEnd = Time.time;
        nextAllowed = Time.time + cooldown / speed;

        // 3. Ease back toward rest; a queued click starts the next sweep from wherever the hammer is
        float back = Mathf.Max(cooldown, recoverTime) / speed;
        for (float t = 0f; t < back; t += Time.deltaTime)
        {
            if (buffered && Time.time >= nextAllowed)
            {
                swingRoutine = StartCoroutine(SweepRoutine(sweeps[sweepIndex]));
                yield break;
            }
            float k = SmoothStep(Mathf.Clamp01(t / back));
            transform.localPosition = Vector3.Lerp(fPos, restPosition, k);
            transform.localRotation = Quaternion.Slerp(fRot, restRotation, k);
            yield return null;
        }
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
        swinging = false;
        swingRoutine = null;

        if (buffered && Time.time >= nextAllowed) StartSweep();
    }

    // ======================================================================
    // Hits and the god reels
    // ======================================================================

    private void HandleHit(Health target, float dealt)
    {
        if (!hitSomethingThisSwing && fx != null) fx.ShakeOnly(hitShake);
        hitSomethingThisSwing = true;

        if (reelsTriggeredThisSwing || !headHitbox.HasRolled || !headHitbox.CurrentRoll.isJackpot) return;
        reelsTriggeredThisSwing = true;

        bool rigged = BoonManager.Instance != null && BoonManager.Instance.Has("rigged_machine");
        Vector3 hitPoint = GodEffects.Center(target);
        reels.Spin(rigged, gods => ApplyGods(gods, hitPoint));
    }

    private void ApplyGods(God[] gods, Vector3 hitPoint)
    {
        Dictionary<God, int> counts = new Dictionary<God, int>();
        foreach (God g in gods) counts[g] = counts.TryGetValue(g, out int n) ? n + 1 : 1;

        foreach (KeyValuePair<God, int> kv in counts)
            GodEffects.Trigger(kv.Key, kv.Value, hitPoint, Owner);

        if (counts.Count == 1 && GameHUD.Instance != null)
            GameHUD.Instance.ShowBoon($"TRIPLE {GodEffects.Title(gods[0])}!", GodEffects.Blurb(gods[0]) + " x3", 2.5f);
    }

    // ======================================================================
    // Pose maths (same approach as Fortune's Edge)
    // ======================================================================

    private Vector3 RestForMath() => Application.isPlaying ? restPosition : transform.localPosition;
    private Vector3 PosePosition(SwingPose p) => RestForMath() + p.handOffset * swingSize;

    private Vector3 SwingDirection(Sweep s)
    {
        Vector3 a = s.windup.handOffset * swingSize + SafeDir(s.windup.headDirection) * hammerLength;
        Vector3 b = s.followThrough.handOffset * swingSize + SafeDir(s.followThrough.headDirection) * hammerLength;
        Vector3 d = b - a;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.left;
    }

    /// <summary>Points the hammer (model's up axis) along headDir, with the head's face leading in swingDir.</summary>
    private Quaternion PoseRotation(Vector3 headDir, Vector3 swingDir)
    {
        Vector3 face = Vector3.ProjectOnPlane(swingDir, headDir);
        if (face.sqrMagnitude < 0.0001f) face = Vector3.ProjectOnPlane(Vector3.forward, headDir);
        if (face.sqrMagnitude < 0.0001f) face = Vector3.ProjectOnPlane(Vector3.right, headDir);
        return Quaternion.LookRotation(headDir, face) * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, faceRoll, 0f);
    }

    private static Vector3 SafeDir(Vector3 v) => v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.up;
    private static float SmoothStep(float x) => x * x * (3f - 2f * x);
    private static float EaseInOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    // ======================================================================
    // Scene view preview: select HammerPivot to see the sweeps
    // ======================================================================

    void OnDrawGizmosSelected()
    {
        if (sweeps == null || transform.parent == null) return;
        Transform space = transform.parent;
        Color[] colors = { new Color(1f, 0.6f, 0.2f), new Color(0.4f, 0.9f, 0.5f) };

        for (int h = 0; h < sweeps.Length; h++)
        {
            Sweep s = sweeps[h];
            if (s == null) continue;
            Color c = colors[h % colors.Length];
            Vector3 w = PosePosition(s.windup), i = PosePosition(s.impact), f = PosePosition(s.followThrough);
            Vector3 control = 2f * i - 0.5f * (w + f);

            Gizmos.color = c;
            Vector3 prev = space.TransformPoint(w);
            for (int k = 1; k <= 16; k++)
            {
                float e = k / 16f, u = 1f - e;
                Vector3 p = space.TransformPoint(u * u * w + 2f * u * e * control + e * e * f);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            DrawHammer(space, w, s.windup.headDirection, new Color(c.r, c.g, c.b, 0.35f));
            DrawHammer(space, i, s.impact.headDirection, c);
            DrawHammer(space, f, s.followThrough.headDirection, new Color(c.r, c.g, c.b, 0.65f));
        }
    }

    private void DrawHammer(Transform space, Vector3 handLocal, Vector3 dirLocal, Color color)
    {
        Gizmos.color = color;
        Vector3 hand = space.TransformPoint(handLocal);
        Vector3 head = space.TransformPoint(handLocal + SafeDir(dirLocal) * hammerLength);
        Gizmos.DrawLine(hand, head);
        Gizmos.DrawWireCube(head, Vector3.one * 0.08f);
    }
}
