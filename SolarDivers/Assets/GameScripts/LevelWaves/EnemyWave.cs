using System;
using UnityEngine;

public enum WaveSpawnStyle
{
    // Whole group appears in the same frame, so the formation shape is exact.
    Formation,
    // Enemies appear one by one (Spawn Interval apart); earlier ones keep moving, so the shape stretches into a stream.
    Stream
}

/// <summary>
/// Inspector-editable description of one wave. A wave is one or more groups of the same formation.
/// </summary>
[Serializable]
public class EnemyWave
{
    public string waveName = "Wave";
    public WaveFormationType formation = WaveFormationType.Line;

    [Tooltip("Enemy prefabs used by this wave. Enemies cycle through the list (A, B, A, B...). Any enemy prefab works; the spawner never adds AI.")]
    public GameObject[] enemyPrefabs = new GameObject[0];

    [Min(0)] public int enemiesPerGroup = 5;
    [Min(1)] public int groupCount = 1;

    [Tooltip("Distance between enemies in the formation (world units).")]
    [Min(0.1f)] public float spacing = 2f;

    [Tooltip("Formation = the group appears at once (exact shape). Stream = one enemy every Spawn Interval seconds.")]
    public WaveSpawnStyle spawnStyle = WaveSpawnStyle.Formation;

    [Tooltip("Stream style only: seconds between individual enemy spawns inside a group.")]
    [Min(0f)] public float spawnInterval = 0.5f;

    [Tooltip("Seconds between groups of this wave.")]
    [Min(0f)] public float delayBetweenGroups = 3f;

    [Tooltip("Sideways entry position of each group, relative to the Spawn Origin. Cycled (group 1 uses element 0, ...). Empty = centre.")]
    public float[] groupEntryOffsets = new float[0];

    [Tooltip("Pause before this wave starts (after the previous wave is cleared).")]
    [Min(0f)] public float delayBeforeWave = 3f;

    [Tooltip("Safeguard: seconds after the last spawn before any remaining enemies are force-removed so the encounter cannot stall.")]
    [Min(1f)] public float clearTimeout = 20f;

    public int TotalEnemies => enemiesPerGroup * groupCount;

    public float GetGroupShift(int groupIndex)
    {
        if (groupEntryOffsets == null || groupEntryOffsets.Length == 0)
        {
            return 0f;
        }
        return groupEntryOffsets[groupIndex % groupEntryOffsets.Length];
    }
}
