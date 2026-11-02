using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runs a list of enemy waves in order. Spawning only: enemy movement/AI lives on the enemy prefabs.
/// An enemy stops counting toward its wave when it is destroyed/disabled, leaves the encounter bounds,
/// or is force-removed by the clear timeout. "Cleared" means every wave finished; it does not mean
/// every enemy was destroyed (see AllEnemiesDestroyed). FailEncounter() ends it without victory.
/// </summary>
public class WaveEncounter : MonoBehaviour
{
    public enum EncounterState
    {
        Idle,
        WaitingForWave,
        Spawning,
        Clearing,
        Completed,
        Failed
    }

    [System.Serializable] public class WaveEvent : UnityEvent<int> { }
    /// <summary>Group about to enter: world centre of its spawn positions, width across them, seconds until it spawns.</summary>
    [System.Serializable] public class GroupWarningEvent : UnityEvent<Vector3, float, float> { }

    [SerializeField] private List<EnemyWave> waves = new List<EnemyWave>();

    [Tooltip("Spawn point for formations. Its forward (blue arrow) is the direction enemies face when spawned.")]
    [SerializeField] private Transform spawnOrigin;

    [Tooltip("Optional parent for spawned enemies. Leave empty to spawn at the scene root.")]
    [SerializeField] private Transform spawnParent;

    [Tooltip("World-space play area. Enemies outside it are counted as escaped and removed.")]
    [SerializeField] private Bounds encounterBounds = new Bounds(new Vector3(0f, 0f, 1f), new Vector3(24f, 10f, 26f));

    [SerializeField] private bool autoStart = true;

    [Tooltip("onGroupIncoming fires this many seconds before each group spawns. The time is taken out of the existing delays, so pacing is unchanged.")]
    [Min(0f)] [SerializeField] private float entryWarningSeconds = 1.2f;

    public WaveEvent onWaveStarted = new WaveEvent();
    public WaveEvent onWaveCleared = new WaveEvent();
    public UnityEvent onEncounterCompleted = new UnityEvent();
    public UnityEvent onEncounterFailed = new UnityEvent();
    public GroupWarningEvent onGroupIncoming = new GroupWarningEvent();

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private Coroutine runRoutine;

    public EncounterState State { get; private set; }
    public int CurrentWaveIndex { get; private set; }
    public int WaveCount => waves.Count;
    public int AliveCount => activeEnemies.Count;
    public int SpawnedCount { get; private set; }
    public int DestroyedCount { get; private set; }
    public int EscapedCount { get; private set; }
    public int TimedOutCount { get; private set; }
    public int ConfigErrorCount { get; private set; }
    public bool IsCompleted => State == EncounterState.Completed;
    public bool IsFailed => State == EncounterState.Failed;
    public bool AllEnemiesDestroyed => IsCompleted && EscapedCount == 0 && TimedOutCount == 0 && SpawnedCount > 0;
    public float StateTimer { get; private set; }
    public float ElapsedTime { get; private set; }

    public EnemyWave CurrentWave =>
        (CurrentWaveIndex >= 0 && CurrentWaveIndex < waves.Count) ? waves[CurrentWaveIndex] : null;

    /// <summary>Used by the scene builder and tests to set up an encounter from code.</summary>
    public void Configure(List<EnemyWave> newWaves, Transform origin, Bounds bounds, bool startAutomatically, Transform parent = null)
    {
        waves = newWaves;
        spawnOrigin = origin;
        spawnParent = parent;
        encounterBounds = bounds;
        autoStart = startAutomatically;
    }

    void Start()
    {
        if (autoStart)
        {
            StartEncounter();
        }
    }

    void OnDisable()
    {
        // Coroutines die when disabled; drop back to Idle so a later StartEncounter starts clean.
        runRoutine = null;
        if (State != EncounterState.Completed && State != EncounterState.Failed)
        {
            State = EncounterState.Idle;
        }
    }

    public void StartEncounter()
    {
        StopEncounter();
        ResetProgress();
        ValidateConfiguration();
        runRoutine = StartCoroutine(RunEncounter());
    }

    /// <summary>Removes every enemy this encounter spawned and starts again from wave 1.</summary>
    [ContextMenu("Restart Encounter")]
    public void RestartEncounter()
    {
        Debug.Log("[WaveEncounter] Restarting encounter.", this);
        StartEncounter();
    }

    public void StopEncounter()
    {
        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
        }
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        activeEnemies.Clear();
        State = EncounterState.Idle;
    }

    /// <summary>
    /// Ends the encounter without victory (e.g. player defeated): no more spawns, and
    /// onEncounterCompleted can no longer fire. Enemies already on screen keep flying until they leave.
    /// </summary>
    public void FailEncounter(string reason)
    {
        if (State == EncounterState.Completed || State == EncounterState.Failed)
        {
            return;
        }
        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
        }
        State = EncounterState.Failed;
        StateTimer = 0f;
        Debug.Log($"[WaveEncounter] ENCOUNTER FAILED ({reason}) on wave {CurrentWaveIndex + 1}/{waves.Count} after {ElapsedTime:F1}s. Destroyed {DestroyedCount}, escaped {EscapedCount}.", this);
        onEncounterFailed.Invoke();
    }

    // Everything is reset explicitly because this project disables domain and scene reload
    // when entering Play mode, so field initialisers alone are not enough.
    private void ResetProgress()
    {
        activeEnemies.Clear();
        State = EncounterState.Idle;
        CurrentWaveIndex = -1;
        SpawnedCount = 0;
        DestroyedCount = 0;
        EscapedCount = 0;
        TimedOutCount = 0;
        ConfigErrorCount = 0;
        StateTimer = 0f;
        ElapsedTime = 0f;
    }

    private void ValidateConfiguration()
    {
        if (waves == null || waves.Count == 0)
        {
            ConfigErrorCount++;
            Debug.LogError("[WaveEncounter] No waves configured. The encounter will complete immediately.", this);
            waves = new List<EnemyWave>();
        }
        if (spawnOrigin == null)
        {
            Debug.LogWarning("[WaveEncounter] Spawn Origin is not assigned; using this object's transform.", this);
            spawnOrigin = transform;
        }
        if (!encounterBounds.Contains(spawnOrigin.position))
        {
            Debug.LogWarning("[WaveEncounter] Spawn Origin is outside the encounter bounds, so enemies will count as escaped immediately.", this);
        }
        for (int i = 0; i < waves.Count; i++)
        {
            EnemyWave wave = waves[i];
            if (wave == null)
            {
                continue;
            }
            for (int p = 0; p < wave.enemyPrefabs.Length; p++)
            {
                if (wave.enemyPrefabs[p] == null)
                {
                    ConfigErrorCount++;
                    Debug.LogError($"[WaveEncounter] Wave {i + 1} '{wave.waveName}': Enemy Prefabs element {p} is missing (None). That slot will be skipped.", this);
                }
            }
        }
    }

    void Update()
    {
        if (State == EncounterState.Idle || State == EncounterState.Completed)
        {
            return;
        }
        if (State == EncounterState.Failed)
        {
            // Keep cleaning up enemies that fly away after a defeat, but stop the clock.
            SweepEnemies();
            return;
        }
        StateTimer += Time.deltaTime;
        ElapsedTime += Time.deltaTime;
        SweepEnemies();
    }

    /// <summary>Drops enemies that were destroyed/disabled elsewhere and removes those outside the bounds.</summary>
    private void SweepEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = activeEnemies[i];
            if (enemy == null || !enemy.activeInHierarchy)
            {
                activeEnemies.RemoveAt(i);
                DestroyedCount++;
            }
            else if (!encounterBounds.Contains(enemy.transform.position))
            {
                activeEnemies.RemoveAt(i);
                EscapedCount++;
                Destroy(enemy);
            }
        }
    }

    private IEnumerator RunEncounter()
    {
        for (int w = 0; w < waves.Count; w++)
        {
            CurrentWaveIndex = w;
            EnemyWave wave = waves[w];
            if (wave == null || !HasAnyPrefab(wave) || wave.TotalEnemies <= 0)
            {
                ConfigErrorCount++;
                string name = wave != null ? wave.waveName : "(null)";
                Debug.LogError($"[WaveEncounter] Wave {w + 1} '{name}' has no valid enemy prefab or an enemy count of 0. Skipping it so the encounter does not hang.", this);
                continue;
            }

            SetState(EncounterState.WaitingForWave);
            yield return WaitThenWarn(wave.delayBeforeWave, wave, 0);

            SetState(EncounterState.Spawning);
            Debug.Log($"[WaveEncounter] Wave {w + 1}/{waves.Count} started: {wave.waveName} ({wave.formation}, {wave.TotalEnemies} enemies).", this);
            onWaveStarted.Invoke(w);
            yield return SpawnWave(wave);

            SetState(EncounterState.Clearing);
            while (activeEnemies.Count > 0 && StateTimer < wave.clearTimeout)
            {
                yield return null;
            }
            if (activeEnemies.Count > 0)
            {
                Debug.LogWarning($"[WaveEncounter] Wave {w + 1} '{wave.waveName}' clear timeout ({wave.clearTimeout}s) reached. Force-removing {activeEnemies.Count} remaining enemies.", this);
                ForceRemoveRemaining();
            }

            Debug.Log($"[WaveEncounter] Wave {w + 1}/{waves.Count} cleared. Totals so far - destroyed: {DestroyedCount}, escaped: {EscapedCount}, timed out: {TimedOutCount}.", this);
            onWaveCleared.Invoke(w);
        }
        CompleteEncounter();
    }

    private IEnumerator SpawnWave(EnemyWave wave)
    {
        int spawnIndex = 0;
        for (int g = 0; g < wave.groupCount; g++)
        {
            if (g > 0)
            {
                yield return WaitThenWarn(wave.delayBetweenGroups, wave, g);
            }
            float shift = wave.GetGroupShift(g);
            GroupExtent(wave, shift, out float minX, out float maxX);
            for (int i = 0; i < wave.enemiesPerGroup; i++)
            {
                GameObject prefab = wave.enemyPrefabs[spawnIndex % wave.enemyPrefabs.Length];
                spawnIndex++;
                if (prefab != null)
                {
                    Vector3 offset = WaveFormation.GetOffset(wave.formation, i, wave.enemiesPerGroup, wave.spacing);
                    offset.x += shift;
                    GameObject enemy = Spawn(prefab, spawnOrigin.TransformPoint(offset));
                    NotifySpawned(enemy, new WaveSpawnInfo
                    {
                        waveIndex = CurrentWaveIndex, groupIndex = g, slotIndex = i, slotCount = wave.enemiesPerGroup,
                        slotOffsetX = offset.x - shift, groupShift = shift, groupMinX = minX, groupMaxX = maxX
                    });
                }
                if (wave.spawnStyle == WaveSpawnStyle.Stream && wave.spawnInterval > 0f && i < wave.enemiesPerGroup - 1)
                {
                    yield return Wait(wave.spawnInterval);
                }
            }
        }
    }

    private GameObject Spawn(GameObject prefab, Vector3 position)
    {
        GameObject enemy = Instantiate(prefab, position, spawnOrigin.rotation, spawnParent);
        activeEnemies.Add(enemy);
        SpawnedCount++;
        return enemy;
    }

    private static void NotifySpawned(GameObject enemy, WaveSpawnInfo info)
    {
        foreach (IWaveSpawnReceiver receiver in enemy.GetComponents<IWaveSpawnReceiver>())
        {
            receiver.OnWaveSpawn(info);
        }
    }

    // Waits 'delay' in total and raises onGroupIncoming entryWarningSeconds before the end.
    private IEnumerator WaitThenWarn(float delay, EnemyWave wave, int group)
    {
        float lead = Mathf.Min(entryWarningSeconds, delay);
        yield return Wait(delay - lead);
        if (wave.enemiesPerGroup > 0)
        {
            GroupExtent(wave, wave.GetGroupShift(group), out float minX, out float maxX);
            Vector3 centre = spawnOrigin.TransformPoint(new Vector3((minX + maxX) * 0.5f, 0f, 0f));
            onGroupIncoming.Invoke(centre, maxX - minX, lead);
        }
        yield return Wait(lead);
    }

    // Leftmost and rightmost slot offsets of one group (spawn-origin space, including the group's shift).
    private static void GroupExtent(EnemyWave wave, float shift, out float minX, out float maxX)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        for (int i = 0; i < wave.enemiesPerGroup; i++)
        {
            float x = WaveFormation.GetOffset(wave.formation, i, wave.enemiesPerGroup, wave.spacing).x + shift;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
        }
    }

    private void ForceRemoveRemaining()
    {
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
                TimedOutCount++;
            }
        }
        activeEnemies.Clear();
    }

    private void CompleteEncounter()
    {
        if (State == EncounterState.Completed || State == EncounterState.Failed)
        {
            return;
        }
        SetState(EncounterState.Completed);
        runRoutine = null;
        Debug.Log($"[WaveEncounter] ENCOUNTER CLEARED in {ElapsedTime:F1}s (all enemies destroyed: {(EscapedCount == 0 && TimedOutCount == 0 ? "yes" : "no")}). Spawned {SpawnedCount}, destroyed {DestroyedCount}, escaped {EscapedCount}, timed out {TimedOutCount}, config errors {ConfigErrorCount}.", this);
        onEncounterCompleted.Invoke();
    }

    private void SetState(EncounterState newState)
    {
        State = newState;
        StateTimer = 0f;
    }

    private static bool HasAnyPrefab(EnemyWave wave)
    {
        if (wave.enemyPrefabs == null)
        {
            return false;
        }
        foreach (GameObject prefab in wave.enemyPrefabs)
        {
            if (prefab != null)
            {
                return true;
            }
        }
        return false;
    }

    // Our own timer (instead of WaitForSeconds) so StateTimer and pause behaviour stay consistent.
    private IEnumerator Wait(float seconds)
    {
        float end = StateTimer + seconds;
        while (StateTimer < end)
        {
            yield return null;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        Gizmos.DrawWireCube(encounterBounds.center, encounterBounds.size);

        if (waves == null)
        {
            return;
        }
        Transform origin = spawnOrigin != null ? spawnOrigin : transform;
        Color[] colours = { Color.cyan, Color.magenta, Color.yellow };
        for (int w = 0; w < waves.Count; w++)
        {
            EnemyWave wave = waves[w];
            if (wave == null)
            {
                continue;
            }
            Gizmos.color = colours[w % colours.Length];
            for (int g = 0; g < wave.groupCount; g++)
            {
                for (int i = 0; i < wave.enemiesPerGroup; i++)
                {
                    Vector3 offset = WaveFormation.GetOffset(wave.formation, i, wave.enemiesPerGroup, wave.spacing);
                    offset.x += wave.GetGroupShift(g);
                    Gizmos.DrawWireSphere(origin.TransformPoint(offset), 0.35f + 0.1f * w);
                }
            }
        }
    }
}
