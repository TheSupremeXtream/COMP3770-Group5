using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Movement and attack settings for the test enemies in one wave. Harder waves change behaviour, not health.</summary>
[Serializable]
public class TestWaveDifficulty
{
    public string label = "Wave";
    [Tooltip("Multiplies each enemy prefab's speed (heavy, fighter and interceptor keep their differences).")]
    [Min(0.5f)] public float speedMultiplier = 1f;
    [Tooltip("Multiplies each enemy prefab's turn rate and turn acceleration.")]
    [Min(0.25f)] public float turnRateMultiplier = 1f;
    [Tooltip("Multiplies how far curves, flank swings, sweeps and retreats move sideways.")]
    [Min(0f)] public float pathWidthMultiplier = 1f;
    [Tooltip("Centre groups all curve to one side (alternating left/right per group) instead of splitting apart.")]
    public bool alternateFlanks;

    [Header("Movement patterns")]
    [Tooltip("Pattern for each group of this wave (cycled). Empty = every enemy flies its prefab's own pattern.")]
    public TestMovePattern[] groupPatterns = new TestMovePattern[0];
    [Tooltip("Classes that always fly their prefab's own pattern, even when Group Patterns is set (e.g. slow heavies).")]
    public TestEnemyClassMask keepOwnPattern;

    [Header("Reactions (limited, readable)")]
    [Tooltip("Distance travelled before the ship looks at the player (once).")]
    [Min(0f)] public float reactAfterDistance = 2.5f;
    [Tooltip("How far (units) a ship may shift its descent towards the player's lane. 0 = never. Read once, never tracked.")]
    [Min(0f)] public float laneSeekDistance;
    [Tooltip("These classes flank the player's last observed position instead of shifting lanes.")]
    public TestEnemyClassMask flankingClasses;
    [Tooltip("Flankers descend this far beside the player's observed x (never on top of it).")]
    [Min(1f)] public float flankOffset = 2.4f;
    [Tooltip("Flankers follow the player's x for at most this long, then commit. 0 = a single look.")]
    [Min(0f)] public float trackingSeconds = 0.6f;
    [Tooltip("Largest sideways change a flanker may make to its planned lane.")]
    [Min(0f)] public float maxReposition = 4f;
    [Tooltip("These classes make a short pursuit: they follow the player's x for Pursuit Seconds, then commit to that lane.")]
    public TestEnemyClassMask pursuitClasses;
    [Tooltip("Longest pursuit (seconds). Pursuit always ends high above the player's zone.")]
    [Min(0f)] public float pursuitSeconds = 1f;

    [Header("Run-to-run variation (bounded)")]
    [Tooltip("0 = identical every run. 0.2 = path widths, dive depth and speed vary by up to +/-20% (and centre groups may pick the other side).")]
    [Range(0f, 0.3f)] public float variation;

    [Header("Aimed shots")]
    [Tooltip("Every Nth ship of a group carries a gun (0 = no shots).")]
    [Min(0)] public int aimedShooterEveryNth;
    [Tooltip("Warning time before a shot or burst (glow + aim line).")]
    [Min(0.3f)] public float telegraphSeconds = 0.9f;
    [Min(1f)] public float shotSpeed = 8f;
    [Tooltip("Shots per attack (1 = single aimed shot, 3+ = burst). Interceptors always fire a single quick shot.")]
    [Min(1)] public int shotsPerBurst = 1;
    [Tooltip("Seconds between the shots of a burst.")]
    [Min(0.05f)] public float burstInterval = 0.14f;
    [Tooltip("Limited prediction: aim this fraction of the way to where the player is heading (0 = at the player). ")]
    [Range(0f, 0.8f)] public float leadFactor;
    [Tooltip("The predicted aim point is never more than this far from the player.")]
    [Min(0f)] public float maxLead = 1.5f;

    [Header("Homing rockets")]
    [Tooltip("These classes carry one homing rocket instead of a gun.")]
    public TestEnemyClassMask rocketClasses;
    [Tooltip("Also arm the leader (slot 0) of every group with a rocket (introduces rockets before the heavies arrive).")]
    public bool groupLeaderRocket;
    [Tooltip("Warning time before a rocket launch (red pulse + lock tone).")]
    [Min(0.5f)] public float rocketWarningSeconds = 1f;
}

/// <summary>
/// PLACEHOLDER difficulty settings for the test enemies (not Leonardo's AI): one entry per wave plus fairness rules
/// for aimed shots and reactions. Kept as an asset so later encounters can reuse or copy it. Waves beyond the list use the last entry.
/// </summary>
[CreateAssetMenu(menuName = "Solar Divers/Level Waves/Test Difficulty Profile", fileName = "TEST_DifficultyProfile")]
public class TestDifficultyProfile : ScriptableObject
{
    public List<TestWaveDifficulty> waves = new List<TestWaveDifficulty>();

    [Header("Escape lane (all waves)")]
    [Tooltip("Reactions are reduced if they would leave no free gap at least this wide across the play area.")]
    [Min(0f)] public float minEscapeGap = 2.6f;

    [Header("Aimed shot fairness (all waves)")]
    [Tooltip("Enemy shots in flight plus shots being charged, at most.")]
    [Min(1)] public int maxShotsInFlight = 2;
    [Tooltip("Minimum time between the start of two attacks, across all enemies.")]
    [Min(0f)] public float minSecondsBetweenAttacks = 1.4f;
    [Tooltip("No attack starts closer to the player than this.")]
    [Min(0f)] public float minFireDistance = 7f;
    [Tooltip("Attacks only start while the enemy is in this world-Z band (upper part of the screen).")]
    public Vector2 fireWindowZ = new Vector2(3f, 8.5f);
    [Tooltip("Attacks only start while the enemy is within this sideways distance of the centre (on screen).")]
    [Min(0f)] public float fireWindowX = 9.5f;
    [Tooltip("The aim stops following the player this long before the shot, so the final line shows exactly where it goes.")]
    [Min(0f)] public float aimLockSeconds = 0.3f;
    [Tooltip("No new attacks while the player is invulnerable after a hit.")]
    public bool holdFireWhilePlayerRecovers = true;

    [Header("Homing rocket fairness (all waves)")]
    [Tooltip("Rockets in flight plus rockets being readied, at most.")]
    [Min(1)] public int maxRocketsInFlight = 2;
    [Tooltip("Minimum time between two rocket launches, across all enemies.")]
    [Min(0f)] public float minSecondsBetweenRockets = 3f;
    [Tooltip("No rocket is launched closer to the player than this.")]
    [Min(0f)] public float minRocketDistance = 8f;

    [Header("Run seed")]
    [Tooltip("0 = a new random seed every run (paths vary). Any other value = the same variation every run (tests, tuning).")]
    public int fixedSeed;

    public TestWaveDifficulty ForWave(int waveIndex)
    {
        if (waves == null || waves.Count == 0)
        {
            return null;
        }
        return waves[Mathf.Clamp(waveIndex, 0, waves.Count - 1)];
    }
}
