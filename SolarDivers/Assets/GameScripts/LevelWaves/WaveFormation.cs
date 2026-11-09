using UnityEngine;

public enum WaveFormationType
{
    Line,
    VFormation,
    Mixed
}

/// <summary>
/// Pure formation maths. Offsets are in the spawn origin's local space:
/// +X is the origin's right, -Z is "behind the leader" (the origin's forward is the travel direction).
/// </summary>
public static class WaveFormation
{
    public static Vector3 GetOffset(WaveFormationType formation, int index, int count, float spacing)
    {
        switch (formation)
        {
            case WaveFormationType.Line:
                // One horizontal row, centred on the origin.
                return new Vector3((index - (count - 1) * 0.5f) * spacing, 0f, 0f);

            case WaveFormationType.VFormation:
                // Leader at the tip, then alternating left/right pairs further back.
                if (index == 0)
                {
                    return Vector3.zero;
                }
                int rank = (index + 1) / 2;
                float side = (index % 2 == 1) ? -1f : 1f;
                return new Vector3(side * rank * spacing, 0f, -rank * spacing);

            case WaveFormationType.Mixed:
                // Staggered pairs on alternating flanks, sweeping in and out across three lanes.
                int pair = index / 2;
                float flank = (index % 2 == 0) ? -1f : 1f;
                int lane = 1 + pair % 3;
                return new Vector3(flank * lane * spacing, 0f, -pair * spacing * 0.5f);
        }
        return Vector3.zero;
    }
}
