#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The temporary audio mix: volume groups, explosions ducking under attack warnings, the rocket motor (heard from the
/// launch, stopped with the rocket however it ends), sound cleanup on restart, defeat, completion and Stop in the
/// integrated demo, and the mixed output staying below full scale. These check behaviour and levels only; whether the
/// sounds are convincing has to be judged by listening.
/// </summary>
public class AudioMixTests
{
    private const string DemoPath = "Assets/Scenes/LevelWaves_Demo_Integrated.unity";
    private const string BankPath = "Assets/Audio/LevelWaves/TEST_SoundBank.asset";
    private const string ProfilePath = "Assets/Prefabs/LevelWaves/TEST_Difficulty_Encounter01.asset";
    private const string RocketPath = "Assets/Prefabs/LevelWaves/TEST_HomingRocket.prefab";
    private const string ShotPath = "Assets/Prefabs/LevelWaves/TEST_EnemyShot.prefab";
    private readonly List<Object> created = new List<Object>();
    private Scene scene;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return LevelWavesTestScenes.UnloadAll(DemoPath);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        foreach (Object o in created)
        {
            if (o != null)
            {
                Object.Destroy(o is Component c ? c.gameObject : o);
            }
        }
        created.Clear();
        yield return LevelWavesTestScenes.UnloadAll(DemoPath);
        yield return null;
    }

    private T Track<T>(T o) where T : Object
    {
        created.Add(o);
        return o;
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    private static IEnumerator WaitGame(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            yield return null;
        }
    }

    private T Find<T>() where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private IEnumerator LoadDemo()
    {
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(DemoPath, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = LevelWavesTestScenes.Newest(DemoPath);
        Assert.IsTrue(scene.isLoaded, "Integrated demo scene did not load");
        yield return null;
    }

    // Every AudioSource that is currently playing (all loaded scenes), optionally ignoring the player's engine loop.
    private static List<AudioSource> Playing(bool exceptEngine)
    {
        AudioClip engine = Load<TestSoundBank>(BankPath).playerEngineLoop;
        var playing = new List<AudioSource>();
        foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (s.isPlaying && !(exceptEngine && s.clip == engine))
            {
                playing.Add(s);
            }
        }
        return playing;
    }

    private static string Describe(List<AudioSource> sources)
    {
        return string.Join(", ", sources.ConvertAll(s => $"{s.name}:{(s.clip != null ? s.clip.name : "none")}"));
    }

    private TestFxManager MakeFx()
    {
        var fx = Track(new GameObject("FxMix")).AddComponent<TestFxManager>();
        fx.SetSounds(Load<TestSoundBank>(BankPath));
        return fx;
    }

    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- Mix ----

    [UnityTest]
    public IEnumerator VolumeGroups_ScaleOnlyTheirOwnSounds_AndExplosionsDuckUnderWarnings()
    {
        TestFxManager fx = MakeFx();
        yield return null;
        float shot = fx.Volume(TestFxManager.Sound.Shot), boom = fx.Volume(TestFxManager.Sound.EnemyDestroyed);
        float hit = fx.Volume(TestFxManager.Sound.PlayerHit), warn = fx.Volume(TestFxManager.Sound.EnemyCharge);
        float engines = fx.EngineVolume;
        Assert.Greater(shot, 0f);

        SetFloat(fx, "weaponsVolume", 0f);
        Assert.AreEqual(0f, fx.Volume(TestFxManager.Sound.Shot), "Weapons volume controls the player's cannon");
        Assert.AreEqual(0f, fx.Volume(TestFxManager.Sound.EnemyShot), "...and enemy gunfire");
        Assert.AreEqual(0f, fx.Volume(TestFxManager.Sound.RocketLaunch), "...and rocket launches");
        Assert.AreEqual(boom, fx.Volume(TestFxManager.Sound.EnemyDestroyed), "Explosions are not a weapon");
        SetFloat(fx, "impactsVolume", 0f);
        Assert.AreEqual(0f, fx.Volume(TestFxManager.Sound.PlayerHit));
        Assert.AreEqual(0f, fx.Volume(TestFxManager.Sound.Impact));
        Assert.AreEqual(warn, fx.Volume(TestFxManager.Sound.EnemyCharge), "Warnings have their own volume");
        SetFloat(fx, "explosionsVolume", 0f);
        Assert.AreEqual(0f, fx.Volume(TestFxManager.Sound.EnemyDestroyed));
        Assert.AreEqual(0f, fx.Volume(TestFxManager.Sound.PlayerDestroyed));
        Assert.AreEqual(0f, fx.Volume(TestFxManager.Sound.RocketBlast));
        Assert.AreEqual(engines, fx.EngineVolume, "Engines are separate from the one-shot groups");
        SetFloat(fx, "enginesVolume", 0f);
        Assert.AreEqual(0f, fx.EngineVolume);
        SetFloat(fx, "weaponsVolume", 0.6f);
        SetFloat(fx, "impactsVolume", 0.7f);
        SetFloat(fx, "explosionsVolume", 0.6f);
        SetFloat(fx, "enginesVolume", 0.7f);
        Assert.AreEqual(hit, fx.Volume(TestFxManager.Sound.PlayerHit), 1e-5f);

        // Balance: the player's own cannon is not quieter than an enemy hit (it was, before this pass).
        Assert.Greater(fx.Volume(TestFxManager.Sound.Shot), fx.Volume(TestFxManager.Sound.Impact));

        // An explosion while a warning sounds is lowered so the warning stays audible.
        fx.PlayExplosion(Vector3.zero, 1f);
        float alone = fx.LastVolume(TestFxManager.Sound.EnemyDestroyed);
        fx.ClearAll();
        fx.PlayEnemyCharge(true);
        fx.PlayExplosion(Vector3.zero, 1f);
        float underWarning = fx.LastVolume(TestFxManager.Sound.EnemyDestroyed);
        Debug.Log($"[Mix] explosion volume {alone:F3} alone, {underWarning:F3} during a rocket warning");
        Assert.AreEqual(alone * (1f - fx.DuckUnderWarnings), underWarning, 1e-4f, "Explosion should duck under the warning");
        Assert.AreEqual(1, fx.ActiveVoiceCount(TestFxManager.Sound.EnemyCharge), "The warning keeps playing");
        fx.ClearAll();
        Assert.AreEqual(0, fx.ActiveVoiceCount());
    }

    [UnityTest]
    public IEnumerator RepeatedGunfire_IsLimited_VariesClips_AndKeepsPitchRestrained()
    {
        TestFxManager fx = MakeFx();
        var bank = Load<TestSoundBank>(BankPath);
        yield return null;
        var clips = new HashSet<string>();
        int maxShots = 0;
        float minPitch = 2f, maxPitch = 0f;
        AudioClip previous = null;
        bool repeated = false;
        AudioSource[] pool = fx.GetComponents<AudioSource>();
        for (int i = 0; i < 25; i++) // 5 s of continuous fire at 5 shots a second
        {
            var before = new Dictionary<AudioSource, (AudioClip clip, float time)>();
            foreach (AudioSource s in pool)
            {
                before[s] = (s.clip, s.isPlaying ? s.time : -1f);
            }
            fx.PlayShot();
            // The voice that was just (re)started: a new clip, or the same source restarted from the beginning.
            AudioSource newest = null;
            foreach (AudioSource s in pool)
            {
                if (s.isPlaying && System.Array.IndexOf(bank.autocannon, s.clip) >= 0 &&
                    (before[s].clip != s.clip || before[s].time < 0f || s.time < before[s].time))
                {
                    newest = s;
                }
            }
            Assert.IsNotNull(newest, "Each shot should start a voice");
            clips.Add(newest.clip.name);
            repeated |= newest.clip == previous;
            previous = newest.clip;
            minPitch = Mathf.Min(minPitch, newest.pitch);
            maxPitch = Mathf.Max(maxPitch, newest.pitch);
            maxShots = Mathf.Max(maxShots, fx.ActiveVoiceCount(TestFxManager.Sound.Shot));
            float end = Time.realtimeSinceStartup + 0.2f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }
        Debug.Log($"[Mix] continuous fire: at most {maxShots} shots ringing, pitch {minPitch:F3}-{maxPitch:F3}, variants heard {clips.Count}");
        Assert.LessOrEqual(maxShots, fx.MaxPerSound, "Overlapping shots are capped");
        Assert.GreaterOrEqual(clips.Count, 2, "Several recorded variants are used");
        Assert.GreaterOrEqual(minPitch, 0.97f, "Pitch variation stays restrained");
        Assert.LessOrEqual(maxPitch, 1.03f, "Pitch variation stays restrained");
        Assert.IsFalse(repeated, "The same recording should never play twice in a row");
        fx.ClearAll();
    }

    // ---- Rocket motor ----

    private (TestAttackDirector director, Transform player) MakeRocketRig(Vector3 playerPosition)
    {
        var go = Track(new GameObject("Player"));
        go.transform.position = playerPosition;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.2f, 0.6f, 1.6f);
        go.AddComponent<Rigidbody>().isKinematic = true;
        go.AddComponent<TestPlayerHealth>();
        var director = Track(new GameObject("Director")).AddComponent<TestAttackDirector>();
        director.Configure(Track(Object.Instantiate(Load<TestDifficultyProfile>(ProfilePath))), go.transform, null, null,
            Load<GameObject>(ShotPath), Load<GameObject>(RocketPath));
        return (director, go.transform);
    }

    private static int MotorsPlaying()
    {
        AudioClip motor = Load<TestSoundBank>(BankPath).rocketLoop;
        int n = 0;
        foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (s.isPlaying && s.clip == motor)
            {
                n++;
            }
        }
        return n;
    }

    [UnityTest]
    public IEnumerator RocketMotor_IsHeardFromLaunch_AndStopsHoweverTheRocketEnds()
    {
        TestFxManager fx = MakeFx();
        var (director, player) = MakeRocketRig(new Vector3(0f, 0f, -5f));
        player.GetComponent<Collider>().enabled = false;
        yield return null;

        // Heard from the launch: 9+ units away (rockets launch at least 8 away) the motor is already audible.
        TestHomingRocket far = director.LaunchRocket(new Vector3(0f, 0f, 5f), Vector3.back);
        AudioSource motor = far.GetComponentInChildren<AudioSource>();
        Assert.IsNotNull(motor, "Rocket needs its motor AudioSource");
        Assert.IsTrue(motor.isPlaying, "Motor starts at launch");
        Assert.AreEqual(1, fx.ActiveVoiceCount(TestFxManager.Sound.RocketLaunch), "Launch blast plays");
        far.enabled = false;
        far.Step(0.05f);
        float early = motor.volume;
        far.Step(0.7f);
        float settled = motor.volume;
        Debug.Log($"[Rocket] motor volume {early:F3} at launch (fading in under the launch blast), {settled:F3} after 0.75 s at ~{Vector3.Distance(far.transform.position, player.position):F1} units");
        Assert.Less(early, settled, "Motor fades in under the launch blast");
        Assert.Greater(settled, 0.3f * 0.5f * fx.EngineVolume, "Motor must be audible from a distance");

        // 1. Expires.
        far.Step(far.Lifetime + 0.1f);
        yield return null;
        Assert.IsTrue(far == null, "Rocket removed after its lifetime");
        Assert.AreEqual(0, MotorsPlaying(), "Motor stops when the rocket expires");

        // 2. Shot down.
        TestHomingRocket shot = director.LaunchRocket(new Vector3(3f, 0f, 5f), Vector3.back);
        Assert.AreEqual(1, MotorsPlaying());
        shot.GetComponent<TestEnemyHealth>().TakeDamage(100);
        yield return null;
        Assert.IsTrue(shot == null);
        Assert.AreEqual(0, MotorsPlaying(), "Motor stops when the rocket is shot down");

        // 3. Removed by restart.
        director.LaunchRocket(new Vector3(-3f, 0f, 5f), Vector3.back);
        director.LaunchRocket(new Vector3(3f, 0f, 6f), Vector3.back);
        Assert.AreEqual(2, MotorsPlaying());
        director.ClearAll();
        fx.ClearAll();
        yield return null;
        Assert.AreEqual(0, MotorsPlaying(), "Restart stops every motor");
        Assert.AreEqual(0, fx.ActiveVoiceCount());

        // 4. Detonates on the player: the blast plays and the motor stops.
        player.GetComponent<Collider>().enabled = true;
        TestHomingRocket hit = director.LaunchRocket(player.position + new Vector3(0f, 0f, 3f), Vector3.back);
        float end = Time.time + 3f;
        while (hit != null && Time.time < end)
        {
            yield return null;
        }
        Assert.IsTrue(hit == null, "Rocket should reach the still player");
        yield return null;
        Assert.AreEqual(0, MotorsPlaying(), "Motor stops when the rocket explodes");
        Assert.AreEqual(1, fx.ActiveVoiceCount(TestFxManager.Sound.RocketBlast), "Detonation sound plays");
        fx.ClearAll();
    }

    // ---- Cleanup in the integrated demo ----

    [UnityTest]
    public IEnumerator Demo_Sound_StopsOnRestart_Defeat_Completion_AndStop()
    {
        yield return LoadDemo();
        TestFxManager fx = Find<TestFxManager>();
        TestFlightAudio flight = Find<TestFlightAudio>();
        TestAttackDirector director = Find<TestAttackDirector>();
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        TestPlayerHealth health = Find<TestPlayerHealth>();
        TestPlayerShip weapon = Find<TestPlayerShip>();
        WaveEncounter encounter = Find<WaveEncounter>();
        Vector3 p = weapon.transform.position;
        yield return WaitGame(1f);

        // R: everything stops at once (the engine fades back in for the new run).
        director.LaunchRocket(p + new Vector3(4f, 0f, 9f), Vector3.back);
        weapon.Fire();
        fx.PlayExplosion(p, 1f);
        fx.PlayEnemyCharge(true);
        Assert.Greater(Playing(true).Count, 0);
        harness.RestartAll();
        yield return null;
        List<AudioSource> left = Playing(true);
        Assert.AreEqual(0, left.Count, "Restart must silence everything except the engine fading back in: " + Describe(left));
        Assert.AreEqual(0, director.ActiveRocketCount);

        // Defeat: a rocket in flight and the destruction sound end on their own; nothing keeps playing.
        yield return WaitGame(1f);
        director.LaunchRocket(p + new Vector3(-6f, 0f, 9f), Vector3.back);
        for (int i = 0; i < 3; i++)
        {
            health.TryTakeHit(1);
            yield return WaitGame(1.05f);
        }
        Assert.IsTrue(health.IsDefeated);
        yield return WaitGame(6f); // longer than a rocket's lifetime and the longest one-shot (3.6 s)
        left = Playing(false);
        Assert.AreEqual(0, left.Count, "After defeat everything should fall silent: " + Describe(left));
        Assert.IsFalse(flight.EngineRunning, "Engine winds down on defeat");

        // Completion: once the last wave is done and the last rockets are gone, only the player's engine remains.
        harness.RestartAll();
        yield return null;
        health.enabled = false;
        Time.timeScale = 8f;
        float end = Time.realtimeSinceStartup + 90f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
        Assert.IsTrue(encounter.IsCompleted, "Demo encounter did not complete");
        yield return WaitGame(6f);
        Time.timeScale = 1f;
        left = Playing(true);
        Assert.AreEqual(0, director.ActiveRocketCount, "No rockets after completion");
        Assert.AreEqual(0, left.Count, "After completion only the engine should still play: " + Describe(left));
        Assert.IsTrue(flight.EngineRunning, "The player is still flying");

        // Stop (unload): no sound left anywhere.
        yield return LevelWavesTestScenes.UnloadAll(DemoPath);
        yield return null;
        left = Playing(false);
        Assert.AreEqual(0, left.Count, "Stopping Play mode leaves no sound behind: " + Describe(left));
        Assert.IsNull(TestFxManager.Active);
        Assert.IsNull(TestFlightAudio.Active);
    }

    // ---- Output level ----

    [UnityTest]
    public IEnumerator MixedOutput_StaysBelowFullScale_DuringHeavyCombat()
    {
        yield return LoadDemo();
        TestFxManager fx = Find<TestFxManager>();
        TestAttackDirector director = Find<TestAttackDirector>();
        TestPlayerShip weapon = Find<TestPlayerShip>();
        Find<TestPlayerHealth>().enabled = false;
        Vector3 p = weapon.transform.position;
        yield return WaitGame(0.5f);

        var buffer = new float[1024];
        float peak = 0f;
        bool any = false;
        float end = Time.realtimeSinceStartup + 4f;
        int frame = 0;
        while (Time.realtimeSinceStartup < end)
        {
            // Worst case on purpose: continuous fire, enemy bursts, kills, a warning, rockets and hull hits together.
            if (frame % 12 == 0) fx.PlayShot();
            if (frame % 8 == 0) fx.PlayEnemyShot(p + Vector3.forward * 8f);
            if (frame % 40 == 0) fx.PlayExplosion(p + new Vector3(Random.Range(-6f, 6f), 0f, 6f), 1f);
            if (frame % 30 == 0) fx.PlayImpact(p + Vector3.forward * 6f);
            if (frame == 20) fx.PlayEnemyCharge(true);
            if (frame == 60 || frame == 90) director.LaunchRocket(p + new Vector3(frame == 60 ? -5f : 5f, 0f, 8f), Vector3.back);
            if (frame == 120) fx.PlayPlayerHit(p);
            if (frame == 150) fx.PlayRocketBlast(p);
            if (frame == 180) fx.PlayPlayerDestroyed(p);
            frame++;
            yield return null;
            for (int ch = 0; ch < 2; ch++)
            {
                AudioListener.GetOutputData(buffer, ch);
                foreach (float s in buffer)
                {
                    float a = Mathf.Abs(s);
                    if (a > 0f) any = true;
                    peak = Mathf.Max(peak, a);
                }
            }
        }
        if (!any)
        {
            Assert.Inconclusive("No audio output in this run (no audio device), so the mixed level could not be measured.");
        }
        Debug.Log($"[Mix] heavy combat output peak {20f * Mathf.Log10(Mathf.Max(peak, 1e-6f)):F1} dBFS over {frame} frames");
        Assert.Less(peak, 1f, "The mixed output clips");
    }
}
#endif
