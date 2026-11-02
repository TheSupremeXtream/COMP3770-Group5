#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The integrated demo scene: teammates' systems are present and wired (Leonardo's enemy model, Abel's player
/// controller), weapons destroy the real enemy prefabs, waves progress and finish once, defeat stops progression,
/// restart (repeatedly) resets everything, spawns and bounds follow a moving rail rig, and Play/Stop/Play starts fresh.
/// </summary>
public class IntegratedDemoTests
{
    private const string DemoPath = "Assets/Scenes/LevelWaves_Demo_Integrated.unity";
    private const string LeonardoPrefab = "Assets/Prefabs/Enemy_model.prefab";
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
        yield return LevelWavesTestScenes.UnloadAll(DemoPath);
        yield return null;
    }

    private IEnumerator LoadDemo()
    {
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(DemoPath, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = LevelWavesTestScenes.Newest(DemoPath);
        Assert.IsTrue(scene.isLoaded, "Integrated demo scene did not load");
        yield return null;
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

    private Behaviour FindByTypeName(string typeName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Behaviour b in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (b != null && b.GetType().Name == typeName)
                {
                    return b;
                }
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

    [UnityTest]
    public IEnumerator Demo_UsesTeamSystems_AndHasNoMissingScriptsMaterialsOrShaders()
    {
        yield return LoadDemo();
        int components = 0, renderers = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component c in t.GetComponents<Component>())
                {
                    Assert.IsNotNull(c, t.name + " has a missing script");
                    components++;
                }
                Renderer r = t.GetComponent<Renderer>();
                if (r != null && !(r is ParticleSystemRenderer && !r.enabled))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m, t.name + " has a missing material");
                        Assert.IsTrue(m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader", t.name + ": unsupported shader " + m.shader.name);
                    }
                    renderers++;
                }
            }
        }

        // Abel's player: his controller moves the ship, the test weapon only fires.
        Behaviour controller = FindByTypeName("PlayerController");
        Assert.IsNotNull(controller, "Abel's PlayerController is missing from the demo");
        Assert.IsTrue(controller.enabled);
        Assert.IsNotNull(controller.GetComponent<CharacterController>());
        Assert.IsNotNull(FindByTypeName("PlayerCollision"), "Abel's PlayerCollision is missing");
        Assert.AreEqual("Player", controller.tag, "The player is tagged Player (enemy shots look for it, Issue #6)");
        TestPlayerShip weapon = Find<TestPlayerShip>();
        Assert.AreSame(controller.gameObject, weapon.gameObject);
        Assert.IsFalse(weapon.HandlesMovement, "Only Abel's controller should move the ship");

        // Leonardo's model is the visual of every wave enemy.
        WaveEncounter encounter = Find<WaveEncounter>();
        var leonardo = AssetDatabase.LoadAssetAtPath<GameObject>(LeonardoPrefab);
        Assert.IsNotNull(leonardo, "Leonardo's Enemy_model prefab is missing");
        var classes = new HashSet<TestEnemyClass>();
        foreach (EnemyWave wave in encounter.Waves)
        {
            foreach (GameObject prefab in wave.enemyPrefabs)
            {
                bool usesModel = false;
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject) &&
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == LeonardoPrefab)
                    {
                        usesModel = true;
                    }
                }
                Assert.IsTrue(usesModel, prefab.name + " does not use Leonardo's model");
                Assert.IsTrue(prefab.GetComponent<BoxCollider>().isTrigger, prefab.name + ": hitbox should be a trigger so the CharacterController is not blocked");
                classes.Add(prefab.GetComponent<TestEnemyMover>().Class);
            }
        }
        Assert.AreEqual(3, classes.Count, "Heavy, fighter and interceptor all appear");

        // Everything that follows the camera is under the rail rig, and the bounds use it.
        var rail = Find<TestRailScroll>();
        Assert.IsNotNull(rail);
        Assert.AreSame(rail.transform, encounter.BoundsSpace, "Encounter bounds should follow the rail rig");
        Assert.IsTrue(controller.transform.IsChildOf(rail.transform), "Player should ride the rail rig");
        Assert.IsTrue(Camera.main.transform.IsChildOf(rail.transform), "Camera should ride the rail rig");
        Debug.Log($"[Demo] {components} components, {renderers} renderers checked; enemy classes: {string.Join(", ", classes)}");
    }

    [UnityTest]
    public IEnumerator Demo_PlayerWeapon_DestroysEnemiesBuiltOnLeonardosModel()
    {
        yield return LoadDemo();
        Find<WaveEncounter>().StopEncounter();
        Find<TestPlayerHealth>().enabled = false;
        TestPlayerShip weapon = Find<TestPlayerShip>();
        TestFxManager fx = Find<TestFxManager>();
        GameObject prefab = Find<WaveEncounter>().Waves[0].enemyPrefabs[0];
        Vector3 at = weapon.transform.position + new Vector3(0f, 0f, 6f);
        GameObject enemy = Object.Instantiate(prefab, at, Quaternion.Euler(0f, 180f, 0f));
        enemy.GetComponent<TestEnemyMover>().enabled = false;
        int shots = 0;
        float end = Time.time + 4f;
        while (enemy != null && Time.time < end)
        {
            weapon.Fire();
            shots++;
            yield return WaitGame(0.2f);
        }
        Debug.Log($"[Demo] {prefab.name} destroyed after {shots} shots");
        Assert.IsTrue(enemy == null, "Player shots should destroy the demo enemy");
        Assert.LessOrEqual(shots, 6);
        Assert.Greater(fx.ActiveEffectCount, 0, "Hits and the explosion should show effects");
    }

    [UnityTest]
    public IEnumerator Demo_RunsAllWaves_CompletesOnce_AndEveryEnemyLeaves()
    {
        yield return LoadDemo();
        WaveEncounter encounter = Find<WaveEncounter>();
        EncounterDebugHUD hud = Find<EncounterDebugHUD>();
        Find<TestPlayerHealth>().enabled = false;
        var started = new List<int>();
        int completions = 0;
        encounter.onWaveStarted.AddListener(started.Add);
        encounter.onEncounterCompleted.AddListener(() => completions++);
        Assert.AreEqual(0, encounter.CurrentWaveIndex);
        yield return null;
        StringAssert.Contains("MARS ORBIT BREAKOUT", hud.BannerShown, "Introduction banner before wave 1");
        Assert.IsNotEmpty(hud.ObjectiveShown, "Objective shown");

        Time.timeScale = 8f;
        float end = Time.realtimeSinceStartup + 90f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        Assert.IsTrue(encounter.IsCompleted, "Demo encounter did not complete");
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, started);
        Assert.AreEqual(1, completions, "Completion must fire exactly once");
        Assert.AreEqual(45, encounter.SpawnedCount);
        Assert.AreEqual(encounter.SpawnedCount, encounter.EscapedCount + encounter.DestroyedCount);
        Assert.AreEqual(0, encounter.TimedOutCount, "Every enemy should leave on its own");
        yield return WaitGame(1f);
        Assert.AreEqual(1, completions);
        Assert.IsTrue(hud.ResultVisible);
        Assert.AreEqual("ROUTE CLEAR", hud.ResultTitle);
        Debug.Log($"[Demo] No-input run: {encounter.ElapsedTime:F1} s, escaped {encounter.EscapedCount}");
    }

    [UnityTest]
    public IEnumerator Demo_Defeat_StopsProgression_AndRepeatedRestartsResetEverything()
    {
        yield return LoadDemo();
        WaveEncounter encounter = Find<WaveEncounter>();
        TestPlayerHealth health = Find<TestPlayerHealth>();
        TestPlayerShip weapon = Find<TestPlayerShip>();
        Behaviour controller = FindByTypeName("PlayerController");
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        TestFxManager fx = Find<TestFxManager>();
        TestAttackDirector director = Find<TestAttackDirector>();
        TestFlightAudio flight = Find<TestFlightAudio>();
        Vector3 start = weapon.transform.position;
        int completions = 0;
        encounter.onEncounterCompleted.AddListener(() => completions++);

        yield return WaitGame(3.5f);
        Assert.Greater(encounter.SpawnedCount, 0);
        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(health.TryTakeHit(1), "Hit " + (i + 1));
            yield return WaitGame(1.05f);
        }
        Assert.IsTrue(health.IsDefeated);
        Assert.IsTrue(encounter.IsFailed, "Defeat should fail the encounter");
        Assert.IsFalse(controller.enabled, "Abel's controller should stop on defeat");
        Assert.IsFalse(weapon.enabled, "Weapon should stop on defeat");
        int spawnedAtDefeat = encounter.SpawnedCount;
        Time.timeScale = 6f;
        yield return WaitGame(12f);
        Time.timeScale = 1f;
        Assert.AreEqual(spawnedAtDefeat, encounter.SpawnedCount, "No spawning after defeat");
        Assert.AreEqual(0, completions, "No victory after defeat");

        for (int r = 0; r < 3; r++)
        {
            weapon.Fire();
            director.Fire(start + new Vector3(0f, 0f, 9f), start, 8f);
            fx.PlayExplosion(start, 1f);
            harness.RestartAll();
            yield return null;
            Assert.IsFalse(health.IsDefeated);
            Assert.AreEqual(3, health.HitsRemaining);
            Assert.IsTrue(controller.enabled && weapon.enabled, "Controls back on after restart");
            Assert.AreEqual(0, encounter.AliveCount, "Enemies removed");
            Assert.AreEqual(0, encounter.SpawnedCount);
            Assert.AreEqual(0, encounter.CurrentWaveIndex);
            Assert.IsFalse(encounter.IsFailed);
            Assert.AreEqual(0, weapon.ActiveProjectileCount, "Player shots removed");
            Assert.AreEqual(0, director.ActiveShotCount, "Enemy shots removed");
            Assert.AreEqual(0, fx.ActiveEffectCount, "Effects removed");
            Assert.AreEqual(0, fx.ActiveVoiceCount(), "Sounds stopped");
            Assert.AreEqual(0, flight.ActiveFlybyCount, "Flybys stopped");
            Assert.Less(Vector3.Distance(start, weapon.transform.position), 0.01f, "Player back at the start");
            yield return WaitGame(0.6f);
        }
        // The restarted run still works: the CharacterController was teleported cleanly and waves spawn again.
        yield return WaitGame(3f);
        Assert.Greater(encounter.SpawnedCount, 0, "Waves spawn again after restart");
    }

    [UnityTest]
    public IEnumerator Demo_MovingRail_KeepsSpawnsBoundsAndExitsWithTheRig()
    {
        yield return LoadDemo();
        WaveEncounter encounter = Find<WaveEncounter>();
        TestRailScroll rail = Find<TestRailScroll>();
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        Find<TestPlayerHealth>().enabled = false;
        Transform player = Find<TestPlayerShip>().transform;
        Vector3 playerLocal = rail.transform.InverseTransformPoint(player.position);
        rail.Speed = 3f;

        var seen = new HashSet<TestEnemyMover>();
        float worstX = 0f;
        Time.timeScale = 6f;
        float end = Time.realtimeSinceStartup + 90f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            foreach (TestEnemyMover m in TestEnemyMover.Active)
            {
                Vector3 local = rail.transform.InverseTransformPoint(m.transform.position);
                if (seen.Add(m))
                {
                    Assert.GreaterOrEqual(local.z, 8.5f, "Spawned away from the player, relative to the moving rig");
                }
                worstX = Mathf.Max(worstX, Mathf.Abs(local.x));
            }
            yield return null;
        }
        Time.timeScale = 1f;
        float travelled = rail.Travelled;
        Debug.Log($"[Demo] Rail moved {travelled:F0} units; {seen.Count} enemies; widest |x| {worstX:F1}; escaped {encounter.EscapedCount}, timed out {encounter.TimedOutCount}");
        Assert.IsTrue(encounter.IsCompleted, "Encounter should still finish with the rig moving");
        Assert.AreEqual(45, seen.Count);
        Assert.AreEqual(0, encounter.TimedOutCount, "No enemy got stuck when the rig moved");
        Assert.LessOrEqual(worstX, 12f);
        Assert.Greater(travelled, 100f, "The rig really moved");
        Assert.Less(Vector3.Distance(playerLocal, rail.transform.InverseTransformPoint(player.position)), 0.05f, "The player stayed with the rig");

        harness.RestartAll(); // checked before the next frame, because the rig keeps scrolling after a restart
        Assert.AreEqual(0f, rail.Travelled, 0.01f, "Restart puts the rig back at its start");
        Assert.Less(Vector3.Distance(playerLocal, rail.transform.InverseTransformPoint(player.position)), 0.05f, "Player back at its place on the rig");
        yield return null;
    }

    // ---- Screenshots (opt-in) ----

    private IEnumerator Shot(Camera cam, Camera uiCam, string dir, string fileName, int width = 1280, int height = 720, RectInt? crop = null)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        uiCam.targetTexture = rt;
        yield return null;
        yield return null;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        uiCam.targetTexture = null;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, fileName), tex.EncodeToPNG());
        if (crop.HasValue)
        {
            // 3x nearest-neighbour enlargement of a region (y from the top) to judge the model's orientation.
            RectInt c = crop.Value;
            var big = new Texture2D(c.width * 3, c.height * 3, TextureFormat.RGB24, false);
            for (int y = 0; y < big.height; y++)
            {
                for (int x = 0; x < big.width; x++)
                {
                    big.SetPixel(x, y, tex.GetPixel(c.x + x / 3, height - c.y - c.height + y / 3));
                }
            }
            big.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, System.IO.Path.GetFileNameWithoutExtension(fileName) + "_crop3x.png"), big.EncodeToPNG());
            Object.Destroy(big);
        }
        Object.Destroy(tex);
        rt.Release();
        Object.Destroy(rt);
    }

    [UnityTest, Explicit("Writes screenshots of the integrated demo; run on demand with SOLAR_CAPTURE_DIR set.")]
    public IEnumerator CaptureDemoScreens()
    {
        string dir = System.Environment.GetEnvironmentVariable("SOLAR_CAPTURE_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            Assert.Ignore("SOLAR_CAPTURE_DIR not set");
        }
        System.IO.Directory.CreateDirectory(dir);
        yield return LoadDemo();
        Camera cam = Camera.main;
        Camera uiCam = null;
        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
        {
            if (c != cam && c.name.Contains("UICamera"))
            {
                uiCam = c;
            }
        }
        Find<TestPlayerHealth>().enabled = false;
        WaveEncounter encounter = Find<WaveEncounter>();
        yield return WaitGame(0.9f);
        yield return Shot(cam, uiCam, dir, "demo_01_intro.png");
        while (encounter.AliveCount < 4)
        {
            yield return null;
        }
        yield return WaitGame(1.6f);
        // Crop around the first enemy on screen.
        TestEnemyMover first = TestEnemyMover.Active.Count > 0 ? TestEnemyMover.Active[0] : null;
        RectInt? crop = null;
        if (first != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(first.transform.position);
            float sx = sp.x * 1280f / cam.pixelWidth, sy = 720f - sp.y * 720f / cam.pixelHeight;
            crop = new RectInt(Mathf.Clamp((int)sx - 60, 0, 1160), Mathf.Clamp((int)sy - 60, 0, 600), 120, 120);
        }
        yield return Shot(cam, uiCam, dir, "demo_02_wave1.png", crop: crop);
        Time.timeScale = 6f;
        while (encounter.CurrentWaveIndex < 1 || encounter.State != WaveEncounter.EncounterState.Spawning)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        yield return WaitGame(3.5f);
        yield return Shot(cam, uiCam, dir, "demo_03_wave2.png");
        Time.timeScale = 6f;
        while (encounter.CurrentWaveIndex < 2 || encounter.State != WaveEncounter.EncounterState.Spawning)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        yield return WaitGame(4.5f);
        yield return Shot(cam, uiCam, dir, "demo_04_wave3.png");
        Time.timeScale = 8f;
        while (!encounter.IsCompleted)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        yield return WaitGame(0.5f);
        yield return Shot(cam, uiCam, dir, "demo_05_route_clear.png");
    }

    [UnityTest]
    public IEnumerator Demo_PlayStopPlay_StartsFresh()
    {
        int targetBefore = Application.targetFrameRate;
        yield return LoadDemo();
        Assert.AreEqual(60, Application.targetFrameRate, "Demo fixes the frame rate for Abel's per-frame movement");
        yield return WaitGame(4f);
        Assert.Greater(Find<WaveEncounter>().SpawnedCount, 0);
        yield return LevelWavesTestScenes.UnloadAll(DemoPath); // like pressing Stop
        yield return null;
        Assert.IsNull(TestFxManager.Active);
        Assert.IsNull(TestFlightAudio.Active);
        Assert.IsNull(TestAttackDirector.Active);
        Assert.AreEqual(0, TestEnemyMover.Active.Count, "No enemies left over");
        Assert.AreEqual(targetBefore, Application.targetFrameRate, "Frame-rate cap released when the scene ends");

        yield return LoadDemo(); // like pressing Play again
        WaveEncounter encounter = Find<WaveEncounter>();
        Assert.AreEqual(0, encounter.SpawnedCount);
        Assert.AreEqual(3, Find<TestPlayerHealth>().HitsRemaining);
        yield return WaitGame(4f);
        Assert.Greater(encounter.SpawnedCount, 0, "Second run spawns normally");
        Assert.AreEqual(0, encounter.CurrentWaveIndex);
    }
}
#endif
