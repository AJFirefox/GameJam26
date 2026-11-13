using UnityEngine;

/// <summary>
/// A floating pickup dropped by Jackpot coins. Walk into it to collect.
///   Health: heals the player.   Coins: refills Heads or Tails.
/// Created in code (Pickup.Spawn). No prefab needed.
/// </summary>
public class Pickup : MonoBehaviour
{
    public enum Kind { Health, Coins }

    public Kind kind;
    public float amount;
    public float collectRange = 1.6f;
    public float lifetime = 20f;

    private Transform player;
    private Vector3 basePos;
    private float spawnTime;

    private static readonly Color HealthColor = new Color(0.35f, 1f, 0.45f);
    private static readonly Color CoinColor = new Color(1f, 0.8f, 0.25f);

    /// <summary>50/50 coins or health (always health if the player has no coin launcher).</summary>
    public static Pickup SpawnRandom(Vector3 position, bool coinsAllowed)
    {
        bool coins = coinsAllowed && Random.value < 0.5f;
        return Spawn(position, coins ? Kind.Coins : Kind.Health, coins ? 4f : 15f);
    }

    public static Pickup Spawn(Vector3 position, Kind kind, float amount)
    {
        // Settle onto the floor below
        if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point + Vector3.up * 0.6f;

        GameObject go = GameObject.CreatePrimitive(kind == Kind.Health ? PrimitiveType.Cube : PrimitiveType.Cylinder);
        go.name = kind == Kind.Health ? "Health Pickup" : "Coin Pickup";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = kind == Kind.Health ? Vector3.one * 0.35f : new Vector3(0.5f, 0.05f, 0.5f);

        Color c = kind == Kind.Health ? HealthColor : CoinColor;
        Material m = go.GetComponent<Renderer>().material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.5f); }

        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.range = 3f;
        l.intensity = 2f;

        Pickup p = go.AddComponent<Pickup>();
        p.kind = kind;
        p.amount = amount;
        return p;
    }

    void Start()
    {
        GameObject pl = GameObject.FindGameObjectWithTag("Player");
        if (pl != null) player = pl.transform;
        basePos = transform.position;
        spawnTime = Time.time;
    }

    void Update()
    {
        float age = Time.time - spawnTime;
        transform.position = basePos + Vector3.up * Mathf.Sin(age * 3f) * 0.15f;
        transform.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);

        if (age > lifetime) { Destroy(gameObject); return; }
        if (player == null) return;

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.magnitude <= collectRange) Collect();
    }

    private void Collect()
    {
        if (kind == Kind.Health)
        {
            Health h = player.GetComponent<Health>();
            if (h != null) h.Heal(amount);
        }
        else
        {
            HeadsOrTails launcher = player.GetComponentInChildren<HeadsOrTails>(true);
            if (launcher != null) launcher.AddCoins(Mathf.RoundToInt(amount));
        }
        EffectVisual.Flash(transform.position, 0.6f, kind == Kind.Health ? HealthColor : CoinColor, 0.25f, false);
        Destroy(gameObject);
    }
}
