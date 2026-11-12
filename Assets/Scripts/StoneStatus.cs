using UnityEngine;

/// <summary>
/// Turned to stone by Snake Eyes: can't move or attack, looks grey, and takes double damage.
/// Use StoneStatus.Apply(enemy, seconds). Added and removed automatically.
/// </summary>
public class StoneStatus : MonoBehaviour
{
    public static float DamageMultiplier = 2f;
    private static readonly Color StoneColor = new Color(0.55f, 0.55f, 0.52f);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private float until;
    private Health health;
    private CommonEnemy enemy;
    private Animator animator;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private float savedAnimatorSpeed = 1f;

    /// <summary>Petrifies an enemy for `seconds` (or extends it). Returns false if it can't be petrified.</summary>
    public static bool Apply(GameObject target, float seconds)
    {
        if (target == null) return false;
        CommonEnemy ce = target.GetComponentInParent<CommonEnemy>();
        if (ce == null) return false;
        Health h = ce.GetComponent<Health>();
        if (h != null && h.IsDead) return false;

        StoneStatus s = ce.GetComponent<StoneStatus>();
        if (s == null) s = ce.gameObject.AddComponent<StoneStatus>();
        s.until = Mathf.Max(s.until, Time.time + seconds);
        return true;
    }

    public static bool IsStone(GameObject target)
    {
        StoneStatus s = target != null ? target.GetComponentInParent<StoneStatus>() : null;
        return s != null && s.enabled;
    }

    void Awake()
    {
        enemy = GetComponent<CommonEnemy>();
        health = GetComponent<Health>();
        animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (enemy != null) enemy.SetPetrified(true);
        if (health != null)
        {
            health.damageTakenMultiplier *= DamageMultiplier;
            health.OnDied += HandleDied;
        }
        if (animator != null) { savedAnimatorSpeed = animator.speed; animator.speed = 0f; }
        SetTint(true);
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.damageTakenMultiplier /= DamageMultiplier;
            health.OnDied -= HandleDied;
        }
        if (enemy != null) enemy.SetPetrified(false);
        if (animator != null) animator.speed = savedAnimatorSpeed;
        SetTint(false);
    }

    void Update()
    {
        if (Time.time >= until) Destroy(this);
    }

    private void HandleDied(GameObject killer) => Destroy(this);

    private void SetTint(bool on)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!on) { r.SetPropertyBlock(null); continue; }
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, StoneColor);   // URP
            block.SetColor(ColorId, StoneColor);       // Built-in
            r.SetPropertyBlock(block);
        }
    }
}
