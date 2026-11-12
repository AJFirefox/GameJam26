using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What a Jackpot feels like: gold screen flash, thunder + coin sounds, a short hitstop freeze and a camera shake.
/// Put this on any GameObject in the scene (e.g. the LuckSystem object). Everything works with no setup;
/// drag sound clips in when you have them.
/// </summary>
public class JackpotEffect : MonoBehaviour
{
    [Header("Flash")]
    public Color flashColor = new Color(1f, 0.82f, 0.25f);
    [Range(0f, 1f)] public float flashAlpha = 0.45f;
    public float flashDuration = 0.35f;

    [Header("Hitstop (brief freeze)")]
    [Tooltip("Real-time seconds the game slows down for on a Jackpot.")]
    public float hitstopDuration = 0.08f;
    [Tooltip("How slow the game runs during hitstop. 0.05 = nearly frozen.")]
    [Range(0f, 1f)] public float hitstopTimeScale = 0.05f;

    [Header("Camera shake")]
    [Tooltip("Leave empty to use the main camera.")]
    public Transform shakeTarget;
    public float shakeStrength = 0.12f;
    public float shakeDuration = 0.25f;

    [Header("Sound")]
    public AudioClip thunderClip;
    public AudioClip coinClip;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("Close Enough (half-Jackpot)")]
    [Tooltip("Half-Jackpots get a smaller flash (this fraction of the full one) and no hitstop.")]
    [Range(0f, 1f)] public float halfJackpotStrength = 0.4f;

    private Image flashImage;
    private AudioSource audioSource;
    private Coroutine flashRoutine;
    private Coroutine shakeRoutine;
    private Coroutine hitstopRoutine;
    private Vector3 shakeRestPosition;
    private bool timeScaleOwned;
    private float timeScaleBeforeHitstop = 1f;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;   // 2D sound
        BuildFlashOverlay();
    }

    void Start()
    {
        if (shakeTarget == null && Camera.main != null) shakeTarget = Camera.main.transform;
        if (shakeTarget != null) shakeRestPosition = shakeTarget.localPosition;
    }

    void OnEnable() => LuckSystem.OnRoll += HandleRoll;

    void OnDisable()
    {
        LuckSystem.OnRoll -= HandleRoll;
        RestoreTimeScale();
        if (shakeTarget != null) shakeTarget.localPosition = shakeRestPosition;
    }

    private void HandleRoll(RollResult roll)
    {
        if (roll.isJackpot) Play(1f, true);
        else if (roll.isHalfJackpot) Play(halfJackpotStrength, false);
    }

    /// <summary>Plays the effect. strength 1 = full Jackpot.</summary>
    public void Play(float strength, bool withHitstop)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash(strength));

        if (shakeTarget != null)
        {
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(Shake(shakeStrength * strength));
        }

        if (thunderClip != null) audioSource.PlayOneShot(thunderClip, volume * strength);
        if (coinClip != null) audioSource.PlayOneShot(coinClip, volume * strength);

        if (withHitstop && hitstopDuration > 0f)
        {
            if (hitstopRoutine != null) StopCoroutine(hitstopRoutine);
            hitstopRoutine = StartCoroutine(Hitstop());
        }
    }

    /// <summary>Just a camera shake, for heavy hits and landings. Strength in metres.</summary>
    public void ShakeOnly(float strength)
    {
        if (shakeTarget == null) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(Shake(strength));
    }

    // ---------- Pieces ----------

    private IEnumerator Flash(float strength)
    {
        float peak = flashAlpha * strength;
        for (float t = 0f; t < flashDuration; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.Lerp(peak, 0f, t / flashDuration);
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, a);
            yield return null;
        }
        flashImage.color = Color.clear;
    }

    private IEnumerator Shake(float strength)
    {
        for (float t = 0f; t < shakeDuration; t += Time.unscaledDeltaTime)
        {
            float falloff = 1f - t / shakeDuration;
            shakeTarget.localPosition = shakeRestPosition + Random.insideUnitSphere * strength * falloff;
            yield return null;
        }
        shakeTarget.localPosition = shakeRestPosition;
    }

    private IEnumerator Hitstop()
    {
        // Don't fight a pause menu (timeScale 0).
        if (!timeScaleOwned)
        {
            if (Time.timeScale <= 0f) yield break;
            timeScaleBeforeHitstop = Time.timeScale;
        }

        timeScaleOwned = true;
        Time.timeScale = hitstopTimeScale;
        yield return new WaitForSecondsRealtime(hitstopDuration);
        RestoreTimeScale();
        hitstopRoutine = null;
    }

    private void RestoreTimeScale()
    {
        if (!timeScaleOwned) return;
        // Only put time back if nothing else (like a pause menu) changed it meanwhile.
        if (Mathf.Approximately(Time.timeScale, hitstopTimeScale)) Time.timeScale = timeScaleBeforeHitstop;
        timeScaleOwned = false;
    }

    private void BuildFlashOverlay()
    {
        GameObject canvasGO = new GameObject("JackpotFlashCanvas", typeof(Canvas));
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // above the HUD

        GameObject imgGO = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        flashImage = imgGO.GetComponent<Image>();
        flashImage.color = Color.clear;
        flashImage.raycastTarget = false;
    }
}
