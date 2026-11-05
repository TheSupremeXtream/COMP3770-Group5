/// <summary>
/// What WaveEncounter knows about a spawned enemy's place in its wave. Passed once, right after spawning,
/// to any component on the enemy that implements IWaveSpawnReceiver. Optional: enemies without one are unaffected.
/// Offsets are in the spawn origin's local space (+X = origin's right).
/// </summary>
public struct WaveSpawnInfo
{
    public int waveIndex;
    public int groupIndex;
    public int slotIndex;
    public int slotCount;
    /// <summary>Sideways position of this slot inside its formation (before the group's entry offset).</summary>
    public float slotOffsetX;
    /// <summary>The group's sideways entry offset (EnemyWave.groupEntryOffsets).</summary>
    public float groupShift;
    /// <summary>Sideways extent of the whole group (leftmost / rightmost slot, including Group Shift).</summary>
    public float groupMinX;
    public float groupMaxX;
}

/// <summary>
/// Optional hook for enemy behaviour that wants to know its formation slot (the test movers use it;
/// Leonardo's AI could too). The spawner itself never moves enemies.
/// </summary>
public interface IWaveSpawnReceiver
{
    void OnWaveSpawn(WaveSpawnInfo info);
}
