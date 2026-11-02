using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Speed class of a test enemy. Each prefab picks one; speed and turn rate are set per prefab.</summary>
public enum TestEnemyClass
{
    Heavy,
    Fighter,
    Interceptor
}

[Flags]
public enum TestEnemyClassMask
{
    None = 0,
    Heavy = 1,
    Fighter = 2,
    Interceptor = 4
}

public enum TestMovePattern
{
    // Straight down its lane.
    Straight,
    // Curved approach into a neighbouring lane (side groups curve in, centre groups split apart or sweep to one side).
    CurvedPass,
    // Swings out to its flank, then back in before descending.
    FlankWeave,
    // Crosses the upper screen sideways (left/right sweep), then descends on the far side.
    SideSweep,
    // Dives to mid-screen, pulls back up for a brief retreat, then makes a second pass down a nearby lane.
    PassAndRetreat
}

/// <summary>
/// PLACEHOLDER movement for wave testing only. Leonardo's enemy AI replaces this; WaveEncounter does not depend on it.
/// The ship steers along a short route of waypoints on the gameplay plane with a capped turn rate and turn acceleration,
/// so direction never changes instantly. Every route ends with a straight descent: below Straight Below Z the ship stops
/// turning, so nothing turns near the player. Reactions are limited: a one-off shift towards the player's lane, or (for
/// some classes) a flank pass beside the player's last observed position that tracks for at most Tracking Seconds.
/// Reactions are shrunk if they would close the last escape lane (see LargestGap). Per-wave settings come from the
/// TestDifficultyProfile entry for the enemy's wave.
/// All positions are in the parent's space (the encounter's Spawn Parent), so if that parent rides a moving rail rig
/// the routes, lanes and safety heights move with it. With no parent this is plain world space.
/// </summary>
public class TestEnemyMover : MonoBehaviour, IWaveSpawnReceiver
{
    // Enabled test enemies, so reactions can check the escape lane. Cleared as enemies are disabled/destroyed.
    private static readonly List<TestEnemyMover> active = new List<TestEnemyMover>();
    public static IReadOnlyList<TestEnemyMover> Active => active;

    [SerializeField] private TestEnemyClass enemyClass = TestEnemyClass.Fighter;
    [Tooltip("Pattern used when the wave's difficulty entry does not set one for this group.")]
    [SerializeField] private TestMovePattern pattern = TestMovePattern.CurvedPass;
    [Tooltip("Cruise speed (units/s) before the wave's speed multiplier.")]
    [SerializeField] private float speed = 2.6f;
    [Tooltip("Speed change per second (no instant speed changes).")]
    [SerializeField] private float acceleration = 3f;
    [Tooltip("Maximum turn rate (degrees/s). Lower = wider, more readable turns.")]
    [SerializeField] private float turnRate = 110f;
    [Tooltip("How quickly the turn rate itself can change (degrees/s per second). Smooths the start and end of every turn.")]
    [SerializeField] private float turnAcceleration = 300f;

    [Header("Route shape (units)")]
    [SerializeField] private float curveWidth = 2.6f;
    [SerializeField] private float flankWidth = 2.2f;
    [Tooltip("Sideways distance of a Side Sweep (shortened if the group would leave Lane Limit X).")]
    [SerializeField] private float sweepWidth = 6f;
    [Tooltip("Height of the sideways part of a Side Sweep.")]
    [SerializeField] private float sweepZ = 6.5f;
    [Tooltip("Pass And Retreat: how far down the first pass goes before pulling back up.")]
    [SerializeField] private float diveZ = 4.8f;
    [Tooltip("Pass And Retreat: how far back up the retreat goes.")]
    [SerializeField] private float retreatZ = 7f;
    [Tooltip("Pass And Retreat: sideways offset of the retreat and the second pass.")]
    [SerializeField] private float retreatShift = 1.6f;

    [Header("Safety")]
    [Tooltip("Below this z the ship flies straight (no turning near the player, whose zone is z <= 0).")]
    [SerializeField] private float straightBelowZ = 1.5f;
    [Tooltip("Band above Straight Below Z in which the ship lines up for its straight descent.")]
    [SerializeField] private float lineUpBand = 1.8f;
    [Tooltip("Route targets stay within +/- this x, inside the player's reach.")]
    [SerializeField] private float laneLimitX = 8f;
    [Tooltip("After this many seconds the ship abandons its route and leaves straight down (no endless pursuit).")]
    [SerializeField] private float maxLifetime = 16f;
    [Tooltip("Half the ship's width, used when checking that an escape lane stays open.")]
    [SerializeField] private float halfWidth = 0.75f;

    private const float SubStep = 1f / 120f;
    private const float ExitZ = -30f;

    private readonly List<Vector2> route = new List<Vector2>();
    private int waypoint;
    private bool waypointWasAhead;
    // Index of the retreat waypoint (Pass And Retreat), which starts behind the ship and must not be skipped; -1 = none.
    private int retreatWaypoint = -1;
    // Time spent heading for the current waypoint; after one full circle at the turn rate the ship moves on.
    private float waypointTime;
    private Vector2 heading;
    private float angularVelocity;
    private float currentSpeed;
    private float travelled;
    private float age;
    private float speedMultiplier = 1f;
    private float turnMultiplier = 1f;
    private float widthMultiplier = 1f;
    private float side = 1f;
    private TestWaveDifficulty difficulty;
    private TestMovePattern activePattern;
    private float laneX;
    private bool reacted;
    private bool flanking;
    private float trackTimer;
    private float flankSide;
    private float plannedLaneX;
    private bool retreating;
    private bool everRetreated;

    public TestEnemyClass Class => enemyClass;
    public TestMovePattern Pattern => pattern;
    public TestMovePattern ActivePattern => activePattern;
    public float Speed => speed * speedMultiplier;
    public float TurnRate => turnRate * turnMultiplier;
    public float TurnAcceleration => turnAcceleration * turnMultiplier;
    public float StraightBelowZ => straightBelowZ;
    public float LaneLimitX => laneLimitX;
    public float MaxLifetime => maxLifetime;
    public float HalfWidth => halfWidth;
    public float Side => side;
    public float Travelled => travelled;
    /// <summary>Velocity relative to the parent, as a world-space direction (excludes the parent's own motion).</summary>
    public Vector3 Velocity
    {
        get
        {
            var local = new Vector3(heading.x, 0f, heading.y) * currentSpeed;
            return transform.parent != null ? transform.parent.TransformDirection(local) : local;
        }
    }
    public Vector2 Heading => heading;
    public float AngularVelocity => angularVelocity;
    /// <summary>x of the final straight descent (where the ship will cross the player's zone).</summary>
    public float LaneX => laneX;
    /// <summary>Sideways change of the descent lane made by a reaction (lane shift or flank).</summary>
    public float SeekShift => laneX - plannedLaneX;
    public bool SeekRead => reacted;
    public bool IsFlanking => flanking;
    public bool IsTracking => flanking && trackTimer > 0f;
    public bool EverRetreated => everRetreated;
    public IReadOnlyList<Vector2> Route => route;
    public int WaypointIndex => waypoint;

    // The last turn of every route ends above this height, so the ship (fast ones need longer) has room to line up
    // for its straight descent before Straight Below Z.
    private float DescendZ => straightBelowZ + Mathf.Max(lineUpBand, 2f * Speed / Mathf.Max(1f, TurnRate * Mathf.Deg2Rad));

    void OnEnable()
    {
        active.Add(this);
        Vector3 forward = transform.localRotation * Vector3.forward;
        heading = new Vector2(forward.x, forward.z);
        heading = heading.sqrMagnitude > 1e-4f ? heading.normalized : Vector2.down;
        angularVelocity = 0f;
        travelled = age = 0f;
        speedMultiplier = turnMultiplier = widthMultiplier = 1f;
        difficulty = null;
        reacted = flanking = retreating = everRetreated = false;
        trackTimer = 0f;
        // Spawned without an encounter (e.g. dropped in the scene): curve towards the centre.
        side = Mathf.Abs(transform.localPosition.x) > 0.5f ? -Mathf.Sign(transform.localPosition.x) : 1f;
        currentSpeed = Speed;
        PlanRoute(pattern, transform.localPosition.x, transform.localPosition.x, transform.localPosition.x);
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    public void OnWaveSpawn(WaveSpawnInfo info)
    {
        TestDifficultyProfile profile = TestAttackDirector.Active != null ? TestAttackDirector.Active.Profile : null;
        TestWaveDifficulty wave = profile != null ? profile.ForWave(info.waveIndex) : null;

        // Formation slot -> world x. The spawn origin faces down the screen, so its right is world -X.
        float rightX = (transform.localRotation * Vector3.right).x;
        float originX = transform.localPosition.x - rightX * (info.slotOffsetX + info.groupShift);
        float a = originX + rightX * info.groupMinX;
        float b = originX + rightX * info.groupMaxX;
        float groupMin = Mathf.Min(a, b);
        float groupMax = Mathf.Max(a, b);
        float groupCentre = (groupMin + groupMax) * 0.5f;

        float slot = Mathf.Abs(info.slotOffsetX) < 0.01f ? 0f : Mathf.Sign(info.slotOffsetX * rightX);
        bool centreGroup = Mathf.Abs(info.groupShift) < 0.01f;
        bool alternate = wave != null && wave.alternateFlanks;
        float alternateSide = info.groupIndex % 2 == 0 ? 1f : -1f;
        float towardsCentre = Mathf.Abs(groupCentre) > 0.5f ? -Mathf.Sign(groupCentre) : alternateSide;

        TestMovePattern chosen = pattern;
        if (wave != null && wave.groupPatterns != null && wave.groupPatterns.Length > 0 && (wave.keepOwnPattern & ToMask(enemyClass)) == 0)
        {
            chosen = wave.groupPatterns[info.groupIndex % wave.groupPatterns.Length];
        }
        switch (chosen)
        {
            case TestMovePattern.CurvedPass:
                // Side groups curve in (opening the outer lane); centre groups split apart, or all sweep one way.
                side = centreGroup ? (alternate ? alternateSide : slot) : towardsCentre;
                break;
            case TestMovePattern.FlankWeave:
                side = centreGroup ? (alternate ? alternateSide : (slot != 0f ? slot : 1f)) : -towardsCentre;
                break;
            default:
                // Sweeps and retreats move the whole group the same way, towards the side with more room.
                side = centreGroup ? alternateSide : towardsCentre;
                break;
        }
        if (wave != null)
        {
            ApplyDifficulty(wave);
        }
        currentSpeed = Speed;
        PlanRoute(chosen, transform.localPosition.x, groupMin, groupMax);
    }

    /// <summary>Speed, turn rate, widths and reactions for this wave. Used by OnWaveSpawn and tests.</summary>
    public void ApplyDifficulty(TestWaveDifficulty wave)
    {
        difficulty = wave;
        speedMultiplier = wave.speedMultiplier;
        turnMultiplier = wave.turnRateMultiplier;
        widthMultiplier = wave.pathWidthMultiplier;
    }

    public static TestEnemyClassMask ToMask(TestEnemyClass c) => (TestEnemyClassMask)(1 << (int)c);

    /// <summary>Builds the waypoint route for a pattern from x0 (this ship), keeping the whole group inside Lane Limit X.</summary>
    public void PlanRoute(TestMovePattern chosen, float x0, float groupMin, float groupMax)
    {
        activePattern = chosen;
        route.Clear();
        retreatWaypoint = -1;
        waypoint = 0;
        waypointWasAhead = false;
        waypointTime = 0f;
        float z0 = transform.localPosition.z;
        if (heading.y > -0.7f)
        {
            // The route shapes assume the test scene (spawned at the top, flying down the screen).
            // Spawned any other way, just fly straight ahead.
            Vector2 ahead = new Vector2(transform.localPosition.x, z0) + heading * 60f;
            route.Add(ahead);
            laneX = plannedLaneX = transform.localPosition.x;
            return;
        }
        float upper = Mathf.Min(z0 - 1.5f, sweepZ + 1f);
        float descendZ = DescendZ;
        switch (chosen)
        {
            case TestMovePattern.CurvedPass:
            {
                float x1 = x0 + side * curveWidth * widthMultiplier;
                route.Add(new Vector2(x0 + side * curveWidth * widthMultiplier * 0.35f, upper));
                route.Add(new Vector2(x1, descendZ + 1.2f));
                break;
            }
            case TestMovePattern.FlankWeave:
            {
                float w = flankWidth * widthMultiplier;
                route.Add(new Vector2(x0 + side * w, upper));
                route.Add(new Vector2(x0 + side * w * 0.25f, descendZ + 1.5f));
                break;
            }
            case TestMovePattern.SideSweep:
            {
                // Sweep as far as the group can go without leaving the lane limits.
                float room = side > 0f ? laneLimitX - groupMax : groupMin + laneLimitX;
                float shift = side * Mathf.Clamp(sweepWidth * widthMultiplier, 0f, Mathf.Max(0f, room));
                route.Add(new Vector2(x0 + shift * 0.15f, sweepZ + 0.8f));
                route.Add(new Vector2(x0 + shift * 0.85f, sweepZ));
                route.Add(new Vector2(x0 + shift, descendZ + 1.2f));
                break;
            }
            case TestMovePattern.PassAndRetreat:
            {
                float s = retreatShift * widthMultiplier * side;
                route.Add(new Vector2(x0, diveZ));
                route.Add(new Vector2(x0 + s * 0.6f, retreatZ));
                retreatWaypoint = route.Count - 1; // starts behind the ship on purpose
                route.Add(new Vector2(x0 + s, Mathf.Max(diveZ - 0.4f, descendZ + 0.8f))); // second pass
                break;
            }
            default:
                route.Add(new Vector2(x0, descendZ + 1f));
                break;
        }
        // Slow turners overshoot a waypoint by about one turning radius, so their targets stay that much further inside.
        float turnRadius = Speed / Mathf.Max(1f, TurnRate * Mathf.Deg2Rad);
        float limit = laneLimitX - Mathf.Clamp(turnRadius - 0.5f, 0f, 2f);
        for (int i = 0; i < route.Count; i++)
        {
            route[i] = new Vector2(Mathf.Clamp(route[i].x, -limit, limit), route[i].y);
        }
        laneX = route[route.Count - 1].x;
        plannedLaneX = laneX;
        route.Add(new Vector2(laneX, ExitZ));
    }

    void Update()
    {
        Step(Time.deltaTime);
    }

    /// <summary>Advances the ship by dt seconds (in fixed sub-steps, so the path is the same at any frame rate).</summary>
    public void Step(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }
        int steps = Mathf.CeilToInt(dt / SubStep);
        float h = dt / steps;
        Vector3 p = transform.localPosition;
        var pos = new Vector2(p.x, p.z);
        for (int i = 0; i < steps; i++)
        {
            pos = SubStepMove(pos, h);
        }
        transform.localPosition = new Vector3(pos.x, p.y, pos.y);
        if (heading.sqrMagnitude > 1e-6f)
        {
            transform.localRotation = Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.y), Vector3.up);
        }
    }

    private Vector2 SubStepMove(Vector2 pos, float h)
    {
        age += h;
        waypointTime += h;
        React(pos, h);
        if (age > maxLifetime && waypoint < route.Count - 1)
        {
            // Safety net: leave straight down from here.
            route.Clear();
            retreatWaypoint = -1;
            route.Add(new Vector2(Mathf.Clamp(pos.x, -laneLimitX, laneLimitX), ExitZ));
            laneX = plannedLaneX = route[0].x;
            waypoint = 0;
            waypointWasAhead = false;
            waypointTime = 0f;
        }

        // Next waypoint once the ship is close to it, or once it is well off the nose (behind, or alongside) within a few
        // turning radii: the ship can no longer reach it without circling, so it moves on instead of orbiting it.
        // The retreat waypoint starts behind the ship on purpose, so it only counts as passed after the ship has turned
        // towards it. The final exit point is never "reached": the encounter removes the ship first.
        float turnRadius = currentSpeed / Mathf.Max(1f, TurnRate * Mathf.Deg2Rad);
        while (waypoint < route.Count - 1)
        {
            Vector2 toTarget = route[waypoint] - pos;
            float distance = toTarget.magnitude;
            float offNose = Vector2.Angle(heading, toTarget);
            waypointWasAhead |= offNose < 45f;
            bool circledOnce = waypointTime > 360f / Mathf.Max(1f, TurnRate);
            if (waypoint == retreatWaypoint && !waypointWasAhead && !circledOnce)
            {
                break; // keep the retreat point until the ship has turned towards it, however close it is
            }
            bool passed = circledOnce || ((waypoint != retreatWaypoint || waypointWasAhead) && offNose > 60f && distance < 2.5f * turnRadius);
            if (distance >= Mathf.Max(0.6f, currentSpeed * 0.3f) && !passed)
            {
                break;
            }
            waypoint++;
            waypointWasAhead = false;
            waypointTime = 0f;
        }
        Vector2 target = route[waypoint];
        Vector2 desired = target - pos;
        desired = desired.sqrMagnitude > 1e-6f ? desired.normalized : heading;
        bool descending = heading.y < 0f;
        // Faster ships need a longer run-up to finish turning before the straight zone.
        float lineUp = Mathf.Max(lineUpBand, 2f * turnRadius);
        if (descending && pos.y < straightBelowZ + lineUp && waypoint >= route.Count - 1)
        {
            desired = Vector2.down; // line up for the straight descent
        }
        retreating = heading.y > 0.2f;
        everRetreated |= retreating;

        if (descending && pos.y < straightBelowZ)
        {
            angularVelocity = 0f; // straight line through the player's zone
        }
        else
        {
            float maxTurn = TurnRate;
            float wanted = Mathf.Clamp(Vector2.SignedAngle(heading, desired) * 4f, -maxTurn, maxTurn);
            angularVelocity = Mathf.MoveTowards(angularVelocity, wanted, turnAcceleration * turnMultiplier * h);
            heading = Rotate(heading, angularVelocity * h).normalized;
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, Speed, acceleration * h);
        float step = currentSpeed * h;
        travelled += step;
        return pos + heading * step;
    }

    // Limited reactions to the player (never during the straight descent, never longer than Tracking Seconds).
    private void React(Vector2 pos, float h)
    {
        TestAttackDirector director = TestAttackDirector.Active;
        if (difficulty == null || director == null || !director.HasPlayer)
        {
            return;
        }
        if (flanking && trackTimer > 0f)
        {
            trackTimer = pos.y < straightBelowZ + lineUpBand + 1f ? 0f : trackTimer - h;
            SetFlankLane(PlayerLocalX(director), pos);
            return;
        }
        if (reacted || travelled < difficulty.reactAfterDistance || pos.y < straightBelowZ + lineUpBand + 1f)
        {
            return;
        }
        reacted = true;
        float playerX = PlayerLocalX(director);
        if ((difficulty.flankingClasses & ToMask(enemyClass)) != 0)
        {
            // Flank the player's last observed position: descend beside it, on the side this ship is already on.
            flanking = true;
            trackTimer = difficulty.trackingSeconds;
            flankSide = Mathf.Abs(pos.x - playerX) > 0.5f ? Mathf.Sign(pos.x - playerX) : (playerX > 0f ? -1f : 1f);
            SetFlankLane(playerX, pos);
        }
        else if (difficulty.laneSeekDistance > 0f)
        {
            // Lane shift: read the player's lane once and move part of the way there.
            float shift = Mathf.Clamp(playerX - plannedLaneX, -difficulty.laneSeekDistance, difficulty.laneSeekDistance);
            SetLane(SafeLane(plannedLaneX, plannedLaneX + shift));
        }
    }

    private void SetFlankLane(float playerX, Vector2 pos)
    {
        float wantedX = playerX + flankSide * difficulty.flankOffset;
        wantedX = Mathf.Clamp(wantedX, plannedLaneX - difficulty.maxReposition, plannedLaneX + difficulty.maxReposition);
        float x = SafeLane(plannedLaneX, Mathf.Clamp(wantedX, -laneLimitX, laneLimitX));
        if (Mathf.Abs(x - laneX) > 0.01f || route.Count != 2)
        {
            route.Clear();
            retreatWaypoint = -1;
            route.Add(new Vector2(x, Mathf.Min(pos.y - 0.5f, DescendZ + 1.2f)));
            route.Add(new Vector2(x, ExitZ));
            waypoint = 0;
            waypointWasAhead = false;
            waypointTime = 0f;
            laneX = x;
        }
    }

    // Moves the remaining route sideways so it descends at newLane.
    private void SetLane(float newLane)
    {
        float delta = newLane - laneX;
        for (int i = waypoint; i < route.Count; i++)
        {
            route[i] = new Vector2(Mathf.Clamp(route[i].x + delta, -laneLimitX, laneLimitX), route[i].y);
        }
        laneX = newLane;
    }

    // The reaction is pulled back towards 'from' until the widest free gap among descending ships stays open.
    private float SafeLane(float from, float to)
    {
        float need = difficulty != null && TestAttackDirector.Active != null && TestAttackDirector.Active.Profile != null
            ? TestAttackDirector.Active.Profile.minEscapeGap
            : 0f;
        if (need <= 0f)
        {
            return to;
        }
        var lanes = new List<Vector2>();
        foreach (TestEnemyMover other in active)
        {
            if (other != this && other.transform.localPosition.z > -9f && Mathf.Abs(other.transform.localPosition.z - transform.localPosition.z) < 6f)
            {
                lanes.Add(new Vector2(other.laneX, other.halfWidth));
            }
        }
        for (int i = 0; i <= 4; i++)
        {
            float x = Mathf.Lerp(to, from, i / 4f);
            lanes.Add(new Vector2(x, halfWidth));
            float gap = LargestGap(lanes, -9.5f, 9.5f);
            lanes.RemoveAt(lanes.Count - 1);
            if (gap >= need)
            {
                return x;
            }
        }
        return from;
    }

    /// <summary>Widest free stretch of [minX, maxX] not covered by any lane +/- halfWidth.</summary>
    public static float LargestGap(List<float> lanes, float halfWidth, float minX, float maxX)
    {
        var withWidths = new List<Vector2>(lanes.Count);
        foreach (float x in lanes)
        {
            withWidths.Add(new Vector2(x, halfWidth));
        }
        return LargestGap(withWidths, minX, maxX);
    }

    /// <summary>Widest free stretch of [minX, maxX] not covered by any ship; each entry is (x, half width).</summary>
    public static float LargestGap(List<Vector2> lanes, float minX, float maxX)
    {
        lanes.Sort((a, b) => (a.x - a.y).CompareTo(b.x - b.y));
        float best = 0f;
        float edge = minX;
        foreach (Vector2 lane in lanes)
        {
            best = Mathf.Max(best, lane.x - lane.y - edge);
            edge = Mathf.Max(edge, lane.x + lane.y);
        }
        return Mathf.Max(best, maxX - edge);
    }

    // The player's x in this ship's parent space (the same space as its route).
    private float PlayerLocalX(TestAttackDirector director)
    {
        Vector3 p = director.PlayerPosition;
        return transform.parent != null ? transform.parent.InverseTransformPoint(p).x : p.x;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
