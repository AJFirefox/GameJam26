using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A coin fired by Heads or Tails. Flies in an arc; when it hits something it rolls the dice once:
/// heads (even) explodes, tails (odd) ricochets between enemies. A Jackpot also drops a pickup.
/// Created in code by HeadsOrTails, no prefab needed.
/// </summary>
public class CoinProjectile : MonoBehaviour
{
    public float gravity = 14f;
    public float radius = 0.2f;
    public float lifetime = 5f;
    public float ricochetSpeed = 35f;

    private GameObject owner;
    private HeadsOrTails launcher;
    private Vector3 velocity;
    private float damage;
    private float dieAt;
    private bool rolled;
    private RollResult roll;
    private bool heads;
    private int ricochets;
    private Health homingTarget;
    private Transform visual;
    private readonly HashSet<Health> alreadyHit = new HashSet<Health>();

    private static readonly Color HeadsColor = new Color(1f, 0.8f, 0.25f);
    private static readonly Color TailsColor = new Color(0.85f, 0.85f, 0.9f);

    public static CoinProjectile Launch(GameObject owner, Vector3 position, Vector3 velocity, float damage,
                                        HeadsOrTails launcher, GameObject model, float modelScale)
    {
        GameObject go = new GameObject("Coin");
        go.transform.position = position;
        CoinProjectile c = go.AddComponent<CoinProjectile>();
        c.owner = owner;
        c.launcher = launcher;
        c.velocity = velocity;
        c.damage = damage;
        c.dieAt = Time.time + c.lifetime;
        c.BuildVisual(model, modelScale);
        return c;
    }

    void Update()
    {
        if (Time.time >= dieAt) { Destroy(gameObject); return; }

        // Ricocheting: fly straight at the target
        if (homingTarget != null)
        {
            if (homingTarget.IsDead) homingTarget = null;
            else velocity = (GodEffects.Center(homingTarget) - transform.position).normalized * ricochetSpeed;
        }
        if (homingTarget == null) velocity += Vector3.down * gravity * Time.deltaTime;

        Vector3 step = velocity * Time.deltaTime;
        if (TryHit(step, out RaycastHit hit))
        {
            transform.position = hit.point;
            Impact(hit.collider, hit.point);
        }
        else transform.position += step;

        if (visual != null) visual.Rotate(Vector3.right, 1080f * Time.deltaTime, Space.Self);
    }

    private bool TryHit(Vector3 step, out RaycastHit best)
    {
        best = default;
        float dist = step.magnitude;
        if (dist < 0.0001f) return false;

        RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, step / dist, dist, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            if (owner != null && h.collider.transform.root == owner.transform) continue;
            Health hp = h.collider.GetComponentInParent<Health>();
            if (hp != null && alreadyHit.Contains(hp)) continue;   // don't re-hit the enemy we just bounced off
            if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
        }
        return found;
    }

    private void Impact(Collider col, Vector3 point)
    {
        Health enemy = col.GetComponentInParent<Health>();
        if (enemy != null && (enemy.IsDead || !enemy.CompareTag("Enemy"))) enemy = null;

        // One roll per coin, on its first impact
        if (!rolled)
        {
            rolled = true;
            roll = LuckSystem.Instance != null ? LuckSystem.Instance.Roll()
                                               : new RollResult { multiplier = 1f, total = 7, tier = RollTier.Normal };
            bool twoHeaded = BoonManager.Instance != null && BoonManager.Instance.Has("two_headed_coin");
            heads = twoHeaded || roll.total % 2 == 0;
            if (roll.isJackpot) Pickup.SpawnRandom(point + Vector3.up * 0.5f, launcher != null);
        }

        float amount = damage * roll.multiplier;

        if (heads)
        {
            Explode(point, amount);
            Destroy(gameObject);
            return;
        }

        // Tails: hurt what we hit, then bounce to the next enemy
        if (enemy != null)
        {
            enemy.TakeDamage(amount, owner);
            alreadyHit.Add(enemy);
            EffectVisual.Flash(point, 0.35f, TailsColor, 0.15f, false);
        }

        Health next = ricochets < GetMaxRicochets() ? GodEffects.NearestEnemy(point, GetRicochetRange(), alreadyHit) : null;
        if (next == null)
        {
            EffectVisual.Flash(point, 0.25f, TailsColor, 0.15f, false);
            Destroy(gameObject);
            return;
        }

        ricochets++;
        homingTarget = next;
        transform.position = point + (GodEffects.Center(next) - point).normalized * 0.3f;
        dieAt = Time.time + 2f;
    }

    private void Explode(Vector3 point, float amount)
    {
        float r = launcher != null ? launcher.explosionRadius : 4f;
        EffectVisual.Flash(point, r * 0.6f, HeadsColor, 0.35f, true);
        JackpotEffect fx = FindAnyObjectByType<JackpotEffect>();
        if (fx != null) fx.ShakeOnly(0.08f);
        foreach (Health h in GodEffects.EnemiesInRadius(point, r)) h.TakeDamage(amount, owner);
    }

    private int GetMaxRicochets() => launcher != null ? launcher.maxRicochets : 3;
    private float GetRicochetRange() => launcher != null ? launcher.ricochetRange : 14f;

    private void BuildVisual(GameObject model, float scale)
    {
        GameObject v;
        if (model != null)
        {
            v = Instantiate(model);
            foreach (Collider c in v.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Rigidbody rb in v.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            v.transform.localScale *= scale;
            WeaponSkin.ApplyAll(v, "Gold");
        }
        else
        {
            v = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(v.GetComponent<Collider>());
            v.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
            Material m = v.GetComponent<Renderer>().material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", HeadsColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", HeadsColor);
        }
        v.transform.SetParent(transform, false);
        v.transform.localPosition = Vector3.zero;
        visual = v.transform;
    }
}
