using UnityEngine;

/// <summary>
/// A boulder die thrown by the Cyclops. Damages the player on contact and breaks on anything solid.
/// (Later: explode for its die face value, per the GDD.)
/// </summary>
public class Boulder : MonoBehaviour
{
    [Tooltip("Used if the thrower doesn't set it.")]
    public float damage = 15f;
    [Tooltip("Seconds before the boulder is cleaned up if it never hits anything.")]
    public float lifetime = 6f;

    private GameObject thrower;

    /// <summary>Called by the Cyclops when it throws this.</summary>
    public void Init(GameObject thrownBy, float boulderDamage)
    {
        thrower = thrownBy;
        damage = boulderDamage;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;                                   // ignore other hitboxes
        if (thrower != null && other.transform.root == thrower.transform.root) return;  // don't hit the Cyclops
        if (other.GetComponentInParent<CommonEnemy>() != null) return;  // fly past other enemies

        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            Health h = other.GetComponentInParent<Health>();
            if (h != null) h.TakeDamage(damage, thrower);
        }

        // Breaks on the player, the floor or walls
        Destroy(gameObject);
    }
}
