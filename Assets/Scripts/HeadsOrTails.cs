using UnityEngine;

/// <summary>
/// Heads or Tails: coin launcher. Fires arcing coins; each coin rolls the dice when it lands.
///   Even total = HEADS: the coin explodes, hurting everything nearby.
///   Odd total  = TAILS: the coin ricochets from enemy to enemy (up to 3 bounces).
///   Jackpot    = also drops a pickup (coins or health).
/// Coins slowly refill on their own, and coin pickups top them up.
///
/// Setup: Main Camera > CoinPivot (this script, at the hand) > coin model as the held look.
/// Drag the Coin model asset into Coin Model too, so fired coins use it.
/// </summary>
public class HeadsOrTails : WeaponBase
{
    [Header("References")]
    [Tooltip("Where coins leave from. Empty = a little in front of the pivot.")]
    public Transform launchPoint;
    [Tooltip("Model used for fired coins (the Coin model asset). Empty = a simple gold disc.")]
    public GameObject coinModel;
    public float coinModelScale = 1f;

    [Header("Launcher")]
    public float damage = 22f;
    public int maxCoins = 12;
    [Tooltip("Seconds per coin refilled on its own (while held).")]
    public float regenInterval = 2.5f;
    public float fireInterval = 0.35f;
    public float launchSpeed = 28f;
    [Tooltip("Aims a bit above the crosshair so the arc lands near it.")]
    public float upwardArc = 0.08f;

    [Header("Coins")]
    public float explosionRadius = 4f;
    public int maxRicochets = 3;
    public float ricochetRange = 14f;

    [Header("Feel")]
    public float recoilAngle = 10f;

    public int Coins => coins;

    private int coins;
    private float nextShot;
    private float regenTimer;
    private float recoil;
    private Quaternion restRotation;

    protected override void Awake()
    {
        base.Awake();
        coins = maxCoins;
        restRotation = transform.localRotation;

        // Solid gold coin
        WeaponSkin.AddDefault(gameObject, "Gold");
    }

    protected override void OnEquip() => UpdateAmmoHUD();

    protected override void OnUnequip()
    {
        recoil = 0f;
        transform.localRotation = restRotation;
    }

    protected override void Update()
    {
        base.Update();

        if (coins < maxCoins && Time.timeScale > 0f)
        {
            regenTimer += Time.deltaTime;
            if (regenTimer >= regenInterval)
            {
                regenTimer = 0f;
                AddCoins(1);
            }
        }
        else regenTimer = 0f;
    }

    void LateUpdate()
    {
        recoil = Mathf.MoveTowards(recoil, 0f, 8f * Time.deltaTime);
        transform.localRotation = restRotation * Quaternion.Euler(-recoilAngle * recoil, 0f, recoil * 8f);
    }

    protected override void HandleAttackInput()
    {
        if (!AttackPressedThisFrame() || Time.time < nextShot) return;
        if (coins <= 0) return;
        Fire();
    }

    public void AddCoins(int amount)
    {
        coins = Mathf.Clamp(coins + amount, 0, maxCoins);
        if (isActiveAndEnabled) UpdateAmmoHUD();
    }

    private void Fire()
    {
        coins--;
        nextShot = Time.time + fireInterval / Mathf.Max(0.1f, attackSpeedMultiplier);
        recoil = 1f;

        Camera cam = Camera.main;
        Vector3 aim = cam != null ? cam.transform.forward : transform.forward;
        aim = (aim + Vector3.up * upwardArc).normalized;
        Vector3 start = launchPoint != null ? launchPoint.position
                      : (cam != null ? cam.transform.position + cam.transform.forward * 0.6f + cam.transform.right * 0.2f - cam.transform.up * 0.15f
                                     : transform.position + transform.forward * 0.4f);

        CoinProjectile.Launch(Owner, start, aim * launchSpeed, damage, this, coinModel, coinModelScale);
        UpdateAmmoHUD();
    }

    private void UpdateAmmoHUD()
    {
        if (GameHUD.Instance != null) GameHUD.Instance.SetAmmo($"{coins} coins");
    }
}
