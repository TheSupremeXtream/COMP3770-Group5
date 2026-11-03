using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PLACEHOLDER engine sound for the wave test scene (cinematic, not a flight-physics simulation).
/// Player engine: one recorded jet loop whose level, pitch and brightness follow the ship's speed and acceleration
/// smoothly (spools up faster than it winds down), fading out on defeat. Enemy flybys: when a test enemy is about to
/// pass close to the player, a recorded jet pass is started so its loudest point lines up with the closest approach.
/// The voice then follows the enemy: level and brightness from distance, stereo position from its side, and a small,
/// capped Doppler-style pitch change from the closing speed. Restart and leaving Play mode stop every loop.
/// </summary>
public class TestFlightAudio : MonoBehaviour
{
    // Set while enabled; cleared on disable, so it never survives leaving Play mode.
    public static TestFlightAudio Active { get; private set; }

    [SerializeField] private TestSoundBank sounds;
    [SerializeField] private TestPlayerShip player;
    [Tooltip("Optional: the engine winds down when the player is defeated.")]
    [SerializeField] private TestPlayerHealth playerHealth;

    [Header("Player engine")]
    [Range(0f, 1f)] [SerializeField] private float engineVolume = 0.3f;
    [Tooltip("Player speed (units/s) that counts as full thrust.")]
    [SerializeField] private float fullThrustSpeed = 9.6f;
    [Tooltip("Engine level at rest, as a fraction of Engine Volume.")]
    [Range(0f, 1f)] [SerializeField] private float idleLevel = 0.55f;
    [Range(0.8f, 1.2f)] [SerializeField] private float idlePitch = 0.95f;
    [Range(0.8f, 1.2f)] [SerializeField] private float fullPitch = 1.05f;
    [Tooltip("Low-pass cutoff at rest / at full thrust (Hz): the engine opens up as it spools.")]
    [SerializeField] private float idleCutoff = 1600f;
    [SerializeField] private float fullCutoff = 7000f;
    [Tooltip("How much accelerating adds to the thrust on top of speed (0 = speed only).")]
    [Range(0f, 1f)] [SerializeField] private float accelerationBoost = 0.35f;
    [Tooltip("Smoothing (seconds) when thrust rises / falls.")]
    [SerializeField] private float spoolUpSeconds = 0.15f;
    [SerializeField] private float spoolDownSeconds = 0.45f;
    [Tooltip("Fade in after (re)start and fade out on defeat.")]
    [SerializeField] private float fadeSeconds = 0.4f;

    [Header("Enemy flybys")]
    [Range(0f, 1f)] [SerializeField] private float flybyVolume = 0.45f;
    [Tooltip("Only enemies whose closest approach to the player is within this distance get a flyby.")]
    [SerializeField] private float passDistance = 4.5f;
    [Tooltip("Full level within this distance...")]
    [SerializeField] private float fullVolumeDistance = 2.5f;
    [Tooltip("...and silent beyond this distance.")]
    [SerializeField] private float hearingDistance = 14f;
    [Range(1, 4)] [SerializeField] private int maxFlybys = 2;
    [Tooltip("Minimum time between two flyby starts.")]
    [SerializeField] private float minGapSeconds = 0.6f;
    [Tooltip("Largest pitch change from closing speed (0.03 = +/-3%). The recordings already contain a real pass.")]
    [Range(0f, 0.1f)] [SerializeField] private float dopplerAmount = 0.03f;
    [Tooltip("Closing speed (units/s) that gives the full Doppler change.")]
    [SerializeField] private float dopplerSpeed = 6f;
    [Tooltip("Stereo width: sideways distance that pans fully, scaled by Max Pan.")]
    [SerializeField] private float panWidth = 8f;
    [Range(0f, 1f)] [SerializeField] private float maxPan = 0.8f;
    [Tooltip("Low-pass cutoff when the enemy is far / close (Hz).")]
    [SerializeField] private float farCutoff = 1800f;
    [SerializeField] private float nearCutoff = 9000f;

    private class FlybyVoice
    {
        public AudioSource source;
        public AudioLowPassFilter filter;
        public TestEnemyMover target;
        public Vector3 position;
        public Vector3 velocity;
        public float fade;
        public bool releasing;
        public float endTime;
        public float pitch = 1f;
        public bool busy;
    }

    private AudioSource engine;
    private AudioLowPassFilter engineFilter;
    private readonly List<FlybyVoice> flybys = new List<FlybyVoice>();
    private readonly HashSet<TestEnemyMover> alreadyPassed = new HashSet<TestEnemyMover>();
    private float throttle;
    private float engineFade;
    private float previousSpeed;
    private Vector3 playerVelocity;
    private Vector3 lastPlayerPosition;
    private bool hasLastPlayerPosition;
    private bool engineRunning;
    private float lastFlybyTime = float.NegativeInfinity;
    private int lastFlybyClip = -1;

    /// <summary>A flyby started: enemy, predicted closest distance, seconds until closest approach.</summary>
    public event Action<TestEnemyMover, float, float> FlybyStarted;

    public float Throttle => throttle;
    public float EngineVolumeNow => engine != null ? engine.volume : 0f;
    public float EnginePitchNow => engine != null ? engine.pitch : 0f;
    public bool EngineRunning => engineRunning;
    public int MaxFlybys => maxFlybys;
    public float DopplerAmount => dopplerAmount;

    public int ActiveFlybyCount
    {
        get
        {
            int n = 0;
            foreach (FlybyVoice v in flybys)
            {
                if (v.busy)
                {
                    n++;
                }
            }
            return n;
        }
    }

    /// <summary>Pitch of every flyby voice that is playing (tests).</summary>
    public IEnumerable<float> FlybyPitches()
    {
        foreach (FlybyVoice v in flybys)
        {
            if (v.busy)
            {
                yield return v.source.pitch;
            }
        }
    }

    /// <summary>Used by the scene builder and tests.</summary>
    public void Configure(TestSoundBank bank, TestPlayerShip ship, TestPlayerHealth health = null)
    {
        sounds = bank;
        player = ship;
        playerHealth = health;
    }

    // The engine runs while the ship exists and has not been destroyed.
    private bool PlayerFlying => player != null && player.gameObject.activeInHierarchy && (playerHealth == null || !playerHealth.IsDefeated);

    void Awake()
    {
        engine = MakeSource("PlayerEngine", out engineFilter);
        engine.loop = true;
        for (int i = 0; i < maxFlybys; i++)
        {
            var v = new FlybyVoice();
            v.source = MakeSource("FlybyVoice" + i, out v.filter);
            flybys.Add(v);
        }
    }

    private AudioSource MakeSource(string sourceName, out AudioLowPassFilter filter)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // positioned by volume / pan / filter below, so it works with any camera height
        source.dopplerLevel = 0f;
        filter = go.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = 22000f;
        return source;
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

    /// <summary>Stops the engine and every flyby (restart). The engine fades back in on the next frame if the ship is active.</summary>
    public void ClearAll()
    {
        if (engine != null)
        {
            engine.Stop();
            engine.volume = 0f;
        }
        engineRunning = false;
        throttle = engineFade = previousSpeed = 0f;
        hasLastPlayerPosition = false;
        foreach (FlybyVoice v in flybys)
        {
            StopVoice(v);
        }
        alreadyPassed.Clear();
        lastFlybyTime = float.NegativeInfinity;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }
        // Engines group: master x engines volume from the FX manager.
        float master = TestFxManager.Active != null ? TestFxManager.Active.EngineVolume : 1f;
        MeasurePlayer(dt);
        UpdateEngine(dt, master);
        StartFlybys();
        UpdateFlybys(dt, master);
    }

    // The player's velocity from how its transform actually moved, so this works with any controller
    // (the test ship or Abel's PlayerController). Teleports (restart) are ignored.
    private void MeasurePlayer(float dt)
    {
        if (player == null)
        {
            playerVelocity = Vector3.zero;
            hasLastPlayerPosition = false;
            return;
        }
        Vector3 position = player.transform.position;
        Vector3 v = hasLastPlayerPosition ? (position - lastPlayerPosition) / dt : Vector3.zero;
        playerVelocity = v.sqrMagnitude > 2500f ? Vector3.zero : v;
        lastPlayerPosition = position;
        hasLastPlayerPosition = true;
    }

    private void UpdateEngine(float dt, float master)
    {
        bool shipActive = PlayerFlying && sounds != null && sounds.playerEngineLoop != null;
        float speedNow = shipActive ? playerVelocity.magnitude : 0f;
        float accel = (speedNow - previousSpeed) / dt;
        previousSpeed = speedNow;
        float target = shipActive
            ? Mathf.Clamp01(speedNow / Mathf.Max(0.1f, fullThrustSpeed) + accelerationBoost * Mathf.Clamp01(accel / 40f))
            : 0f;
        float tau = target > throttle ? spoolUpSeconds : spoolDownSeconds;
        throttle += (target - throttle) * (1f - Mathf.Exp(-dt / Mathf.Max(0.01f, tau)));
        engineFade = Mathf.MoveTowards(engineFade, shipActive ? 1f : 0f, dt / Mathf.Max(0.01f, fadeSeconds));

        if (engineFade > 0f && !engineRunning && sounds != null && sounds.playerEngineLoop != null)
        {
            engine.clip = sounds.playerEngineLoop;
            engine.Play();
            engine.time = UnityEngine.Random.Range(0f, engine.clip.length * 0.9f);
            engineRunning = true;
        }
        else if (engineFade <= 0f && engineRunning)
        {
            engine.Stop();
            engineRunning = false;
        }
        engine.volume = engineVolume * master * engineFade * Mathf.Lerp(idleLevel, 1f, throttle);
        engine.pitch = Mathf.Lerp(idlePitch, fullPitch, throttle);
        engineFilter.cutoffFrequency = Mathf.Lerp(idleCutoff, fullCutoff, throttle);
    }

    private void StartFlybys()
    {
        if (sounds == null || sounds.flybys == null || sounds.flybys.Length == 0 || !PlayerFlying)
        {
            return;
        }
        if (Time.time - lastFlybyTime < minGapSeconds)
        {
            return;
        }
        FlybyVoice free = null;
        foreach (FlybyVoice v in flybys)
        {
            if (!v.busy)
            {
                free = v;
                break;
            }
        }
        if (free == null)
        {
            return;
        }
        float lead = sounds.flybyPassSeconds;
        TestEnemyMover best = null;
        float bestDistance = float.MaxValue, bestTime = 0f;
        Vector3 p = player.transform.position;
        foreach (TestEnemyMover enemy in TestEnemyMover.Active)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || alreadyPassed.Contains(enemy))
            {
                continue;
            }
            if (PredictPass(enemy.transform.position, enemy.Velocity, p, playerVelocity, out float t, out float d) &&
                t <= lead + 0.25f && d <= passDistance && d < bestDistance)
            {
                best = enemy;
                bestDistance = d;
                bestTime = t;
            }
        }
        if (best == null)
        {
            return;
        }
        int index = UnityEngine.Random.Range(0, sounds.flybys.Length);
        if (sounds.flybys.Length > 1 && index == lastFlybyClip)
        {
            index = (index + 1) % sounds.flybys.Length;
        }
        AudioClip clip = sounds.flybys[index];
        if (clip == null)
        {
            return;
        }
        lastFlybyClip = index;
        alreadyPassed.Add(best);
        lastFlybyTime = Time.time;

        free.busy = true;
        free.target = best;
        free.position = best.transform.position;
        free.velocity = best.Velocity;
        free.fade = 1f;
        free.releasing = false;
        free.pitch = 1f;
        free.source.clip = clip;
        float offset = Mathf.Clamp(lead - bestTime, 0f, Mathf.Max(0f, clip.length - 0.5f));
        free.source.pitch = 1f;
        free.source.volume = 0f;
        free.source.Play();
        free.source.time = offset;
        free.endTime = Time.time + (clip.length - offset);
        FlybyStarted?.Invoke(best, bestDistance, bestTime);
    }

    /// <summary>Time until two points moving in a straight line are closest (>= 0) and that closest distance (on the play plane).</summary>
    public static bool PredictPass(Vector3 enemy, Vector3 enemyVelocity, Vector3 player, Vector3 playerVelocity, out float time, out float distance)
    {
        var rel = new Vector2(enemy.x - player.x, enemy.z - player.z);
        var relV = new Vector2(enemyVelocity.x - playerVelocity.x, enemyVelocity.z - playerVelocity.z);
        float vv = relV.sqrMagnitude;
        time = vv > 1e-4f ? -Vector2.Dot(rel, relV) / vv : 0f;
        distance = (rel + relV * Mathf.Max(0f, time)).magnitude;
        return vv > 1e-4f && time >= 0f;
    }

    private void UpdateFlybys(float dt, float master)
    {
        Vector3 p = player != null ? player.transform.position : Vector3.zero;
        Vector3 pv = playerVelocity;
        foreach (FlybyVoice v in flybys)
        {
            if (!v.busy)
            {
                continue;
            }
            if (v.target != null && v.target.isActiveAndEnabled)
            {
                v.position = v.target.transform.position;
                v.velocity = v.target.Velocity;
            }
            else
            {
                // Destroyed mid-pass: keep drifting along its last course and fade quickly.
                v.position += v.velocity * dt;
                v.releasing = true;
            }
            if (v.releasing)
            {
                v.fade = Mathf.MoveTowards(v.fade, 0f, dt / 0.25f);
            }
            if (Time.time >= v.endTime || v.fade <= 0f)
            {
                StopVoice(v);
                continue;
            }
            Vector3 rel = v.position - p;
            rel.y = 0f;
            float distance = rel.magnitude;
            float g = Mathf.Clamp01((hearingDistance - distance) / Mathf.Max(0.01f, hearingDistance - fullVolumeDistance));
            g = g * g * (3f - 2f * g);
            Vector3 relV = v.velocity - pv;
            float closing = distance > 0.01f ? -Vector3.Dot(rel / distance, relV) : 0f;
            float targetPitch = 1f + dopplerAmount * Mathf.Clamp(closing / Mathf.Max(0.1f, dopplerSpeed), -1f, 1f);
            v.pitch = Mathf.Lerp(v.pitch, targetPitch, 1f - Mathf.Exp(-dt / 0.15f));
            v.source.pitch = v.pitch;
            v.source.volume = flybyVolume * master * g * v.fade;
            v.source.panStereo = Mathf.Clamp(rel.x / Mathf.Max(0.5f, panWidth), -1f, 1f) * maxPan;
            v.filter.cutoffFrequency = Mathf.Lerp(farCutoff, nearCutoff, g);
        }
    }

    private static void StopVoice(FlybyVoice v)
    {
        if (v.source != null)
        {
            v.source.Stop();
            v.source.clip = null;
            v.source.volume = 0f;
        }
        v.busy = false;
        v.target = null;
    }
}
