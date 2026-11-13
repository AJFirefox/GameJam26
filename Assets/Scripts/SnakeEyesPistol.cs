using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Snake Eyes: hitscan revolver. Every shot that hits rolls the dice.
/// Snake eyes (double 1s) turns the enemy to stone: frozen, grey, and taking double damage.
/// With Medusa's Gaze, any double petrifies.
///
/// Setup: Main Camera > PistolPivot (this script, at the hand) > Kimber model.
/// Optional: an empty child at the barrel tip dragged into Muzzle.
/// Fire = left click / right trigger. Reload = R / gamepad Y. Reloads automatically when empty.
/// </summary>
public class SnakeEyesPistol : WeaponBase
{
    [Header("References")]
    [Tooltip("Empty at the end of the barrel. Empty = a little in front of the pivot.")]
    public Transform muzzle;

    [Header("Gun")]
    public float damage = 18f;
    [Tooltip("Seconds between shots.")]
    public float fireInterval = 0.22f;
    public int magazineSize = 6;
    public float reloadTime = 1.2f;
    public float range = 120f;

    [Header("Stone")]
    public float stoneDuration = 3f;

    [Header("Feel")]
    public float recoilAngle = 14f;
    public float recoilKick = 0.08f;
    public float recoilRecoverSpeed = 9f;
    public float reloadTilt = 40f;
    public Color tracerColor = new Color(1f, 0.85f, 0.45f);
    public Color stoneColor = new Color(0.7f, 0.7f, 0.65f);

    public int Rounds => rounds;
    public bool IsReloading => reloading;

    private int rounds;
    private bool reloading;
    private float nextShot;
    private float recoil;          // 0..1, eases back to 0
    private float tilt;            // reload tilt angle
    private Quaternion restRotation;
    private Vector3 restPosition;
    private Coroutine reloadRoutine;

    protected override void Awake()
    {
        base.Awake();
        restRotation = transform.localRotation;
        restPosition = transform.localPosition;
        rounds = magazineSize;

        // Dark gunmetal revolver
        WeaponSkin.AddDefault(gameObject, "Gunmetal");
    }

    protected override void OnEquip()
    {
        UpdateAmmoHUD();
    }

    protected override void OnUnequip()
    {
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloading = false;
        recoil = 0f;
        tilt = 0f;
        transform.localRotation = restRotation;
        transform.localPosition = restPosition;
    }

    protected override void HandleAttackInput()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        bool reloadPressed = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);

        if (reloading) return;
        if (reloadPressed && rounds < magazineSize) { StartReload(); return; }
        if (!AttackPressedThisFrame() || Time.time < nextShot) return;

        if (rounds <= 0) { StartReload(); return; }
        Fire();
    }

    void LateUpdate()
    {
        recoil = Mathf.MoveTowards(recoil, 0f, recoilRecoverSpeed * Time.deltaTime);
        transform.localRotation = restRotation * Quaternion.Euler(-recoilAngle * recoil + tilt, 0f, 0f);
        transform.localPosition = restPosition + Vector3.back * recoilKick * recoil;
    }

    private void Fire()
    {
        rounds--;
        nextShot = Time.time + fireInterval / Mathf.Max(0.1f, attackSpeedMultiplier);
        recoil = 1f;

        Camera cam = Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : transform.position;
        Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
        Vector3 muzzlePos = muzzle != null ? muzzle.position : transform.position + transform.forward * 0.35f;

        // First thing in the way that isn't us
        Vector3 end = origin + dir * range;
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == Owner.transform) continue;
            end = hit.point;
            HitSomething(hit);
            break;
        }

        EffectVisual.Tracer(muzzlePos, end, tracerColor, 0.06f, 0.03f);
        EffectVisual.LightBlip(muzzlePos, tracerColor, 4f, 6f, 0.05f);

        UpdateAmmoHUD();
        if (rounds <= 0) StartReload();
    }

    private void HitSomething(RaycastHit hit)
    {
        Health h = hit.collider.GetComponentInParent<Health>();
        if (h == null || h.IsDead || !h.CompareTag("Enemy"))
        {
            EffectVisual.Flash(hit.point, 0.15f, tracerColor, 0.12f, false);
            return;
        }

        RollResult roll = RollLuck();
        h.TakeDamage(damage * roll.multiplier, Owner);

        bool medusa = BoonManager.Instance != null && BoonManager.Instance.Has("medusa_gaze");
        if (roll.isSnakeEyes || (medusa && roll.isDouble))
        {
            if (StoneStatus.Apply(h.gameObject, stoneDuration))
                EffectVisual.Flash(GodEffects.Center(h), 1f, stoneColor, 0.35f, false);
        }
        else
        {
            EffectVisual.Flash(hit.point, roll.isJackpot ? 0.6f : 0.25f, roll.isJackpot ? UIBuilder.Gold : tracerColor, 0.15f, false);
        }
    }

    private void StartReload()
    {
        if (reloading || rounds >= magazineSize) return;
        reloadRoutine = StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        reloading = true;
        UpdateAmmoHUD();
        float duration = reloadTime / Mathf.Max(0.1f, attackSpeedMultiplier);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // Tip the gun down, then back up
            tilt = Mathf.Sin(t / duration * Mathf.PI) * reloadTilt;
            yield return null;
        }
        tilt = 0f;
        rounds = magazineSize;
        reloading = false;
        reloadRoutine = null;
        UpdateAmmoHUD();
    }

    private void UpdateAmmoHUD()
    {
        if (GameHUD.Instance == null) return;
        GameHUD.Instance.SetAmmo(reloading ? "RELOADING" : $"{rounds} / {magazineSize}");
    }
}
