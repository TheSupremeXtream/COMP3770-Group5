#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Opt-in runtime screenshots of the prototype scene (scene camera + HUD camera), for review.
/// Not part of "Run All": run explicitly with the environment variable SOLAR_CAPTURE_DIR set to an output folder,
/// and without -nographics. Also writes 2x crops of the HUD text, a frame-time log and the generated sounds as WAV.
/// The player's placeholder health is switched off so the auto-pilot can keep shooting, except for the hit and defeat shots.
/// </summary>
public class PrototypeCaptureTests
{
    private const string ScenePath = "Assets/Scenes/LevelWaves_Encounter01_Test.unity";
    private Scene scene;
    private string outputDir;
    private Camera cam;
    private Camera uiCam;

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

    private IEnumerator Capture(int width, int height, string fileName, params RectInt[] crops)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        uiCam.targetTexture = rt; // so the HUD lays out for this size
        yield return null; // HUD re-lays out for the new size
        yield return null; // a full frame rendered into the texture
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        uiCam.targetTexture = null;
        File.WriteAllBytes(Path.Combine(outputDir, fileName), tex.EncodeToPNG());
        for (int i = 0; i < crops.Length; i++)
        {
            WriteCrop(tex, crops[i], Path.GetFileNameWithoutExtension(fileName) + "_crop" + (i + 1) + "_2x.png");
        }
        Object.Destroy(tex);
        rt.Release();
        Object.Destroy(rt);
        Debug.Log("[Capture] wrote " + fileName);
    }

    // Nearest-neighbour 2x enlargement of a region (y measured from the top), to judge text edges pixel by pixel.
    private void WriteCrop(Texture2D source, RectInt fromTop, string fileName)
    {
        int y0 = source.height - fromTop.y - fromTop.height;
        Color32[] src = source.GetPixels32();
        var crop = new Texture2D(fromTop.width * 2, fromTop.height * 2, TextureFormat.RGB24, false);
        var dst = new Color32[crop.width * crop.height];
        for (int y = 0; y < crop.height; y++)
        {
            for (int x = 0; x < crop.width; x++)
            {
                dst[y * crop.width + x] = src[(y0 + y / 2) * source.width + fromTop.x + x / 2];
            }
        }
        crop.SetPixels32(dst);
        crop.Apply();
        File.WriteAllBytes(Path.Combine(outputDir, fileName), crop.EncodeToPNG());
        Object.Destroy(crop);
    }

    private IEnumerator AutoPilot(TestPlayerShip ship, float seconds, List<float> frameTimes = null)
    {
        float end = Time.time + seconds;
        float nextShot = 0f;
        while (Time.time < end)
        {
            TestEnemyHealth target = Find<TestEnemyHealth>();
            if (target != null)
            {
                Vector3 p = ship.transform.position;
                p.x = Mathf.MoveTowards(p.x, target.transform.position.x, 7f * Time.deltaTime);
                ship.transform.position = p;
            }
            if (Time.time >= nextShot)
            {
                ship.Fire();
                nextShot = Time.time + 0.2f;
            }
            yield return null;
            frameTimes?.Add(Time.unscaledDeltaTime);
        }
    }

    private void WriteSounds(TestFxManager fx)
    {
        string audioDir = Path.Combine(outputDir, "audio");
        Directory.CreateDirectory(audioDir);
        var stats = new StringBuilder("clip, seconds, peak, rms_dbfs\n");
        if (fx.Sounds == null)
        {
            return;
        }
        foreach (AudioClip clip in fx.Sounds.AllClips())
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            double sum = 0;
            float peak = 0f;
            foreach (float s in data)
            {
                sum += s * s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            double rms = System.Math.Sqrt(sum / data.Length);
            stats.AppendLine($"{clip.name}, {clip.length:F3}, {peak:F3}, {20.0 * System.Math.Log10(rms):F1}");
            using (var writer = new BinaryWriter(File.Create(Path.Combine(audioDir, clip.name + ".wav"))))
            {
                int bytes = data.Length * 2;
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + bytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(clip.frequency);
                writer.Write(clip.frequency * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(bytes);
                foreach (float s in data)
                {
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
                }
            }
        }
        File.WriteAllText(Path.Combine(audioDir, "audio_stats.csv"), stats.ToString());
    }

    private void WritePerformance(List<float> frameTimes, int width, int height)
    {
        if (frameTimes.Count == 0)
        {
            return;
        }
        var sorted = new List<float>(frameTimes);
        sorted.Sort();
        float mean = 0f;
        foreach (float t in sorted)
        {
            mean += t;
        }
        mean /= sorted.Count;
        float p95 = sorted[Mathf.Min(sorted.Count - 1, Mathf.FloorToInt(sorted.Count * 0.95f))];
        string report =
            $"Measured in the editor's play mode, batch mode with graphics, rendering to a {width}x{height} render texture.\n" +
            "Not a player build; editor overhead included, no vsync. Treat as a rough indication only.\n" +
            $"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})\n" +
            $"CPU: {SystemInfo.processorType}\n" +
            $"Frames: {sorted.Count}\n" +
            $"Mean frame time: {mean * 1000f:F2} ms ({1f / mean:F0} fps)\n" +
            $"95th percentile: {p95 * 1000f:F2} ms\n" +
            $"Worst frame: {sorted[sorted.Count - 1] * 1000f:F2} ms\n";
        File.WriteAllText(Path.Combine(outputDir, "performance.txt"), report);
        Debug.Log("[Capture] " + report);
    }

    [UnityTest, Explicit("Writes screenshots; run on demand with SOLAR_CAPTURE_DIR set.")]
    public IEnumerator CapturePrototypeScreens()
    {
        outputDir = System.Environment.GetEnvironmentVariable("SOLAR_CAPTURE_DIR");
        if (string.IsNullOrEmpty(outputDir))
        {
            Assert.Ignore("SOLAR_CAPTURE_DIR not set");
        }
        Directory.CreateDirectory(outputDir);
        // Package-internal editor messages in batch mode are not what this capture is checking.
        LogAssert.ignoreFailingMessages = true;
        // Draw with fully compiled shaders (the editor otherwise skips objects while variants compile).
        bool asyncShaders = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;

        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = LevelWavesTestScenes.Newest(ScenePath);
        TestPlayerShip ship = Find<TestPlayerShip>();
        TestPlayerHealth health = Find<TestPlayerHealth>();
        WaveEncounter encounter = Find<WaveEncounter>();
        EncounterDebugHUD hud = Find<EncounterDebugHUD>();
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        TestFxManager fx = Find<TestFxManager>();
        TestBackdropPreset backdrop = Find<TestBackdropPreset>();
        cam = Find<TestCameraShake>().GetComponent<Camera>();
        yield return null;
        uiCam = hud.HudCanvas.worldCamera;
        health.enabled = false;
        WriteSounds(fx);

        var hudCrops1080 = new[] { new RectInt(0, 0, 900, 190), new RectInt(0, 960, 1300, 120) };
        var hudCrops720 = new[] { new RectInt(0, 0, 640, 130), new RectInt(0, 640, 900, 80) };

        // 1. Close-up of the three ship designs, before wave 1 arrives.
        var dart = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelWaves/TEST_Enemy_Basic.prefab"),
            new Vector3(-2.6f, 0f, -2.2f), Quaternion.Euler(0f, 180f, 0f));
        var manta = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelWaves/TEST_Enemy_Weaver.prefab"),
            new Vector3(2.6f, 0f, -2.2f), Quaternion.Euler(0f, 180f, 0f));
        dart.GetComponent<TestEnemyMover>().enabled = false;
        manta.GetComponent<TestEnemyMover>().enabled = false;
        Vector3 camPos = cam.transform.position;
        cam.transform.position = new Vector3(0f, 6.5f, -3.8f);
        yield return new WaitForSeconds(0.3f);
        yield return Capture(1920, 1080, "01_ships_closeup_1920x1080.png");
        cam.transform.position = camPos;
        Object.Destroy(dart);
        Object.Destroy(manta);

        // 2. Wave 1 entry warning + banner (warning shows 1.2 s before the first group).
        harness.RestartAll();
        yield return new WaitForSeconds(1.2f);
        yield return Capture(1920, 1080, "02_wave1_entry_warning_1920x1080.png", hudCrops1080);
        yield return Capture(1280, 720, "03_wave1_entry_warning_1280x720.png", hudCrops720);

        // 3. Wave 1 under fire at both resolutions.
        yield return AutoPilot(ship, 4.5f);
        yield return Capture(1920, 1080, "04_wave1_combat_1920x1080.png");
        yield return AutoPilot(ship, 0.6f);
        yield return Capture(1280, 720, "05_wave1_combat_1280x720.png");

        // 4. Player hit: edge flash, red ship flash, pip pulse, "HIT -1".
        health.enabled = true;
        health.TryTakeHit(1);
        yield return Capture(1920, 1080, "06_player_hit_feedback_1920x1080.png", hudCrops1080);
        yield return new WaitForSeconds(1.1f);
        health.enabled = false;

        // 5. Debug counters toggled on.
        hud.SetDebugVisible(true);
        yield return Capture(1280, 720, "07_debug_counters_1280x720.png");
        hud.SetDebugVisible(false);

        // 6. Performance sample: normal play at 1920x1080 for 8 s (rendering every frame into the target).
        var perfTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = perfTarget;
        uiCam.targetTexture = perfTarget;
        var frameTimes = new List<float>();
        yield return AutoPilot(ship, 8f, frameTimes);
        cam.targetTexture = null;
        uiCam.targetTexture = null;
        perfTarget.Release();
        Object.Destroy(perfTarget);
        WritePerformance(frameTimes, 1920, 1080);

        // 6b. Wave 2: alternating flank sweeps leaning towards the player's lane (player parked off-centre).
        Time.timeScale = 6f;
        while (encounter.CurrentWaveIndex < 1 && !encounter.IsCompleted)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        ship.transform.position = new Vector3(4f, 0f, -5f);
        yield return new WaitForSeconds(4.5f);
        yield return Capture(1920, 1080, "07b_wave2_lane_shift_1920x1080.png");

        // 7. Wave 3 (mixed) with the incoming banner, then combat.
        Time.timeScale = 6f;
        while (encounter.CurrentWaveIndex < 2 && !encounter.IsCompleted)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        if (encounter.State == WaveEncounter.EncounterState.WaitingForWave)
        {
            yield return Capture(1920, 1080, "08_wave3_incoming_banner_1920x1080.png");
        }
        // Wait for an aimed attack (autopilot keeps playing), then show the warning line and the shot in flight.
        TestAttackDirector director = Find<TestAttackDirector>();
        bool attacked = false;
        System.Action<Vector3> onAttack = _ => attacked = true;
        director.AttackStarted += onAttack;
        float attackLimit = Time.time + 15f;
        while (!attacked && Time.time < attackLimit)
        {
            yield return AutoPilot(ship, 0.1f);
        }
        director.AttackStarted -= onAttack;
        yield return new WaitForSeconds(0.65f); // aim line locked, just before the shot
        yield return Capture(1920, 1080, "09_wave3_attack_warning_1920x1080.png");
        yield return new WaitForSeconds(0.4f);
        yield return Capture(1280, 720, "10_wave3_enemy_shot_1280x720.png");
        yield return AutoPilot(ship, 1.5f);
        yield return Capture(1920, 1080, "10b_wave3_mixed_1920x1080.png");

        // Enemy destruction close-up timing: flash + blast, then sparks and fading debris.
        fx.PlayExplosion(new Vector3(-3f, 0f, 1f), 1f);
        yield return new WaitForSeconds(0.05f);
        yield return Capture(1920, 1080, "10c_explosion_flash_1920x1080.png", new RectInt(560, 280, 400, 300));
        yield return new WaitForSeconds(0.25f);
        yield return Capture(1920, 1080, "10d_explosion_debris_1920x1080.png", new RectInt(560, 280, 400, 300));

        // 8. Cleared result panel.
        Time.timeScale = 10f;
        while (!encounter.IsCompleted)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        yield return Capture(1920, 1080, "11_result_cleared_1920x1080.png", new RectInt(540, 300, 840, 480));
        yield return Capture(1280, 720, "12_result_cleared_1280x720.png");

        // 9. Defeat: health back on, park the ship on enemies.
        harness.RestartAll();
        health.enabled = true;
        float limit = Time.realtimeSinceStartup + 20f;
        while (!health.IsDefeated && Time.realtimeSinceStartup < limit)
        {
            TestContactDamage enemy = Find<TestContactDamage>();
            if (enemy != null)
            {
                ship.transform.position = enemy.transform.position;
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.12f);
        yield return Capture(1920, 1080, "13_defeat_explosion_1920x1080.png");
        yield return new WaitForSeconds(1f);
        yield return Capture(1280, 720, "14_result_defeated_1280x720.png");

        // 10. Optional Sun backdrop preset during play.
        backdrop.Apply(TestBackdropPreset.Preset.Sun);
        harness.RestartAll();
        health.enabled = false;
        yield return new WaitForSeconds(2.2f);
        yield return AutoPilot(ship, 3.5f);
        yield return Capture(1920, 1080, "15_sun_preset_1920x1080.png");
        backdrop.Apply(TestBackdropPreset.Preset.PlanetAndMoon);

        Time.timeScale = 1f;
        ShaderUtil.allowAsyncCompilation = asyncShaders;
        yield return SceneManager.UnloadSceneAsync(scene);
    }
}
#endif
