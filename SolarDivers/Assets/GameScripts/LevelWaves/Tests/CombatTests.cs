#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Homing rocket limits (turn rate, speed, burn-out, lifetime, dodging, caps), aimed bursts with limited prediction,
/// bounded seeded variation between runs, and short pursuit.
/// </summary>
public class CombatTests
{
    private const string ProfilePath = "Assets/Prefabs/LevelWaves/TEST_Difficulty_Encounter01.asset";
    private const string RocketPath = "Assets/Prefabs/LevelWaves/TEST_HomingRocket.prefab";
    private const string ShotPath = "Assets/Prefabs/LevelWaves/TEST_EnemyShot.prefab";
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
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
    }

    private T Track<T>(T o) where T : Object
    {
        created.Add(o);
        return o;
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    // A profile copy we can change freely (the asset stays untouched).
    private TestDifficultyProfile ProfileCopy()
    {
        return Track(Object.Instantiate(Load<TestDifficultyProfile>(ProfilePath)));
    }

    private TestAttackDirector MakeDirector(TestDifficultyProfile profile, Transform player)
    {
        var director = Track(new GameObject("Director")).AddComponent<TestAttackDirector>();
        director.Configure(profile, player, null, null, Load<GameObject>(ShotPath), Load<GameObject>(RocketPath));
        return director;
    }

    private Transform MakePlayer(Vector3 position)
    {
        // A real hitbox: kinematic trigger + TestPlayerHealth, like the test ship.
        var go = Track(new GameObject("Player"));
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.2f, 0.6f, 1.6f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        go.AddComponent<TestPlayerHealth>();
        return go.transform;
    }

    // ---- Homing rockets ----

    [Test]
    public void Rocket_TurnsWithinItsLimit_NeverExceedsMaxSpeed_BurnsOut_AndExpires()
    {
        Transform player = MakePlayer(new Vector3(6f, 0f, -5f));
        player.GetComponent<Collider>().enabled = false; // only the flight path here
        TestAttackDirector director = MakeDirector(ProfileCopy(), player);
        TestHomingRocket rocket = director.LaunchRocket(new Vector3(-4f, 0f, 7f), Vector3.back);
        rocket.enabled = false; // driven by Step
        const float dt = 1f / 60f;
        Vector2 heading = rocket.Heading;
        float worstTurn = 0f, worstSpeed = 0f, t = 0f;
        Vector2 headingAtBurnOut = Vector2.zero;
        bool burnedOut = false;
        while (rocket != null && t < rocket.Lifetime + 1f)
        {
            rocket.Step(dt);
            t += dt;
            if (rocket == null || !rocket)
            {
                break;
            }
            float turn = Mathf.Abs(Vector2.SignedAngle(heading, rocket.Heading)) / dt;
            worstTurn = Mathf.Max(worstTurn, turn);
            worstSpeed = Mathf.Max(worstSpeed, rocket.Speed);
            heading = rocket.Heading;
            if (rocket.Age < rocket.ArmingSeconds)
            {
                Assert.Less(turn, 0.5f, "No homing before it is armed (flies straight off the launcher)");
            }
            if (!burnedOut && rocket.Age > rocket.ArmingSeconds + rocket.HomingSeconds)
            {
                burnedOut = true;
                headingAtBurnOut = rocket.Heading;
            }
            if (burnedOut)
            {
                Assert.Less(Vector2.Angle(headingAtBurnOut, rocket.Heading), 0.01f, "After burn-out it flies straight");
            }
        }
        Debug.Log($"[Rocket] worst turn {worstTurn:F0} deg/s (limit {rocket?.TurnRate ?? 70f}), top speed {worstSpeed:F2}, gone after {t:F2} s");
        Assert.LessOrEqual(worstTurn, 70f + 1f, "Turn rate limit");
        Assert.LessOrEqual(worstSpeed, 6.5f + 1e-3f, "Speed limit");
        Assert.IsTrue(burnedOut, "Homing must stop (burn-out)");
        Assert.IsTrue(rocket == null || !rocket || rocket.Age >= rocket.Lifetime, "Removed by its lifetime or on leaving the area");
    }

    // Flies one rocket at a player who either stands still or side-steps once the rocket is close; returns how close it got.
    private IEnumerator RocketPass(bool dodge, System.Action<float, bool> result)
    {
        Transform player = MakePlayer(new Vector3(0f, 0f, -5f));
        TestPlayerHealth health = player.GetComponent<TestPlayerHealth>();
        TestAttackDirector director = MakeDirector(ProfileCopy(), player);
        yield return new WaitForFixedUpdate();
        TestHomingRocket rocket = director.LaunchRocket(new Vector3(0f, 0f, 7f), Vector3.back);
        float closest = float.MaxValue;
        bool dodging = false;
        float end = Time.time + 6f;
        while (rocket != null && rocket && Time.time < end && health.HitsRemaining == 3)
        {
            Vector3 rel = rocket.transform.position - player.position;
            rel.y = 0f;
            closest = Mathf.Min(closest, rel.magnitude);
            // Side-step at full speed once the rocket is about 4.5 units away (its turning circle is ~5 units).
            if (dodge && (dodging || rel.magnitude < 4.5f))
            {
                dodging = true;
                player.position += Vector3.right * (9f * Time.deltaTime);
            }
            yield return null;
        }
        result(closest, health.HitsRemaining < 3);
    }

    [UnityTest]
    public IEnumerator Rocket_HitsAStillPlayer_ButASideStepEvadesIt()
    {
        float stillClosest = 0f, dodgeClosest = 0f;
        bool stillHit = false, dodgeHit = false;
        yield return RocketPass(false, (c, h) => { stillClosest = c; stillHit = h; });
        TearDown();
        yield return null;
        yield return RocketPass(true, (c, h) => { dodgeClosest = c; dodgeHit = h; });
        Debug.Log($"[Rocket] still player: hit {stillHit} (closest {stillClosest:F2}); side-step: hit {dodgeHit} (closest {dodgeClosest:F2})");
        Assert.IsTrue(stillHit, "A rocket should reach a player who does not move");
        Assert.IsFalse(dodgeHit, "A late side-step at full speed should make it miss");
        Assert.Greater(dodgeClosest, 1f, "It should miss clearly, not graze");
    }

    [Test]
    public void Rockets_AreCappedSpacedAndKeptAtADistance()
    {
        TestDifficultyProfile profile = ProfileCopy();
        Transform player = MakePlayer(new Vector3(0f, 0f, -5f));
        TestAttackDirector director = MakeDirector(profile, player);
        Assert.IsFalse(director.TryBeginRocket(new Vector3(0f, 0f, 1f)), "Too close to the player");
        Assert.IsFalse(director.TryBeginRocket(new Vector3(0f, 0f, 9.5f)), "Above the on-screen fire window");
        Assert.IsTrue(director.TryBeginRocket(new Vector3(0f, 0f, 6f)));
        Assert.IsFalse(director.TryBeginRocket(new Vector3(3f, 0f, 6f)), "Second rocket inside the minimum gap");
        profile.minSecondsBetweenRockets = 0f;
        Assert.IsTrue(director.TryBeginRocket(new Vector3(3f, 0f, 6f)));
        Assert.IsFalse(director.TryBeginRocket(new Vector3(-3f, 0f, 6f)), "Cap of rockets in flight or being readied");
        Assert.AreEqual(profile.maxRocketsInFlight, director.PendingRockets);
        director.ClearAll();
        Assert.AreEqual(0, director.PendingRockets, "Restart clears reservations");
    }

    // ---- Bursts and limited prediction ----

    [UnityTest]
    public IEnumerator Burst_FiresTheConfiguredShots_AlongOneLockedLine_WithCappedLead()
    {
        Transform player = MakePlayer(new Vector3(0f, 0f, -5f));
        player.GetComponent<Collider>().enabled = false;
        TestDifficultyProfile profile = ProfileCopy();
        TestAttackDirector director = MakeDirector(profile, player);
        var shooterGo = Track(Object.Instantiate(Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_Enemy_Basic.prefab"),
            new Vector3(-3f, 0f, 6f), Quaternion.Euler(0f, 180f, 0f)));
        shooterGo.GetComponent<TestEnemyMover>().enabled = false;
        var shooter = shooterGo.GetComponent<TestEnemyShooter>();
        shooter.Arm(true, 0.9f, 8f, 4, 0.13f, 0.5f, 1.8f);
        var shots = new List<(Vector3 dir, Vector3 aim)>();
        director.ShotFired += (from, dir, aim) => shots.Add((dir, aim));
        // The player strafes right at a steady 6 units/s while the burst is aimed.
        float end = Time.time + 2.5f;
        Vector3 predicted = Vector3.zero, playerAtPrediction = Vector3.zero;
        while (Time.time < end && !shooter.HasFired)
        {
            player.position += Vector3.right * (6f * Time.deltaTime);
            yield return null;
            // Measured while the player is still moving (the director reads its velocity each frame).
            predicted = director.PredictedAim(new Vector3(-3f, 0f, 6f), 8f, 0.5f, 1.8f);
            playerAtPrediction = director.PlayerPosition;
        }
        yield return null;
        Assert.AreEqual(4, shots.Count, "A 4-shot burst");
        foreach (var s in shots)
        {
            Assert.Less(Vector3.Angle(s.dir, shots[0].dir), 0.01f, "Every shot of a burst follows the same locked line");
        }
        // Lead: ahead of the player in the direction it is moving, but never more than Max Lead.
        Debug.Log($"[Burst] {shots.Count} shots on one line; predicted aim {predicted.x - playerAtPrediction.x:F2} units ahead of a player moving at 6 units/s");
        Assert.LessOrEqual(Vector3.Distance(predicted, playerAtPrediction), 1.8f + 1e-3f, "Lead is capped");
        Assert.Greater(predicted.x, playerAtPrediction.x + 0.3f, "Lead points the way the player is moving");
    }

    // ---- Variation and pursuit ----

    private List<Vector2> RouteFor(TestDifficultyProfile profile, int seed, float x, int group)
    {
        profile.fixedSeed = seed;
        var director = MakeDirector(profile, MakePlayer(new Vector3(0f, 0f, -5f)));
        director.enabled = false;
        director.enabled = true; // pick up the fixed seed
        var go = Track(Object.Instantiate(Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_Enemy_Basic.prefab"),
            new Vector3(x, 0f, 10f), Quaternion.Euler(0f, 180f, 0f)));
        var mover = go.GetComponent<TestEnemyMover>();
        mover.enabled = false;
        mover.OnWaveSpawn(new WaveSpawnInfo { waveIndex = 1, groupIndex = group, slotIndex = 1, slotCount = 5, slotOffsetX = 0f, groupShift = 0f });
        var route = new List<Vector2>(mover.Route);
        Object.DestroyImmediate(go);
        director.enabled = false;
        return route;
    }

    [Test]
    public void Variation_IsSeeded_Bounded_AndDiffersBetweenRuns()
    {
        TestDifficultyProfile profile = ProfileCopy();
        List<Vector2> a1 = RouteFor(profile, 1234, 0f, 0);
        List<Vector2> a2 = RouteFor(profile, 1234, 0f, 0);
        CollectionAssert.AreEqual(a1, a2, "Same seed, same path");
        int differ = 0;
        float v = profile.waves[1].variation;
        for (int seed = 1; seed <= 12; seed++)
        {
            List<Vector2> b = RouteFor(profile, seed * 7919, 0f, 0);
            for (int i = 0; i < Mathf.Min(b.Count, a1.Count); i++)
            {
                if (Vector2.Distance(b[i], a1[i]) > 0.05f)
                {
                    differ++;
                    break;
                }
            }
            foreach (Vector2 p in b)
            {
                Assert.LessOrEqual(Mathf.Abs(p.x), 8f + 1e-3f, "Variation stays inside the lane limits");
            }
        }
        Debug.Log($"[Variation] {differ}/12 seeds gave a different path (variation {v:P0})");
        Assert.Greater(differ, 6, "Most runs should differ");
    }

    [UnityTest]
    public IEnumerator Pursuit_IsShort_ThenCommitsToTheLastSeenLane()
    {
        TestDifficultyProfile profile = ProfileCopy();
        TestWaveDifficulty wave2 = profile.waves[1];
        Transform player = MakePlayer(new Vector3(3f, 0f, -5f));
        player.GetComponent<Collider>().enabled = false;
        TestAttackDirector director = MakeDirector(profile, player);
        yield return null;
        var go = Track(Object.Instantiate(Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_Enemy_Interceptor.prefab"),
            new Vector3(-2f, 0f, 10f), Quaternion.Euler(0f, 180f, 0f)));
        var m = go.GetComponent<TestEnemyMover>();
        m.enabled = false;
        m.ApplyDifficulty(wave2);
        m.PlanRoute(TestMovePattern.CurvedPass, -2f, -4f, 0f);
        const float dt = 1f / 60f;
        float t = 0f, trackStart = -1f, trackEnd = -1f;
        while (t < 6f && trackEnd < 0f)
        {
            if (m.IsTracking && trackStart < 0f) trackStart = t;
            if (trackStart >= 0f && !m.IsTracking) { trackEnd = t; break; }
            if (trackStart >= 0f) player.position += new Vector3(1.5f * dt, 0f, 0f);
            m.Step(dt);
            t += dt;
        }
        Assert.IsTrue(m.IsPursuing, "Wave 2 interceptors pursue");
        Assert.IsFalse(m.IsFlanking);
        Assert.LessOrEqual(trackEnd - trackStart, wave2.pursuitSeconds + 2f * dt, "Pursuit is short");
        float lane = m.LaneX;
        Assert.AreEqual(player.position.x, lane, 0.25f, "Commits to the player's last seen lane");
        player.position = new Vector3(-6f, 0f, -5f);
        for (int i = 0; i < 60; i++) m.Step(dt);
        Assert.AreEqual(lane, m.LaneX, 1e-4f, "No pursuit after it committed");
    }
}
#endif
