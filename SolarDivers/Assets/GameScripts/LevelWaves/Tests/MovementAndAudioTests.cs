#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Enemy speed classes, steering limits, pattern shapes, limited reactions, escape lanes and exits;
/// recorded sound clips, player engine, enemy flybys and audio cleanup.
/// </summary>
public class MovementAndAudioTests
{
    private const string ScenePath = "Assets/Scenes/LevelWaves_Encounter01_Test.unity";
    private const string ProfilePath = "Assets/Prefabs/LevelWaves/TEST_Difficulty_Encounter01.asset";
    private const string BankPath = "Assets/Audio/LevelWaves/TEST_SoundBank.asset";
    private const string Fighter = "Assets/Prefabs/LevelWaves/TEST_Enemy_Basic.prefab";
    private const string Heavy = "Assets/Prefabs/LevelWaves/TEST_Enemy_Weaver.prefab";
    private const string Interceptor = "Assets/Prefabs/LevelWaves/TEST_Enemy_Interceptor.prefab";
    private readonly List<GameObject> created = new List<GameObject>();
    private Scene scene;

    // Start and end with no copy of the test scene loaded (a failed assertion elsewhere could leave one behind,
    // and its enemies would be counted by the whole-encounter checks).
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return LevelWavesTestScenes.UnloadAll(ScenePath);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
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
        yield return LevelWavesTestScenes.UnloadAll(ScenePath);
        yield return null;
    }

    private GameObject Track(GameObject go)
    {
        created.Add(go);
        return go;
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

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

    private IEnumerator LoadScene()
    {
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = LevelWavesTestScenes.Newest(ScenePath);
        Assert.IsTrue(scene.isLoaded);
        yield return null;
    }

    private static IEnumerator WaitGame(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            yield return null;
        }
    }

    // Signed angle (degrees, counter-clockwise) from atan2: exact for the tiny per-frame angles measured here
    // (Vector2.SignedAngle uses acos, which loses ~0.02 degrees of precision near zero).
    private static float PreciseAngle(Vector2 from, Vector2 to)
    {
        return Mathf.Atan2(from.x * to.y - from.y * to.x, Vector2.Dot(from, to)) * Mathf.Rad2Deg;
    }

    // A test enemy facing down the screen, driven only by Step() (its own Update is off).
    private TestEnemyMover MakeEnemy(string prefab, float x, TestWaveDifficulty wave, TestMovePattern pattern, float groupMin, float groupMax)
    {
        GameObject go = Track(Object.Instantiate(Load<GameObject>(prefab), new Vector3(x, 0f, 10f), Quaternion.Euler(0f, 180f, 0f)));
        var mover = go.GetComponent<TestEnemyMover>();
        mover.enabled = false;
        if (wave != null)
        {
            mover.ApplyDifficulty(wave);
        }
        mover.PlanRoute(pattern, x, groupMin, groupMax);
        return mover;
    }

    // ---- Classes ----

    [Test]
    public void EnemyClasses_HaveDistinctSpeedsAndTurnRates()
    {
        var heavy = Load<GameObject>(Heavy).GetComponent<TestEnemyMover>();
        var fighter = Load<GameObject>(Fighter).GetComponent<TestEnemyMover>();
        var interceptor = Load<GameObject>(Interceptor).GetComponent<TestEnemyMover>();
        Assert.AreEqual(TestEnemyClass.Heavy, heavy.Class);
        Assert.AreEqual(TestEnemyClass.Fighter, fighter.Class);
        Assert.AreEqual(TestEnemyClass.Interceptor, interceptor.Class);
        Assert.Less(heavy.Speed, fighter.Speed * 0.8f, "Heavies are clearly slower");
        Assert.Greater(interceptor.Speed, fighter.Speed * 1.3f, "Interceptors are clearly faster");
        Assert.Less(heavy.TurnRate, fighter.TurnRate);
        Assert.Less(fighter.TurnRate, interceptor.TurnRate);
        Assert.LessOrEqual(interceptor.TurnRate, 180f, "Even the fastest class turns in a readable arc");
        // Re-uses the existing Dart model (smaller), no new art.
        Assert.AreEqual(Load<GameObject>(Fighter).GetComponentInChildren<MeshFilter>().sharedMesh,
            Load<GameObject>(Interceptor).GetComponentInChildren<MeshFilter>().sharedMesh);
    }

    // ---- Steering limits for every class, pattern and wave ----

    [Test]
    public void AllPatterns_TurnSmoothly_StayInBounds_FlyStraightNearThePlayer_AndLeave()
    {
        TestDifficultyProfile profile = Load<TestDifficultyProfile>(ProfilePath);
        var patterns = new[] { TestMovePattern.Straight, TestMovePattern.CurvedPass, TestMovePattern.FlankWeave, TestMovePattern.SideSweep, TestMovePattern.PassAndRetreat };
        const float dt = 1f / 60f;
        float worstTurn = 0f, worstTurnRatio = 0f, worstLife = 0f, lowestRetreat = float.MaxValue;
        foreach (string prefab in new[] { Heavy, Fighter, Interceptor })
        {
            foreach (TestWaveDifficulty wave in profile.waves)
            {
                foreach (TestMovePattern pattern in patterns)
                {
                    foreach (float x0 in new[] { -7f, -2.5f, 0f, 3f, 7f })
                    {
                        TestMovePattern p = pattern;
                        TestEnemyMover m = MakeEnemy(prefab, x0, wave, p, x0 - 2f, x0 + 2f);
                        string label = $"{m.name} {wave.label} {p} x0={x0}";
                        Vector2 heading = m.Heading;
                        float lastTurn = 0f, t = 0f, maxSide = 0f;
                        var trace = new System.Text.StringBuilder();
                        bool exited = false;
                        while (t < m.MaxLifetime + 10f)
                        {
                            float zBefore = m.transform.position.z;
                            m.Step(dt);
                            t += dt;
                            Vector3 pos = m.transform.position;
                            float turn = PreciseAngle(heading, m.Heading) / dt;
                            heading = m.Heading;
                            worstTurn = Mathf.Max(worstTurn, Mathf.Abs(turn));
                            worstTurnRatio = Mathf.Max(worstTurnRatio, Mathf.Abs(turn) / m.TurnRate);
                            Assert.LessOrEqual(Mathf.Abs(turn), m.TurnRate + 0.5f, label + ": turned faster than its turn rate");
                            bool inStraightZone = zBefore < m.StraightBelowZ && heading.y < 0f;
                            bool entering = !inStraightZone && pos.z < m.StraightBelowZ && heading.y < 0f;
                            if (entering)
                            {
                                // The remaining turn is cut to zero here; it must already be almost nothing.
                                Assert.LessOrEqual(Mathf.Abs(lastTurn), 5f, label + ": still turning when it reached the straight zone");
                            }
                            else if (!inStraightZone)
                            {
                                // Turn rate builds up gradually: no instant change of direction or of turning speed.
                                Assert.LessOrEqual(Mathf.Abs(turn - lastTurn), m.TurnAcceleration * dt + 1f, label + ": turn started or stopped abruptly");
                            }
                            else
                            {
                                Assert.AreEqual(0f, turn, 1e-3f, label + ": turning near the player");
                                Assert.LessOrEqual(Vector2.Angle(heading, Vector2.down), 30f, label + ": crosses the player's zone at a shallow angle");
                            }
                            lastTurn = turn;
                            if (Mathf.Repeat(t, 0.5f) < dt)
                            {
                                trace.Append($" [{t:F1}s ({pos.x:F1},{pos.z:F1}) wp{m.WaypointIndex} av{m.AngularVelocity:F0}]");
                            }
                            if (heading.y > 0.2f)
                            {
                                lowestRetreat = Mathf.Min(lowestRetreat, pos.z);
                            }
                            maxSide = Mathf.Max(maxSide, Mathf.Abs(pos.x - x0));
                            Assert.LessOrEqual(Mathf.Abs(pos.x), m.LaneLimitX + 2f, label + $": left the play area sideways at {pos} (t {t:F2}, heading {m.Heading}, route " +
                                string.Join(" ", m.Route) + ") trace" + trace);
                            Assert.LessOrEqual(pos.z, 10.6f, label + ": flew back off the top");
                            if (pos.z < -12f)
                            {
                                exited = true;
                                break;
                            }
                        }
                        Assert.IsTrue(exited, label + ": never left the screen (endless pursuit)");
                        worstLife = Mathf.Max(worstLife, t);
                        if (p == TestMovePattern.PassAndRetreat)
                        {
                            Assert.IsTrue(m.EverRetreated, label + ": no retreat");
                        }
                        if (p == TestMovePattern.CurvedPass || (p == TestMovePattern.SideSweep && Mathf.Abs(x0) < 5f))
                        {
                            Assert.Greater(maxSide, 1.5f, label + ": should move clearly sideways");
                        }
                        Object.DestroyImmediate(m.gameObject);
                    }
                }
            }
        }
        Debug.Log($"[Movement] worst turn {worstTurn:F0} deg/s ({worstTurnRatio:P0} of the class limit), lowest retreat z {lowestRetreat:F2}, longest time on screen {worstLife:F1} s");
        float straightBelow = Load<GameObject>(Fighter).GetComponent<TestEnemyMover>().StraightBelowZ;
        Assert.GreaterOrEqual(lowestRetreat, straightBelow + 1.5f, "Retreats must happen well above the player's zone");
    }

    [Test]
    public void Steering_IsFrameRateIndependent()
    {
        TestWaveDifficulty wave = Load<TestDifficultyProfile>(ProfilePath).waves[1];
        TestEnemyMover a = MakeEnemy(Fighter, -2f, wave, TestMovePattern.PassAndRetreat, -4f, 0f);
        TestEnemyMover b = MakeEnemy(Fighter, -2f, wave, TestMovePattern.PassAndRetreat, -4f, 0f);
        b.transform.position = a.transform.position;
        for (int i = 0; i < 30 * 5; i++) a.Step(1f / 30f);
        for (int i = 0; i < 144 * 5; i++) b.Step(1f / 144f);
        float gap = Vector3.Distance(a.transform.position, b.transform.position);
        Debug.Log($"[Movement] 30 vs 144 fps after 5 s: {gap:F3} units apart");
        Assert.Less(gap, 0.15f);
    }

    // ---- Reactions ----

    [UnityTest]
    public IEnumerator Flankers_TrackBriefly_ThenCommitBesideThePlayersLastPosition()
    {
        TestDifficultyProfile profile = Load<TestDifficultyProfile>(ProfilePath);
        TestWaveDifficulty wave3 = profile.waves[2];
        var player = Track(new GameObject("Player")).transform;
        player.position = new Vector3(1f, 0f, -5f);
        var director = Track(new GameObject("Director")).AddComponent<TestAttackDirector>();
        director.Configure(profile, player, null, null, Load<GameObject>("Assets/Prefabs/LevelWaves/TEST_EnemyShot.prefab"));
        yield return null;

        TestEnemyMover m = MakeEnemy(Interceptor, -5f, wave3, TestMovePattern.CurvedPass, -7f, -3f);
        float dt = 1f / 60f, t = 0f, trackStart = -1f, trackEnd = -1f;
        while (t < 6f && trackEnd < 0f)
        {
            if (m.IsTracking && trackStart < 0f)
            {
                trackStart = t;
            }
            if (trackStart >= 0f && !m.IsTracking)
            {
                trackEnd = t;
                break;
            }
            if (trackStart >= 0f)
            {
                player.position += new Vector3(1.5f * dt, 0f, 0f); // player drifts right while being watched
            }
            m.Step(dt);
            t += dt;
        }
        Assert.IsTrue(m.IsFlanking, "Interceptor should flank in wave 3");
        Assert.Greater(trackStart, 0f);
        Assert.LessOrEqual(trackEnd - trackStart, wave3.trackingSeconds + 2f * dt, "Tracking time is capped");
        float lastSeen = player.position.x;
        float lane = m.LaneX;
        Assert.Less(lane, lastSeen, "Stays on its own side (came from the left)");
        Assert.AreEqual(wave3.flankOffset, lastSeen - lane, 0.1f, "Descends beside the last observed position, not on it");

        player.position = new Vector3(-6f, 0f, -5f); // player moves after the flanker committed
        for (int i = 0; i < 60; i++)
        {
            m.Step(dt);
        }
        Assert.AreEqual(lane, m.LaneX, 1e-4f, "No pursuit after the tracking window");
    }

    [Test]
    public void LargestGap_FindsTheWidestFreeLane()
    {
        Assert.AreEqual(19f, TestEnemyMover.LargestGap(new List<float>(), 0.75f, -9.5f, 9.5f), 1e-4f);
        Assert.AreEqual(8.75f, TestEnemyMover.LargestGap(new List<float> { 0f }, 0.75f, -9.5f, 9.5f), 1e-4f);
        Assert.AreEqual(2.5f, TestEnemyMover.LargestGap(new List<float> { -8.75f, -5f, -1f, 2.5f, 6f, 8.75f }, 0.75f, -9.5f, 9.5f), 1e-4f);
    }

    // ---- Whole encounter: escape lane, spawns, exits ----

    [UnityTest]
    public IEnumerator Encounter_AlwaysLeavesAnEscapeLane_SpawnsAwayFromThePlayer_AndEveryEnemyLeaves()
    {
        yield return LoadScene();
        WaveEncounter encounter = Find<WaveEncounter>();
        TestPlayerShip ship = Find<TestPlayerShip>();
        TestPlayerHealth health = Find<TestPlayerHealth>();
        TestDifficultyProfile profile = Find<TestAttackDirector>().Profile;
        health.enabled = false;
        ship.enabled = false;

        var firstSeen = new Dictionary<TestEnemyMover, float>();
        var lastSeen = new Dictionary<TestEnemyMover, float>();
        float minGap = float.MaxValue, minGapTime = 0f;
        int samples = 0, flankers = 0, retreats = 0;
        var seenFlank = new HashSet<TestEnemyMover>();
        var seenRetreat = new HashSet<TestEnemyMover>();
        var lanes = new List<Vector2>();
        float end = Time.realtimeSinceStartup + 120f;
        Time.timeScale = 5f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            // The player weaves across the screen like someone dodging, so reactions see different positions.
            ship.transform.position = new Vector3(6f * Mathf.Sin(Time.time * 0.35f), 0f, -5f);
            lanes.Clear();
            foreach (TestEnemyMover m in TestEnemyMover.Active)
            {
                TestEnemyMover id = m;
                Vector3 p = m.transform.position;
                if (!firstSeen.ContainsKey(id))
                {
                    firstSeen[id] = Time.time;
                    Assert.GreaterOrEqual(p.z, 8.5f, "Enemies must spawn at the top, far from the player");
                }
                lastSeen[id] = Time.time;
                Assert.LessOrEqual(Mathf.Abs(p.x), 12f, m.name + " left the play area sideways");
                if (p.z > -8.6f && p.z < 0.6f)
                {
                    lanes.Add(new Vector2(p.x, m.HalfWidth));
                }
                if (m.IsFlanking && seenFlank.Add(id)) flankers++;
                if (m.EverRetreated && seenRetreat.Add(id)) retreats++;
            }
            if (lanes.Count > 0)
            {
                samples++;
                float gap = TestEnemyMover.LargestGap(lanes, -9.5f, 9.5f);
                if (gap < minGap)
                {
                    minGap = gap;
                    minGapTime = encounter.ElapsedTime;
                }
            }
            yield return null;
        }
        Time.timeScale = 1f;
        float longest = 0f;
        foreach (var pair in firstSeen)
        {
            longest = Mathf.Max(longest, lastSeen[pair.Key] - pair.Value);
        }
        Debug.Log($"[EscapeLane] {firstSeen.Count} enemies, {samples} samples with ships in the player's zone, narrowest free lane {minGap:F2} units (at {minGapTime:F1} s), " +
                  $"{flankers} flanked, {retreats} retreated, longest on screen {longest:F1} s, encounter {encounter.ElapsedTime:F1} s, timed out {encounter.TimedOutCount}");
        Assert.IsTrue(encounter.IsCompleted);
        Assert.AreEqual(45, firstSeen.Count);
        Assert.AreEqual(0, encounter.TimedOutCount, "Every enemy should leave on its own (no stalls, no endless pursuit)");
        Assert.Greater(flankers, 0, "Wave 3 interceptors should flank");
        Assert.GreaterOrEqual(retreats, 5, "Pass-and-retreat ships (two groups in wave 2, one in wave 3) should actually retreat");
        Assert.GreaterOrEqual(minGap, profile.minEscapeGap, "A free lane wide enough to dodge into must always remain");
        Assert.LessOrEqual(longest, Load<GameObject>(Fighter).GetComponent<TestEnemyMover>().MaxLifetime + 10f, "After Max Lifetime a ship leaves straight down (a slow heavy needs up to ~10 s)");
        Assert.AreEqual(0, TestEnemyMover.Active.Count, "No enemies left behind");
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    // ---- Recorded sounds ----

    private static float[] Samples(AudioClip clip)
    {
        var data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);
        return data;
    }

    private static (float subShare, float rmsHz, float tail) Measure(AudioClip clip)
    {
        float[] data = Samples(clip);
        float a = 1f - Mathf.Exp(-2f * Mathf.PI * 60f / clip.frequency);
        float l1 = 0f, l2 = 0f;
        double sub = 0, total = 0, diff = 0;
        for (int i = 0; i < data.Length; i++)
        {
            l1 += (data[i] - l1) * a;
            l2 += (l1 - l2) * a;
            sub += l2 * l2;
            total += data[i] * data[i];
            if (i > 0) diff += (data[i] - data[i - 1]) * (data[i] - data[i - 1]);
        }
        // Time until the level stays 40 dB under its loudest 10 ms.
        int win = clip.frequency / 100;
        var levels = new List<double>();
        for (int s = 0; s + win <= data.Length; s += win)
        {
            double e = 0;
            for (int i = s; i < s + win; i++) e += data[i] * data[i];
            levels.Add(e / win);
        }
        double max = 0;
        foreach (double l in levels) max = System.Math.Max(max, l);
        int last = 0;
        for (int i = 0; i < levels.Count; i++) if (levels[i] > max * 1e-4) last = i;
        float rmsHz = (float)(System.Math.Sqrt(diff / System.Math.Max(total, 1e-12)) * clip.frequency / (2.0 * System.Math.PI));
        return ((float)(sub / System.Math.Max(total, 1e-12)), rmsHz, (last + 1) / 100f);
    }

    [Test]
    public void RecordedSounds_AreCredited_Clean_AndDistinct()
    {
        var bank = Load<TestSoundBank>(BankPath);
        Assert.IsNotNull(bank, "Sound bank missing");
        string credits = File.ReadAllText(Path.Combine(Application.dataPath, "Audio/LevelWaves/CREDITS.md"));
        foreach (string source in new[] { "freesound.org/people/qubodup/sounds/189446", "freesound.org/people/Nice_Audio/sounds/420560",
                     "opengameart.org/content/the-free-firearm-sound-library", "opengameart.org/content/cannon-fire",
                     "opengameart.org/content/mechanical-explosion", "opengameart.org/content/metal-impact-sounds", "opengameart.org/node/92774" })
        {
            StringAssert.Contains(source, credits, "CREDITS.md must list " + source);
        }
        StringAssert.Contains("CC0", credits);

        var stats = new Dictionary<string, (float subShare, float rmsHz, float tail)>();
        foreach (AudioClip clip in bank.AllClips())
        {
            Assert.AreEqual(1, clip.channels, clip.name + " should be mono");
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
            }
            float[] data = Samples(clip);
            float peak = 0f;
            foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            Assert.LessOrEqual(peak, 0.92f, clip.name + " would clip");
            Assert.Greater(peak, 0.5f, clip.name + " is nearly silent");
            if (clip != bank.playerEngineLoop && clip != bank.rocketLoop) // loops continue into their own start instead of ending at zero
            {
                Assert.AreEqual(0f, data[data.Length - 1], 0.01f, clip.name + " ends with a click");
            }
            stats[clip.name] = Measure(clip);
            Debug.Log($"[Sound] {clip.name}: {clip.length:F2} s, sub-60 Hz {stats[clip.name].subShare:P0}, RMS frequency {stats[clip.name].rmsHz:F0} Hz, tail {stats[clip.name].tail:F2} s");
        }

        foreach (AudioClip shot in bank.autocannon)
        {
            Assert.LessOrEqual(stats[shot.name].tail, 0.2f, shot.name + ": rings into the next shot (0.2 s apart)");
            Assert.LessOrEqual(shot.length, 0.35f);
            Assert.Less(stats[shot.name].subShare, 0.2f, shot.name + ": too much sub-bass for continuous fire");
        }
        foreach (AudioClip boom in bank.enemyExplosions)
        {
            Assert.GreaterOrEqual(boom.length, 0.8f, boom.name + ": needs a short debris tail");
            Assert.LessOrEqual(boom.length, 1.3f, boom.name + ": tail too long for several kills in a row");
            Assert.Less(stats[boom.name].subShare, 0.5f, boom.name + ": low end not controlled");
        }
        string impact = bank.metalImpacts[0].name, explosion = bank.enemyExplosions[0].name, shot0 = bank.autocannon[0].name, hit = bank.playerHit[0].name;
        Assert.Greater(stats[impact].rmsHz, stats[explosion].rmsHz * 2f, "Metal impacts are much brighter than explosions");
        Assert.Greater(stats[impact].rmsHz, stats[shot0].rmsHz, "Impacts are brighter than gunfire");
        Assert.Less(stats[hit].rmsHz, stats[impact].rmsHz, "Player damage is heavier than an enemy hit");
        Assert.Greater(bank.playerHit[0].length, bank.metalImpacts[0].length, "Player damage lasts longer than an enemy hit");

        // Engine loop is seamless: no jump and no level step at the loop point.
        float[] loop = Samples(bank.playerEngineLoop);
        Assert.Less(Mathf.Abs(loop[loop.Length - 1] - loop[0]), 0.05f, "Engine loop clicks at the seam");
        int n = bank.playerEngineLoop.frequency / 10;
        double startE = 0, endE = 0;
        for (int i = 0; i < n; i++) { startE += loop[i] * loop[i]; endE += loop[loop.Length - 1 - i] * loop[loop.Length - 1 - i]; }
        Assert.Less(System.Math.Abs(10.0 * System.Math.Log10(startE / endE)), 2.0, "Engine loop level jumps at the seam");
        float[] rocketLoop = Samples(bank.rocketLoop);
        Assert.Less(Mathf.Abs(rocketLoop[rocketLoop.Length - 1] - rocketLoop[0]), 0.05f, "Rocket loop clicks at the seam");
        foreach (AudioClip flyby in bank.flybys)
        {
            Assert.Greater(flyby.length, bank.flybyPassSeconds + 1f, flyby.name + ": too short around its pass point");
        }
    }

    [UnityTest]
    public IEnumerator Explosions_DuckEachOther_AndAreLimited()
    {
        var fx = Track(new GameObject("FxDuck")).AddComponent<TestFxManager>();
        fx.SetSounds(Load<TestSoundBank>(BankPath));
        fx.PlayExplosion(Vector3.zero, 1f);
        float first = fx.LastVolume(TestFxManager.Sound.EnemyDestroyed);
        int maxRinging = 0;
        float second = 0f;
        for (int i = 0; i < 6; i++)
        {
            float end = Time.realtimeSinceStartup + 0.05f;
            while (Time.realtimeSinceStartup < end) yield return null;
            fx.PlayExplosion(new Vector3(i - 3f, 0f, 0f), 1f);
            if (i == 0) second = fx.LastVolume(TestFxManager.Sound.EnemyDestroyed);
            maxRinging = Mathf.Max(maxRinging, fx.ActiveVoiceCount(TestFxManager.Sound.EnemyDestroyed));
        }
        Debug.Log($"[Sound] explosion volume {first:F2} then {second:F2} while the first rings; at most {maxRinging} at once");
        Assert.Less(second, first, "A second explosion should be ducked under the first");
        Assert.LessOrEqual(maxRinging, fx.MaxExplosions, "Too many explosions overlapping");
        fx.ClearAll();
        Assert.AreEqual(0, fx.ActiveVoiceCount());
    }

    // ---- Player engine ----

    private TestFlightAudio MakeFlight(out TestPlayerShip ship)
    {
        var shipGo = Track(new GameObject("EngineShip"));
        shipGo.transform.position = new Vector3(0f, 0f, -5f);
        ship = shipGo.AddComponent<TestPlayerShip>();
        ship.enabled = false; // driven by Step() below, no keyboard
        var flight = Track(new GameObject("Flight")).AddComponent<TestFlightAudio>();
        flight.Configure(Load<TestSoundBank>(BankPath), ship);
        return flight;
    }

    [UnityTest]
    public IEnumerator PlayerEngine_FollowsThrust_Smoothly_AndStopsOnRestartAndDisable()
    {
        TestFlightAudio flight = MakeFlight(out TestPlayerShip ship);
        yield return WaitGame(0.6f);
        Assert.IsTrue(flight.EngineRunning, "Engine should start with the ship");
        float idle = flight.EnginePitchNow;
        Assert.Greater(flight.EngineVolumeNow, 0f);

        float previous = idle, maxStep = 0f, peak = idle;
        float end = Time.time + 0.8f;
        while (Time.time < end)
        {
            ship.Step(Vector2.right * (ship.transform.position.x < 6f ? 1f : -1f), Time.deltaTime);
            yield return null;
            maxStep = Mathf.Max(maxStep, Mathf.Abs(flight.EnginePitchNow - previous));
            previous = flight.EnginePitchNow;
            peak = Mathf.Max(peak, previous);
        }
        float loud = flight.EngineVolumeNow;
        end = Time.time + 1.2f;
        while (Time.time < end)
        {
            ship.Step(Vector2.zero, Time.deltaTime);
            yield return null;
            maxStep = Mathf.Max(maxStep, Mathf.Abs(flight.EnginePitchNow - previous));
            previous = flight.EnginePitchNow;
        }
        Debug.Log($"[Engine] pitch idle {idle:F3}, full thrust {peak:F3}, after stopping {previous:F3}; largest change per frame {maxStep:F4}");
        Assert.Greater(peak, idle + 0.05f, "Engine should spool up with thrust");
        Assert.LessOrEqual(peak, 1.1f, "Pitch change stays restrained");
        Assert.Less(maxStep, 0.02f, "Pitch must change smoothly, not jump");
        Assert.Less(previous, peak - 0.04f, "Engine should wind down after stopping");
        Assert.Less(flight.EngineVolumeNow, loud, "Quieter at rest than under thrust");

        flight.ClearAll(); // what R does
        Assert.IsFalse(flight.EngineRunning, "Restart must stop the engine loop");
        Assert.AreEqual(0f, flight.EngineVolumeNow);
        yield return null;
        yield return null;
        Assert.IsTrue(flight.EngineRunning, "Engine restarts for the new run");
        Assert.Less(flight.EngineVolumeNow, loud * 0.5f, "...fading in from silence");

        flight.enabled = false; // what leaving Play mode does
        Assert.IsFalse(flight.EngineRunning, "Disabling must stop the engine loop");
        Assert.IsNull(TestFlightAudio.Active);
    }

    [UnityTest]
    public IEnumerator PlayerEngine_WindsDown_WhenThePlayerIsDefeated()
    {
        yield return LoadScene();
        TestFlightAudio flight = Find<TestFlightAudio>();
        TestPlayerHealth health = Find<TestPlayerHealth>();
        Assert.IsNotNull(flight, "Scene needs PLACEHOLDER_FlightAudio");
        yield return WaitGame(0.5f);
        Assert.IsTrue(flight.EngineRunning);
        for (int i = 0; i < 3; i++)
        {
            health.TryTakeHit(1);
            yield return WaitGame(1.1f);
        }
        Assert.IsTrue(health.IsDefeated);
        Assert.IsFalse(flight.EngineRunning, "Engine should fade out after defeat");
        Find<EncounterTestHarness>().RestartAll();
        yield return WaitGame(0.2f);
        Assert.IsTrue(flight.EngineRunning, "Engine returns after restart");
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    // ---- Enemy flybys ----

    [UnityTest]
    public IEnumerator Flybys_StartForClosePasses_FollowTheEnemy_AreLimited_AndStop()
    {
        TestFlightAudio flight = MakeFlight(out TestPlayerShip ship);
        var starts = new List<(TestEnemyMover enemy, float distance, float time)>();
        flight.FlybyStarted += (e, d, t) => starts.Add((e, d, t));

        TestEnemyMover Pass(string prefab, float x, float z)
        {
            GameObject go = Track(Object.Instantiate(Load<GameObject>(prefab), new Vector3(x, 0f, z), Quaternion.Euler(0f, 180f, 0f)));
            var m = go.GetComponent<TestEnemyMover>();
            m.PlanRoute(TestMovePattern.Straight, x, x, x);
            return m;
        }
        TestEnemyMover close = Pass(Interceptor, 1.5f, 6f);
        TestEnemyMover far = Pass(Fighter, 8.5f, 6f);
        Pass(Fighter, -2f, 4f);
        Pass(Fighter, 2.5f, 4.2f); // more close passes at the same time: tests the voice limit

        int maxActive = 0;
        float minPitch = 2f, maxPitch = 0f;
        bool destroyed = false;
        float end = Time.time + 8f;
        while (Time.time < end)
        {
            yield return null;
            maxActive = Mathf.Max(maxActive, flight.ActiveFlybyCount);
            foreach (float p in flight.FlybyPitches())
            {
                minPitch = Mathf.Min(minPitch, p);
                maxPitch = Mathf.Max(maxPitch, p);
            }
            if (!destroyed && starts.Count >= 2 && starts[1].enemy != null)
            {
                destroyed = true;
                Object.Destroy(starts[1].enemy.gameObject); // shot down mid-pass: its voice must fade out, not hang
            }
        }
        Debug.Log($"[Flyby] {starts.Count} flybys; closest predicted passes {string.Join(", ", starts.ConvertAll(s => s.distance.ToString("F2")))}; " +
                  $"pitch range {minPitch:F3}-{maxPitch:F3}; at most {maxActive} at once");
        Assert.IsTrue(starts.Exists(s => s.enemy == close), "A close pass should get a flyby");
        Assert.IsFalse(starts.Exists(s => s.enemy == far), "A distant enemy should not");
        Assert.IsTrue(destroyed);
        Assert.LessOrEqual(maxActive, flight.MaxFlybys, "Flybys are limited");
        Assert.GreaterOrEqual(minPitch, 1f - flight.DopplerAmount - 0.002f, "Doppler change stays subtle");
        Assert.LessOrEqual(maxPitch, 1f + flight.DopplerAmount + 0.002f, "Doppler change stays subtle");
        Assert.Greater(maxPitch - minPitch, 0.005f, "Pitch should move a little as the enemy approaches and passes");
        foreach (var s in starts)
        {
            Assert.LessOrEqual(s.time, Load<TestSoundBank>(BankPath).flybyPassSeconds + 0.3f, "Started too early for the pass");
        }

        Assert.AreEqual(0, flight.ActiveFlybyCount, "Every flyby should have finished or faded out after 8 s");
        Assert.IsNotNull(ship);
        flight.ClearAll();
        Assert.AreEqual(0, flight.ActiveFlybyCount, "Restart must stop every flyby");
    }

    [UnityTest]
    public IEnumerator Restart_StopsEveryLoop_AndRemovesEnemiesShotsAndEffects()
    {
        yield return LoadScene();
        WaveEncounter encounter = Find<WaveEncounter>();
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        TestFlightAudio flight = Find<TestFlightAudio>();
        TestFxManager fx = Find<TestFxManager>();
        TestAttackDirector director = Find<TestAttackDirector>();
        TestPlayerShip ship = Find<TestPlayerShip>();
        Find<TestPlayerHealth>().enabled = false;
        Time.timeScale = 4f;
        float end = Time.realtimeSinceStartup + 30f;
        while (flight.ActiveFlybyCount == 0 && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        Assert.Greater(flight.ActiveFlybyCount, 0, "Expected at least one flyby during the encounter");
        Assert.Greater(encounter.AliveCount, 0);
        ship.Fire();
        fx.PlayExplosion(Vector3.zero, 1f);
        director.Fire(new Vector3(0f, 0f, 6f), ship.transform.position, 8f);

        harness.RestartAll();
        Assert.AreEqual(0, flight.ActiveFlybyCount, "Restart must stop flybys");
        Assert.IsFalse(flight.EngineRunning, "Restart must stop the engine loop (it fades back in)");
        Assert.AreEqual(0, fx.ActiveVoiceCount(), "Restart must silence one-shot sounds");
        yield return null;
        Assert.AreEqual(0, encounter.AliveCount, "Restart must remove enemies");
        Assert.AreEqual(0, ship.ActiveProjectileCount, "Restart must remove the player's shots");
        Assert.AreEqual(0, director.ActiveShotCount, "Restart must remove enemy shots");
        Assert.AreEqual(0, fx.ActiveEffectCount, "Restart must remove effects");
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return null;
        Assert.IsNull(TestFlightAudio.Active, "Unloading the scene (like leaving Play mode) leaves no engine audio running");
        Assert.IsNull(TestFxManager.Active);
    }
}
#endif
