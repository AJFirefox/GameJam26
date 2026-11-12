using System.Collections.Generic;
using UnityEngine;

public enum God { Zeus, Poseidon, Hades, Nike, Athena }

/// <summary>
/// What each god does when they land on The Jackpot's reels.
/// Landing the same god on more than one reel makes their effect stronger.
/// </summary>
public static class GodEffects
{
    // ---------- Tuning ----------
    public static float ZeusDamage = 30f;
    public static int ZeusJumps = 4;
    public static float ZeusJumpRange = 10f;

    public static float PoseidonDamage = 35f;
    public static float PoseidonRadius = 6f;
    public static float PoseidonKnockback = 14f;

    public static float HadesLifesteal = 0.3f;
    public static float HadesDuration = 6f;

    public static float NikeSpeed = 1.4f;
    public static float NikeDuration = 6f;

    public static float AthenaDuration = 10f;

    public static readonly God[] All = { God.Zeus, God.Poseidon, God.Hades, God.Nike, God.Athena };

    public static string Title(God g)
    {
        switch (g)
        {
            case God.Zeus: return "ZEUS";
            case God.Poseidon: return "POSEIDON";
            case God.Hades: return "HADES";
            case God.Nike: return "NIKE";
            default: return "ATHENA";
        }
    }

    public static string Blurb(God g)
    {
        switch (g)
        {
            case God.Zeus: return "Chain Lightning";
            case God.Poseidon: return "Shockwave";
            case God.Hades: return "Lifesteal";
            case God.Nike: return "Swiftness";
            default: return "Foresight";
        }
    }

    public static Color Colour(God g)
    {
        switch (g)
        {
            case God.Zeus: return new Color(0.55f, 0.85f, 1f);
            case God.Poseidon: return new Color(0.2f, 0.6f, 1f);
            case God.Hades: return new Color(0.75f, 0.3f, 1f);
            case God.Nike: return new Color(1f, 0.95f, 0.5f);
            default: return new Color(0.9f, 0.9f, 0.75f);
        }
    }

    /// <summary>Runs a god's effect. count = how many reels landed on them (1-3).</summary>
    public static void Trigger(God god, int count, Vector3 hitPoint, GameObject owner)
    {
        PlayerBuffs buffs = PlayerBuffs.Get(owner);
        switch (god)
        {
            case God.Zeus:
                ChainLightning(hitPoint, ZeusDamage * count, ZeusJumps + (count - 1) * 2, owner);
                break;
            case God.Poseidon:
                Vector3 center = owner != null ? owner.transform.position : hitPoint;
                Shockwave(center, PoseidonRadius + (count - 1) * 2f, PoseidonDamage * count, owner);
                break;
            case God.Hades:
                if (buffs != null) buffs.GrantLifesteal(HadesLifesteal, HadesDuration * count);
                EffectVisual.Flash(hitPoint, 1.2f, Colour(God.Hades), 0.4f);
                break;
            case God.Nike:
                if (buffs != null) buffs.GrantNike(NikeSpeed + 0.1f * (count - 1), NikeDuration * count);
                break;
            case God.Athena:
                if (buffs != null) buffs.GrantForesight(AthenaDuration * count);
                break;
        }
    }

    // ---------- Effects ----------

    public static void ChainLightning(Vector3 start, float damage, int jumps, GameObject owner)
    {
        HashSet<Health> hit = new HashSet<Health>();
        Vector3 from = start + Vector3.up * 0.5f;
        Vector3 searchFrom = start;
        Color c = Colour(God.Zeus);

        for (int i = 0; i <= jumps; i++)
        {
            Health next = NearestEnemy(searchFrom, ZeusJumpRange, hit);
            if (next == null) break;
            hit.Add(next);
            Vector3 to = Center(next);
            EffectVisual.Bolt(from, to, c, 0.3f, 0.14f);
            next.TakeDamage(damage, owner);
            from = to;
            searchFrom = next.transform.position;
        }
        // Strike from the sky at the start, for drama
        EffectVisual.Bolt(start + Vector3.up * 12f, start + Vector3.up * 0.2f, c, 0.25f, 0.2f);
    }

    public static void Shockwave(Vector3 center, float radius, float damage, GameObject owner)
    {
        EffectVisual.Ring(center, radius, new Color(0.3f, 0.65f, 1f, 0.7f), 0.45f);
        JackpotEffect fx = Object.FindAnyObjectByType<JackpotEffect>();
        if (fx != null) fx.ShakeOnly(0.2f);

        foreach (Health h in EnemiesInRadius(center, radius))
        {
            h.TakeDamage(damage, owner);
            CommonEnemy ce = h.GetComponent<CommonEnemy>();
            if (ce != null)
            {
                Vector3 away = h.transform.position - center;
                away.y = 0f;
                ce.Knockback(away.normalized * PoseidonKnockback, 0.35f);
            }
        }
    }

    // ---------- Finding enemies ----------

    /// <summary>Every living enemy (tagged Enemy, with Health) within radius.</summary>
    public static List<Health> EnemiesInRadius(Vector3 center, float radius)
    {
        List<Health> result = new List<Health>();
        foreach (Collider c in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            Health h = c.GetComponentInParent<Health>();
            if (h == null || h.IsDead || result.Contains(h)) continue;
            if (!h.CompareTag("Enemy")) continue;
            result.Add(h);
        }
        return result;
    }

    public static Health NearestEnemy(Vector3 from, float range, ICollection<Health> exclude = null)
    {
        Health best = null;
        float bestDist = float.MaxValue;
        foreach (Health h in EnemiesInRadius(from, range))
        {
            if (exclude != null && exclude.Contains(h)) continue;
            float d = (h.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }

    /// <summary>Roughly the middle of an enemy's body.</summary>
    public static Vector3 Center(Health h)
    {
        Collider c = h.GetComponent<Collider>();
        return c != null ? c.bounds.center : h.transform.position + Vector3.up;
    }
}
