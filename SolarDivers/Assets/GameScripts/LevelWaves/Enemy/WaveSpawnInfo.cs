// Info about a single enemy's spawn slot in a wave. This is passed to enemies that implement IWaveSpawnReceiver.
// Optional: enemies without one are unaffected.
// Offsets are relative to the spawn origin's local space (+X = origin's right).
public struct WaveSpawnInfo
{
    public int waveIndex;
    public int groupIndex;

    // The index of this enemy inside its group (0 = leftmost, count-1 = rightmost).
    public int slotIndex;

    // Total number of slots in this group (group size).
    public int slotCount;

    //Sideways position of this slot inside its formation (before the group's entry offset).
    public float slotOffsetX;

    // The group's sideways entry offset (EnemyWave.groupEntryOffsets).
    public float groupShift;

    //Sideways extent of the whole group (leftmost / rightmost slot, including Group Shift).
    public float groupMinX;
    public float groupMaxX;
}

// Optional interface for enemy behaviour that wants to know its formation slot (the test movers use it;
// Leonardo's AI could too). The spawner itself never moves enemies.
public interface IWaveSpawnReceiver
{
    void OnWaveSpawn(WaveSpawnInfo info);
}
