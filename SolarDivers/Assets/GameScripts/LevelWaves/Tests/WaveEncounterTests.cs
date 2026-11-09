using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class WaveEncounterTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private GameObject enemyTemplate;
    private Transform spawnRoot;
    private Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 10f, 20f));

    [SetUp]
    public void SetUp()
    {
        // Plain template "enemy" with no AI. Kept outside the bounds so it is never mistaken for a spawn.
        enemyTemplate = Track(new GameObject("TestTemplateEnemy"));
        enemyTemplate.transform.position = new Vector3(500f, 0f, 500f);
        spawnRoot = Track(new GameObject("SpawnRoot")).transform;
    }

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

    private EnemyWave MakeWave(string name, WaveFormationType formation, int count, params GameObject[] prefabs)
    {
        return new EnemyWave
        {
            waveName = name, formation = formation, enemyPrefabs = prefabs,
            enemiesPerGroup = count, groupCount = 1, spacing = 1f, spawnInterval = 0f,
            delayBetweenGroups = 0f, delayBeforeWave = 0.05f, clearTimeout = 5f
        };
    }

    private WaveEncounter MakeEncounter(List<EnemyWave> waves)
    {
        var go = Track(new GameObject("Encounter"));
        var encounter = go.AddComponent<WaveEncounter>();
        var origin = new GameObject("Origin").transform;
        origin.SetParent(go.transform);
        encounter.Configure(waves, origin, bounds, false, spawnRoot);
        return encounter;
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
    }

    // Active enemies spawned by the encounter (all spawns go under spawnRoot).
    private List<GameObject> FindSpawned()
    {
        var result = new List<GameObject>();
        foreach (Transform child in spawnRoot)
        {
            if (child.gameObject.activeSelf)
            {
                result.Add(child.gameObject);
            }
        }
        return result;
    }

    [Test]
    public void Formations_ProduceDistinctPositions()
    {
        foreach (WaveFormationType formation in System.Enum.GetValues(typeof(WaveFormationType)))
        {
            var seen = new HashSet<Vector3>();
            for (int i = 0; i < 12; i++)
            {
                Assert.IsTrue(seen.Add(WaveFormation.GetOffset(formation, i, 12, 2f)), $"{formation} index {i} overlaps another enemy");
            }
        }
        Assert.AreEqual(Vector3.zero, WaveFormation.GetOffset(WaveFormationType.VFormation, 0, 7, 2f), "V leader should be at the tip");
        Assert.AreEqual(-WaveFormation.GetOffset(WaveFormationType.VFormation, 1, 7, 2f).x,
            WaveFormation.GetOffset(WaveFormationType.VFormation, 2, 7, 2f).x, 1e-5f, "V should be symmetric");
    }

    [UnityTest]
    public IEnumerator Waves_StartInOrder_AndCompleteOnce()
    {
        var encounter = MakeEncounter(new List<EnemyWave>
        {
            MakeWave("A", WaveFormationType.Line, 3, enemyTemplate),
            MakeWave("B", WaveFormationType.VFormation, 3, enemyTemplate),
            MakeWave("C", WaveFormationType.Mixed, 3, enemyTemplate)
        });
        var started = new List<int>();
        var cleared = new List<int>();
        int completions = 0;
        encounter.onWaveStarted.AddListener(started.Add);
        encounter.onWaveCleared.AddListener(cleared.Add);
        encounter.onEncounterCompleted.AddListener(() => completions++);

        encounter.StartEncounter();
        float end = Time.realtimeSinceStartup + 5f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            foreach (GameObject enemy in FindSpawned())
            {
                Object.Destroy(enemy); // "player kills everything"
            }
            yield return null;
        }

        Assert.IsTrue(encounter.IsCompleted, "Encounter did not complete");
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, started);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, cleared);
        Assert.AreEqual(9, encounter.SpawnedCount);
        Assert.AreEqual(9, encounter.DestroyedCount);

        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }
        Assert.AreEqual(1, completions, "Completion signal must fire exactly once");
    }

    [UnityTest]
    public IEnumerator DestroyedAndEscapedEnemies_AreCountedSeparately()
    {
        var encounter = MakeEncounter(new List<EnemyWave> { MakeWave("A", WaveFormationType.Line, 4, enemyTemplate) });
        encounter.StartEncounter();
        yield return WaitUntilOrTimeout(() => encounter.AliveCount == 4, 2f);
        Assert.AreEqual(4, encounter.AliveCount);

        List<GameObject> spawned = FindSpawned();
        Object.Destroy(spawned[0]);
        spawned[1].SetActive(false); // e.g. returned to a pool by another system
        Track(spawned[1]);
        spawned[2].transform.position = new Vector3(0f, 0f, -50f);
        spawned[3].transform.position = new Vector3(50f, 0f, 0f);

        yield return WaitUntilOrTimeout(() => encounter.IsCompleted, 2f);
        Assert.IsTrue(encounter.IsCompleted);
        Assert.AreEqual(2, encounter.DestroyedCount);
        Assert.AreEqual(2, encounter.EscapedCount);
        Assert.AreEqual(0, encounter.TimedOutCount);
        Assert.AreEqual(0, encounter.AliveCount);
    }

    [UnityTest]
    public IEnumerator MissingPrefab_LogsError_AndDoesNotHang()
    {
        var encounter = MakeEncounter(new List<EnemyWave>
        {
            MakeWave("Broken", WaveFormationType.Line, 3, new GameObject[] { null }),
            MakeWave("Fine", WaveFormationType.Line, 1, enemyTemplate)
        });
        LogAssert.Expect(LogType.Error, new Regex("Wave 1 'Broken': Enemy Prefabs element 0 is missing"));
        LogAssert.Expect(LogType.Error, new Regex("Wave 1 'Broken' has no valid enemy prefab"));

        encounter.StartEncounter();
        yield return WaitUntilOrTimeout(() => encounter.AliveCount == 1, 2f);
        foreach (GameObject enemy in FindSpawned())
        {
            Object.Destroy(enemy);
        }
        yield return WaitUntilOrTimeout(() => encounter.IsCompleted, 2f);

        Assert.IsTrue(encounter.IsCompleted, "Encounter hung on a missing prefab");
        Assert.AreEqual(2, encounter.ConfigErrorCount);
        Assert.AreEqual(1, encounter.SpawnedCount);
    }

    [UnityTest]
    public IEnumerator StalledEnemies_AreForceRemovedByClearTimeout()
    {
        EnemyWave wave = MakeWave("Stuck", WaveFormationType.Line, 2, enemyTemplate);
        wave.clearTimeout = 1f;
        var encounter = MakeEncounter(new List<EnemyWave> { wave });
        LogAssert.Expect(LogType.Warning, new Regex("clear timeout"));

        encounter.StartEncounter();
        yield return WaitUntilOrTimeout(() => encounter.IsCompleted, 4f);

        Assert.IsTrue(encounter.IsCompleted);
        Assert.AreEqual(2, encounter.TimedOutCount);
        yield return null;
        Assert.AreEqual(0, FindSpawned().Count, "Timed-out enemies should be destroyed");
    }

    [UnityTest]
    public IEnumerator Restart_ResetsProgress_AndRemovesSpawnedEnemies()
    {
        EnemyWave first = MakeWave("A", WaveFormationType.Line, 2, enemyTemplate);
        EnemyWave second = MakeWave("B", WaveFormationType.Line, 2, enemyTemplate);
        first.delayBeforeWave = 0.5f; // long enough that nothing respawns in the frame we check after restarting
        var encounter = MakeEncounter(new List<EnemyWave> { first, second });
        int completions = 0;
        encounter.onEncounterCompleted.AddListener(() => completions++);

        encounter.StartEncounter();
        yield return WaitUntilOrTimeout(() => encounter.AliveCount == 2, 2f);
        Object.Destroy(FindSpawned()[0]);
        yield return null;
        yield return null;
        Assert.AreEqual(1, encounter.DestroyedCount);

        encounter.RestartEncounter();
        Assert.AreEqual(0, encounter.DestroyedCount);
        Assert.AreEqual(0, encounter.SpawnedCount);
        Assert.AreEqual(0, encounter.CurrentWaveIndex, "Restart should go back to wave 1");
        Assert.AreEqual(WaveEncounter.EncounterState.WaitingForWave, encounter.State);
        Assert.IsFalse(encounter.IsCompleted);
        yield return null;
        Assert.AreEqual(0, FindSpawned().Count, "Restart should remove enemies from the previous run");

        float end = Time.realtimeSinceStartup + 5f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            foreach (GameObject enemy in FindSpawned())
            {
                Object.Destroy(enemy);
            }
            yield return null;
        }
        Assert.IsTrue(encounter.IsCompleted);
        Assert.AreEqual(4, encounter.SpawnedCount);
        Assert.AreEqual(1, completions);
    }

#if UNITY_EDITOR
    private const string ScenePath = "Assets/Scenes/LevelWaves_Encounter01_Test.unity";
    private UnityEngine.SceneManagement.Scene loadedScene;

    private IEnumerator LoadPrototypeScene()
    {
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
        loadedScene = LevelWavesTestScenes.Newest(ScenePath);
        Assert.IsTrue(loadedScene.isLoaded, "Prototype scene did not load");
    }

    private T FindInScene<T>() where T : Component
    {
        foreach (GameObject root in loadedScene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>();
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    // Smoke test of the real prototype scene with nobody playing. The placeholder health is switched off so
    // enemies flying through the ship's start position do not end the run; every enemy must escape or time out.
    [UnityTest]
    public IEnumerator PrototypeScene_RunsAllThreeWaves_WithoutPlayerInput()
    {
        yield return LoadPrototypeScene();
        WaveEncounter encounter = FindInScene<WaveEncounter>();
        Assert.IsNotNull(encounter, "No WaveEncounter in the prototype scene");
        Assert.AreEqual(3, encounter.WaveCount);
        FindInScene<TestPlayerHealth>().enabled = false;

        var started = new List<int>();
        int completions = 0;
        encounter.onWaveStarted.AddListener(started.Add);
        encounter.onEncounterCompleted.AddListener(() => completions++);

        Time.timeScale = 10f;
        float end = Time.realtimeSinceStartup + 60f;
        while (!encounter.IsCompleted && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
        Time.timeScale = 1f;

        Assert.IsTrue(encounter.IsCompleted, "Prototype encounter did not complete");
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, started);
        Assert.AreEqual(1, completions);
        Assert.AreEqual(45, encounter.SpawnedCount);
        Assert.AreEqual(45, encounter.EscapedCount + encounter.TimedOutCount + encounter.DestroyedCount);
        Assert.AreEqual(0, encounter.ConfigErrorCount);
        Assert.IsFalse(encounter.AllEnemiesDestroyed, "Nobody shot, so 'cleared' must not mean 'all destroyed'");
        Debug.Log($"[Smoke] No-kill encounter length: {encounter.ElapsedTime:F1}s game time, escaped {encounter.EscapedCount}, timed out {encounter.TimedOutCount}, destroyed {encounter.DestroyedCount}.");

        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(loadedScene);
    }

    // Real prefabs and physics: park the ship on top of an enemy until it is defeated, then press "R".
    [UnityTest]
    public IEnumerator PrototypeScene_EnemyContact_DefeatsPlayer_AndRestartResetsEverything()
    {
        yield return LoadPrototypeScene();
        WaveEncounter encounter = FindInScene<WaveEncounter>();
        TestPlayerHealth health = FindInScene<TestPlayerHealth>();
        TestPlayerShip ship = FindInScene<TestPlayerShip>();
        EncounterTestHarness harness = FindInScene<EncounterTestHarness>();
        Assert.IsNotNull(health);
        Assert.IsNotNull(ship);
        Assert.IsNotNull(harness);
        Assert.AreEqual(3, health.HitsRemaining);

        int completions = 0;
        int failures = 0;
        var hitTimes = new List<float>();
        encounter.onEncounterCompleted.AddListener(() => completions++);
        encounter.onEncounterFailed.AddListener(() => failures++);
        health.onDamaged.AddListener(() => hitTimes.Add(Time.time));
        health.onDefeated.AddListener(() => hitTimes.Add(Time.time));
        ship.Fire();
        ship.Fire();
        Assert.AreEqual(2, ship.ActiveProjectileCount);

        float end = Time.realtimeSinceStartup + 20f;
        while (!health.IsDefeated && Time.realtimeSinceStartup < end)
        {
            TestContactDamage target = FindInScene<TestContactDamage>();
            if (target != null)
            {
                ship.transform.position = target.transform.position; // ship stays on top of an enemy
            }
            yield return null;
        }

        Assert.IsTrue(health.IsDefeated, "Touching enemies never defeated the player");
        Assert.AreEqual(3, hitTimes.Count, "Expected exactly 3 counted hits");
        Assert.GreaterOrEqual(hitTimes[1] - hitTimes[0], 0.95f, "Second hit landed during invulnerability");
        Assert.GreaterOrEqual(hitTimes[2] - hitTimes[1], 0.95f, "Third hit landed during invulnerability");
        Assert.IsTrue(encounter.IsFailed);
        Assert.IsFalse(ship.enabled, "Ship controls should be disabled after defeat");
        int spawnedAtDefeat = encounter.SpawnedCount;

        // Let time pass at speed: no more spawns, no victory after defeat.
        Time.timeScale = 10f;
        float wait = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < wait)
        {
            yield return null;
        }
        Time.timeScale = 1f;
        Assert.AreEqual(spawnedAtDefeat, encounter.SpawnedCount, "Spawning continued after defeat");
        Assert.AreEqual(0, completions, "Victory fired after defeat");
        Assert.AreEqual(1, failures);
        TestFxManager fx = FindInScene<TestFxManager>();
        EncounterDebugHUD hud = FindInScene<EncounterDebugHUD>();
        Assert.IsNotNull(fx);
        Assert.IsTrue(hud.ResultVisible, "Defeat should show the result panel");

        ship.Fire(); // disabled component can still be called directly; make sure restart clears shots too
        harness.RestartAll();
        yield return null;
        Assert.AreEqual(3, health.HitsRemaining);
        Assert.IsFalse(health.IsDefeated);
        Assert.IsTrue(ship.enabled);
        Assert.AreEqual(0, ship.ActiveProjectileCount, "Restart should remove projectiles");
        Assert.AreEqual(0, encounter.AliveCount, "Restart should remove enemies");
        Assert.AreEqual(0, encounter.SpawnedCount);
        Assert.AreEqual(0, encounter.CurrentWaveIndex);
        Assert.IsFalse(encounter.IsFailed);
        Assert.AreEqual(new Vector3(0f, 0f, -5f), ship.transform.position);
        Assert.AreEqual(0, fx.ActiveEffectCount, "Restart should remove explosions and sparks");
        Assert.IsFalse(hud.ResultVisible, "Restart should hide the result panel");

        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(loadedScene);
    }
#endif
}
