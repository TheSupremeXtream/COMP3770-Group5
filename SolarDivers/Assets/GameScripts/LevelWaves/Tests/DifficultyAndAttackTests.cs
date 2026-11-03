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
        // Wave 1 introduces the patterns, one per group, with no reactions and only single, slow, unleading shots.
        Assert.AreEqual(3, new HashSet<TestMovePattern>(w1.groupPatterns).Count, "Wave 1 shows three different patterns");
        Assert.AreEqual(0f, w1.laneSeekDistance, "Wave 1: no repositioning");
        Assert.AreEqual(TestEnemyClassMask.None, w1.flankingClasses | w1.pursuitClasses, "Wave 1: no flanking or pursuit");
        Assert.AreEqual(1, w1.shotsPerBurst, "Wave 1: single shots only");
        Assert.AreEqual(0f, w1.leadFactor, "Wave 1: no prediction");
        Assert.IsTrue(w1.rocketClasses == TestEnemyClassMask.None && !w1.groupLeaderRocket, "Wave 1: no rockets");
        Assert.GreaterOrEqual(w1.telegraphSeconds, 0.9f, "Wave 1: long, readable warnings");
        // Wave 2 combines them and introduces bursts, a little lead, pursuit and the first rockets.
        CollectionAssert.Contains(w2.groupPatterns, TestMovePattern.DiveRegroup, "Wave 2 adds dive-and-regroup");
        Assert.Greater(new HashSet<TestMovePattern>(w2.groupPatterns).Count, 1, "Wave 2 combines patterns");
        Assert.IsTrue(w2.laneSeekDistance > 0f && w2.laneSeekDistance <= 2f, "Wave 2: limited lane shift");
        Assert.AreNotEqual(TestEnemyClassMask.None, w2.pursuitClasses, "Wave 2: short pursuit");
        Assert.LessOrEqual(w2.pursuitSeconds, 1f, "Pursuit is short");
        Assert.Greater(w2.shotsPerBurst, 1, "Wave 2: bursts");
        Assert.IsTrue(w2.leadFactor > 0f && w2.leadFactor <= 0.5f, "Wave 2: limited prediction");
        Assert.IsTrue(w2.groupLeaderRocket && w2.rocketClasses == TestEnemyClassMask.None, "Wave 2: one rocket per group (the leader)");
        // Wave 3 puts on more pressure, still within readable limits.
        Assert.AreNotEqual(TestEnemyClassMask.None, w3.flankingClasses & TestEnemyClassMask.Interceptor, "Wave 3: interceptors flank");
        Assert.AreNotEqual(TestEnemyClassMask.None, w3.pursuitClasses, "Wave 3: pursuit");
        Assert.GreaterOrEqual(w3.flankOffset, 2f, "Flankers pass beside the player, not over it");
        Assert.LessOrEqual(Mathf.Max(w3.trackingSeconds, w3.pursuitSeconds), 1f, "Tracking is brief");
        Assert.GreaterOrEqual(w3.shotsPerBurst, w2.shotsPerBurst, "Wave 3 bursts at least as long");
        Assert.IsTrue(w3.leadFactor >= w2.leadFactor && w3.leadFactor <= 0.8f, "Wave 3 leads more, but never fully");
        Assert.AreNotEqual(TestEnemyClassMask.None, w3.rocketClasses, "Wave 3: heavies carry rockets");
        Assert.GreaterOrEqual(w3.aimedShooterEveryNth, 2, "Only some ships shoot");
        foreach (TestWaveDifficulty w in profile.waves)
        {
            Assert.GreaterOrEqual(w.telegraphSeconds, 0.6f, w.label + ": attacks need a readable warning");
            Assert.GreaterOrEqual(w.rocketWarningSeconds, 0.8f, w.label + ": rockets need a readable warning");
            Assert.LessOrEqual(w.shotSpeed, 10f, w.label + ": shots must be slow enough to dodge");
            Assert.LessOrEqual(w.maxLead, 2f, w.label + ": prediction is capped");
            Assert.IsTrue(w.variation > 0f && w.variation <= 0.3f, w.label + ": some bounded variation between runs");
        }
        Assert.LessOrEqual(w1.speedMultiplier, w2.speedMultiplier);
        Assert.LessOrEqual(w2.speedMultiplier, w3.speedMultiplier);
        Assert.LessOrEqual(w3.speedMultiplier, 1.25f, "Difficulty comes from behaviour, not raw speed");
        Assert.GreaterOrEqual(profile.minEscapeGap, 2f, "An escape lane wider than the player ship must stay open");
        Assert.LessOrEqual(profile.maxShotsInFlight, 3);
        Assert.GreaterOrEqual(profile.minSecondsBetweenAttacks, 1f);
        Assert.GreaterOrEqual(profile.minFireDistance, 6f);
        Assert.LessOrEqual(profile.maxRocketsInFlight, 2, "At most two rockets at once");
        Assert.GreaterOrEqual(profile.minSecondsBetweenRockets, 2.5f, "Rockets are spaced out");
        Assert.GreaterOrEqual(profile.minRocketDistance, 7f, "Rockets launch from far enough away to react");
        Assert.GreaterOrEqual(profile.fireWindowZ.x, 2f, "No attacks start inside the player's zone");
        Assert.AreEqual(0, profile.fixedSeed, "The game varies between runs (tests fix the seed themselves)");
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
    public IEnumerator Encounter_Attacks_GrowWaveByWave_FollowFairnessRules_AndRestartClearsThem()
    {
        yield return EditorSceneManagerLoad();
        TestAttackDirector director = Find<TestAttackDirector>();
        TestPlayerHealth health = Find<TestPlayerHealth>();
        WaveEncounter encounter = Find<WaveEncounter>();
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        TestFxManager fx = Find<TestFxManager>();
        TestDifficultyProfile profile = director.Profile;
        health.enabled = false; // survive the whole run; the player sits still at its start position

        var attacks = new List<(float time, int wave, float distance)>();
        var shots = new List<(int wave, Vector3 from, Vector3 dir, Vector3 aim)>();
        var rockets = new List<(float time, int wave, float distance)>();
        director.AttackStarted += from => attacks.Add((Time.time, encounter.CurrentWaveIndex, Vector3.Distance(from, health.transform.position)));
        director.ShotFired += (from, dir, aim) => shots.Add((encounter.CurrentWaveIndex, from, dir, aim));
        director.RocketWarningStarted += from => rockets.Add((Time.time, encounter.CurrentWaveIndex, Vector3.Distance(from, health.transform.position)));
        int maxBusy = 0, maxRockets = 0;

        float end = Time.realtimeSinceStartup + 120f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            Time.timeScale = 4f;
            maxBusy = Mathf.Max(maxBusy, director.BusyAttacks);
            maxRockets = Mathf.Max(maxRockets, director.ActiveRocketCount + director.PendingRockets);
            yield return null;
        }
        Time.timeScale = 1f;
        Assert.IsTrue(encounter.IsCompleted);
        int[] perWave = new int[3], shotsPerWave = new int[3], rocketsPerWave = new int[3];
        foreach (var a in attacks) perWave[a.wave]++;
        foreach (var s in shots) shotsPerWave[s.wave]++;
        foreach (var r in rockets) rocketsPerWave[r.wave]++;
        Debug.Log($"[Attacks] gun attacks per wave {perWave[0]}/{perWave[1]}/{perWave[2]}, shots {shotsPerWave[0]}/{shotsPerWave[1]}/{shotsPerWave[2]}, " +
                  $"rockets {rocketsPerWave[0]}/{rocketsPerWave[1]}/{rocketsPerWave[2]}; at most {maxBusy} gun attacks and {maxRockets} rockets busy at once");

        Assert.Greater(perWave[0], 0, "Wave 1 has a few single shots");
        Assert.AreEqual(perWave[0], shotsPerWave[0], "Wave 1: one shot per attack");
        Assert.AreEqual(0, rocketsPerWave[0], "No rockets in wave 1");
        Assert.Greater(rocketsPerWave[1], 0, "Rockets are introduced in wave 2");
        Assert.Greater(rocketsPerWave[2], 0, "Rockets in wave 3");
        Assert.Greater(shotsPerWave[1] + shotsPerWave[2], perWave[1] + perWave[2], "Later waves fire bursts");
        Assert.Greater(shots.Count, 0);
        foreach (var s in shots)
        {
            Vector3 toAim = s.aim - s.from;
            toAim.y = 0f;
            Assert.Less(Vector3.Angle(s.dir, toAim), 0.5f, "Every shot flies along its locked aim line");
            // The player stands still, so limited prediction must aim exactly at it.
            Assert.Less(Vector3.Distance(s.aim, health.transform.position), 0.01f, "Prediction adds nothing when the player is still");
        }
        for (int i = 0; i < attacks.Count; i++)
        {
            Assert.GreaterOrEqual(attacks[i].distance, profile.minFireDistance - 0.01f, "Attack started too close to the player");
            if (i > 0)
            {
                Assert.GreaterOrEqual(attacks[i].time - attacks[i - 1].time, profile.minSecondsBetweenAttacks - 0.02f, "Attacks too close together");
            }
        }
        for (int i = 0; i < rockets.Count; i++)
        {
            Assert.GreaterOrEqual(rockets[i].distance, profile.minRocketDistance - 0.01f, "Rocket launched too close");
            if (i > 0)
            {
                Assert.GreaterOrEqual(rockets[i].time - rockets[i - 1].time, profile.minSecondsBetweenRockets - 0.02f, "Rockets too close together");
            }
        }
        Assert.LessOrEqual(maxBusy, profile.maxShotsInFlight, "Too many gun attacks at once");
        Assert.LessOrEqual(maxRockets, profile.maxRocketsInFlight, "Too many rockets at once");
        Assert.AreEqual(0, director.TryBeginAttack(new Vector3(0f, 0f, 6f)), "No attacks after the encounter is over");
        Assert.IsFalse(director.TryBeginRocket(new Vector3(0f, 0f, 7f)), "No rockets after the encounter is over");

        // Restart removes enemy shots and rockets and silences sounds.
        director.Fire(new Vector3(0f, 0f, 6f), health.transform.position, 8f);
        director.LaunchRocket(new Vector3(2f, 0f, 7f), Vector3.back);
        Assert.AreEqual(1, director.ActiveShotCount);
        Assert.AreEqual(1, director.ActiveRocketCount);
        harness.RestartAll();
        yield return null;
        Assert.AreEqual(0, director.ActiveShotCount, "Restart should remove enemy shots");
        Assert.AreEqual(0, director.ActiveRocketCount, "Restart should remove rockets (and their engine sound)");
        Assert.AreEqual(0, director.PendingAttacks + director.PendingRockets);
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
