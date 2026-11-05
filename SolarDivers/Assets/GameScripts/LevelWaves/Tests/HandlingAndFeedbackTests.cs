#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>Ship handling, entry warnings, sound limits, hit feedback, HUD readability and scenery checks.</summary>
public class HandlingAndFeedbackTests
{
    private const string ScenePath = "Assets/Scenes/LevelWaves_Encounter01_Test.unity";
    private const float SceneSpawnZ = 10f; // SpawnOrigin z in the test scene
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

    private TestPlayerShip MakeShip()
    {
        var go = Track(new GameObject("TestShip"));
        go.transform.position = new Vector3(0f, 0f, -4f);
        return go.AddComponent<TestPlayerShip>();
    }

    private static void Run(TestPlayerShip ship, Vector2 input, float seconds, float fps)
    {
        int steps = Mathf.RoundToInt(seconds * fps);
        for (int i = 0; i < steps; i++)
        {
            ship.Step(input, 1f / fps);
        }
    }

    private IEnumerator LoadScene()
    {
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = LevelWavesTestScenes.Newest(ScenePath);
        Assert.IsTrue(scene.isLoaded);
        yield return null; // HUD builds in Start
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

    private static IEnumerator WaitReal(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
    }

    // ---- Player handling ----

    [Test]
    public void PlayerHandling_IsFrameRateIndependent()
    {
        TestPlayerShip slow = MakeShip();
        TestPlayerShip fast = MakeShip();
        // Durations are whole frames at both rates (0.5 s = 15 and 72 frames).
        Run(slow, Vector2.right, 0.5f, 30f);
        Run(fast, Vector2.right, 0.5f, 144f);
        Run(slow, Vector2.zero, 0.5f, 30f);
        Run(fast, Vector2.zero, 0.5f, 144f);
        Assert.AreEqual(slow.transform.position.x, fast.transform.position.x, 0.02f, "Same input should travel the same distance at 30 and 144 fps");
        Assert.AreEqual(0f, slow.Velocity.magnitude, 1e-4f);
        Assert.AreEqual(0f, fast.Velocity.magnitude, 1e-4f);
    }

    [Test]
    public void PlayerHandling_IsResponsive_WithoutSliding()
    {
        TestPlayerShip ship = MakeShip();
        float t = 0f;
        while (ship.Velocity.x < ship.MaxSpeed * 0.9f && t < 1f)
        {
            ship.Step(Vector2.right, 1f / 60f);
            t += 1f / 60f;
        }
        Assert.Less(t, 0.2f, "Should reach 90% of top speed within 0.2 s");
        Run(ship, Vector2.right, 0.5f, 60f);
        Assert.AreEqual(ship.MaxSpeed, ship.Velocity.x, 1e-3f, "Top speed should be reached and held");

        float releasedAt = ship.transform.position.x;
        t = 0f;
        while (ship.Velocity.sqrMagnitude > 0f && t < 1f)
        {
            ship.Step(Vector2.zero, 1f / 60f);
            t += 1f / 60f;
        }
        Assert.Less(t, 0.15f, "Should stop within 0.15 s of releasing the keys");
        Assert.Less(ship.transform.position.x - releasedAt, 0.6f, "Too much sliding after release");

        Run(ship, Vector2.left, 0.2f, 60f);
        Assert.Less(ship.Velocity.x, -ship.MaxSpeed * 0.9f, "Reversing should be quick");
        Run(ship, new Vector2(1f, 1f), 1f, 60f);
        Assert.LessOrEqual(ship.Velocity.magnitude, ship.MaxSpeed + 1e-3f, "Diagonals must not be faster");
    }

    [Test]
    public void PlayerHandling_StaysInsideLimits_AndStopsAtTheEdge()
    {
        TestPlayerShip ship = MakeShip();
        Run(ship, Vector2.right, 4f, 60f);
        Assert.AreEqual(ship.XLimits.y, ship.transform.position.x, 1e-4f);
        Assert.AreEqual(0f, ship.Velocity.x, "Velocity into the edge should be cleared");
        Run(ship, new Vector2(-1f, -1f), 6f, 60f);
        Assert.AreEqual(ship.XLimits.x, ship.transform.position.x, 1e-4f);
        Assert.AreEqual(ship.ZLimits.x, ship.transform.position.z, 1e-4f);
        Run(ship, Vector2.up, 4f, 60f);
        Assert.AreEqual(ship.ZLimits.y, ship.transform.position.z, 1e-4f);
        // Leaving the edge works immediately.
        Run(ship, Vector2.down, 0.2f, 60f);
        Assert.Less(ship.transform.position.z, ship.ZLimits.y - 0.5f);
        ship.ResetShip();
        Assert.AreEqual(Vector3.zero, ship.Velocity, "Restart should leave the ship at rest");
    }

    [UnityTest]
    public IEnumerator Banking_RotatesOnlyTheModel_AndReturnsToLevel()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelWaves/TEST_PlayerShip.prefab");
        GameObject ship = Track(Object.Instantiate(prefab, new Vector3(0f, 0f, 300f), Quaternion.identity));
        ship.GetComponent<TestPlayerShip>().enabled = false;
        ship.GetComponent<TestPlayerHealth>().enabled = false;
        var bank = ship.GetComponent<TestShipBanking>();
        var hitbox = ship.GetComponent<BoxCollider>();
        Vector3 hitboxSize = hitbox.size;
        yield return null;

        float end = Time.time + 0.6f;
        while (Time.time < end)
        {
            ship.transform.position += Vector3.right * (7f * Time.deltaTime);
            yield return null;
        }
        Assert.Less(bank.Roll, -20f, "Moving right should roll the model into the turn");
        Assert.AreEqual(Quaternion.identity, ship.transform.rotation, "The root (and its hitbox) must not rotate");
        Assert.AreEqual(hitboxSize, hitbox.size);
        Assert.AreNotEqual(Quaternion.identity, bank.Model.localRotation);

        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(0f, bank.Roll, 1f, "Model should ease back to level when the ship stops");
    }

    // ---- Enemy movement: see MovementAndAudioTests (steering, classes, escape lane, exits) ----

    [UnityTest]
    public IEnumerator Encounter_WarnsBeforeEachGroup_WithoutChangingPacing()
    {
        var template = Track(new GameObject("WarnTemplate"));
        template.transform.position = new Vector3(500f, 0f, 500f);
        var spawnRoot = Track(new GameObject("WarnSpawnRoot")).transform;
        var go = Track(new GameObject("WarnEncounter"));
        var encounter = go.AddComponent<WaveEncounter>();
        var origin = new GameObject("Origin").transform;
        origin.SetParent(go.transform);
        var wave = new EnemyWave
        {
            waveName = "Warn", formation = WaveFormationType.Line, enemyPrefabs = new[] { template }, enemiesPerGroup = 3,
            groupCount = 2, spacing = 2f, delayBeforeWave = 1.5f, delayBetweenGroups = 1.5f, groupEntryOffsets = new[] { -3f, 3f },
            clearTimeout = 3f
        };
        encounter.Configure(new List<EnemyWave> { wave }, origin, new Bounds(Vector3.zero, new Vector3(30f, 10f, 30f)), false, spawnRoot);

        var warnings = new List<(float time, float lead, Vector3 centre, float width)>();
        var spawns = new List<float>();
        encounter.onGroupIncoming.AddListener((c, w, lead) => warnings.Add((Time.time, lead, c, w)));
        float start = Time.time;
        encounter.StartEncounter();
        int lastCount = 0;
        float end = Time.realtimeSinceStartup + 6f;
        while (spawns.Count < 2 && Time.realtimeSinceStartup < end)
        {
            if (encounter.SpawnedCount > lastCount)
            {
                spawns.Add(Time.time);
                lastCount = encounter.SpawnedCount;
            }
            yield return null;
        }
        Assert.AreEqual(2, spawns.Count);
        Assert.AreEqual(2, warnings.Count, "One warning per group");
        Assert.AreEqual(1.5f, spawns[0] - start, 0.1f, "First group should still spawn after Delay Before Wave");
        Assert.AreEqual(1.5f, spawns[1] - spawns[0], 0.1f, "Groups should still be Delay Between Groups apart");
        for (int i = 0; i < 2; i++)
        {
            Assert.AreEqual(1.2f, warnings[i].lead, 1e-4f);
            Assert.AreEqual(spawns[i], warnings[i].time + warnings[i].lead, 0.1f, "Warning should lead the spawn by its lead time");
            Assert.AreEqual(4f, warnings[i].width, 1e-4f, "Width spans the group's outer ships");
        }
        Assert.AreEqual(-warnings[0].centre.x, warnings[1].centre.x, 1e-4f, "Warnings follow each group's entry offset");
    }

    // ---- Sound ----

    [UnityTest]
    public IEnumerator Sound_IsVaried_Limited_AndSilencedOnRestart()
    {
        var go = Track(new GameObject("FxForSound"));
        var fx = go.AddComponent<TestFxManager>();
        var bank = AssetDatabase.LoadAssetAtPath<TestSoundBank>("Assets/Audio/LevelWaves/TEST_SoundBank.asset");
        Assert.IsNotNull(bank, "Sound bank missing (run the scene builder)");
        fx.SetSounds(bank);
        foreach (TestFxManager.Sound sound in System.Enum.GetValues(typeof(TestFxManager.Sound)))
        {
            Assert.Greater(bank.For(sound).Length, 0, "No clip for " + sound);
        }
        Assert.GreaterOrEqual(bank.autocannon.Length, 3, "Expected several autocannon variants");

        int maxShots = 0, maxAll = 0;
        float end = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < end)
        {
            fx.PlayShot();
            fx.PlayImpact(Vector3.zero);
            maxShots = Mathf.Max(maxShots, fx.ActiveVoiceCount(TestFxManager.Sound.Shot));
            maxAll = Mathf.Max(maxAll, fx.ActiveVoiceCount());
            yield return WaitReal(0.04f);
        }
        fx.PlayExplosion(Vector3.zero, 1f);
        fx.PlayPlayerHit(Vector3.zero);
        maxAll = Mathf.Max(maxAll, fx.ActiveVoiceCount());
        Assert.Greater(maxShots, 1, "Rapid fire should overlap a little");
        Assert.LessOrEqual(maxShots, fx.MaxPerSound, "Too many overlapping shots");
        Assert.LessOrEqual(maxAll, fx.MaxVoices);
        Assert.AreEqual(1, fx.ActiveVoiceCount(TestFxManager.Sound.PlayerHit));

        fx.ClearAll();
        Assert.AreEqual(0, fx.ActiveVoiceCount(), "Restart should silence every sound");
        Object.Destroy(go);
        yield return null;
        Assert.IsNull(TestFxManager.Active);
    }

    // ---- Camera shake ----

    [UnityTest]
    public IEnumerator CameraShake_IsShort_ReturnsToRest_AndCanBeTurnedOff()
    {
        var go = Track(new GameObject("ShakeCam"));
        go.transform.SetPositionAndRotation(new Vector3(0f, 16f, 600f), Quaternion.Euler(90f, 0f, 0f));
        var shake = go.AddComponent<TestCameraShake>();
        Vector3 rest = go.transform.position;
        shake.Shake(1f);
        float maxOffset = 0f;
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            maxOffset = Mathf.Max(maxOffset, shake.CurrentOffset.magnitude);
        }
        Assert.Greater(maxOffset, 0.01f, "Shake should move the camera");
        Assert.LessOrEqual(maxOffset, 0.31f, "Shake should stay small");
        Assert.AreEqual(rest.y, go.transform.position.y, 1e-4f, "Shake must stay in the screen plane");
        go.transform.position += Vector3.right; // something else moves the camera mid-shake
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(0f, shake.Strength);
        Assert.Less(Vector3.Distance(rest + Vector3.right, go.transform.position), 1e-4f, "Camera should end exactly where it was put");

        shake.ShakeEnabled = false;
        shake.Shake(1f);
        yield return null;
        Assert.Less(Vector3.Distance(rest + Vector3.right, go.transform.position), 1e-4f, "Disabled shake must not move the camera");
    }

    // ---- Real scene: HUD, hit feedback, scenery ----

    [UnityTest]
    public IEnumerator Hud_DrawsOutsidePostProcessing_WithReadableSizes()
    {
        yield return LoadScene();
        EncounterDebugHUD hud = Find<EncounterDebugHUD>();
        Canvas canvas = hud.HudCanvas;
        Assert.IsNotNull(canvas);
        Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
        Assert.IsTrue(canvas.pixelPerfect, "Pixel snapping keeps legacy text crisp");
        Camera uiCam = canvas.worldCamera;
        Camera mainCam = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
            {
                if (c != uiCam)
                {
                    mainCam = c;
                }
            }
        }
        Assert.IsNotNull(uiCam);
        Assert.IsNotNull(mainCam);
        var uiData = uiCam.GetUniversalAdditionalCameraData();
        var mainData = mainCam.GetUniversalAdditionalCameraData();
        Assert.AreEqual(CameraRenderType.Overlay, uiData.renderType);
        Assert.IsFalse(uiData.renderPostProcessing, "The HUD camera must not run bloom/vignette/anti-aliasing");
        Assert.Contains(uiCam, mainData.cameraStack);
        Assert.AreNotEqual(AntialiasingMode.FastApproximateAntialiasing, mainData.antialiasing,
            "FXAA runs at the end of a camera stack and would soften the HUD");
        Assert.AreEqual(1 << 5, uiCam.cullingMask);
        Assert.AreEqual(0, mainCam.cullingMask & (1 << 5), "The scene camera must not draw the UI layer (it would get post-processed)");
        foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
        {
            Assert.AreEqual(5, t.gameObject.layer, t.name + " is not on the UI layer");
        }
        int largest = 0;
        foreach (Text text in canvas.GetComponentsInChildren<Text>(true))
        {
            Assert.GreaterOrEqual(text.fontSize, 26, $"Text '{text.text}' is too small at 1920x1080");
            largest = Mathf.Max(largest, text.fontSize);
        }
        Assert.GreaterOrEqual(largest, 48, "Result headings should be 48 px or more");

        // Scale at 1280x720 and in a small window.
        var rt720 = new RenderTexture(1280, 720, 24);
        var rtSmall = new RenderTexture(800, 450, 24);
        mainCam.targetTexture = rt720;
        uiCam.targetTexture = rt720;
        yield return null;
        yield return null;
        Assert.AreEqual(720f / 1080f, hud.CurrentScale, 0.01f);
        Assert.GreaterOrEqual(28 * hud.CurrentScale, 18f, "Body text should stay at least 18 px tall at 1280x720");
        mainCam.targetTexture = rtSmall;
        uiCam.targetTexture = rtSmall;
        yield return null;
        yield return null;
        Assert.AreEqual(0.6f, hud.CurrentScale, 0.01f, "Small windows should not shrink the HUD below its minimum");
        mainCam.targetTexture = null;
        uiCam.targetTexture = null;
        rt720.Release();
        rtSmall.Release();
        Object.Destroy(rt720);
        Object.Destroy(rtSmall);
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator PlayerHit_ShowsFlash_ShakesBriefly_AndRestartClearsFeedback()
    {
        yield return LoadScene();
        EncounterDebugHUD hud = Find<EncounterDebugHUD>();
        TestPlayerHealth health = Find<TestPlayerHealth>();
        TestCameraShake shake = Find<TestCameraShake>();
        TestFxManager fx = Find<TestFxManager>();
        TestEntryWarnings warnings = Find<TestEntryWarnings>();
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        Vector3 camRest = shake.transform.position;

        // Wave 1's first warning appears before its first group.
        float end = Time.realtimeSinceStartup + 4f;
        while (warnings.ActiveCount == 0 && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
        Assert.Greater(warnings.ActiveCount, 0, "An entry warning should show before wave 1 arrives");

        Assert.IsTrue(health.TryTakeHit(1));
        var hull = health.GetComponentInChildren<MeshRenderer>();
        var block = new MaterialPropertyBlock();
        hull.GetPropertyBlock(block);
        Color tint = block.GetColor("_BaseColor");
        Assert.IsTrue(hull.enabled && tint.r > 0.9f && tint.g < 0.3f, "The ship should flash red at the moment of the hit");
        yield return null;
        Assert.Greater(hud.DamageFlashAlpha, 0.15f, "A hit should flash the screen edges");
        Assert.Greater(shake.Strength, 0f, "A hit should shake the camera");
        Assert.Greater(fx.ActiveVoiceCount(TestFxManager.Sound.PlayerHit), 0, "A hit should play the damage sound");
        Assert.AreEqual(2, health.HitsRemaining);
        yield return WaitReal(0.7f);
        Assert.AreEqual(0f, hud.DamageFlashAlpha, 1e-3f, "Flash should be gone well within a second");
        Assert.Less(Vector3.Distance(camRest, shake.transform.position), 1e-4f, "Camera should be back at rest");

        harness.RestartAll();
        yield return null;
        Assert.AreEqual(0, fx.ActiveVoiceCount(), "Restart should silence every sound");
        Assert.AreEqual(0, warnings.ActiveCount, "Restart should remove warnings");
        Assert.AreEqual(0f, shake.Strength);
        Assert.Less(Vector3.Distance(camRest, shake.transform.position), 1e-4f);
        Assert.AreEqual(3, health.HitsRemaining);
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator Scenery_IsDecorOnly_BelowThePlayPlane_AndPresetsSwitch()
    {
        yield return LoadScene();
        TestBackdropPreset preset = Find<TestBackdropPreset>();
        Assert.IsNotNull(preset);
        Assert.AreEqual(0, preset.GetComponentsInChildren<Collider>(true).Length, "Scenery must have no colliders");
        foreach (Renderer r in preset.GetComponentsInChildren<Renderer>(true))
        {
            Assert.Less(r.transform.position.y, -3f, r.name + " is too close to the play plane");
        }
        Assert.AreEqual(TestBackdropPreset.Preset.PlanetAndMoon, preset.Current);
        Transform planet = preset.transform.Find("Backdrop_PlanetAndMoon");
        Transform sun = preset.transform.Find("Backdrop_Sun");
        Assert.IsTrue(planet.gameObject.activeSelf);
        Assert.IsFalse(sun.gameObject.activeSelf, "Only one backdrop at a time");
        preset.Apply(TestBackdropPreset.Preset.Sun);
        Assert.IsFalse(planet.gameObject.activeSelf);
        Assert.IsTrue(sun.gameObject.activeSelf);
        preset.Apply(TestBackdropPreset.Preset.PlanetAndMoon);
        Assert.IsTrue(planet.gameObject.activeSelf);
        yield return SceneManager.UnloadSceneAsync(scene);
    }
}
#endif
