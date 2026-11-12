using UnityEngine;

/// <summary>
/// Background music. Loops the track, fades it in, turns it down while the game is paused
/// (Altar menu, game over) and fades it down when the player dies.
/// Put this on any GameObject in the scene and drag the music into Music.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public AudioClip music;
    [Range(0f, 1f)] public float volume = 0.45f;
    [Tooltip("Seconds to fade in when the scene starts.")]
    public float fadeInTime = 2f;
    [Tooltip("Volume multiplier while the game is paused (Altar menu).")]
    [Range(0f, 1f)] public float pausedVolume = 0.4f;
    [Tooltip("Volume multiplier after the player dies.")]
    [Range(0f, 1f)] public float deathVolume = 0.25f;
    [Tooltip("How fast volume changes (per second).")]
    public float fadeSpeed = 1.5f;

    private AudioSource source;
    private Health playerHealth;
    private float current;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        if (music != null) source.clip = music;
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerHealth = p.GetComponent<Health>();

        if (source.clip == null) { Debug.LogWarning("MusicManager: no music assigned."); return; }
        current = 0f;
        source.volume = 0f;
        source.Play();
    }

    void Update()
    {
        if (source.clip == null) return;

        float target = volume;
        if (playerHealth != null && playerHealth.IsDead) target *= deathVolume;
        else if (Time.timeScale <= 0.01f) target *= pausedVolume;

        float speed = Time.timeSinceLevelLoad < fadeInTime ? volume / Mathf.Max(0.01f, fadeInTime) : fadeSpeed;
        current = Mathf.MoveTowards(current, target, speed * Time.unscaledDeltaTime);
        source.volume = current;
    }
}
