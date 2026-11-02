using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerDamageAndPacingTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
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

    private TestPlayerHealth MakePlayer(Vector3 position, int hits, float invulnerability)
    {
        var go = Track(new GameObject("TestPlayer"));
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var health = go.AddComponent<TestPlayerHealth>();
        health.Configure(hits, invulnerability);
        return health;
    }

    private GameObject MakeHazard(Vector3 position)
    {
        var go = Track(new GameObject("TestHazard"));
        go.transform.position = position;
        go.AddComponent<BoxCollider>();
        go.AddComponent<TestContactDamage>();
        return go;
    }

    private static IEnumerator WaitSeconds(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator EnemyContact_DamagesPlayer_ThroughPhysics()
    {
        TestPlayerHealth health = MakePlayer(new Vector3(0f, 0f, 100f), 3, 1f);
        MakeHazard(new Vector3(0f, 0f, 100f));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.AreEqual(2, health.HitsRemaining, "Overlapping a TestContactDamage collider should cost one hit");
        Assert.IsTrue(health.IsInvulnerable);
    }

    [UnityTest]
    public IEnumerator NonHazardContact_DoesNotDamage()
    {
        TestPlayerHealth health = MakePlayer(new Vector3(0f, 0f, 200f), 3, 1f);
        var plain = Track(new GameObject("Scenery"));
        plain.transform.position = new Vector3(0f, 0f, 200f);
        plain.AddComponent<BoxCollider>();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(3, health.HitsRemaining);
    }

    [UnityTest]
    public IEnumerator Invulnerability_BlocksHits_ForConfiguredTime()
    {
        TestPlayerHealth health = MakePlayer(new Vector3(0f, 0f, 300f), 3, 1f);
        Assert.IsTrue(health.TryTakeHit(1));
        Assert.IsFalse(health.TryTakeHit(1), "Hit during invulnerability should be ignored");
        yield return WaitSeconds(0.5f);
        Assert.IsFalse(health.TryTakeHit(1), "Still invulnerable at 0.5 s");
        Assert.AreEqual(2, health.HitsRemaining);
        yield return WaitSeconds(0.6f);
        Assert.IsFalse(health.IsInvulnerable);
        Assert.IsTrue(health.TryTakeHit(1));
        Assert.AreEqual(1, health.HitsRemaining);
    }

    [UnityTest]
    public IEnumerator ThirdHit_Defeats_Once_AndFurtherHitsAreIgnored()
    {
        TestPlayerHealth health = MakePlayer(new Vector3(0f, 0f, 400f), 3, 0.05f);
        int defeats = 0;
        health.onDefeated.AddListener(() => defeats++);
        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(health.TryTakeHit(1), $"Hit {i + 1} should count");
            yield return WaitSeconds(0.1f);
        }
        Assert.IsTrue(health.IsDefeated);
        Assert.AreEqual(0, health.HitsRemaining);
        Assert.IsFalse(health.TryTakeHit(1));
        Assert.AreEqual(1, defeats);

        health.ResetHealth();
        Assert.IsFalse(health.IsDefeated);
        Assert.AreEqual(3, health.HitsRemaining);
    }

    [UnityTest]
    public IEnumerator Defeat_FailsEncounter_StopsSpawning_AndBlocksVictory()
    {
        var template = Track(new GameObject("Template"));
        template.transform.position = new Vector3(500f, 0f, 0f);
        var waves = new List<EnemyWave>
        {
            new EnemyWave { waveName = "A", enemyPrefabs = new[] { template }, enemiesPerGroup = 1, groupCount = 1, delayBeforeWave = 0f, clearTimeout = 5f },
            new EnemyWave { waveName = "B", enemyPrefabs = new[] { template }, enemiesPerGroup = 1, groupCount = 1, delayBeforeWave = 0.3f, clearTimeout = 5f }
        };
        var encounterGo = Track(new GameObject("Encounter"));
        var encounter = encounterGo.AddComponent<WaveEncounter>();
        var spawnRoot = Track(new GameObject("SpawnRoot")).transform;
        encounter.Configure(waves, encounterGo.transform, new Bounds(Vector3.zero, Vector3.one * 20f), false, spawnRoot);

        TestPlayerHealth health = MakePlayer(new Vector3(0f, 0f, 600f), 1, 0f);
        var ship = health.gameObject.AddComponent<TestPlayerShip>();
        var harness = Track(new GameObject("Harness")).AddComponent<EncounterTestHarness>();
        harness.Configure(encounter, health, ship);

        int completions = 0;
        encounter.onEncounterCompleted.AddListener(() => completions++);
        encounter.StartEncounter();
        yield return WaitSeconds(0.1f);
        Assert.AreEqual(1, encounter.SpawnedCount);

        health.TryTakeHit(1);
        Assert.IsTrue(encounter.IsFailed);
        Assert.IsFalse(ship.enabled);

        foreach (Transform child in spawnRoot)
        {
            Object.Destroy(child.gameObject); // the only enemy dies after the defeat
        }
        yield return WaitSeconds(0.6f);
        Assert.AreEqual(1, encounter.SpawnedCount, "Wave B must not spawn after defeat");
        Assert.AreEqual(0, completions, "Victory must not fire after defeat");
        Assert.IsTrue(encounter.IsFailed);

        harness.RestartAll();
        Assert.IsTrue(ship.enabled);
        Assert.IsFalse(health.IsDefeated);
        Assert.IsFalse(encounter.IsFailed);
        yield return WaitSeconds(0.1f);
        Assert.AreEqual(1, encounter.SpawnedCount, "Restarted encounter spawns wave A again");
    }

    [UnityTest]
    public IEnumerator FormationStyle_SpawnsLineLevel_StreamStyle_Staggers()
    {
        var template = Track(new GameObject("Mover"));
        template.transform.position = new Vector3(500f, 0f, 0f);
        template.AddComponent<TestEnemyMover>();

        foreach (WaveSpawnStyle style in new[] { WaveSpawnStyle.Formation, WaveSpawnStyle.Stream })
        {
            var wave = new EnemyWave
            {
                waveName = style.ToString(), formation = WaveFormationType.Line, enemyPrefabs = new[] { template },
                enemiesPerGroup = 4, groupCount = 1, spacing = 2f, spawnStyle = style, spawnInterval = 0.2f,
                delayBeforeWave = 0f, clearTimeout = 5f
            };
            var encounterGo = Track(new GameObject("Encounter"));
            var encounter = encounterGo.AddComponent<WaveEncounter>();
            var spawnRoot = Track(new GameObject("SpawnRoot")).transform;
            encounter.Configure(new List<EnemyWave> { wave }, encounterGo.transform, new Bounds(Vector3.zero, Vector3.one * 50f), false, spawnRoot);
            encounter.StartEncounter();
            yield return WaitSeconds(1f);

            var zs = new List<float>();
            foreach (Transform child in spawnRoot)
            {
                zs.Add(child.position.z);
            }
            Assert.AreEqual(4, zs.Count, style + ": expected 4 enemies");
            float spread = Mathf.Max(zs.ToArray()) - Mathf.Min(zs.ToArray());
            if (style == WaveSpawnStyle.Formation)
            {
                Assert.Less(spread, 0.01f, "Formation style line should stay level (no diagonal)");
            }
            else
            {
                Assert.Greater(spread, 1f, "Stream style should stagger enemies along their path");
            }
            encounter.StopEncounter();
        }
    }

    [Test]
    public void GroupEntryOffsets_CycleThroughEntryPositions()
    {
        var wave = new EnemyWave { groupCount = 4, groupEntryOffsets = new[] { -4f, 4f, 0f } };
        Assert.AreEqual(-4f, wave.GetGroupShift(0));
        Assert.AreEqual(4f, wave.GetGroupShift(1));
        Assert.AreEqual(0f, wave.GetGroupShift(2));
        Assert.AreEqual(-4f, wave.GetGroupShift(3));
        Assert.AreEqual(0f, new EnemyWave().GetGroupShift(1), "No offsets = centre");
    }
}
