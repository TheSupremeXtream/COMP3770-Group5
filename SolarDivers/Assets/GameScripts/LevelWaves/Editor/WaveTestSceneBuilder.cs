using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Rebuilds the wave prototype's placeholder prefabs, meshes, textures, materials, effects and test scene from code,
/// so the test setup can be recreated if it gets broken. Menu: Solar Divers > Level Waves.
/// Everything it creates is original (generated here) and lives in LevelWaves folders / the test scene only.
/// The only external content is the edited CC0 sound clips in Assets/Audio/LevelWaves (see CREDITS.md there);
/// the builder sets their import options and collects them into TEST_SoundBank.
/// </summary>
public static partial class WaveTestSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/LevelWaves_Encounter01_Test.unity";
    const string PrefabFolder = "Assets/Prefabs/LevelWaves";
    const string MaterialFolder = "Assets/Materials/LevelWaves";
    const string TextureFolder = "Assets/Materials/LevelWaves/Textures";
    const string ModelFolder = "Assets/Models/LevelWaves";
    const string AudioFolder = "Assets/Audio/LevelWaves";
    const int UiLayer = 5; // Unity's built-in "UI" layer

    // Light direction painted into the planet and moon images (disc space: x right, y up the screen, z to camera).
    // Matches the key light of the planet preset (from the upper left).
    static readonly Vector3 BackdropLight = new Vector3(-0.6f, 0.72f, 0.2f);

    // Assets from earlier versions of this prototype that are no longer used.
    static readonly string[] ObsoleteAssets =
    {
        MaterialFolder + "/TEST_EnemyBasic.mat", MaterialFolder + "/TEST_EnemyWeaver.mat", MaterialFolder + "/TEST_PlayerShip.mat",
        MaterialFolder + "/TEST_Projectile.mat", MaterialFolder + "/TEST_SpaceFloor.mat", MaterialFolder + "/TEST_SpawnMarker.mat",
        MaterialFolder + "/TEST_Asteroid.mat", MaterialFolder + "/TEST_Rock.mat", MaterialFolder + "/TEST_Planet.mat",
        ModelFolder + "/TEST_Mesh_Rock1.asset", ModelFolder + "/TEST_Mesh_Rock2.asset", ModelFolder + "/TEST_Mesh_Rock3.asset"
    };

    [MenuItem("Solar Divers/Level Waves/Rebuild Encounter 01 Test Scene")]
    static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Rebuild wave test scene",
                "This overwrites " + ScenePath + " and the TEST_ prefabs in " + PrefabFolder +
                ". Inspector changes made to them will be lost. Continue?", "Rebuild", "Cancel"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        Build();
    }

    /// <summary>Batch-mode entry: Unity -batchmode -executeMethod WaveTestSceneBuilder.BuildFromCommandLine -quit</summary>
    public static void BuildFromCommandLine()
    {
        Build();
    }

    static void Build()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(TextureFolder);
        EnsureFolder(ModelFolder);
        foreach (string path in ObsoleteAssets)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        // Textures (all generated).
        Texture2D softDot = SoftDotTexture();
        Texture2D flareTex = SaveTexture("TEST_Tex_Flare", 128, ProceduralSpaceTextures.Flare(128), false);
        Texture2D ringTex = SaveTexture("TEST_Tex_Ring", 128, ProceduralSpaceTextures.Ring(128), false);
        Texture2D chevronTex = SaveTexture("TEST_Tex_Chevron", 64, ProceduralSpaceTextures.Chevron(64), true);
        Texture2D planetTex = SaveTexture("TEST_Tex_Planet", 1024, ProceduralSpaceTextures.Planet(1024, BackdropLight, 7), false);
        Texture2D moonTex = SaveTexture("TEST_Tex_Moon", 512, ProceduralSpaceTextures.Moon(512, BackdropLight, 3), false);
        Texture2D sunTex = SaveTexture("TEST_Tex_Sun", 512, ProceduralSpaceTextures.Sun(512, 5), false);
        Texture2D nebulaTexA = SaveTexture("TEST_Tex_NebulaA", 512,
            ProceduralSpaceTextures.Nebula(512, new Color(0.42f, 0.22f, 0.78f), new Color(0.14f, 0.36f, 0.8f), 11), false);
        Texture2D nebulaTexB = SaveTexture("TEST_Tex_NebulaB", 512,
            ProceduralSpaceTextures.Nebula(512, new Color(0.1f, 0.48f, 0.62f), new Color(0.55f, 0.2f, 0.55f), 23), false);

        // Effect and backdrop materials. Additive = glows; alpha = solid-looking bodies.
        Material additive = Transparent("TEST_FX_Additive", "Universal Render Pipeline/Particles/Unlit", softDot, Color.white, true);
        Material flare = Transparent("TEST_FX_Flare", "Universal Render Pipeline/Particles/Unlit", flareTex, Color.white, true);
        Material ring = Transparent("TEST_FX_Ring", "Universal Render Pipeline/Particles/Unlit", ringTex, Color.white, true);
        Material muzzle = Transparent("TEST_FX_MuzzleFlash", "Universal Render Pipeline/Unlit", flareTex, new Color(0.9f, 1.9f, 2.4f, 1f), true);
        Material warning = Transparent("TEST_FX_Warning", "Universal Render Pipeline/Unlit", chevronTex, new Color(1.6f, 1.1f, 0.3f, 1f), true);
        // Backdrop tints slightly above 1 lift the bodies a little (still under the bloom threshold).
        Material planetMat = Transparent("TEST_Backdrop_Planet", "Universal Render Pipeline/Unlit", planetTex, new Color(1.1f, 1.1f, 1.1f, 1f), false);
        Material moonMat = Transparent("TEST_Backdrop_Moon", "Universal Render Pipeline/Unlit", moonTex, new Color(1.15f, 1.15f, 1.15f, 1f), false);
        Material sunMat = Transparent("TEST_Backdrop_Sun", "Universal Render Pipeline/Unlit", sunTex, new Color(2.2f, 1.9f, 1.5f, 1f), true);
        Material nebulaMatA = Transparent("TEST_Backdrop_NebulaA", "Universal Render Pipeline/Unlit", nebulaTexA, new Color(0.85f, 0.85f, 0.85f, 0.8f), true);
        Material nebulaMatB = Transparent("TEST_Backdrop_NebulaB", "Universal Render Pipeline/Unlit", nebulaTexB, new Color(0.8f, 0.8f, 0.8f, 0.75f), true);
        // Enemy attack visuals: warm orange/red, clearly unlike the player's cyan bolts.
        Material aimLine = Transparent("TEST_FX_AimLine", "Universal Render Pipeline/Particles/Unlit", softDot, Color.white, true);
        Material enemyShotCore = Transparent("TEST_FX_EnemyShotCore", "Universal Render Pipeline/Unlit", softDot, new Color(2.6f, 1.3f, 0.6f, 1f), true);
        Material enemyShotFlare = Transparent("TEST_FX_EnemyShotFlare", "Universal Render Pipeline/Unlit", flareTex, new Color(2.2f, 0.55f, 0.2f, 1f), true);
        Material debrisMat = Debris("TEST_FX_Debris");
        Material rocketBeacon = Transparent("TEST_FX_RocketBeacon", "Universal Render Pipeline/Unlit", flareTex, new Color(2.6f, 0.3f, 0.25f, 1f), true);

        // Ship materials: light enough to read against space; low metallic because there is no environment to reflect.
        Material[] playerMats =
        {
            Lit("TEST_Player_Hull", new Color(0.86f, 0.9f, 0.95f), 0.1f, 0.62f, new Color(0.03f, 0.035f, 0.04f)),
            Lit("TEST_Player_Accent", new Color(0.15f, 0.5f, 1f), 0.1f, 0.55f, new Color(0.03f, 0.1f, 0.26f)),
            Lit("TEST_Player_Cockpit", new Color(0.05f, 0.12f, 0.18f), 0.1f, 0.9f, new Color(0.15f, 0.7f, 1f) * 0.9f),
            Lit("TEST_Player_EngineGlow", Color.black, 0f, 0.5f, new Color(0.3f, 0.85f, 1f) * 4f)
        };
        Material[] dartMats =
        {
            Lit("TEST_Dart_Hull", new Color(0.42f, 0.13f, 0.15f), 0.1f, 0.5f, new Color(0.04f, 0.01f, 0.012f)),
            Lit("TEST_Dart_Wings", new Color(0.95f, 0.22f, 0.16f), 0.1f, 0.45f, new Color(0.16f, 0.02f, 0.01f)),
            Lit("TEST_Dart_Eye", Color.black, 0f, 0.8f, new Color(1f, 0.15f, 0.1f) * 2.5f),
            Lit("TEST_Dart_EngineGlow", Color.black, 0f, 0.5f, new Color(1f, 0.45f, 0.15f) * 4f)
        };
        Material[] mantaMats =
        {
            Lit("TEST_Manta_Hull", new Color(0.42f, 0.3f, 0.58f), 0.1f, 0.5f, new Color(0.035f, 0.025f, 0.05f)),
            Lit("TEST_Manta_Wings", new Color(1f, 0.58f, 0.15f), 0.1f, 0.45f, new Color(0.18f, 0.07f, 0f)),
            Lit("TEST_Manta_Eye", Color.black, 0f, 0.8f, new Color(1f, 0.85f, 0.2f) * 2f),
            Lit("TEST_Manta_EngineGlow", Color.black, 0f, 0.5f, new Color(0.9f, 0.3f, 1f) * 3.5f)
        };
        // Interceptor: the Dart model, smaller, in pale gold with a hot white-blue engine so the fast class reads at a glance.
        Material[] interceptorMats =
        {
            Lit("TEST_Interceptor_Hull", new Color(0.55f, 0.48f, 0.3f), 0.1f, 0.55f, new Color(0.05f, 0.04f, 0.02f)),
            Lit("TEST_Interceptor_Wings", new Color(1f, 0.85f, 0.35f), 0.1f, 0.5f, new Color(0.2f, 0.15f, 0.03f)),
            Lit("TEST_Interceptor_Eye", Color.black, 0f, 0.8f, new Color(1f, 0.95f, 0.6f) * 2.5f),
            Lit("TEST_Interceptor_EngineGlow", Color.black, 0f, 0.5f, new Color(0.6f, 0.85f, 1f) * 4.5f)
        };
        Material boltMat = Lit("TEST_LaserBolt", Color.black, 0f, 0.5f, new Color(0.35f, 1f, 0.9f) * 6f);

        Mesh playerMesh = SaveMesh(LowPolyShipMeshes.PlayerFighter());
        Mesh dartMesh = SaveMesh(LowPolyShipMeshes.EnemyDart());
        Mesh mantaMesh = SaveMesh(LowPolyShipMeshes.EnemyManta());
        Mesh boltMesh = SaveMesh(LowPolyShipMeshes.LaserBolt());

        GameObject hitSpark = BuildSparks("TEST_FX_HitSpark", additive, flare, new Color(0.75f, 1f, 1f), new Color(0.35f, 0.9f, 1f), 12, 0.9f);
        GameObject playerHit = BuildSparks("TEST_FX_PlayerHit", additive, flare, new Color(1f, 0.85f, 0.6f), new Color(1f, 0.25f, 0.12f), 18, 1.6f);
        GameObject explosion = BuildExplosion(additive, flare, ring, debrisMat);
        GameObject projectile = BuildProjectile(boltMesh, boltMat, additive);
        GameObject enemyShot = BuildEnemyShot(enemyShotCore, enemyShotFlare, additive);
        // Three speed classes. Health is unchanged from the first prototype (fighter 2 hits, heavy 3); the new interceptor
        // takes 1 hit because it is small and fast. Speeds / turn rates are prefab settings (TestEnemyMover).
        var fighterClass = new EnemyClassSettings
        {
            enemyClass = TestEnemyClass.Fighter, pattern = TestMovePattern.CurvedPass, speed = 2.6f, turnRate = 110f, turnAcceleration = 300f,
            acceleration = 3f, health = 20, halfWidth = 0.75f, modelScale = 1f
        };
        var heavyClass = new EnemyClassSettings
        {
            enemyClass = TestEnemyClass.Heavy, pattern = TestMovePattern.SideSweep, speed = 1.7f, turnRate = 55f, turnAcceleration = 110f,
            acceleration = 1.5f, health = 30, halfWidth = 1.1f, modelScale = 1.1f
        };
        var interceptorClass = new EnemyClassSettings
        {
            enemyClass = TestEnemyClass.Interceptor, pattern = TestMovePattern.CurvedPass, speed = 4f, turnRate = 150f, turnAcceleration = 450f,
            acceleration = 5f, health = 10, halfWidth = 0.6f, modelScale = 0.8f
        };
        GameObject basic = BuildEnemy("TEST_Enemy_Basic", dartMesh, dartMats, new Vector3(1.5f, 0.6f, 1.5f), fighterClass,
            additive, new Color(1f, 0.5f, 0.2f), new[] { new Vector3(0f, 0f, -0.82f) }, 1f, aimLine, 1.0f);
        GameObject weaver = BuildEnemy("TEST_Enemy_Weaver", mantaMesh, mantaMats, new Vector3(2.4f, 0.6f, 1.3f), heavyClass,
            additive, new Color(0.85f, 0.35f, 1f), new[] { new Vector3(-0.3f, 0f, -0.57f), new Vector3(0.3f, 0f, -0.57f) }, 1.35f, aimLine, 0.75f);
        GameObject interceptor = BuildEnemy("TEST_Enemy_Interceptor", dartMesh, interceptorMats, new Vector3(1.2f, 0.6f, 1.2f), interceptorClass,
            additive, new Color(0.6f, 0.85f, 1f), new[] { new Vector3(0f, 0f, -0.82f) }, 0.8f, aimLine, 0.8f);
        GameObject ship = BuildPlayerShip(playerMesh, playerMats, projectile, additive, muzzle);
        TestDifficultyProfile difficulty = DifficultyProfile();
        TestSoundBank soundBank = SoundBank();
        GameObject rocket = BuildHomingRocket(boltMesh, additive, rocketBeacon, soundBank);

        var backdrop = new Backdrop
        {
            additive = additive, planet = planetMat, moon = moonMat, sun = sunMat, nebulaA = nebulaMatA, nebulaB = nebulaMatB
        };
        BuildScene(basic, weaver, interceptor, ship, hitSpark, playerHit, explosion, warning, backdrop, enemyShot, difficulty, soundBank, rocket);
        AssetDatabase.SaveAssets();
        Debug.Log("[WaveTestSceneBuilder] Built " + ScenePath);
    }

    struct Backdrop
    {
        public Material additive, planet, moon, sun, nebulaA, nebulaB;
    }

    struct EnemyClassSettings
    {
        public TestEnemyClass enemyClass;
        public TestMovePattern pattern;
        public float speed, turnRate, turnAcceleration, acceleration, halfWidth, modelScale;
        public int health;
    }

    /// <summary>
    /// Imports the edited CC0 clips in Assets/Audio/LevelWaves as uncompressed mono (short, so PCM is small and loops
    /// stay seamless) and collects them into TEST_SoundBank.
    /// </summary>
    static TestSoundBank SoundBank()
    {
        EnsureFolder("Assets/Audio");
        EnsureFolder(AudioFolder);
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter;
            if (importer == null)
            {
                continue;
            }
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (!importer.forceToMono || settings.loadType != AudioClipLoadType.DecompressOnLoad || settings.compressionFormat != AudioCompressionFormat.PCM)
            {
                importer.forceToMono = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        string path = AudioFolder + "/TEST_SoundBank.asset";
        var bank = AssetDatabase.LoadAssetAtPath<TestSoundBank>(path);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<TestSoundBank>();
            AssetDatabase.CreateAsset(bank, path);
        }
        bank.autocannon = Clips("LW_Cannon_A", "LW_Cannon_B", "LW_Cannon_C", "LW_Cannon_D");
        bank.metalImpacts = Clips("LW_Impact_Metal_A", "LW_Impact_Metal_B", "LW_Impact_Metal_C");
        bank.playerHit = Clips("LW_PlayerHit", "LW_PlayerHit_B", "LW_PlayerHit_C");
        bank.enemyExplosions = Clips("LW_Explosion_A", "LW_Explosion_B", "LW_Explosion_C");
        bank.playerDestroyed = Clips("LW_PlayerDestroyed");
        bank.enemyCharge = Clips("LW_EnemyCharge");
        bank.enemyGun = Clips("LW_EnemyGun_A", "LW_EnemyGun_B", "LW_EnemyGun_C");
        bank.rocketLaunch = Clips("LW_RocketLaunch", "LW_RocketLaunch_B");
        bank.rocketBlast = Clips("LW_RocketBlast_A", "LW_RocketBlast_B");
        AudioClip[] rocketLoop = Clips("LW_RocketLoop");
        bank.rocketLoop = rocketLoop.Length > 0 ? rocketLoop[0] : null;
        bank.flybys = Clips("LW_Flyby_A", "LW_Flyby_B", "LW_Flyby_C", "LW_Flyby_D");
        AudioClip[] loop = Clips("LW_Engine_PlayerLoop");
        bank.playerEngineLoop = loop.Length > 0 ? loop[0] : null;
        bank.flybyPassSeconds = 1.4f; // where the recorded jet is closest in every LW_Flyby clip (set when they were edited)
        EditorUtility.SetDirty(bank);
        return bank;
    }

    static AudioClip[] Clips(params string[] names)
    {
        var found = new List<AudioClip>();
        foreach (string clipName in names)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + clipName + ".wav");
            if (clip != null)
            {
                found.Add(clip);
            }
            else
            {
                Debug.LogError("[WaveTestSceneBuilder] Missing sound " + AudioFolder + "/" + clipName + ".wav");
            }
        }
        return found.ToArray();
    }

    /// <summary>
    /// Per-wave difficulty for this encounter (PLACEHOLDER enemies). Harder through movement and attacks, not health.
    /// Wave 1 introduces the patterns one per group with single slow shots; wave 2 combines them with a lane shift,
    /// interceptor pursuit, 3-shot bursts with a little lead and the first rocket per group (the V leader); wave 3 adds
    /// pressure with all classes, flanks, fighter pursuit, 4-shot bursts with more lead and rockets from the heavies.
    /// Paths vary a little between runs (bounded, seeded per run).
    /// </summary>
    static TestDifficultyProfile DifficultyProfile()
    {
        string path = PrefabFolder + "/TEST_Difficulty_Encounter01.asset";
        var profile = AssetDatabase.LoadAssetAtPath<TestDifficultyProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<TestDifficultyProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        profile.waves = new List<TestWaveDifficulty>
        {
            // Wave 1 introduces one pattern per group (sweep, curved pass, dive-and-retreat) and the first aimed shots:
            // one single, slow, unleading shot per group with a long warning. No reactions, no rockets.
            new TestWaveDifficulty
            {
                label = "Wave 1 - sweep, curved pass, dive and retreat", speedMultiplier = 1f, turnRateMultiplier = 1f, pathWidthMultiplier = 1f,
                groupPatterns = new[] { TestMovePattern.SideSweep, TestMovePattern.CurvedPass, TestMovePattern.PassAndRetreat },
                variation = 0.15f,
                aimedShooterEveryNth = 4, telegraphSeconds = 1f, shotSpeed = 7f, shotsPerBurst = 1, leadFactor = 0f
            },
            // Wave 2 combines them: dive-and-regroup, sweeps and retreats in one wave, a one-off lane shift, short
            // pursuit by the interceptors, 3-shot bursts with a little lead, and the first rocket (the V leader).
            new TestWaveDifficulty
            {
                label = "Wave 2 - combined patterns, bursts, first rockets", speedMultiplier = 1.05f, turnRateMultiplier = 1f,
                pathWidthMultiplier = 1.1f, alternateFlanks = true,
                groupPatterns = new[] { TestMovePattern.DiveRegroup, TestMovePattern.SideSweep, TestMovePattern.PassAndRetreat },
                reactAfterDistance = 2.5f, laneSeekDistance = 1.5f,
                pursuitClasses = TestEnemyClassMask.Interceptor, pursuitSeconds = 0.8f,
                variation = 0.2f,
                aimedShooterEveryNth = 3, telegraphSeconds = 0.9f, shotSpeed = 8f, shotsPerBurst = 3, burstInterval = 0.14f,
                leadFactor = 0.35f, maxLead = 1.5f,
                groupLeaderRocket = true, rocketWarningSeconds = 1.1f
            },
            // Wave 3 adds pressure: all three classes, heavies sweep and carry rockets, interceptors flank, fighters
            // make short pursuits, 4-shot bursts with more lead.
            new TestWaveDifficulty
            {
                label = "Wave 3 - pressure: flanks, pursuit, bursts and rockets", speedMultiplier = 1.1f, turnRateMultiplier = 1.1f,
                pathWidthMultiplier = 1f, alternateFlanks = true,
                groupPatterns = new[] { TestMovePattern.DiveRegroup, TestMovePattern.CurvedPass, TestMovePattern.SideSweep },
                keepOwnPattern = TestEnemyClassMask.Heavy,
                reactAfterDistance = 2.5f, laneSeekDistance = 1.2f,
                flankingClasses = TestEnemyClassMask.Interceptor, flankOffset = 2.4f, trackingSeconds = 0.6f, maxReposition = 4f,
                pursuitClasses = TestEnemyClassMask.Fighter, pursuitSeconds = 0.8f,
                variation = 0.2f,
                aimedShooterEveryNth = 2, telegraphSeconds = 0.85f, shotSpeed = 8.5f, shotsPerBurst = 4, burstInterval = 0.13f,
                leadFactor = 0.5f, maxLead = 1.8f,
                rocketClasses = TestEnemyClassMask.Heavy, rocketWarningSeconds = 1f
            }
        };
        profile.minEscapeGap = 2.6f;
        profile.maxShotsInFlight = 2;
        profile.minSecondsBetweenAttacks = 1.2f;
        profile.minFireDistance = 7f;
        profile.fireWindowZ = new Vector2(3f, 8.5f);
        profile.fireWindowX = 9.5f;
        profile.aimLockSeconds = 0.3f;
        profile.holdFireWhilePlayerRecovers = true;
        profile.maxRocketsInFlight = 2;
        profile.minSecondsBetweenRockets = 3f;
        profile.minRocketDistance = 8f;
        profile.fixedSeed = 0;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static void BuildScene(GameObject basic, GameObject weaver, GameObject interceptor, GameObject ship, GameObject hitSpark, GameObject playerHit,
        GameObject explosion, Material warningMat, Backdrop backdrop, GameObject enemyShot, TestDifficultyProfile difficulty, TestSoundBank soundBank,
        GameObject rocket)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.23f, 0.31f);
        RenderSettings.skybox = null;

        // Scene camera: post-processing (SMAA, bloom, vignette) for the game view only; never draws the UI layer.
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.042f, 0.08f); // lifted deep blue, not black
        cam.fieldOfView = 60f;
        cam.farClipPlane = 200f;
        cam.cullingMask = ~(1 << UiLayer);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<TestCameraShake>();
        camGo.transform.SetPositionAndRotation(new Vector3(0f, 16f, 0f), Quaternion.Euler(90f, 0f, 0f));
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;

        // HUD camera: overlay in the scene camera's stack, UI layer only, post-processing OFF, so text is drawn
        // after bloom/vignette/anti-aliasing and stays pixel-sharp. Not parented to the scene camera, so it never shakes.
        var uiCamGo = new GameObject("PLACEHOLDER_UICamera (HUD only, no post-processing)");
        uiCamGo.transform.SetPositionAndRotation(new Vector3(0f, 200f, 0f), Quaternion.Euler(90f, 0f, 0f));
        var uiCam = uiCamGo.AddComponent<Camera>();
        uiCam.cullingMask = 1 << UiLayer;
        uiCam.clearFlags = CameraClearFlags.Depth;
        var uiCamData = uiCam.GetUniversalAdditionalCameraData();
        uiCamData.renderType = CameraRenderType.Overlay;
        uiCamData.renderPostProcessing = false;
        uiCamData.antialiasing = AntialiasingMode.None;
        uiCamData.renderShadows = false;
        camData.cameraStack.Add(uiCam);

        var volumeGo = new GameObject("PLACEHOLDER_PostFX (scene only)");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = PostFxProfile();

        var key = new GameObject("Key Light").AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.6f;
        key.color = new Color(1f, 0.96f, 0.9f);
        key.shadows = LightShadows.None; // small flat-shaded ships seen from above: shadows mostly add acne
        key.transform.rotation = Quaternion.Euler(52f, 150f, 0f);
        var fill = new GameObject("Fill Light").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.55f;
        fill.color = new Color(0.5f, 0.65f, 1f);
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

        BuildBackground(backdrop, key);

        var encounterGo = new GameObject("WaveEncounter_01");
        var encounter = encounterGo.AddComponent<WaveEncounter>();
        var origin = new GameObject("SpawnOrigin").transform;
        origin.SetParent(encounterGo.transform);
        origin.SetPositionAndRotation(new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 180f, 0f));

        // Pacing: each wave adds one enemy per group and shortens the gaps. Groups alternate sides so
        // the player always has an open lane to dodge into; the last group of each wave comes down the middle.
        // Prefab lists cycle per enemy, so a list as long as the group assigns a class to each slot:
        // wave 2 = four fighters + one interceptor (rear right of the V); wave 3 = fighter, interceptor, heavy, repeated.
        var waves = new List<EnemyWave>
        {
            new EnemyWave
            {
                waveName = "Wave 1 - Basic Line", formation = WaveFormationType.Line,
                enemyPrefabs = new[] { basic }, enemiesPerGroup = 4, groupCount = 3, spacing = 2.5f,
                spawnStyle = WaveSpawnStyle.Formation, spawnInterval = 0f, delayBetweenGroups = 3.5f,
                groupEntryOffsets = new[] { -4f, 4f, 0f }, delayBeforeWave = 2f, clearTimeout = 15f
            },
            new EnemyWave
            {
                waveName = "Wave 2 - V Formation", formation = WaveFormationType.VFormation,
                enemyPrefabs = new[] { basic, basic, basic, basic, interceptor }, enemiesPerGroup = 5, groupCount = 3, spacing = 1.8f,
                spawnStyle = WaveSpawnStyle.Formation, spawnInterval = 0f, delayBetweenGroups = 3f,
                groupEntryOffsets = new[] { 4.5f, -4.5f, 0f }, delayBeforeWave = 2.5f, clearTimeout = 15f
            },
            new EnemyWave
            {
                waveName = "Wave 3 - Mixed", formation = WaveFormationType.Mixed,
                enemyPrefabs = new[] { basic, interceptor, weaver }, enemiesPerGroup = 6, groupCount = 3, spacing = 2f,
                spawnStyle = WaveSpawnStyle.Stream, spawnInterval = 0.5f, delayBetweenGroups = 2f,
                groupEntryOffsets = new[] { -2f, 2f, 0f }, delayBeforeWave = 2.5f, clearTimeout = 26f
            }
        };
        var spawned = new GameObject("SpawnedEnemies").transform;
        encounter.Configure(waves, origin, new Bounds(new Vector3(0f, 0f, 1f), new Vector3(24f, 10f, 26f)), true, spawned);

        var warningsGo = new GameObject("PLACEHOLDER_EntryWarnings");
        var warnings = warningsGo.AddComponent<TestEntryWarnings>();
        GameObject marker = Quad("MarkerTemplate", warningMat, 1);
        marker.transform.SetParent(warningsGo.transform, false);
        marker.transform.localScale = new Vector3(1f, 0.9f, 1f);
        marker.SetActive(false);
        SetObject(warnings, "encounter", encounter);
        SetObject(warnings, "markerPrefab", marker);

        var shipInstance = (GameObject)PrefabUtility.InstantiatePrefab(ship);
        shipInstance.transform.position = new Vector3(0f, 0f, -5f);

        var fxGo = new GameObject("PLACEHOLDER_FX");
        var fx = fxGo.AddComponent<TestFxManager>();
        SetObject(fx, "hitSparkPrefab", hitSpark);
        SetObject(fx, "playerHitPrefab", playerHit);
        SetObject(fx, "explosionPrefab", explosion);
        SetObject(fx, "sounds", soundBank);
        var flight = new GameObject("PLACEHOLDER_FlightAudio").AddComponent<TestFlightAudio>();
        SetObject(flight, "sounds", soundBank);
        SetObject(flight, "player", shipInstance.GetComponent<TestPlayerShip>());
        SetObject(flight, "playerHealth", shipInstance.GetComponent<TestPlayerHealth>());

        var hudGo = new GameObject("PLACEHOLDER_HUD");
        var hud = hudGo.AddComponent<EncounterDebugHUD>();
        SetObject(hud, "encounter", encounter);
        SetObject(hud, "playerHealth", shipInstance.GetComponent<TestPlayerHealth>());
        SetObject(hud, "uiCamera", uiCam);

        var directorGo = new GameObject("PLACEHOLDER_AttackDirector");
        var director = directorGo.AddComponent<TestAttackDirector>();
        director.Configure(difficulty, shipInstance.transform, shipInstance.GetComponent<TestPlayerHealth>(), encounter, enemyShot, rocket);

        var harnessGo = new GameObject("PLACEHOLDER_TestHarness");
        harnessGo.AddComponent<EncounterTestHarness>().Configure(encounter, shipInstance.GetComponent<TestPlayerHealth>(),
            shipInstance.GetComponent<TestPlayerShip>(), fx, camGo.GetComponent<TestCameraShake>(), warnings, director, flight);

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Everything here is far below the play plane (y = 0), has no colliders and never hurts the player.
    // Depth from the camera (y = 16): dust 20, play plane 16, planet 46, moon 50, mid stars 66, nebula 86, far stars 106.
    static void BuildBackground(Backdrop b, Light key)
    {
        var scenery = new GameObject("PLACEHOLDER_Background (decor only)");
        BuildStarLayer(scenery.transform, "Stars_Far", b.additive, -90f, 66f, 230f, 1.2f, new Vector2(0.2f, 0.4f), 7.5f, 1000,
            new Color(0.78f, 0.86f, 1f, 1f), new Color(1f, 0.92f, 0.78f, 0.9f), false, -30);
        BuildStarLayer(scenery.transform, "Stars_Mid", b.additive, -50f, 40f, 145f, 3f, new Vector2(0.15f, 0.3f), 9f, 350,
            new Color(0.85f, 0.92f, 1f, 1f), new Color(1f, 1f, 1f, 0.9f), false, -25);
        BuildStarLayer(scenery.transform, "Dust_Near", b.additive, -4f, 13f, 44f, 9f, new Vector2(0.05f, 0.08f), 6f, 40,
            new Color(0.75f, 0.85f, 1f, 0.4f), new Color(0.75f, 0.85f, 1f, 0.25f), true, -5);

        // Two soft nebula clouds drifting very slowly (additive, faint, behind everything else).
        GameObject nebulaA = Quad("Nebula_A", b.nebulaA, -40);
        nebulaA.transform.SetParent(scenery.transform, false);
        nebulaA.transform.SetPositionAndRotation(new Vector3(-28f, -70f, 14f), Quaternion.Euler(90f, 0f, 20f));
        nebulaA.transform.localScale = Vector3.one * 75f;
        GameObject nebulaB = Quad("Nebula_B", b.nebulaB, -40);
        nebulaB.transform.SetParent(scenery.transform, false);
        nebulaB.transform.SetPositionAndRotation(new Vector3(34f, -70f, -40f), Quaternion.Euler(90f, 0f, -35f));
        nebulaB.transform.localScale = Vector3.one * 62f;
        foreach (GameObject cloud in new[] { nebulaA, nebulaB })
        {
            var drift = cloud.AddComponent<TestBackgroundDrift>();
            SetFloat(drift, "speed", 0.45f);
            SetFloat(drift, "wrapBelowZ", -90f); // fully off-screen at this depth before wrapping
            SetFloat(drift, "wrapToZ", 90f);
            SetVector(drift, "spinDegreesPerSecond", Vector3.zero);
        }

        // Preset A: one main planet (bottom-right, mostly outside the lanes) and a small moon (left, below the HUD panel).
        var planetAndMoon = new GameObject("Backdrop_PlanetAndMoon");
        planetAndMoon.transform.SetParent(scenery.transform, false);
        GameObject planet = Quad("Planet", b.planet, -10);
        planet.transform.SetParent(planetAndMoon.transform, false);
        planet.transform.position = new Vector3(44f, -30f, -15f);
        planet.transform.localScale = Vector3.one * (2f * 15f * 1.1f); // radius 15 + 10% atmosphere margin
        GameObject moon = Quad("Moon", b.moon, -10);
        moon.transform.SetParent(planetAndMoon.transform, false);
        moon.transform.position = new Vector3(-40f, -34f, 13f);
        moon.transform.localScale = Vector3.one * (2f * 4f * 1.02f);

        // Preset B (off by default): the Sun in the top-right corner, with the key light turned to match.
        var sunRoot = new GameObject("Backdrop_Sun");
        sunRoot.transform.SetParent(scenery.transform, false);
        GameObject sun = Quad("Sun", b.sun, -20);
        sun.transform.SetParent(sunRoot.transform, false);
        sun.transform.position = new Vector3(42f, -40f, 22f);
        sun.transform.localScale = Vector3.one * (2f * 5f * 1.9f);
        sunRoot.SetActive(false);

        var preset = scenery.AddComponent<TestBackdropPreset>();
        SetObject(preset, "planetAndMoon", planetAndMoon);
        SetObject(preset, "sun", sunRoot);
        SetObject(preset, "keyLight", key);
    }

    // ---- Prefabs ----

    static GameObject BuildEnemy(string name, Mesh mesh, Material[] mats, Vector3 hitbox, EnemyClassSettings settings,
        Material exhaustMat, Color exhaustColour, Vector3[] nozzles, float explosionScale, Material aimLineMat, float muzzleZ,
        GameObject visualPrefab = null, bool triggerHitbox = false, string folder = PrefabFolder)
    {
        var root = new GameObject(name);
        // Gameplay hitbox on the root, sized to the ship; the model below has no colliders.
        // Trigger hitboxes (integrated demo) let a CharacterController player pass through instead of being blocked;
        // shots and contact damage still work because those objects carry kinematic rigidbodies.
        var box = root.AddComponent<BoxCollider>();
        box.size = hitbox;
        box.isTrigger = triggerHitbox;
        Transform model = visualPrefab != null
            ? NestedVisual(root.transform, visualPrefab, hitbox.x * settings.modelScale)
            : MakeModel(root.transform, mesh, mats);
        if (visualPrefab == null)
        {
            model.localScale = Vector3.one * settings.modelScale;
        }
        // A nested teammate model has its own rotation and scale, so its exhaust hangs off the ship root instead.
        Transform exhaustParent = visualPrefab != null ? root.transform : model;
        foreach (Vector3 nozzle in nozzles)
        {
            BuildExhaust(exhaustParent, nozzle, exhaustMat, exhaustColour, 0.22f);
        }
        var mover = root.AddComponent<TestEnemyMover>();
        var moverSo = new SerializedObject(mover);
        moverSo.FindProperty("enemyClass").enumValueIndex = (int)settings.enemyClass;
        moverSo.FindProperty("pattern").enumValueIndex = (int)settings.pattern;
        moverSo.FindProperty("speed").floatValue = settings.speed;
        moverSo.FindProperty("acceleration").floatValue = settings.acceleration;
        moverSo.FindProperty("turnRate").floatValue = settings.turnRate;
        moverSo.FindProperty("turnAcceleration").floatValue = settings.turnAcceleration;
        moverSo.FindProperty("halfWidth").floatValue = settings.halfWidth;
        moverSo.ApplyModifiedPropertiesWithoutUndo();
        var hp = root.AddComponent<TestEnemyHealth>();
        var hpSo = new SerializedObject(hp);
        hpSo.FindProperty("maxHealth").intValue = settings.health;
        hpSo.FindProperty("visualRoot").objectReferenceValue = model;
        hpSo.FindProperty("explosionScale").floatValue = explosionScale;
        hpSo.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<TestContactDamage>();
        var bank = root.AddComponent<TestShipBanking>();
        SetObject(bank, "model", model);
        // The root turns to face its heading, so the model only rolls into turns (no extra yaw).
        SetFloat(bank, "maxBankDegrees", 32f);
        SetFloat(bank, "fullBankSpeed", 1000f);
        SetFloat(bank, "maxYawDegrees", 0f);
        SetFloat(bank, "fullBankTurnRate", settings.turnRate * 0.7f);
        SetFloat(bank, "responsiveness", 7f);

        // Aimed shot (armed only in waves whose difficulty asks for it): muzzle at the nose, aim line hidden until charging.
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(root.transform, false);
        muzzle.localPosition = new Vector3(0f, 0f, muzzleZ);
        var lineGo = new GameObject("AimLine");
        lineGo.transform.SetParent(root.transform, false);
        var line = lineGo.AddComponent<LineRenderer>();
        line.sharedMaterial = aimLineMat;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.textureMode = LineTextureMode.Stretch;
        line.enabled = false;
        var shooter = root.AddComponent<TestEnemyShooter>();
        SetObject(shooter, "muzzle", muzzle);
        SetObject(shooter, "aimLine", line);
        SetObject(shooter, "model", model);
        return SavePrefab(root, folder);
    }

    // Enemy shot: hot orange core + star flare + short trail; trigger + kinematic body; hurts via TestContactDamage.
    static GameObject BuildEnemyShot(Material core, Material flareMat, Material additive)
    {
        var root = new GameObject("TEST_EnemyShot");
        var sphere = root.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.22f;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        root.AddComponent<TestEnemyBullet>();
        root.AddComponent<TestContactDamage>();
        GameObject glow = Quad("Core", core, 3);
        glow.transform.SetParent(root.transform, false);
        glow.transform.localScale = Vector3.one * 0.6f;
        GameObject star = Quad("Flare", flareMat, 3);
        star.transform.SetParent(root.transform, false);
        star.transform.localRotation = Quaternion.Euler(90f, 0f, 45f);
        star.transform.localScale = Vector3.one * 1.2f;
        var trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = additive;
        trail.time = 0.18f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = AnimationCurve.Linear(0f, 0.3f, 1f, 0f);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.75f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.alignment = LineAlignment.View;
        return SavePrefab(root);
    }

    // Homing rocket: dark metal body with a red nose, a blinking red beacon, an orange engine flame and a grey smoke
    // trail, so it reads differently from the round orange shots. Trigger + kinematic body; can be shot down (10 HP).
    static GameObject BuildHomingRocket(Mesh bodyMesh, Material additive, Material beaconMat, TestSoundBank sounds)
    {
        var root = new GameObject("TEST_HomingRocket");
        var sphere = root.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.3f;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        Material body = Lit("TEST_Rocket_Body", new Color(0.32f, 0.33f, 0.36f), 0.3f, 0.55f, new Color(0.02f, 0.02f, 0.02f));
        Material nose = Lit("TEST_Rocket_Nose", new Color(0.9f, 0.15f, 0.12f), 0.1f, 0.5f, new Color(0.6f, 0.05f, 0.03f));
        Transform model = MakeModel(root.transform, bodyMesh, new[] { body });
        model.localScale = new Vector3(2.2f, 2.2f, 1.6f);
        Transform tip = MakeModel(root.transform, bodyMesh, new[] { nose });
        tip.name = "Nose";
        tip.localPosition = new Vector3(0f, 0.02f, 0.32f);
        tip.localScale = new Vector3(1.6f, 1.6f, 0.45f);
        GameObject beacon = Quad("Beacon", beaconMat, 4);
        beacon.transform.SetParent(root.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        beacon.transform.localScale = Vector3.one * 0.75f;
        BuildExhaust(root.transform, new Vector3(0f, 0f, -0.45f), additive, new Color(1f, 0.55f, 0.15f), 0.3f);
        var trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = additive;
        trail.time = 0.6f;
        trail.minVertexDistance = 0.08f;
        trail.widthCurve = AnimationCurve.Linear(0f, 0.28f, 1f, 0.05f);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.75f, 0.72f, 0.7f), 0f), new GradientColorKey(new Color(0.4f, 0.4f, 0.42f), 1f) },
            new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.alignment = LineAlignment.View;

        var audioGo = new GameObject("EngineLoop");
        audioGo.transform.SetParent(root.transform, false);
        var loop = audioGo.AddComponent<AudioSource>();
        loop.playOnAwake = false;
        loop.loop = true;
        loop.spatialBlend = 0f;
        loop.dopplerLevel = 0f;
        loop.clip = sounds != null ? sounds.rocketLoop : null;

        var rocket = root.AddComponent<TestHomingRocket>();
        SetObject(rocket, "beacon", beacon);
        SetObject(rocket, "loop", loop);
        root.AddComponent<TestContactDamage>();
        var hp = root.AddComponent<TestEnemyHealth>();
        var hpSo = new SerializedObject(hp);
        hpSo.FindProperty("maxHealth").intValue = 10;
        hpSo.FindProperty("visualRoot").objectReferenceValue = model;
        hpSo.FindProperty("explosionScale").floatValue = 0.55f;
        hpSo.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(root);
    }

    static GameObject BuildPlayerShip(Mesh mesh, Material[] mats, GameObject projectile, Material additive, Material muzzleMat)
    {
        var root = new GameObject("TEST_PlayerShip");
        // Trigger + kinematic body so touching an enemy collider reports to TestPlayerHealth.
        // Slightly smaller than the wingspan so grazes feel fair.
        var hitBox = root.AddComponent<BoxCollider>();
        hitBox.isTrigger = true;
        hitBox.size = new Vector3(1.2f, 0.6f, 1.6f);
        hitBox.center = new Vector3(0f, 0f, 0.15f);
        var hitBody = root.AddComponent<Rigidbody>();
        hitBody.isKinematic = true;
        hitBody.useGravity = false;

        Transform model = MakeModel(root.transform, mesh, mats);
        foreach (float x in new[] { -0.24f, 0.24f })
        {
            BuildExhaust(model, new Vector3(x, 0f, -1.02f), additive, new Color(0.35f, 0.85f, 1f), 0.22f);
        }
        var firePoint = new GameObject("FirePoint").transform;
        firePoint.SetParent(root.transform);
        firePoint.localPosition = new Vector3(0f, 0f, 1.55f);
        GameObject flash = Quad("MuzzleFlash", muzzleMat, 2);
        flash.transform.SetParent(firePoint, false);
        flash.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        flash.transform.localScale = new Vector3(0.9f, 1.3f, 1f);
        flash.SetActive(false);

        var health = root.AddComponent<TestPlayerHealth>();
        SetObject(health, "visualRoot", model);
        var shipScript = root.AddComponent<TestPlayerShip>();
        var so = new SerializedObject(shipScript);
        so.FindProperty("projectilePrefab").objectReferenceValue = projectile;
        so.FindProperty("firePoint").objectReferenceValue = firePoint;
        so.FindProperty("muzzleFlash").objectReferenceValue = flash;
        so.ApplyModifiedPropertiesWithoutUndo();
        var bank = root.AddComponent<TestShipBanking>();
        SetObject(bank, "model", model);
        SetFloat(bank, "maxBankDegrees", 30f);
        SetFloat(bank, "fullBankSpeed", 7f);
        SetFloat(bank, "responsiveness", 10f);
        return SavePrefab(root);
    }

    static GameObject BuildProjectile(Mesh boltMesh, Material boltMat, Material additive)
    {
        var root = new GameObject("TEST_Projectile");
        var sphere = root.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.18f;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        root.AddComponent<TestProjectile>();
        Transform model = MakeModel(root.transform, boltMesh, new[] { boltMat });
        model.localScale = new Vector3(1.2f, 1.2f, 1.3f);
        var trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = additive;
        trail.time = 0.1f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = AnimationCurve.Linear(0f, 0.22f, 1f, 0f);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 1f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.95f, 0.9f), 0.35f), new GradientColorKey(new Color(0.2f, 0.7f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.55f, 0.35f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.alignment = LineAlignment.View;
        return SavePrefab(root);
    }

    // Short spray of stretched sparks plus a star flash; used for bolt impacts and, warmer and larger, for player hits.
    static GameObject BuildSparks(string name, Material additive, Material flare, Color hot, Color cool, int count, float flashSize)
    {
        var root = new GameObject(name);
        ParticleSystem sparks = NewParticles(root, additive);
        var main = sparks.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = hot;
        main.maxParticles = count + 4;
        main.stopAction = ParticleSystemStopAction.Destroy;
        Burst(sparks, count);
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;
        ColourOverLife(sparks, hot, cool, 1f);
        ShrinkOverLife(sparks, 1f, 0f);
        var renderer = sparks.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.035f;
        renderer.lengthScale = 2f;

        ParticleSystem flash = NewParticles(Child(root, "Flash"), flare);
        var flashMain = flash.main;
        flashMain.duration = 0.1f;
        flashMain.loop = false;
        flashMain.startLifetime = 0.07f;
        flashMain.startSpeed = 0f;
        flashMain.startSize = flashSize;
        flashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
        flashMain.startColor = Color.Lerp(hot, Color.white, 0.4f);
        Burst(flash, 1);
        ColourOverLife(flash, hot, cool, 0.95f);
        ShrinkOverLife(flash, 1f, 0.3f);
        return SavePrefab(root);
    }

    // Brief and compact so it never hides incoming attacks: a 0.1 s star flash, an expanding fireball and shock ring,
    // sparks, and a few tumbling hull fragments that shrink away within 0.8 s.
    static GameObject BuildExplosion(Material additive, Material flare, Material ring, Material debrisMat)
    {
        var root = new GameObject("TEST_FX_Explosion");
        ParticleSystem debris = NewParticles(root, additive);
        var main = debris.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(1f, 0.85f, 0.5f);
        main.maxParticles = 20;
        main.stopAction = ParticleSystemStopAction.Destroy;
        Burst(debris, 14);
        var shape = debris.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        ColourOverLife(debris, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.35f, 0.1f), 1f);
        ShrinkOverLife(debris, 1f, 0.2f);
        var debrisRenderer = debris.GetComponent<ParticleSystemRenderer>();
        debrisRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        debrisRenderer.velocityScale = 0.04f;
        debrisRenderer.lengthScale = 1.5f;

        ParticleSystem fireball = NewParticles(Child(root, "Fireball"), additive);
        var fireMain = fireball.main;
        fireMain.duration = 0.3f;
        fireMain.loop = false;
        fireMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.42f);
        fireMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
        fireMain.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        fireMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fireMain.startColor = Color.white;
        fireMain.maxParticles = 10;
        Burst(fireball, 8);
        var fireShape = fireball.shape;
        fireShape.shapeType = ParticleSystemShapeType.Sphere;
        fireShape.radius = 0.2f;
        var fireCol = fireball.colorOverLifetime;
        fireCol.enabled = true;
        var fg = new Gradient();
        fg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f), new GradientColorKey(new Color(0.7f, 0.15f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        fireCol.color = fg;
        ShrinkOverLife(fireball, 0.6f, 1.3f);

        ParticleSystem flash = NewParticles(Child(root, "Flash"), flare);
        var flashMain = flash.main;
        flashMain.duration = 0.15f;
        flashMain.loop = false;
        flashMain.startLifetime = 0.1f;
        flashMain.startSpeed = 0f;
        flashMain.startSize = 2.6f;
        flashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
        flashMain.startColor = new Color(1f, 0.9f, 0.7f, 0.9f);
        Burst(flash, 1);
        ColourOverLife(flash, new Color(1f, 0.9f, 0.7f), new Color(1f, 0.6f, 0.3f), 0.9f);
        ShrinkOverLife(flash, 0.6f, 1.2f);

        ParticleSystem shock = NewParticles(Child(root, "ShockRing"), ring);
        var shockMain = shock.main;
        shockMain.duration = 0.3f;
        shockMain.loop = false;
        shockMain.startLifetime = 0.3f;
        shockMain.startSpeed = 0f;
        shockMain.startSize = 3.6f;
        shockMain.startColor = new Color(1f, 0.75f, 0.45f, 0.55f);
        Burst(shock, 1);
        ColourOverLife(shock, new Color(1f, 0.8f, 0.5f), new Color(1f, 0.5f, 0.3f), 0.55f);
        ShrinkOverLife(shock, 0.15f, 1f);

        ParticleSystem chunks = NewParticles(Child(root, "Debris"), debrisMat);
        var chunkMain = chunks.main;
        chunkMain.duration = 0.2f;
        chunkMain.loop = false;
        chunkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        chunkMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        chunkMain.startSize3D = false;
        chunkMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        chunkMain.startRotation3D = true;
        chunkMain.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        chunkMain.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        chunkMain.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        chunkMain.maxParticles = 10;
        Burst(chunks, 7);
        var chunkShape = chunks.shape;
        chunkShape.shapeType = ParticleSystemShapeType.Sphere;
        chunkShape.radius = 0.2f;
        var spin = chunks.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
        spin.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
        spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        var drag = chunks.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 2.5f;
        ShrinkOverLife(chunks, 1f, 0f); // fades by shrinking (opaque lit fragments)
        var chunkRenderer = chunks.GetComponent<ParticleSystemRenderer>();
        chunkRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        chunkRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        return SavePrefab(root);
    }

    static void BuildExhaust(Transform parent, Vector3 localPosition, Material additive, Color colour, float size)
    {
        var go = new GameObject("Exhaust");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // emit backwards
        ParticleSystem ps = NewParticles(go, additive);
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.75f, size * 1.25f); // slight flicker
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 16;
        var emission = ps.emission;
        emission.rateOverTime = 45f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.03f;
        // Hot white core at the nozzle cooling into the engine colour.
        ColourOverLife(ps, Color.Lerp(colour, Color.white, 0.65f), colour, 0.95f);
        ShrinkOverLife(ps, 1f, 0f);
    }

    static void BuildStarLayer(Transform parent, string name, Material additive, float height, float startZ, float width, float speed,
        Vector2 size, float rate, int maxParticles, Color colourA, Color colourB, bool streaks, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, height, startZ);
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // emit toward the bottom of the screen
        ParticleSystem ps = NewParticles(go, additive);
        float lifetime = 2f * startZ / speed;
        var main = ps.main;
        main.duration = lifetime; // so prewarm fills the whole screen at start
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
        main.maxParticles = maxParticles;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.5f, 0.5f);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = sortingOrder;
        if (streaks)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;
        }
    }

    // ---- Helpers ----

    static Transform MakeModel(Transform parent, Mesh mesh, Material[] mats)
    {
        var model = new GameObject("Model");
        model.transform.SetParent(parent, false);
        model.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = model.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = mats;
        return model.transform;
    }

    /// <summary>Flat quad facing the overhead camera (+Y), no collider, no shadows. Texture up = screen up (+Z).</summary>
    static GameObject Quad(string name, Material mat, int sortingOrder)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = sortingOrder;
        return quad;
    }

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static ParticleSystem NewParticles(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = 0f;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void ColourOverLife(ParticleSystem ps, Color from, Color to, float startAlpha)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    static void ShrinkOverLife(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
    }

    static VolumeProfile PostFxProfile()
    {
        string path = MaterialFolder + "/TEST_EncounterPostFX.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        if (!profile.TryGet(out Bloom bloom))
        {
            bloom = profile.Add<Bloom>(true);
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }
        bloom.threshold.Override(1.1f); // only HDR emissive glows bloom, not lit hulls (the HUD is outside post-processing)
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.55f);
        if (!profile.TryGet(out Vignette vignette))
        {
            vignette = profile.Add<Vignette>(true);
            AssetDatabase.AddObjectToAsset(vignette, profile);
        }
        vignette.intensity.Override(0.12f); // light: corners were too dark
        vignette.smoothness.Override(0.5f);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static Texture2D SoftDotTexture()
    {
        string path = MaterialFolder + "/TEST_SoftDot.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
        {
            return existing;
        }
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "TEST_SoftDot", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply(true);
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    /// <summary>Writes a generated texture as PNG (only when the pixels changed) and sets its import settings.</summary>
    static Texture2D SaveTexture(string name, int size, Color32[] pixels, bool repeatU)
    {
        string path = TextureFolder + "/" + name + ".png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply(false);
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        if (!File.Exists(fullPath) || !SameBytes(File.ReadAllBytes(fullPath), png))
        {
            File.WriteAllBytes(fullPath, png);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = Mathf.Max(64, size);
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static bool SameBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    static Material Lit(string name, Color baseColour, float metallic, float smoothness, Color emission)
    {
        Material mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", baseColour);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetColor("_EmissionColor", emission);
        mat.EnableKeyword("_EMISSION");
        // "None" makes URP strip _EMISSION on import; Baked keeps it (there is no baked lighting here).
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Opaque lit material for mesh-particle hull fragments: dark metal with hot orange edges.</summary>
    static Material Debris(string name)
    {
        Material mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Particles/Lit");
        mat.SetColor("_BaseColor", new Color(0.35f, 0.3f, 0.28f));
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_Metallic", 0.1f);
        mat.SetFloat("_Smoothness", 0.4f);
        mat.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.08f) * 1.2f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Transparent unlit material (URP Unlit or Particles/Unlit): additive for glows, alpha-blended for bodies.</summary>
    static Material Transparent(string name, string shaderName, Texture texture, Color colour, bool additive)
    {
        Material mat = LoadOrCreateMaterial(name, shaderName);
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", colour);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LoadOrCreateMaterial(string name, string shaderName)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError("[WaveTestSceneBuilder] Shader not found: " + shaderName);
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null && mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    static Mesh SaveMesh(Mesh mesh)
    {
        string path = ModelFolder + "/" + mesh.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        // Keep the asset (and its GUID) so prefab references stay stable across rebuilds.
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    static GameObject SavePrefab(GameObject root, string folder = PrefabFolder)
    {
        string path = folder + "/" + root.name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void SetFloat(Object target, string property, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetVector(Object target, string property, Vector3 value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).vector3Value = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObject(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
