using System;
using UnityEngine;

// Determines how enemies are spawned into the scene.
public enum WaveSpawnStyle
{
    // Spawns all enemies in a group at the same time.
    // This keeps the formation in its exact intended shape.
    Formation,

    // Spawns enemies one at a time with a delay between each spawn.
    // Since earlier enemies continue moving, the formation may stretch out.
    Stream
}

/// Stores the settings and information for a single enemy wave.
/// A wave can contain multiple groups of enemies using the same formation.
[Serializable]
public class EnemyWave
{
    // Name used to identify this wave in the Unity Inspector.
    public string waveName = "Wave";

    // Determines the shape or arrangement of enemies in this wave.
    public WaveFormationType formation = WaveFormationType.Line;

    // List of enemy prefabs that can be spawned during this wave.
    // Enemies cycle through the list in order (A, B, A, B...).
    // The spawner does not add AI to the enemy prefabs.
    [Tooltip("Enemy prefabs used by this wave. Enemies cycle through the list (A, B, A, B...). Any enemy prefab works; the spawner never adds AI.")]
    public GameObject[] enemyPrefabs = new GameObject[0];

    // Number of enemies that will spawn in each group.
    // Cannot be set below 0 in the Inspector.
    [Min(0)] public int enemiesPerGroup = 5;

    // Number of groups that will spawn during this wave.
    // Cannot be set below 1 in the Inspector.
    [Min(1)] public int groupCount = 1;

    // Distance between each enemy in the formation, measured in world units.
    [Tooltip("Distance between enemies in the formation (world units).")]
    [Min(0.1f)] public float spacing = 2f;

    // Determines whether enemies spawn together or individually.
    [Tooltip("Formation = the group appears at once (exact shape). Stream = one enemy every Spawn Interval seconds.")]
    public WaveSpawnStyle spawnStyle = WaveSpawnStyle.Formation;

    // Only used when Stream spawning is selected.
    // Determines how many seconds to wait between spawning individual enemies.
    [Tooltip("Stream style only: seconds between individual enemy spawns inside a group.")]
    [Min(0f)] public float spawnInterval = 0.5f;

    // Amount of time to wait between spawning each group in the wave.
    [Tooltip("Seconds between groups of this wave.")]
    [Min(0f)] public float delayBetweenGroups = 3f;

    // Stores the sideways spawn position for each group.
    // If the array is empty, groups spawn at the centre.
    [Tooltip("Sideways entry position of each group, relative to the Spawn Origin. Cycled (group 1 uses element 0, ...). Empty = centre.")]
    public float[] groupEntryOffsets = new float[0];

    // Amount of time to wait before this wave begins.
    // This happens after the previous wave has been cleared.
    [Tooltip("Pause before this wave starts (after the previous wave is cleared).")]
    [Min(0f)] public float delayBeforeWave = 3f;

    // Maximum time allowed after the final enemy spawn before remaining enemies are removed.
    // Prevents the encounter from getting stuck if enemies are not cleared normally.
    [Tooltip("Safeguard: seconds after the last spawn before any remaining enemies are force-removed so the encounter cannot stall.")]
    [Min(1f)] public float clearTimeout = 20f;

    // Calculates the total number of enemies that will spawn in this wave.
    // Multiplies the number of enemies per group by the total number of groups.
    public int TotalEnemies => enemiesPerGroup * groupCount;

    // Gets the sideways spawn offset for a specific group.
    // Uses the group index to select an offset from the groupEntryOffsets array.
    public float GetGroupShift(int groupIndex)
    {
        // If no offsets have been assigned, return 0 (centre position).
        if (groupEntryOffsets == null || groupEntryOffsets.Length == 0)
        {
            return 0f;
        }

        // Each group uses the next offset in the array.
        // Once the end of the array is reached, the offsets start again.
        return groupEntryOffsets[groupIndex % groupEntryOffsets.Length];
    }
}
