using UnityEngine;

/// <summary>
/// Quick placeholder visuals made in code: lightning bolts, expanding rings, flashes, tracers.
/// Each one animates itself and then deletes itself.
/// </summary>
public class EffectVisual : MonoBehaviour
{
    private static Material sharedMaterial;

    private float life, age;
    private Vector3 startScale, endScale;
    private Color color;
    private Renderer rend;
    private LineRenderer line;
    private Light lightSource;
    private float startLightIntensity;
    private MaterialPropertyBlock block;

    private static Material Mat
    {
        get
        {
            if (sharedMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
                sharedMaterial = new Material(s);
            }
            return sharedMaterial;
        }
    }

    // ---------- Makers ----------

    /// <summary>A jagged lightning bolt between two points.</summary>
    public static void Bolt(Vector3 from, Vector3 to, Color c, float life = 0.25f, float width = 0.12f)
    {
        int segments = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) / 0.7f), 3, 16);
        Vector3[] pts = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = Vector3.Lerp(from, to, t);
            if (i > 0 && i < segments) p += Random.insideUnitSphere * 0.35f;
            pts[i] = p;
        }
        Line(pts, c, life, width, true);
    }

    /// <summary>A straight line (bullet tracer).</summary>
    public static void Tracer(Vector3 from, Vector3 to, Color c, float life = 0.06f, float width = 0.03f)
    {
        Line(new[] { from, to }, c, life, width, false);
    }

    /// <summary>A flat ring on the ground that expands to `radius`.</summary>
    public static void Ring(Vector3 center, float radius, Color c, float life = 0.4f)
    {
        GameObject go = Primitive(PrimitiveType.Cylinder, "Shockwave", center + Vector3.up * 0.05f);
        EffectVisual v = Setup(go, c, life);
        v.startScale = new Vector3(0.5f, 0.02f, 0.5f);
        v.endScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
    }

    /// <summary>An expanding glowing ball (explosions, impacts).</summary>
    public static void Flash(Vector3 center, float radius, Color c, float life = 0.3f, bool withLight = true)
    {
        GameObject go = Primitive(PrimitiveType.Sphere, "Flash", center);
        EffectVisual v = Setup(go, c, life);
        v.startScale = Vector3.one * radius * 0.3f;
        v.endScale = Vector3.one * radius * 2f;
        if (withLight) v.AddLight(c, radius * 2.5f, 6f);
    }

    /// <summary>A very short point light (muzzle flash).</summary>
    public static void LightBlip(Vector3 pos, Color c, float range = 4f, float intensity = 5f, float life = 0.05f)
    {
        GameObject go = new GameObject("LightBlip");
        go.transform.position = pos;
        EffectVisual v = go.AddComponent<EffectVisual>();
        v.life = life;
        v.color = c;
        v.AddLight(c, range, intensity);
    }

    // ---------- Internals ----------

    private static void Line(Vector3[] pts, Color c, float life, float width, bool withLight)
    {
        GameObject go = new GameObject("Line");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
        lr.startWidth = lr.endWidth = width;
        lr.material = Mat;
        lr.startColor = lr.endColor = c;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        EffectVisual v = go.AddComponent<EffectVisual>();
        v.life = life;
        v.color = c;
        v.line = lr;
        if (withLight)
        {
            go.transform.position = pts[pts.Length / 2];
            v.AddLight(c, 6f, 4f);
        }
    }

    private static GameObject Primitive(PrimitiveType type, string name, Vector3 pos)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = Mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    private static EffectVisual Setup(GameObject go, Color c, float life)
    {
        EffectVisual v = go.AddComponent<EffectVisual>();
        v.life = life;
        v.color = c;
        v.rend = go.GetComponent<Renderer>();
        v.block = new MaterialPropertyBlock();
        return v;
    }

    private void AddLight(Color c, float range, float intensity)
    {
        lightSource = gameObject.AddComponent<Light>();
        lightSource.type = LightType.Point;
        lightSource.color = c;
        lightSource.range = range;
        lightSource.intensity = intensity;
        startLightIntensity = intensity;
    }

    void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / Mathf.Max(0.001f, life));
        float alpha = color.a * (1f - k);

        if (rend != null)
        {
            transform.localScale = Vector3.Lerp(startScale, endScale, 1f - (1f - k) * (1f - k));
            rend.GetPropertyBlock(block);
            block.SetColor("_Color", new Color(color.r, color.g, color.b, alpha));
            block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            rend.SetPropertyBlock(block);
        }
        if (line != null)
        {
            Color lc = new Color(color.r, color.g, color.b, alpha);
            line.startColor = line.endColor = lc;
        }
        if (lightSource != null) lightSource.intensity = startLightIntensity * (1f - k);

        if (age >= life) Destroy(gameObject);
    }
}
