using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Controls and runs an entire enemy encounter by spawning a sequence of waves.

// This script manages enemy spawning, wave progression, and enemy tracking.

// Enemy movement and AI are handled separately by the enemy prefabs.

// Enemies stop being tracked when they are destroyed, disabled, leave the encounter bounds,
// or are removed after the clear timeout.

// The encounter is considered completed when all waves have finished,
// even if some enemies escaped or had to be removed by the timeout.
public class WaveEncounter : MonoBehaviour
{
    // Defines the different stages an encounter can go through.
    public enum EncounterState
    {
        Idle,           // No encounter is currently running.
        WaitingForWave, // Waiting for the next wave to begin.
        Spawning,       // Enemies are currently being spawned.
        Clearing,       // Waiting for all remaining enemies to be cleared.
        Completed,      // All waves have finished.
        Failed          // The encounter has ended without victory.
    }

    // Custom UnityEvent that sends an integer value when triggered.
    // Used to communicate which wave has started or been cleared.
    [System.Serializable] public class WaveEvent : UnityEvent<int> { }

    // Custom UnityEvent triggered shortly before an enemy wave spawns.
    // Sends three values:
    // Vector3 = World position of the wave's centre.
    // float = Width of the formation.
    // float = Time remaining before the wave spawns.
    [System.Serializable] public class GroupWarningEvent : UnityEvent<Vector3, float, float> { }

    // Stores all the enemy waves that will be played during this encounter.
    // Each wave contains its own enemy prefabs, formation, and spawning settings.
    [SerializeField] private List<EnemyWave> waves = new List<EnemyWave>();

    // Determines where enemy formations will spawn.
    [Tooltip("Spawn point for formations. Its forward (blue arrow) is the direction enemies face when spawned.")]
    [SerializeField] private Transform spawnOrigin;

    // Optional parent object for newly spawned enemies.
    // If left empty, spawned enemies will be placed at the scene root.
    [Tooltip("Optional parent for spawned enemies. Leave empty to spawn at the scene root.")]
    [SerializeField] private Transform spawnParent;

    // Defines the playable area of the encounter.
    // Enemies that leave these boundaries are counted as escaped and removed.
    // The bounds can use world coordinates or local coordinates relative to Bounds Space.
    [Tooltip("Play area. Enemies outside it are counted as escaped and removed. World space, or relative to Bounds Space if set.")]
    [SerializeField] private Bounds encounterBounds = new Bounds(new Vector3(0f, 0f, 1f), new Vector3(24f, 10f, 26f));

    // Optional transform used as the reference point for the encounter bounds.
    // Allows the playable area to move and rotate with another object, such as a camera rig or moving level environment.
    // If left empty, the bounds remain fixed in world space.
    [Tooltip("Optional: the bounds move and turn with this transform (e.g. the rail camera rig). Empty = world space. " +
             "Parent the Spawn Origin and Spawn Parent to the same rig so spawns follow it too.")]
    [SerializeField] private Transform boundsSpace;

    // Determines whether the encounter automatically starts when the object is enabled.
    [SerializeField] private bool autoStart = true;

    // Controls how many seconds before a group spawns that its incoming warning is triggered.
    // This warning time is included within the existing delay rather than added to it.
    [Tooltip("onGroupIncoming fires this many seconds before each group spawns. The time is taken out of the existing delays, so pacing is unchanged.")]
    [Min(0f)] [SerializeField] private float entryWarningSeconds = 1.2f;


    // Events that allow other scripts to respond to different encounter stages.
    // These can be connected to UI, audio, visual effects, or other gameplay systems.

    // Triggered when a new wave begins.
    // Sends the index of the wave that has started.
    public WaveEvent onWaveStarted = new WaveEvent();

    // Triggered when a wave has finished and all its enemies are cleared.
    // Sends the index of the wave that has been cleared.
    public WaveEvent onWaveCleared = new WaveEvent();

    // Triggered when every wave in the encounter has finished.
    public UnityEvent onEncounterCompleted = new UnityEvent();

    // Triggered when the encounter ends in failure.
    public UnityEvent onEncounterFailed = new UnityEvent();

    // Triggered shortly before an enemy group spawns.
    // Sends the group's centre position, formation width, and warning time.
    public GroupWarningEvent onGroupIncoming = new GroupWarningEvent();

    // Stores references to all enemies currently being tracked by this encounter.
    // Used to determine how many enemies remain before a wave can be cleared.
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    
    // Stores a reference to the coroutine running the encounter.
    // Allows the encounter sequence to be stopped or restarted when needed.
    private Coroutine runRoutine;

    // Stores the encounter's current state.
    // Other scripts can read this value, but only this class can change it.
    public EncounterState State { get; private set; }

    // Stores the zero-based index of the wave currently being processed.
    // Starts at -1 before the encounter begins.
    public int CurrentWaveIndex { get; private set; }

    // Returns the total number of waves configured for this encounter.
    public int WaveCount => waves.Count;

    // Returns the number of enemies currently being tracked by this encounter.
    public int AliveCount => activeEnemies.Count;

    // Tracks the total number of enemies spawned during this encounter.
    public int SpawnedCount { get; private set; }

    // Tracks enemies that were destroyed or became inactive while being tracked.
    public int DestroyedCount { get; private set; }

    // Tracks enemies that have left the encounter bounds and been removed.
    public int EscapedCount { get; private set; }

    // Tracks enemies forcibly removed when a wave reaches its clear timeout.
    public int TimedOutCount { get; private set; }

    // Tracks configuration errors found while validating or processing waves.
    public int ConfigErrorCount { get; private set; }

    // Returns true when the encounter has reached the Completed state.
    public bool IsCompleted => State == EncounterState.Completed;

    // Returns true when the encounter has reached the Failed state.
    public bool IsFailed => State == EncounterState.Failed;

    // Returns true if the encounter completed with at least one enemy spawned,
    // and no enemies escaped or were forcibly removed by a timeout.
    public bool AllEnemiesDestroyed => IsCompleted && EscapedCount == 0 && TimedOutCount == 0 && SpawnedCount > 0;

    // Tracks the time elapsed since the current encounter state began.
    public float StateTimer { get; private set; }

    // Tracks the total time elapsed since the encounter started.
    // This timer continues even if the encounter is paused or fails.
    public float ElapsedTime { get; private set; }

    // The configured waves (read-only list; the waves themselves can be tuned from code, e.g. by a scene builder).
    public IReadOnlyList<EnemyWave> Waves => waves;

    // Returns the currently selected wave, or null if the index is invalid.
    public EnemyWave CurrentWave =>
        (CurrentWaveIndex >= 0 && CurrentWaveIndex < waves.Count) ? waves[CurrentWaveIndex] : null;

    // Used by the scene builder and tests to set up an encounter from code.
    public void Configure(List<EnemyWave> newWaves, Transform origin, Bounds bounds, bool startAutomatically, Transform parent = null)
    {
        waves = newWaves;
        spawnOrigin = origin;
        spawnParent = parent;
        encounterBounds = bounds;
        autoStart = startAutomatically;
    }

    // Makes the bounds relative to 'space' (e.g. a moving rail rig). Null = world space.
    public void SetBoundsSpace(Transform space)
    {
        boundsSpace = space;
    }

    public Transform BoundsSpace => boundsSpace;

    // Whether a world position is inside the encounter bounds (following Bounds Space if set).
    public bool IsInsideBounds(Vector3 worldPosition)
    {
        Vector3 p = boundsSpace != null ? boundsSpace.InverseTransformPoint(worldPosition) : worldPosition;
        return encounterBounds.Contains(p);
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

    // Starts the encounter from the beginning, stopping any current run and resetting all progress.
    public void StartEncounter()
    {
        StopEncounter();
        ResetProgress();
        ValidateConfiguration();
        runRoutine = StartCoroutine(RunEncounter());
    }

    // Removes every enemy this encounter spawned and starts again from wave 1.
    [ContextMenu("Restart Encounter")]
    public void RestartEncounter()
    {
        Debug.Log("[WaveEncounter] Restarting encounter.", this);
        StartEncounter();
    }

    // Stops the encounter coroutine, destroys all tracked enemies, 
    // clears the active enemy list, and returns the encounter to Idle.
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

    // Ends the encounter without victory (e.g. player defeated): no more spawns, and
    // onEncounterCompleted can no longer fire. Enemies already on screen keep flying until they leave.
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

    // Checks the configured waves for missing prefabs or other issues, and logs errors/warnings.
    // Increments ConfigErrorCount for each problem found.
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
        if (!IsInsideBounds(spawnOrigin.position))
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

    // Updates the encounter state and tracks elapsed time. Cleans up destroyed or escaped enemies.
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

    // Drops enemies that were destroyed/disabled elsewhere and removes those outside the bounds.
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
            else if (!IsInsideBounds(enemy.transform.position))
            {
                activeEnemies.RemoveAt(i);
                EscapedCount++;
                Destroy(enemy);
            }
        }
    }

    // Runs the encounter by spawning each wave in sequence, waiting for them to clear, and finishing the encounter.
    // Main coroutine that controls the encounter flow. It handles wave timing, spawning, and clearing.
    private IEnumerator RunEncounter()
    {
        for (int w = 0; w < waves.Count; w++)
        {
            CurrentWaveIndex = w;
            EnemyWave wave = waves[w];

            // Validate the wave before starting it. If it has no valid prefabs or zero enemies, log an error and skip it.
            if (wave == null || !HasAnyPrefab(wave) || wave.TotalEnemies <= 0)
            {
                ConfigErrorCount++;
                string name = wave != null ? wave.waveName : "(null)";
                Debug.LogError($"[WaveEncounter] Wave {w + 1} '{name}' has no valid enemy prefab or an enemy count of 0. Skipping it so the encounter does not hang.", this);
                continue;
            }

            // Wait for the configured delay before starting the wave, then trigger the onGroupIncoming warning.
            SetState(EncounterState.WaitingForWave);
            yield return WaitThenWarn(wave.delayBeforeWave, wave, 0);

            // Start spawning the wave and wait for it to finish.
            SetState(EncounterState.Spawning);
            Debug.Log($"[WaveEncounter] Wave {w + 1}/{waves.Count} started: {wave.waveName} ({wave.formation}, {wave.TotalEnemies} enemies).", this);
            onWaveStarted.Invoke(w);
            yield return SpawnWave(wave);

            // Wait for the wave to be cleared or time out, then move on to the next wave.
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

    // Spawns all enemies in a wave according to EnemyWave settings.
    private IEnumerator SpawnWave(EnemyWave wave)
    {
        // Tracks which prefab to spawn next (cycles through the wave's enemyPrefabs array).
        int spawnIndex = 0; 

        // Spawn each group in the wave, applying the configured formation and spacing.
        for (int g = 0; g < wave.groupCount; g++)
        {
            // Wait for the configured delay between groups, then trigger the onGroupIncoming warning.
            if (g > 0)
            {
                yield return WaitThenWarn(wave.delayBetweenGroups, wave, g);
            }

            // Calculate the group's sideways shift and the leftmost/rightmost slot positions for the formation.
            float shift = wave.GetGroupShift(g);
            GroupExtent(wave, shift, out float minX, out float maxX);

            // Spawn each enemy in the group, applying the formation offset and group shift.
            for (int i = 0; i < wave.enemiesPerGroup; i++)
            {
                GameObject prefab = wave.enemyPrefabs[spawnIndex % wave.enemyPrefabs.Length];
                spawnIndex++;
                if (prefab != null)
                {
                    Vector3 offset = WaveFormation.GetOffset(wave.formation, i, wave.enemiesPerGroup, wave.spacing);
                    offset.x += shift;
                    GameObject enemy = Spawn(prefab, spawnOrigin.TransformPoint(offset));

                    // Notify any components on the enemy that implement IWaveSpawnReceiver about their spawn info.
                    NotifySpawned(enemy, new WaveSpawnInfo
                    {
                        waveIndex = CurrentWaveIndex, groupIndex = g, slotIndex = i, slotCount = wave.enemiesPerGroup,
                        slotOffsetX = offset.x - shift, groupShift = shift, groupMinX = minX, groupMaxX = maxX
                    });
                }

                // If the wave is set to stream spawn, wait for the configured interval before spawning the next enemy.
                if (wave.spawnStyle == WaveSpawnStyle.Stream && wave.spawnInterval > 0f && i < wave.enemiesPerGroup - 1)
                {
                    yield return Wait(wave.spawnInterval);
                }
            }
        }
    }

    // Instantiates an enemy prefab at the specified position, adds it to the activeEnemies list, and increments SpawnedCount.
    private GameObject Spawn(GameObject prefab, Vector3 position)
    {
        GameObject enemy = Instantiate(prefab, position, spawnOrigin.rotation, spawnParent);
        activeEnemies.Add(enemy);
        SpawnedCount++;
        return enemy;
    }

    // Notifies any components on the enemy that implement IWaveSpawnReceiver about their spawn info.
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

    // Destroys all remaining enemies and clears the activeEnemies list, incrementing TimedOutCount for each.
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

    // Completes the encounter, sets the state to Completed, and triggers the onEncounterCompleted event.
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

    // Sets the encounter state and resets the state timer. Used to transition between different stages of the encounter.
    private void SetState(EncounterState newState)
    {
        State = newState;
        StateTimer = 0f;
    }

    // Returns true if the wave has at least one valid enemy prefab assigned.
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

    // Draws the encounter bounds and the spawn positions of each wave in the Scene view for debugging and visualization.
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        Matrix4x4 previous = Gizmos.matrix;
        if (boundsSpace != null)
        {
            Gizmos.matrix = boundsSpace.localToWorldMatrix;
        }
        Gizmos.DrawWireCube(encounterBounds.center, encounterBounds.size);
        Gizmos.matrix = previous;

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
