using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Integrated demo: the Encounter 01 waves with the team's published work plugged in.
/// - Enemies: Leonardo's Enemy_model prefab (Assets/Prefabs/Enemy_model.prefab) is nested as the visual of three
///   wave enemy prefabs (heavy / fighter / interceptor). His prefab is model-only for now (no collider, health or
///   attacks) and his EnemyMovement script loops scene objects around a polygon forever, so wave lifecycle
///   (movement in and out, health, contact damage) still comes from the LevelWaves test components.
/// - Player: Abel's PlayerController + CharacterController + PlayerCollision move the ship (branch
///   movement-rail-system). Weapons (Taha) and player health / UI (Ronit) do not exist yet, so the test weapon,
///   test health and test HUD stay, and are labelled as such.
/// - Rail: everything that should follow the camera sits under one "RailRig" object (camera, player, boundaries,
///   spawn origin, spawned enemies, attack director, warnings, backdrop) and the encounter bounds are relative to it.
///   Abel's LevelCamera does not move yet, so the rig is static; TestRailScroll (speed 0) can move it for testing.
/// Teammates' scripts are attached by type name because they live in Assembly-CSharp, which this editor assembly
/// cannot reference. Nothing in their scripts or prefabs is modified.
/// </summary>
public static partial class WaveTestSceneBuilder
{
    public const string DemoScenePath = "Assets/Scenes/LevelWaves_Demo_Integrated.unity";
    const string DemoPrefabFolder = PrefabFolder + "/Demo";
    const string LeonardoEnemyPrefab = "Assets/Prefabs/Enemy_model.prefab";
    const string MissionTitle = "Mars Orbit Breakout";
    const string Objective = "Survive the Marquis's patrols and keep the route to Earth open";
    const string ClearedTitle = "ROUTE CLEAR";
    const string ClearedMessage = "<color=#B8C6D8>Next: the frigate (boss encounter not built yet)</color>";
    // Abel's PlayerController moves by Speed per frame; at the demo's fixed 60 fps this is ~9 units/s,
    // close to the test ship's 9.6.
    const float AbelSpeedPerFrame = 0.15f;
    // Leonardo's model faces a different axis than the test ships; this yaw turns its nose down the screen.
    // Confirmed from a runtime screenshot of the demo scene.
    const float LeonardoModelYaw = 0f;

    [MenuItem("Solar Divers/Level Waves/Rebuild Integrated Demo Scene")]
    static void BuildDemoFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Rebuild integrated demo",
                "This rebuilds " + ScenePath + ", " + DemoScenePath + " and the TEST_/LW_ prefabs. Inspector changes made to them will be lost. Continue?",
                "Rebuild", "Cancel"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        BuildDemo();
    }

    /// <summary>Batch-mode entry: Unity -batchmode -executeMethod WaveTestSceneBuilder.BuildDemoFromCommandLine -quit</summary>
    public static void BuildDemoFromCommandLine()
    {
        BuildDemo();
    }

    static void BuildDemo()
    {
        Build(); // test scene, shared prefabs, materials, sounds
        EnsureFolder(DemoPrefabFolder);
        var leonardo = AssetDatabase.LoadAssetAtPath<GameObject>(LeonardoEnemyPrefab);
        if (leonardo == null)
        {
            Debug.LogWarning("[WaveTestSceneBuilder] Leonardo's " + LeonardoEnemyPrefab + " not found; demo enemies use the test models.");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var encounter = Object.FindAnyObjectByType<WaveEncounter>();
        var harness = Object.FindAnyObjectByType<EncounterTestHarness>();
        var director = Object.FindAnyObjectByType<TestAttackDirector>();
        var hud = Object.FindAnyObjectByType<EncounterDebugHUD>();
        var flight = Object.FindAnyObjectByType<TestFlightAudio>();
        var fx = Object.FindAnyObjectByType<TestFxManager>();
        var warnings = Object.FindAnyObjectByType<TestEntryWarnings>();
        var shake = Object.FindAnyObjectByType<TestCameraShake>();
        var oldShip = Object.FindAnyObjectByType<TestPlayerShip>();

        // ---- Enemies with Leonardo's model ----
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/TEST_FX_Additive.mat");
        var aimLine = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/TEST_FX_AimLine.mat");
        var map = new Dictionary<GameObject, GameObject>();
        foreach (EnemyWave wave in encounter.Waves)
        {
            foreach (GameObject prefab in wave.enemyPrefabs)
            {
                if (prefab != null && !map.ContainsKey(prefab))
                {
                    map[prefab] = DemoEnemyFor(prefab, leonardo, material, aimLine);
                }
            }
        }
        string[] waveNames = { "Wave 1 - Patrol Screen", "Wave 2 - Interceptor Dives", "Wave 3 - Marquis Escort" };
        for (int w = 0; w < encounter.Waves.Count; w++)
        {
            EnemyWave wave = encounter.Waves[w];
            for (int i = 0; i < wave.enemyPrefabs.Length; i++)
            {
                if (wave.enemyPrefabs[i] != null)
                {
                    wave.enemyPrefabs[i] = map[wave.enemyPrefabs[i]];
                }
            }
            if (w < waveNames.Length)
            {
                wave.waveName = waveNames[w];
            }
        }
        EditorUtility.SetDirty(encounter);

        // ---- Rail rig: everything that follows the camera ----
        var rig = new GameObject("RailRig (follows the rail camera)").transform;
        var rail = rig.gameObject.AddComponent<TestRailScroll>();
        foreach (string objectName in new[] { "Main Camera", "PLACEHOLDER_UICamera (HUD only, no post-processing)", "PLACEHOLDER_Background (decor only)",
                     "SpawnedEnemies", "PLACEHOLDER_AttackDirector", "PLACEHOLDER_EntryWarnings" })
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null)
            {
                go.transform.SetParent(rig, true);
            }
            else
            {
                Debug.LogWarning("[WaveTestSceneBuilder] Demo: " + objectName + " not found.");
            }
        }
        Transform origin = encounter.transform.Find("SpawnOrigin");
        origin.SetParent(rig, true);
        SetObject(encounter, "boundsSpace", rig);

        // ---- Player: Abel's controller moves it; the test weapon and test health stay until Taha's / Ronit's exist ----
        GameObject player = BuildDemoPlayer(oldShip, rig, out TestPlayerShip weapon, out TestPlayerHealth health, out Behaviour movement);
        BuildBoundaries(rig);

        director.Configure(director.Profile, player.transform, health, encounter,
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/TEST_EnemyShot.prefab"));
        harness.Configure(encounter, health, weapon, fx, shake, warnings, director, flight);
        harness.SetDisableOnDefeat(movement);
        UnityEventTools.AddVoidPersistentListener(harness.onRestart, rail.ResetToStart);
        harness.gameObject.AddComponent<DemoFrameRateCap>();
        SetObject(hud, "playerHealth", health);
        SetString(hud, "missionTitle", MissionTitle);
        SetString(hud, "objective", Objective);
        SetString(hud, "clearedTitle", ClearedTitle);
        SetString(hud, "clearedMessage", ClearedMessage);
        SetObject(flight, "player", weapon);
        SetObject(flight, "playerHealth", health);
        EditorUtility.SetDirty(harness);
        EditorUtility.SetDirty(director);

        EditorSceneManager.SaveScene(scene, DemoScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[WaveTestSceneBuilder] Built " + DemoScenePath + (leonardo != null ? " (Leonardo's enemy model)" : "") +
                  (movement != null && movement.GetType().Name == "PlayerController" ? " (Abel's PlayerController)" : " (test ship movement)"));
    }

    // Same gameplay as the test enemy, with Leonardo's model as the visual and a trigger hitbox (so Abel's
    // CharacterController is not physically blocked by enemies; shots and contact damage still register).
    static GameObject DemoEnemyFor(GameObject testPrefab, GameObject leonardo, Material additive, Material aimLine)
    {
        var mover = testPrefab.GetComponent<TestEnemyMover>();
        var hitboxSource = testPrefab.GetComponent<BoxCollider>();
        var settings = new EnemyClassSettings { enemyClass = mover.Class };
        var so = new SerializedObject(mover);
        settings.pattern = mover.Pattern;
        settings.speed = so.FindProperty("speed").floatValue;
        settings.acceleration = so.FindProperty("acceleration").floatValue;
        settings.turnRate = so.FindProperty("turnRate").floatValue;
        settings.turnAcceleration = so.FindProperty("turnAcceleration").floatValue;
        settings.halfWidth = mover.HalfWidth;
        settings.modelScale = 1f;
        settings.health = new SerializedObject(testPrefab.GetComponent<TestEnemyHealth>()).FindProperty("maxHealth").intValue;
        string className = mover.Class.ToString();
        Color engine = mover.Class == TestEnemyClass.Heavy ? new Color(0.85f, 0.35f, 1f)
            : mover.Class == TestEnemyClass.Interceptor ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.5f, 0.2f);
        Vector3 hitbox = hitboxSource.size;
        float explosion = mover.Class == TestEnemyClass.Heavy ? 1.35f : mover.Class == TestEnemyClass.Interceptor ? 0.8f : 1f;
        if (leonardo == null)
        {
            return testPrefab;
        }
        return BuildEnemy("LW_Enemy_" + className, null, null, hitbox, settings, additive, engine,
            new[] { new Vector3(0f, 0f, -hitbox.z * 0.5f) }, explosion, aimLine, hitbox.z * 0.5f,
            visualPrefab: leonardo, triggerHitbox: true, folder: DemoPrefabFolder);
    }

    /// <summary>Nests a teammate's model prefab as the ship's visual, sized to 'width' and centred on the hitbox.</summary>
    static Transform NestedVisual(Transform parent, GameObject visualPrefab, float width)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        instance.name = "Model (" + visualPrefab.name + ")";
        Transform t = instance.transform;
        t.SetParent(parent, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.Euler(0f, LeonardoModelYaw, 0f) * visualPrefab.transform.localRotation;
        foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(c); // gameplay hitbox lives on the root
        }
        Bounds b = RendererBounds(instance);
        float size = Mathf.Max(b.size.x, b.size.z);
        if (size > 1e-4f)
        {
            t.localScale = visualPrefab.transform.localScale * (width / size);
        }
        b = RendererBounds(instance);
        Vector3 offset = parent.InverseTransformPoint(b.center);
        t.localPosition -= offset;
        return t;
    }

    static Bounds RendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(go.transform.position, Vector3.zero);
        }
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            b.Encapsulate(r.bounds);
        }
        return b;
    }

    // Replaces the test ship with: Abel's PlayerController + CharacterController + PlayerCollision on the root (movement),
    // the test weapon (TestPlayerShip with movement off), and the test health on a child hitbox (a kinematic trigger,
    // kept off the CharacterController so the two do not fight).
    static GameObject BuildDemoPlayer(TestPlayerShip oldShip, Transform rig, out TestPlayerShip weapon, out TestPlayerHealth health, out Behaviour movement)
    {
        GameObject player = oldShip.gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(player))
        {
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        player.name = "Player (Abel's PlayerController + test weapon)";
        player.tag = "Player";
        player.transform.SetParent(rig, true);

        var oldBox = player.GetComponent<BoxCollider>();
        var oldBody = player.GetComponent<Rigidbody>();
        var oldHealth = player.GetComponent<TestPlayerHealth>();
        var hitbox = new GameObject("Hitbox (test health)");
        hitbox.transform.SetParent(player.transform, false);
        var box = hitbox.AddComponent<BoxCollider>();
        EditorUtility.CopySerialized(oldBox, box);
        var body = hitbox.AddComponent<Rigidbody>();
        EditorUtility.CopySerialized(oldBody, body);
        health = hitbox.AddComponent<TestPlayerHealth>();
        EditorUtility.CopySerialized(oldHealth, health);
        Object.DestroyImmediate(oldHealth);
        Object.DestroyImmediate(oldBody);
        Object.DestroyImmediate(oldBox);

        weapon = player.GetComponent<TestPlayerShip>();
        SetBool(weapon, "handleMovement", false);
        movement = null;
        Type controllerType = Type.GetType("PlayerController, Assembly-CSharp");
        Type collisionType = Type.GetType("PlayerCollision, Assembly-CSharp");
        if (controllerType != null)
        {
            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.6f;
            controller.height = 1.2f;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.05f;
            controller.stepOffset = 0f;
            movement = (Behaviour)player.AddComponent(controllerType);
            SetFloat(movement, "speed", AbelSpeedPerFrame);
            if (collisionType != null)
            {
                player.AddComponent(collisionType);
            }
        }
        else
        {
            Debug.LogWarning("[WaveTestSceneBuilder] Abel's PlayerController not found (branch movement-rail-system not merged); the demo uses the test ship movement.");
            SetBool(weapon, "handleMovement", true);
        }
        return player;
    }

    // Walls for the CharacterController (Abel's boundary idea from railSystemTest), around the same area the test
    // ship was limited to (x +/-9, z -8..0). Tagged "Boundary" when that tag exists.
    static void BuildBoundaries(Transform rig)
    {
        var root = new GameObject("PlayerBoundaries").transform;
        root.SetParent(rig, false);
        bool hasTag = Array.IndexOf(InternalEditorUtility.tags, "Boundary") >= 0;
        void Wall(string wallName, Vector3 position, Vector3 size)
        {
            var go = new GameObject(wallName);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.AddComponent<BoxCollider>().size = size;
            if (hasTag)
            {
                go.tag = "Boundary";
            }
        }
        Wall("Left", new Vector3(-9.8f, 0f, -4f), new Vector3(0.4f, 3f, 12f));
        Wall("Right", new Vector3(9.8f, 0f, -4f), new Vector3(0.4f, 3f, 12f));
        Wall("Top", new Vector3(0f, 0f, 0.8f), new Vector3(20f, 3f, 0.4f));
        Wall("Bottom", new Vector3(0f, 0f, -8.8f), new Vector3(20f, 3f, 0.4f));
    }

    static void SetBool(Object target, string property, bool value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetString(Object target, string property, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- Local playable build (outside the repository) ----

    [MenuItem("Solar Divers/Level Waves/Build Integrated Demo (Windows)")]
    static void BuildPlayerFromMenu()
    {
        BuildWindowsDemo();
    }

    /// <summary>
    /// Batch-mode entry: builds only the integrated demo scene for Windows into SOLAR_BUILD_DIR, or by default
    /// into a "Builds" folder next to the repository (never inside it). Does not change Build Settings.
    /// </summary>
    public static void BuildWindowsDemo()
    {
        string dir = Environment.GetEnvironmentVariable("SOLAR_BUILD_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Builds", "SolarDivers_WaveDemo"));
        }
        var options = new BuildPlayerOptions
        {
            scenes = new[] { DemoScenePath },
            locationPathName = Path.Combine(dir, "SolarDivers_WaveDemo.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[WaveTestSceneBuilder] Build {report.summary.result}: {options.locationPathName}, {report.summary.totalSize / (1024 * 1024)} MB, " +
                  $"{report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, {report.summary.totalTime.TotalSeconds:F0} s");
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }
}
