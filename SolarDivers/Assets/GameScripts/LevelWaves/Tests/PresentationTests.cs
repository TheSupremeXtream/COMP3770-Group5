#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Checks for the prototype's presentation layer: asset integrity, effect cleanup, HUD states.</summary>
public class PresentationTests
{
    private const string ScenePath = "Assets/Scenes/LevelWaves_Encounter01_Test.unity";
    // Reference fields that are optional and intentionally empty in the test scene.
    private static readonly string[] OptionalReferences = { "WaveEncounter.boundsSpace" };

    private static readonly string[] Prefabs =
    {
        "Assets/Prefabs/LevelWaves/TEST_PlayerShip.prefab", "Assets/Prefabs/LevelWaves/TEST_Enemy_Basic.prefab",
        "Assets/Prefabs/LevelWaves/TEST_Enemy_Weaver.prefab", "Assets/Prefabs/LevelWaves/TEST_Projectile.prefab",
        "Assets/Prefabs/LevelWaves/TEST_FX_HitSpark.prefab", "Assets/Prefabs/LevelWaves/TEST_FX_Explosion.prefab",
        "Assets/Prefabs/LevelWaves/TEST_FX_PlayerHit.prefab", "Assets/Prefabs/LevelWaves/TEST_EnemyShot.prefab",
        "Assets/Prefabs/LevelWaves/TEST_Enemy_Interceptor.prefab"
    };

    private Scene scene;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
    }

    private static void CheckObject(GameObject go, string context, List<string> problems)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            string where = context + " > " + t.name;
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
            {
                problems.Add(where + ": missing script");
            }
            foreach (Renderer r in t.GetComponents<Renderer>())
            {
                // Particle renderers also expose an optional trail-material slot; only the main one is required.
                Material[] mats = r is ParticleSystemRenderer ? new[] { r.sharedMaterial } : r.sharedMaterials;
                if (mats.Length == 0)
                {
                    problems.Add(where + ": renderer has no materials");
                }
                foreach (Material m in mats)
                {
                    if (m == null)
                    {
                        problems.Add(where + ": missing material");
                    }
                    else if (m.shader == null || !m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader")
                    {
                        problems.Add(where + ": unsupported/pink shader on " + m.name);
                    }
                }
                var filter = t.GetComponent<MeshFilter>();
                if (r is MeshRenderer && (filter == null || filter.sharedMesh == null))
                {
                    problems.Add(where + ": missing mesh");
                }
                if (r is MeshRenderer && filter != null && filter.sharedMesh != null && filter.sharedMesh.subMeshCount > mats.Length)
                {
                    problems.Add(where + ": fewer materials than sub-meshes");
                }
            }
            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null || !mb.GetType().Assembly.GetName().Name.StartsWith("SolarDivers"))
                {
                    continue;
                }
                var so = new SerializedObject(mb);
                SerializedProperty p = so.GetIterator();
                while (p.NextVisible(true))
                {
                    // Every reference in our prototype components is expected to be set; null = unassigned or missing.
                    // Exceptions: fields documented as optional and empty on purpose in the test scene
                    // (WaveEncounter.boundsSpace: empty = world-space bounds).
                    string field = mb.GetType().Name + "." + p.propertyPath;
                    bool optionalEmpty = System.Array.IndexOf(OptionalReferences, field) >= 0;
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null && !optionalEmpty)
                    {
                        problems.Add(where + ": unassigned/missing reference " + mb.GetType().Name + "." + p.propertyPath);
                    }
                }
            }
        }
    }

    [Test]
    public void Prefabs_HaveMaterials_SupportedShaders_AndNoBrokenReferences()
    {
        var problems = new List<string>();
        foreach (string path in Prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                problems.Add(path + ": prefab missing");
                continue;
            }
            CheckObject(prefab, path, problems);
        }
        CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void Prefabs_KeepGameplayCollidersOffTheVisualModel()
    {
        foreach (string path in new[] { Prefabs[0], Prefabs[1], Prefabs[2] })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab.GetComponent<Collider>(), path + ": hitbox should be on the root");
            Transform model = prefab.transform.Find("Model");
            Assert.IsNotNull(model, path + ": no Model child");
            Assert.AreEqual(0, model.GetComponentsInChildren<Collider>(true).Length, path + ": visual model has colliders");
        }
    }

    private IEnumerator LoadScene()
    {
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = LevelWavesTestScenes.Newest(ScenePath);
        Assert.IsTrue(scene.isLoaded);
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

    [UnityTest]
    public IEnumerator Scene_HasNoMissingMaterialsOrReferences()
    {
        yield return LoadScene();
        var problems = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CheckObject(root, "scene", problems);
        }
        CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator Effects_CleanThemselvesUp_AndClearAllRemovesThem()
    {
        yield return LoadScene();
        TestFxManager fx = Find<TestFxManager>();
        Assert.AreSame(fx, TestFxManager.Active);
        fx.PlayImpact(new Vector3(0f, 0f, 3f));
        fx.PlayExplosion(new Vector3(2f, 0f, 3f), 1f);
        fx.PlayPlayerHit(new Vector3(-2f, 0f, 3f));
        Assert.AreEqual(3, fx.ActiveEffectCount);

        float end = Time.realtimeSinceStartup + 3f;
        while (fx.ActiveEffectCount > 0 && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
        Assert.AreEqual(0, fx.ActiveEffectCount, "Effects should destroy themselves after playing");

        for (int i = 0; i < 10; i++)
        {
            fx.PlayExplosion(Vector3.zero, 1f);
        }
        fx.ClearAll();
        yield return null;
        Assert.AreEqual(0, fx.ActiveEffectCount, "ClearAll should remove every effect at once");
        yield return SceneManager.UnloadSceneAsync(scene);
        Assert.IsNull(TestFxManager.Active, "FX manager must not outlive its scene");
    }

    [UnityTest]
    public IEnumerator Shooting_ProducesHitEffects_AndNothingAccumulatesAcrossRestarts()
    {
        yield return LoadScene();
        TestFxManager fx = Find<TestFxManager>();
        TestPlayerShip ship = Find<TestPlayerShip>();
        EncounterTestHarness harness = Find<EncounterTestHarness>();
        WaveEncounter encounter = Find<WaveEncounter>();
        Find<TestPlayerHealth>().enabled = false;

        int maxProjectiles = 0;
        int maxEffects = 0;
        for (int run = 0; run < 2; run++)
        {
            float end = Time.realtimeSinceStartup + 8f;
            int destroyedBefore = encounter.DestroyedCount;
            while (Time.realtimeSinceStartup < end)
            {
                // Auto-pilot: line up under the first live enemy and fire at the normal rate.
                TestEnemyHealth target = Find<TestEnemyHealth>();
                if (target != null)
                {
                    Vector3 p = ship.transform.position;
                    p.x = Mathf.MoveTowards(p.x, target.transform.position.x, 8f * Time.deltaTime);
                    ship.transform.position = p;
                }
                ship.Fire();
                maxProjectiles = Mathf.Max(maxProjectiles, ship.ActiveProjectileCount);
                maxEffects = Mathf.Max(maxEffects, fx.ActiveEffectCount);
                yield return new WaitForSeconds(0.2f);
            }
            Assert.Greater(encounter.DestroyedCount, destroyedBefore, $"Run {run + 1}: shooting should destroy enemies");
            Assert.Greater(maxEffects, 0, "Hits should spawn effects");
            harness.RestartAll();
            yield return null;
            Assert.AreEqual(0, fx.ActiveEffectCount, $"Run {run + 1}: effects left after restart");
            Assert.AreEqual(0, ship.ActiveProjectileCount, $"Run {run + 1}: projectiles left after restart");
            Assert.AreEqual(0, encounter.AliveCount, $"Run {run + 1}: enemies left after restart");
        }
        // 2 s bolt lifetime at one shot per 0.2 s => at most ~11 in flight; effects stay small too.
        Assert.LessOrEqual(maxProjectiles, 12, "Projectiles are accumulating");
        Assert.LessOrEqual(maxEffects, 12, "Effects are accumulating");
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator Hud_DebugCountersHiddenByDefault_AndToggle()
    {
        yield return LoadScene();
        yield return null;
        EncounterDebugHUD hud = Find<EncounterDebugHUD>();
        Assert.IsFalse(hud.DebugVisible);
        Assert.IsFalse(hud.ResultVisible);
        hud.SetDebugVisible(true);
        Assert.IsTrue(hud.DebugVisible);
        hud.SetDebugVisible(false);
        Assert.IsFalse(hud.DebugVisible);
        yield return SceneManager.UnloadSceneAsync(scene);
    }
}
#endif
