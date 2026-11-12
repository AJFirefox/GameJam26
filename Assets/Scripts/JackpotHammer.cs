using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Jackpot: slow, heavy war hammer. Big overhead slam that rolls the dice.
/// A Jackpot slam spins 3 god reels: Zeus (chain lightning), Poseidon (shockwave), Hades (lifesteal),
/// Nike (speed) and Athena (see your next rolls). The same god on several reels = a stronger effect.
///
/// Setup: Main Camera > HammerPivot (this script, at the hand) > slot hammer model, handle on the pivot,
/// head pointing up at rest > hammer head has a trigger collider + Damage (Target Tag = Enemy).
/// </summary>
public class JackpotHammer : WeaponBase
{
    [Header("References")]
    [Tooltip("Damage on the hammer head. Trigger collider, Target Tag = Enemy. Roll Luck is turned on for you.")]
    public Damage headHitbox;

    [Header("Slam")]
    public float damage = 45f;
    [Tooltip("Raising the hammer overhead.")]
    public float windupTime = 0.35f;
    [Tooltip("The slam itself.")]
    public float slamTime = 0.13f;
    [Tooltip("Hammer stays down for a moment after landing.")]
    public float impactHold = 0.12f;
    public float recoverTime = 0.35f;
    [Tooltip("Pause after a slam before the next one can start.")]
    public float cooldown = 0.2f;

    [Header("Poses (pivot rotation / position offset)")]
    public Vector3 windupRotation = new Vector3(-75f, 15f, 20f);
    public Vector3 windupOffset = new Vector3(0.05f, 0.2f, -0.1f);
    public Vector3 slamRotation = new Vector3(75f, -10f, -10f);
    public Vector3 slamOffset = new Vector3(-0.15f, -0.1f, 0.35f);

    [Header("Feel")]
    public float impactShake = 0.06f;
    public float hitShake = 0.12f;

    private Quaternion restRotation;
    private Vector3 restPosition;
    private bool swinging;
    private bool buffered;
    private float nextAllowed;
    private bool reelsTriggeredThisSwing;
    private bool hitSomethingThisSwing;
    private Vector3 lastHitPoint;
    private SlotReels reels;
    private Coroutine swingRoutine;
    private JackpotEffect fx;

    protected override void Awake()
    {
        base.Awake();
        restRotation = transform.localRotation;
        restPosition = transform.localPosition;
        reels = GetComponent<SlotReels>();
        if (reels == null) reels = gameObject.AddComponent<SlotReels>();
    }

    void Start()
    {
        fx = FindAnyObjectByType<JackpotEffect>();
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
        transform.localRotation = restRotation;
        transform.localPosition = restPosition;
    }

    protected override void HandleAttackInput()
    {
        if (!AttackPressedThisFrame()) return;
        if (swinging) { buffered = true; return; }
        if (Time.time < nextAllowed) return;
        swingRoutine = StartCoroutine(Slam());
    }

    private IEnumerator Slam()
    {
        swinging = true;
        buffered = false;
        reelsTriggeredThisSwing = false;
        hitSomethingThisSwing = false;
        float speed = Mathf.Max(0.1f, attackSpeedMultiplier);

        Quaternion wRot = Quaternion.Euler(windupRotation);
        Quaternion sRot = Quaternion.Euler(slamRotation);
        Vector3 wPos = restPosition + windupOffset;
        Vector3 sPos = restPosition + slamOffset;

        // Raise overhead
        yield return Move(transform.localPosition, wPos, transform.localRotation, wRot, windupTime / speed, Ease.InOut);

        // Slam down, hitbox on
        headHitbox.Activate(damage);
        yield return Move(wPos, sPos, wRot, sRot, slamTime / speed, Ease.In);

        // Impact
        if (fx != null) fx.ShakeOnly(hitSomethingThisSwing ? hitShake : impactShake);
        yield return new WaitForSeconds(impactHold / speed);
        headHitbox.Deactivate();

        // Lift back to rest
        yield return Move(sPos, restPosition, sRot, restRotation, recoverTime / speed, Ease.InOut);

        nextAllowed = Time.time + cooldown / speed;
        swinging = false;
        swingRoutine = null;

        if (buffered)
        {
            buffered = false;
            yield return new WaitForSeconds(cooldown / speed);
            if (isActiveAndEnabled && !swinging) swingRoutine = StartCoroutine(Slam());
        }
    }

    private enum Ease { In, InOut }

    private IEnumerator Move(Vector3 p0, Vector3 p1, Quaternion r0, Quaternion r1, float duration, Ease ease)
    {
        for (float t = 0f; t < 1f;)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float k = Mathf.Clamp01(t);
            k = ease == Ease.In ? k * k * k : k * k * (3f - 2f * k);
            transform.localPosition = Vector3.Lerp(p0, p1, k);
            transform.localRotation = Quaternion.Slerp(r0, r1, k);
            yield return null;
        }
    }

    private void HandleHit(Health target, float dealt)
    {
        hitSomethingThisSwing = true;
        lastHitPoint = GodEffects.Center(target);

        if (reelsTriggeredThisSwing || !headHitbox.HasRolled || !headHitbox.CurrentRoll.isJackpot) return;
        reelsTriggeredThisSwing = true;

        bool rigged = BoonManager.Instance != null && BoonManager.Instance.Has("rigged_machine");
        Vector3 hitPoint = lastHitPoint;
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
}
