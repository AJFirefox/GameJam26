using UnityEngine;

/// <summary>
/// The wave Fortune's Edge fires on a hit-3 Jackpot. Flies forward, damages every enemy it passes through once.
/// Works with a prefab (trigger collider + this script) or builds a simple golden one in code.
/// </summary>
public class CleaveWave : MonoBehaviour
{
    private float speed;
    private float dieAt;

    public void Launch(GameObject owner, float damage, float speed, float lifetime)
    {
        this.speed = speed;
        dieAt = Time.time + lifetime;

        Damage dmg = GetComponent<Damage>();
        if (dmg == null) dmg = gameObject.AddComponent<Damage>();
        dmg.owner = owner;
        dmg.targetTag = "Enemy";
        dmg.rollLuck = false;   // it already came from a Jackpot
        dmg.Activate(damage);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
        if (Time.time >= dieAt) Destroy(gameObject);
    }

    /// <summary>A flat golden slab with a trigger collider. Used when no prefab is assigned.</summary>
    public static GameObject CreateDefault(Vector3 position, Quaternion rotation)
    {
        GameObject wave = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wave.name = "CleaveWave";
        wave.transform.SetPositionAndRotation(position, rotation);
        wave.transform.localScale = new Vector3(4f, 0.3f, 0.6f);

        wave.GetComponent<Collider>().isTrigger = true;

        Rigidbody rb = wave.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Renderer r = wave.GetComponent<Renderer>();
        Color gold = new Color(1f, 0.8f, 0.2f);
        if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", gold); // URP
        if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", gold);         // Built-in

        return wave;
    }
}
