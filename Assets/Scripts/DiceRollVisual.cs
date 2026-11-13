using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The 3D dice shown next to the crosshair when you roll. Created and driven by GameHUD.
/// The dice tumble in, then settle showing the face that was actually rolled.
/// They float just in front of the camera, lined up with slots in the HUD.
/// </summary>
public class DiceRollVisual
{
    public const int MaxDice = 3;

    private readonly Camera cam;
    private readonly GameObject[] dice = new GameObject[MaxDice];
    private readonly Light glow;
    private float baseSize = 1f;

    // Direction each number's face points, in the die model's own space. Index 1..6.
    // Worked out from the stone dice model; checked against the mesh in the Editor.
    private readonly Vector3[] faceNormal =
    {
        Vector3.zero,
        Vector3.down,     // 1
        Vector3.right,    // 2
        Vector3.back,     // 3
        Vector3.forward,  // 4
        Vector3.left,     // 5
        Vector3.up,       // 6
    };

    // Animation state
    private bool active;
    private int count;
    private bool jackpot;
    private float t;
    private readonly Quaternion[] startRot = new Quaternion[MaxDice];
    private readonly Quaternion[] finalRot = new Quaternion[MaxDice];
    private readonly Vector3[] spinAxis = new Vector3[MaxDice];
    private readonly float[] spinSpeed = new float[MaxDice];

    private const float TumbleTime = 0.35f;
    private const float SettleTime = 0.18f;

    public DiceRollVisual(GameObject model, Camera camera)
    {
        cam = camera;

        for (int i = 0; i < MaxDice; i++)
        {
            GameObject d = Object.Instantiate(model);
            d.name = $"HUD Die {i + 1}";
            d.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            d.transform.localScale = Vector3.one;

            if (i == 0)
            {
                Calibrate(d);
                Renderer r0 = d.GetComponentInChildren<Renderer>();
                if (r0 != null) baseSize = Mathf.Max(r0.bounds.size.x, r0.bounds.size.y, r0.bounds.size.z);
                if (baseSize <= 0.0001f) baseSize = 1f;
            }

            foreach (Collider c in d.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (Renderer r in d.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            d.transform.SetParent(cam.transform, false);
            d.SetActive(false);
            dice[i] = d;
        }

        // A little light so the dice are readable even in shadow
        GameObject lightGO = new GameObject("HUD Dice Light");
        lightGO.transform.SetParent(cam.transform, false);
        glow = lightGO.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 0.6f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }

    /// <summary>Starts the roll animation for these dice values (1-6 each).</summary>
    public void Show(int[] values, bool isJackpot, float tiltDegrees)
    {
        count = Mathf.Min(values != null ? values.Length : 0, MaxDice);
        jackpot = isJackpot;
        t = 0f;
        active = count > 0;

        for (int i = 0; i < MaxDice; i++)
        {
            bool on = i < count;
            dice[i].SetActive(on);
            if (!on) continue;

            int v = Mathf.Clamp(values[i], 1, 6);
            float roll = Random.Range(-18f, 18f);
            finalRot[i] = Quaternion.AngleAxis(-tiltDegrees, Vector3.right)
                        * Quaternion.AngleAxis(roll, Vector3.forward)
                        * Quaternion.FromToRotation(faceNormal[v], Vector3.back);
            startRot[i] = Random.rotation;
            spinAxis[i] = Random.onUnitSphere;
            spinSpeed[i] = Random.Range(900f, 1400f);
        }
        glow.enabled = active;
        glow.color = isJackpot ? new Color(1f, 0.85f, 0.4f) : Color.white;
    }

    /// <summary>
    /// Call every frame (LateUpdate). Lines each die up with its HUD slot.
    /// slotPixels = slot size on screen, alpha = HUD readout fade (0 hides).
    /// </summary>
    public void Tick(RectTransform[] slots, float slotPixels, float depth, float alpha)
    {
        if (!active) return;
        if (alpha <= 0.01f)
        {
            for (int i = 0; i < MaxDice; i++) dice[i].SetActive(false);
            glow.enabled = false;
            active = false;
            return;
        }

        t += Time.unscaledDeltaTime;
        float worldPerPixel = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);

        // Pop-in scale, with an extra bounce for Jackpots
        float pop = t < 0.12f ? Mathf.Lerp(0.5f, 1.15f, t / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.12f) / 0.2f));
        if (jackpot && t > TumbleTime && t < TumbleTime + 0.3f)
            pop *= 1f + 0.25f * Mathf.Sin((t - TumbleTime) / 0.3f * Mathf.PI);

        Vector3 centre = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Transform d = dice[i].transform;

            // Hop up while tumbling
            float hop = t < TumbleTime ? Mathf.Sin(t / TumbleTime * Mathf.PI) * slotPixels * 0.35f : 0f;
            Vector3 screen = slots[i].position + Vector3.up * hop;
            d.position = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            centre += d.position;

            float size = slotPixels * worldPerPixel;
            d.localScale = Vector3.one * (size / baseSize) * pop * Mathf.Clamp01(alpha * 1.5f);

            // Tumble, slowing down, then settle on the rolled face
            if (t < TumbleTime)
            {
                float k = t / TumbleTime;
                d.localRotation = Quaternion.AngleAxis(spinSpeed[i] * t * (1f - 0.5f * k), spinAxis[i]) * startRot[i];
            }
            else
            {
                Quaternion tumbleEnd = Quaternion.AngleAxis(spinSpeed[i] * TumbleTime * 0.5f, spinAxis[i]) * startRot[i];
                float k = Mathf.Clamp01((t - TumbleTime) / SettleTime);
                k = 1f - (1f - k) * (1f - k);
                d.localRotation = Quaternion.Slerp(tumbleEnd, finalRot[i], k);
            }
        }

        // Light sits just in front of the dice, towards the camera
        glow.transform.position = centre / count - cam.transform.forward * 0.15f;
        glow.intensity = 2f * alpha;
    }

    // ---------- Face detection ----------

    /// <summary>
    /// In the Editor, reads the dice mesh to confirm which way each numbered face points,
    /// using the texture layout (3 columns x 2 rows: top 5 3 6, bottom 2 4 1).
    /// </summary>
    private void Calibrate(GameObject die)
    {
        MeshFilter mf = die.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Mesh mesh = mf.sharedMesh;
        if (!Application.isEditor && !mesh.isReadable) return;

        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;
        int[] tris = mesh.triangles;
        if (normals.Length == 0 || uvs.Length == 0) return;

        // votes[axisIndex][number]
        int[,] votes = new int[6, 7];
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 n = normals[tris[i]] + normals[tris[i + 1]] + normals[tris[i + 2]];
            n = die.transform.InverseTransformDirection(mf.transform.TransformDirection(n)).normalized;
            int axis = -1;
            for (int a = 0; a < 6; a++) if (Vector3.Dot(n, axes[a]) > 0.9f) { axis = a; break; }
            if (axis < 0) continue;

            Vector2 uv = (uvs[tris[i]] + uvs[tris[i + 1]] + uvs[tris[i + 2]]) / 3f;
            int col = uv.x < 0.40f ? 0 : uv.x < 0.71f ? 1 : 2;
            bool top = uv.y >= 0.40f;
            int number = top ? new[] { 5, 3, 6 }[col] : new[] { 2, 4, 1 }[col];
            votes[axis, number]++;
        }

        Vector3[] detected = new Vector3[7];
        HashSet<int> seen = new HashSet<int>();
        for (int a = 0; a < 6; a++)
        {
            int best = 0, bestVotes = 0;
            for (int num = 1; num <= 6; num++) if (votes[a, num] > bestVotes) { bestVotes = votes[a, num]; best = num; }
            if (best == 0 || !seen.Add(best)) return;   // unclear, keep the built-in table
            detected[best] = axes[a];
        }

        bool differs = false;
        for (int num = 1; num <= 6; num++) if (detected[num] != faceNormal[num]) differs = true;
        for (int num = 1; num <= 6; num++) faceNormal[num] = detected[num];
        if (differs)
            Debug.LogWarning("DiceRollVisual: the dice faces point differently than expected. Using what the mesh says in the Editor, " +
                             "but turn on Read/Write in the dice model's import settings so builds match.");
    }
}
