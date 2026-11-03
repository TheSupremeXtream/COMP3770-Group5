using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PLACEHOLDER effects and sound for the wave test scene (not the team's audio/VFX system).
/// Spawned effects are parented here so a restart can remove them. Sounds are edited CC0 recordings from a
/// TestSoundBank: several variants per sound, never the same variant twice in a row, small pitch changes, a voice
/// pool with per-sound limits, and explosions that duck each other instead of stacking up. Everything is stopped on
/// restart and when Play mode ends. Engine and flyby sounds live in TestFlightAudio.
/// </summary>
public class TestFxManager : MonoBehaviour
{
    public enum Sound
    {
        Shot,
        Impact,
        EnemyDestroyed,
        PlayerHit,
        PlayerDestroyed,
        EnemyCharge,
        EnemyShot,
        RocketLaunch,
        RocketBlast
    }

    // Set while a manager is enabled; cleared on disable, so it never survives leaving Play mode.
    public static TestFxManager Active { get; private set; }

    [SerializeField] private GameObject hitSparkPrefab;
    [SerializeField] private GameObject playerHitPrefab;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private TestSoundBank sounds;

    [Header("Mix (0-1)")]
    [Tooltip("Scales every placeholder sound, including engines and flybys.")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.6f;
    [Tooltip("Player autocannon (5 shots a second while firing, so keep it moderate).")]
    [Range(0f, 1f)] [SerializeField] private float firingVolume = 0.32f;
    [Tooltip("Metal impacts when shots hit enemies.")]
    [Range(0f, 1f)] [SerializeField] private float impactVolume = 0.4f;
    [Tooltip("Enemy and player explosions.")]
    [Range(0f, 1f)] [SerializeField] private float explosionVolume = 0.6f;
    [Tooltip("Player taking damage.")]
    [Range(0f, 1f)] [SerializeField] private float playerDamageVolume = 0.8f;
    [Tooltip("Enemy charge warning and enemy cannon shot.")]
    [Range(0f, 1f)] [SerializeField] private float enemyAttackVolume = 0.5f;
    [Tooltip("Rocket launch, rocket engine loop and rocket detonation.")]
    [Range(0f, 1f)] [SerializeField] private float rocketVolume = 0.55f;

    [Header("Variation and limits")]
    [Tooltip("Random pitch change per play (0.03 = +/-3%). Kept small so recordings stay natural.")]
    [Range(0f, 0.1f)] [SerializeField] private float pitchVariation = 0.03f;
    [Tooltip("Total one-shot sounds that can play at once.")]
    [Range(2, 16)] [SerializeField] private int maxVoices = 10;
    [Tooltip("Overlapping autocannon shots / impacts; the oldest is cut when exceeded.")]
    [Range(1, 6)] [SerializeField] private int maxPerSound = 3;
    [Tooltip("Explosions that can ring at once. Extra ones are skipped while the others are still loud.")]
    [Range(1, 4)] [SerializeField] private int maxExplosions = 2;
    [Tooltip("Each explosion already playing lowers the next one by this fraction, so a chain of kills does not pile up.")]
    [Range(0f, 1f)] [SerializeField] private float explosionDucking = 0.35f;
    [Tooltip("Sideways position (world x) that pans an explosion or impact fully to one side, scaled by Max Pan.")]
    [SerializeField] private float panWidth = 12f;
    [Range(0f, 1f)] [SerializeField] private float maxPan = 0.6f;

    private class Voice
    {
        public AudioSource source;
        public Sound sound;
        public float startedAt;
        public float busyUntil;
    }

    private readonly List<Voice> voices = new List<Voice>();
    private readonly Dictionary<Sound, int> lastVariant = new Dictionary<Sound, int>();
    private readonly Dictionary<Sound, float> lastPlayed = new Dictionary<Sound, float>();

    public int ActiveEffectCount => transform.childCount;
    public int MaxVoices => maxVoices;
    public int MaxPerSound => maxPerSound;
    public int MaxExplosions => maxExplosions;
    public float MasterVolume => masterVolume;
    public float RocketVolume => rocketVolume;
    public TestSoundBank Sounds => sounds;

    void Awake()
    {
        for (int i = 0; i < maxVoices; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            voices.Add(new Voice { source = source });
        }
    }

    void OnEnable()
    {
        Active = this;
    }

    void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
        ClearAll();
    }

    /// <summary>Used by the scene builder and tests.</summary>
    public void SetSounds(TestSoundBank bank)
    {
        sounds = bank;
    }

    public void PlayShot()
    {
        Play(Sound.Shot, 0f);
    }

    public void PlayImpact(Vector3 position)
    {
        Spawn(hitSparkPrefab, position, 1f);
        Play(Sound.Impact, position.x);
    }

    public void PlayExplosion(Vector3 position, float scale)
    {
        Spawn(explosionPrefab, position, scale);
        Play(Sound.EnemyDestroyed, position.x);
    }

    public void PlayPlayerHit(Vector3 position)
    {
        Spawn(playerHitPrefab != null ? playerHitPrefab : hitSparkPrefab, position, 1f);
        Play(Sound.PlayerHit, 0f);
    }

    public void PlayPlayerDestroyed(Vector3 position)
    {
        Spawn(explosionPrefab, position, 1.6f);
        Spawn(playerHitPrefab, position, 1.3f);
        Play(Sound.PlayerDestroyed, 0f);
    }

    /// <summary>Attack warning: the turbine whine for guns, the same whine a little lower for a rocket lock.</summary>
    public void PlayEnemyCharge(bool rocket = false)
    {
        Play(Sound.EnemyCharge, 0f, rocket ? 0.85f : 1f);
    }

    public void PlayEnemyShot(Vector3 from = default)
    {
        Play(Sound.EnemyShot, from.x);
    }

    public void PlayRocketLaunch(Vector3 from)
    {
        Play(Sound.RocketLaunch, from.x);
    }

    /// <summary>A rocket detonating on the player: a small blast effect and the rocket's own detonation sound.</summary>
    public void PlayRocketBlast(Vector3 position)
    {
        Spawn(explosionPrefab, position, 0.6f);
        Play(Sound.RocketBlast, position.x);
    }

    /// <summary>Removes every effect still playing and silences all sounds.</summary>
    public void ClearAll()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        foreach (Voice v in voices)
        {
            if (v.source != null)
            {
                v.source.Stop();
                v.source.clip = null;
            }
            v.busyUntil = 0f;
        }
        lastPlayed.Clear();
    }

    /// <summary>Sounds currently playing (all, or of one kind).</summary>
    public int ActiveVoiceCount(Sound? sound = null)
    {
        int count = 0;
        float now = Time.unscaledTime;
        foreach (Voice v in voices)
        {
            if (v.busyUntil > now && (sound == null || v.sound == sound.Value))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Volume the newest voice of this sound was started at (tests: ducking).</summary>
    public float LastVolume(Sound sound)
    {
        Voice latest = null;
        foreach (Voice v in voices)
        {
            if (v.sound == sound && v.busyUntil > Time.unscaledTime && (latest == null || v.startedAt >= latest.startedAt))
            {
                latest = v;
            }
        }
        return latest != null ? latest.source.volume : 0f;
    }

    private void Spawn(GameObject prefab, Vector3 position, float scale)
    {
        if (prefab == null)
        {
            return;
        }
        GameObject fx = Instantiate(prefab, position, Quaternion.identity, transform);
        fx.transform.localScale = Vector3.one * scale;
    }

    private void Play(Sound sound, float worldX, float pitchScale = 1f)
    {
        AudioClip[] options = sounds != null ? sounds.For(sound) : null;
        if (options == null || options.Length == 0)
        {
            return;
        }
        float now = Time.unscaledTime;
        // The same sound within 30 ms would only stack volume (e.g. several kills in one frame).
        if (lastPlayed.TryGetValue(sound, out float last) && now - last < 0.03f)
        {
            return;
        }

        float volume = Volume(sound) * masterVolume;
        if (sound == Sound.EnemyDestroyed || sound == Sound.PlayerDestroyed)
        {
            int ringing = ActiveVoiceCount(Sound.EnemyDestroyed);
            if (sound == Sound.EnemyDestroyed && ringing >= maxExplosions && NewestStart(sound) > now - 0.25f)
            {
                return; // several kills at once: the ones already ringing cover it
            }
            volume *= 1f / (1f + explosionDucking * ringing);
        }

        Voice voice = PickVoice(sound, now);
        if (voice == null)
        {
            return;
        }
        lastPlayed[sound] = now;
        // Never the same variant twice in a row.
        int index = options.Length == 1 ? 0 : Random.Range(0, options.Length);
        if (options.Length > 1 && lastVariant.TryGetValue(sound, out int previous) && previous == index)
        {
            index = (index + 1) % options.Length;
        }
        lastVariant[sound] = index;
        AudioClip clip = options[index];
        if (clip == null)
        {
            return;
        }
        float pitch = pitchScale * (1f + Random.Range(-pitchVariation, pitchVariation));

        voice.source.Stop();
        voice.source.clip = clip;
        voice.source.pitch = pitch;
        voice.source.volume = volume;
        voice.source.panStereo = Mathf.Clamp(worldX / Mathf.Max(1f, panWidth), -1f, 1f) * maxPan;
        voice.source.Play();
        voice.sound = sound;
        voice.startedAt = now;
        voice.busyUntil = now + clip.length / pitch;
    }

    private float NewestStart(Sound sound)
    {
        float newest = float.NegativeInfinity;
        foreach (Voice v in voices)
        {
            if (v.sound == sound && v.busyUntil > Time.unscaledTime)
            {
                newest = Mathf.Max(newest, v.startedAt);
            }
        }
        return newest;
    }

    private int Cap(Sound sound)
    {
        switch (sound)
        {
            case Sound.PlayerHit:
            case Sound.PlayerDestroyed: return 1;
            case Sound.EnemyCharge: return 2;
            case Sound.RocketLaunch:
            case Sound.RocketBlast: return 2;
            case Sound.EnemyDestroyed: return maxExplosions;
            default: return maxPerSound;
        }
    }

    private Voice PickVoice(Sound sound, float now)
    {
        int cap = Cap(sound);
        Voice oldestSame = null;
        int same = 0;
        Voice free = null;
        Voice oldestAny = null;
        foreach (Voice v in voices)
        {
            bool busy = v.busyUntil > now;
            if (!busy)
            {
                free ??= v;
                continue;
            }
            if (v.sound == sound)
            {
                same++;
                if (oldestSame == null || v.startedAt < oldestSame.startedAt)
                {
                    oldestSame = v;
                }
            }
            // Gunfire is the first thing to give way when every voice is busy.
            if (oldestAny == null || Priority(v.sound) < Priority(oldestAny.sound) ||
                (Priority(v.sound) == Priority(oldestAny.sound) && v.startedAt < oldestAny.startedAt))
            {
                oldestAny = v;
            }
        }
        if (same >= cap)
        {
            return oldestSame;
        }
        if (free != null)
        {
            return free;
        }
        return oldestAny != null && Priority(oldestAny.sound) <= Priority(sound) ? oldestAny : null;
    }

    private static int Priority(Sound sound)
    {
        switch (sound)
        {
            case Sound.Shot: return 0;
            case Sound.Impact: return 1;
            case Sound.EnemyDestroyed: return 2;
            default: return 3; // player damage and enemy attack warnings matter most
        }
    }

    private float Volume(Sound sound)
    {
        switch (sound)
        {
            case Sound.Shot: return firingVolume;
            case Sound.Impact: return impactVolume;
            case Sound.EnemyDestroyed:
            case Sound.PlayerDestroyed: return explosionVolume;
            case Sound.PlayerHit: return playerDamageVolume;
            case Sound.RocketLaunch:
            case Sound.RocketBlast: return rocketVolume;
            default: return enemyAttackVolume;
        }
    }
}
