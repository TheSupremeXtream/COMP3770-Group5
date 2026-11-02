#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Progressive difficulty, aimed-shot fairness, dodgeability, faster player and destruction effects (sounds: MovementAndAudioTests).</summary>
public class DifficultyAndAttackTests
{
    private const string ScenePath = "Assets/Scenes/LevelWaves_Encounter01_Test.unity";
    private const string ProfilePath = "Assets/Prefabs/LevelWaves/TEST_Difficulty_Encounter01.asset";
    private readonly List<GameObject> created = new List<GameObject>();
    private Scene scene;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (GameObject go in created)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        created.Clear();
    }

    private GameObject Track(GameObject go)
    {
        created.Add(go);
        return go;
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    private TestAttackDirector MakeDirector(Transform player, TestPlayerHealth health)
    {
        var director = Track(new GameObject("TestDirector")).AddComponent<TestAttackDirector>();
        director.Configure(Load<TestDifficultyProfile>(ProfilePath), player, health, null,
            Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_EnemyShot.prefab"));
        return director;
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

    private static IEnumerator WaitGame(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            yield return null;
        }
    }

    // ---- Difficulty profile ----

    [UnityTest]
    public IEnumerator DifficultyProfile_GetsHarderThroughBehaviour_AndStaysReadable()
    {
        TestDifficultyProfile profile = Load<TestDifficultyProfile>(ProfilePath);
        Assert.IsNotNull(profile);
        Assert.AreEqual(3, profile.waves.Count, "One entry per wave of this encounter");
        TestWaveDifficulty w1 = profile.waves[0], w2 = profile.waves[1], w3 = profile.waves[2];
        // Patterns are introduced gradually.
        foreach (TestMovePattern p in w1.groupPatterns)
        {
            Assert.IsTrue(p == TestMovePattern.CurvedPass || p == TestMovePattern.SideSweep || p == TestMovePattern.Straight,
                "Wave 1: only curved approaches and sweeps, not " + p);
        }
        CollectionAssert.Contains(w1.groupPatterns, TestMovePattern.SideSweep, "Wave 1 introduces the left/right sweep");
        CollectionAssert.Contains(w2.groupPatterns, TestMovePattern.PassAndRetreat, "Wave 2 introduces pass-and-retreat");
        Assert.GreaterOrEqual(new HashSet<TestMovePattern>(w3.groupPatterns).Count, 3, "Wave 3 mixes the patterns");
        // Reactions are introduced gradually and stay limited.
        Assert.AreEqual(0f, w1.laneSeekDistance, "Wave 1: no repositioning");
        Assert.AreEqual(TestEnemyClassMask.None, w1.flankingClasses, "Wave 1: no flanking");
        Assert.AreEqual(0, w1.aimedShooterEveryNth, "Wave 1: no attacks");
        Assert.IsTrue(w2.alternateFlanks, "Wave 2: alternating flanks");
        Assert.Greater(w2.laneSeekDistance, 0f, "Wave 2: limited repositioning towards the player's lane");
        Assert.LessOrEqual(w2.laneSeekDistance, 2f);
        Assert.AreEqual(TestEnemyClassMask.None, w2.flankingClasses, "Wave 2: no flanking yet");
        Assert.AreEqual(0, w2.aimedShooterEveryNth, "Wave 2: still no attacks");
        Assert.AreNotEqual(TestEnemyClassMask.None, w3.flankingClasses & TestEnemyClassMask.Interceptor, "Wave 3: interceptors flank");
        Assert.GreaterOrEqual(w3.flankOffset, 2f, "Flankers pass beside the player, not over it");
        Assert.LessOrEqual(w3.trackingSeconds, 1f, "Tracking is brief");
        Assert.Greater(w3.aimedShooterEveryNth, 0, "Wave 3: occasional aimed attacks");
        Assert.GreaterOrEqual(w3.aimedShooterEveryNth, 2, "Only some ships shoot ('occasional')");
        Assert.GreaterOrEqual(w3.telegraphSeconds, 0.6f, "Attacks need a readable warning");
        Assert.LessOrEqual(w3.shotSpeed, 10f, "Shots must be slow enough to dodge");
        Assert.LessOrEqual(w1.speedMultiplier, w2.speedMultiplier);
        Assert.LessOrEqual(w2.speedMultiplier, w3.speedMultiplier);
        Assert.LessOrEqual(w3.speedMultiplier, 1.25f, "Difficulty comes from behaviour, not raw speed");
        Assert.GreaterOrEqual(profile.minEscapeGap, 2f, "An escape lane wider than the player ship must stay open");
        Assert.LessOrEqual(profile.maxShotsInFlight, 3);
        Assert.GreaterOrEqual(profile.minSecondsBetweenAttacks, 1f);
        Assert.GreaterOrEqual(profile.minFireDistance, 6f);
        Assert.GreaterOrEqual(profile.fireWindowZ.x, 2f, "No attacks start inside the player's zone");
        Assert.Greater(profile.aimLockSeconds, 0.1f);
        yield return null;
    }

    [UnityTest]
    public IEnumerator LaneSeek_ReadsThePlayersLaneOnce_AndIsLimited()
    {
        var player = Track(new GameObject("Player")).transform;
        player.position = new Vector3(6f, 0f, -5f);
        TestAttackDirector director = MakeDirector(player, null);
        TestWaveDifficulty wave2 = director.Profile.waves[1];

        GameObject dart = Track(Object.Instantiate(Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_Enemy_Basic.prefab"),
            new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 180f, 0f)));
        var mover = dart.GetComponent<TestEnemyMover>();
        mover.OnWaveSpawn(new WaveSpawnInfo { waveIndex = 1, groupIndex = 0, slotIndex = 0, slotCount = 5 });
        float end = Time.realtimeSinceStartup + 5f;
        while (!mover.SeekRead && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
        Assert.IsTrue(mover.SeekRead);
        float plannedLane = mover.LaneX - mover.SeekShift;
        Assert.Greater(mover.SeekShift * (player.position.x - plannedLane), 0f, "Shift should be towards the player's lane");
        Assert.AreEqual(wave2.laneSeekDistance, Mathf.Abs(mover.SeekShift), 1e-4f, "Shift is capped (player is further away than the cap)");
        Assert.IsFalse(mover.IsFlanking, "Wave 2 ships shift lanes; they do not flank");

        float decided = mover.SeekShift;
        player.position = new Vector3(-6f, 0f, -5f); // player moves to the other side afterwards
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(decided, mover.SeekShift, "The lane is read once: no tracking");

        GameObject calm = Track(Object.Instantiate(Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_Enemy_Basic.prefab"),
            new Vector3(0f, 0f, 30f), Quaternion.Euler(0f, 180f, 0f)));
        var calmMover = calm.GetComponent<TestEnemyMover>();
        calmMover.OnWaveSpawn(new WaveSpawnInfo { waveIndex = 0, slotCount = 4 });
        yield return new WaitForSeconds(1.2f);
        Assert.AreEqual(0f, calmMover.SeekShift, "Wave 1 ships never reposition");
    }

    // ---- Aimed shots ----

    [UnityTest]
    public IEnumerator AimedShot_FliesStraight_HitsAStillPlayer_AndCanBeDodged()
    {
        GameObject ship = Track(Object.Instantiate(Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_PlayerShip.prefab"),
            new Vector3(0f, 0f, -5f), Quaternion.identity));
        var move = ship.GetComponent<TestPlayerShip>();
        move.enabled = false; // driven by Step below, not the keyboard
        var health = ship.GetComponent<TestPlayerHealth>();
        TestAttackDirector director = MakeDirector(ship.transform, health);
        yield return new WaitForFixedUpdate();

        // 1. Standing still: the shot (aimed where the player is) hits.
        director.Fire(new Vector3(0f, 0f, 3f), ship.transform.position, 8f);
        Vector3 v0 = director.transform.GetChild(0).GetComponent<TestEnemyBullet>().Velocity;
        Assert.AreEqual(8f, v0.magnitude, 1e-3f);
        yield return WaitGame(1.6f);
        Assert.AreEqual(2, health.HitsRemaining, "A shot at a player who does not move should hit");
        Assert.AreEqual(0, director.ActiveShotCount, "The shot ends on contact");

        yield return WaitGame(1.1f);
        health.ResetHealth();

        // 2. Moving sideways when the shot leaves: it misses, and it never turns to follow.
        director.Fire(new Vector3(0f, 0f, 3f), ship.transform.position, 8f);
        var bullet = director.transform.GetChild(0).GetComponent<TestEnemyBullet>();
        Vector3 launched = bullet.Velocity;
        float end = Time.time + 0.5f;
        while (Time.time < end)
        {
            move.Step(Vector2.right, Time.deltaTime);
            yield return null;
        }
        Assert.AreEqual(launched, bullet.Velocity, "No homing");
        yield return WaitGame(2.2f); // past the player and out of the play area
        Assert.AreEqual(3, health.HitsRemaining, "Moving out of the line should dodge the shot");
        Assert.AreEqual(0, director.ActiveShotCount, "Missed shots are removed once they leave the area");
    }

    [UnityTest]
    public IEnumerator Encounter_AimedAttacks_OnlyInWave3_FollowFairnessRules_AndRestartClearsThem()
    {
        yield return EditorSceneManagerLoad();
        TestAttackDirector director = Find<TestAttackDirector>();
        TestPlayerHealth health = Find<TestPlayerHealth>();
        WaveEncounter encounter = Find<WaveEncounter>();
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        TestFxManager fx = Find<TestFxManager>();
        TestDifficultyProfile profile = director.Profile;
        health.enabled = false; // survive the whole run; the player sits still at its start position

        var attacks = new List<(float time, int wave, Vector3 from, float distance)>();
        var shots = new List<(float time, Vector3 from, Vector3 dir, Vector3 aim)>();
        director.AttackStarted += from => attacks.Add((Time.time, encounter.CurrentWaveIndex, from, Vector3.Distance(from, health.transform.position)));
        director.ShotFired += (from, dir, aim) => shots.Add((Time.time, from, dir, aim));
        int maxBusy = 0;

        float end = Time.realtimeSinceStartup + 90f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            Time.timeScale = encounter.CurrentWaveIndex < 2 ? 8f : 2f;
            maxBusy = Mathf.Max(maxBusy, director.ActiveShotCount + director.PendingAttacks);
            yield return null;
        }
        Time.timeScale = 1f;
        Assert.IsTrue(encounter.IsCompleted);
        Debug.Log($"[Attacks] {attacks.Count} attacks, {shots.Count} shots, max busy {maxBusy}");

        Assert.Greater(attacks.Count, 0, "Wave 3 should attack");
        Assert.AreEqual(attacks.Count, shots.Count, "Every warning ends in exactly one shot (no hidden shots)");
        float telegraph = profile.waves[2].telegraphSeconds;
        for (int i = 0; i < attacks.Count; i++)
        {
            Assert.AreEqual(2, attacks[i].wave, "Only wave 3 attacks");
            Assert.GreaterOrEqual(attacks[i].distance, profile.minFireDistance - 0.01f, "Attack started too close to the player");
            Assert.GreaterOrEqual(attacks[i].from.z, profile.fireWindowZ.x - 0.01f);
            Assert.GreaterOrEqual(shots[i].time - attacks[i].time, telegraph - 0.05f, "Shot fired before its warning finished");
            Vector3 toAim = shots[i].aim - shots[i].from;
            toAim.y = 0f;
            Assert.Less(Vector3.Angle(shots[i].dir, toAim), 0.5f, "Shot must go where the locked aim line pointed");
            Assert.Less(Vector3.Distance(shots[i].aim, health.transform.position), 0.01f, "Aim is the player's position");
            if (i > 0)
            {
                Assert.GreaterOrEqual(attacks[i].time - attacks[i - 1].time, profile.minSecondsBetweenAttacks - 0.02f, "Attacks too close together");
            }
        }
        Assert.LessOrEqual(maxBusy, profile.maxShotsInFlight, "Too many shots at once");
        Assert.IsFalse(director.TryBeginAttack(new Vector3(0f, 0f, 6f)), "No attacks after the encounter is over");

        // Restart removes enemy shots and silences charge/shot sounds.
        director.Fire(new Vector3(0f, 0f, 6f), health.transform.position, 8f);
        Assert.AreEqual(1, director.ActiveShotCount);
        harness.RestartAll();
        yield return null;
        Assert.AreEqual(0, director.ActiveShotCount, "Restart should remove enemy shots");
        Assert.AreEqual(0, director.PendingAttacks);
        Assert.AreEqual(0, fx.ActiveVoiceCount());
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    private IEnumerator EditorSceneManagerLoad()
    {
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = LevelWavesTestScenes.Newest(ScenePath);
        Assert.IsTrue(scene.isLoaded);
    }

    // ---- Player speed ----

    [Test]
    public void PlayerSpeed_Is20PercentHigher_AndStillFitsThePlayArea()
    {
        var go = Track(new GameObject("FastShip"));
        var ship = go.AddComponent<TestPlayerShip>();
        Assert.AreEqual(9.6f, ship.MaxSpeed, 1e-4f, "8 x 1.2");
        go.transform.position = new Vector3(ship.XLimits.x, 0f, -4f);
        float t = 0f;
        while (go.transform.position.x < ship.XLimits.y - 1e-3f && t < 5f)
        {
            ship.Step(Vector2.right, 1f / 60f);
            t += 1f / 60f;
        }
        Debug.Log($"[Speed] Crossing the {ship.XLimits.y - ship.XLimits.x:F0}-unit play area takes {t:F2} s");
        Assert.That(t, Is.InRange(1.6f, 2.4f), "Crossing the play area should take about 2 s (not twitchy, not sluggish)");

        // Same result at 60 and 240 fps.
        var a = Track(new GameObject("A")).AddComponent<TestPlayerShip>();
        var b = Track(new GameObject("B")).AddComponent<TestPlayerShip>();
        for (int i = 0; i < 30; i++) a.Step(new Vector2(1f, 1f), 1f / 60f);
        for (int i = 0; i < 120; i++) b.Step(new Vector2(1f, 1f), 1f / 240f);
        for (int i = 0; i < 15; i++) a.Step(Vector2.zero, 1f / 60f);
        for (int i = 0; i < 60; i++) b.Step(Vector2.zero, 1f / 240f);
        Assert.Less(Vector3.Distance(a.transform.position, b.transform.position), 0.02f);
        Assert.LessOrEqual(a.Velocity.magnitude, 1e-4f, "Stops precisely");
    }

    // ---- Sound and effects ----

    [Test]
    public void EnemyExplosion_HasFlashBlastSparksAndDebris_AndIsBrief()
    {
        var prefab = Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_FX_Explosion.prefab");
        var names = new HashSet<string>();
        foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            names.Add(ps.name);
            float longest = ps.main.duration + ps.main.startLifetime.constantMax;
            Assert.LessOrEqual(longest, 1f, ps.name + " lingers too long (could hide incoming attacks)");
        }
        foreach (string part in new[] { "Flash", "Fireball", "ShockRing", "Debris" })
        {
            Assert.IsTrue(names.Contains(part), "Explosion is missing its " + part);
        }
        Transform flash = prefab.transform.Find("Flash");
        Assert.LessOrEqual(flash.GetComponent<ParticleSystem>().main.startLifetime.constantMax, 0.15f, "The flash must be brief");
        var debris = prefab.transform.Find("Debris").GetComponent<ParticleSystemRenderer>();
        Assert.AreEqual(ParticleSystemRenderMode.Mesh, debris.renderMode);
        Assert.IsNotNull(debris.mesh);
    }
}
#endif
