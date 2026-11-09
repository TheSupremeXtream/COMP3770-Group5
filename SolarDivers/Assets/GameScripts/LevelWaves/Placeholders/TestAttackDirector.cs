using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PLACEHOLDER: referee for the test enemies' attacks (not Leonardo's AI). Enemies ask before charging a shot, burst or
/// rocket; the director enforces the fairness rules of the difficulty profile (attacks and rockets in flight, gaps between
/// attacks, distance, on-screen window, no attacks while the player recovers or after defeat). Enemy shots and rockets are
/// parented here so a restart can remove them. Also tells the test movers where the player is and how it is moving
/// (for limited prediction), and holds the run seed for bounded run-to-run variation.
/// </summary>
public class TestAttackDirector : MonoBehaviour
{
    // Set while enabled; cleared on disable, so it never survives leaving Play mode.
    public static TestAttackDirector Active { get; private set; }

    [SerializeField] private TestDifficultyProfile profile;
    [SerializeField] private Transform player;
    [SerializeField] private TestPlayerHealth playerHealth;
    [SerializeField] private WaveEncounter encounter;
    [SerializeField] private GameObject shotPrefab;
    [Tooltip("Homing rocket prefab (TestHomingRocket). Empty = no rockets.")]
    [SerializeField] private GameObject rocketPrefab;

    /// <summary>An enemy started charging a gun attack (enemy position).</summary>
    public event Action<Vector3> AttackStarted;
    /// <summary>A shot left (origin, direction, aim point).</summary>
    public event Action<Vector3, Vector3, Vector3> ShotFired;
    /// <summary>An enemy started its rocket warning (enemy position).</summary>
    public event Action<Vector3> RocketWarningStarted;
    /// <summary>A rocket was launched.</summary>
    public event Action<TestHomingRocket> RocketLaunched;

    private float lastAttackTime = float.NegativeInfinity;
    private float lastRocketTime = float.NegativeInfinity;
    private readonly HashSet<int> pendingAttacks = new HashSet<int>();
    private int pendingRockets;
    private int nextAttackId = 1;
    private Vector3 lastPlayerPosition;
    private bool hasLastPlayerPosition;
    private int runCounter;

    public TestDifficultyProfile Profile => profile;
    public bool HasPlayer => player != null;
    public Vector3 PlayerPosition => player != null ? player.position : Vector3.zero;
    /// <summary>The player's measured velocity on the play plane (from how its transform moved).</summary>
    public Vector3 PlayerVelocity { get; private set; }
    /// <summary>Seed for this run's bounded variation; new on every enable and restart unless the profile fixes it.</summary>
    public int RunSeed { get; private set; }
    public int ActiveShotCount => GetComponentsInChildren<TestEnemyBullet>().Length;
    public int ActiveRocketCount => GetComponentsInChildren<TestHomingRocket>().Length;
    public int PendingAttacks => pendingAttacks.Count;
    public int PendingRockets => pendingRockets;
    public GameObject RocketPrefab => rocketPrefab;

    /// <summary>Gun attacks busy at once: attacks charging or firing, plus earlier bursts whose shots are still flying.</summary>
    public int BusyAttacks
    {
        get
        {
            var ids = new HashSet<int>(pendingAttacks);
            foreach (TestEnemyBullet b in GetComponentsInChildren<TestEnemyBullet>())
            {
                ids.Add(b.AttackId);
            }
            return ids.Count;
        }
    }

    /// <summary>Used by the scene builder and tests.</summary>
    public void Configure(TestDifficultyProfile newProfile, Transform target, TestPlayerHealth health, WaveEncounter newEncounter, GameObject shot,
        GameObject rocket = null)
    {
        profile = newProfile;
        player = target;
        playerHealth = health;
        encounter = newEncounter;
        shotPrefab = shot;
        if (rocket != null)
        {
            rocketPrefab = rocket;
        }
    }

    void OnEnable()
    {
        Active = this;
        ResetState();
    }

    void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
        ClearAll();
    }

    void Update()
    {
        if (player == null || Time.deltaTime <= 0f)
        {
            return;
        }
        Vector3 p = player.position;
        Vector3 v = hasLastPlayerPosition ? (p - lastPlayerPosition) / Time.deltaTime : Vector3.zero;
        v.y = 0f;
        PlayerVelocity = v.sqrMagnitude > 2500f ? Vector3.zero : v; // ignore teleports (restart)
        lastPlayerPosition = p;
        hasLastPlayerPosition = true;
    }

    private void ResetState()
    {
        lastAttackTime = lastRocketTime = float.NegativeInfinity;
        pendingAttacks.Clear();
        pendingRockets = 0;
        hasLastPlayerPosition = false;
        PlayerVelocity = Vector3.zero;
        runCounter++;
        RunSeed = profile != null && profile.fixedSeed != 0
            ? profile.fixedSeed
            : unchecked(Environment.TickCount * 397 ^ runCounter * 7919);
    }

    // Shared rules: a live player who is not recovering, a running encounter, an on-screen attacker far enough away.
    private bool BasicRulesAllow(Vector3 from, float minDistance)
    {
        if (profile == null || player == null)
        {
            return false;
        }
        if (playerHealth != null && (playerHealth.IsDefeated || (profile.holdFireWhilePlayerRecovers && playerHealth.IsInvulnerable)))
        {
            return false;
        }
        if (encounter != null && (encounter.IsFailed || encounter.IsCompleted))
        {
            return false;
        }
        // The fire window is measured in this object's space, so it follows a moving rig if the director is parented to it.
        Vector3 local = transform.InverseTransformPoint(from);
        if (local.z < profile.fireWindowZ.x || local.z > profile.fireWindowZ.y || Mathf.Abs(local.x) > profile.fireWindowX)
        {
            return false;
        }
        return Vector3.Distance(from, player.position) >= minDistance;
    }

    /// <summary>Reserves a gun attack (single shot or burst) if every fairness rule allows it. Returns its id, or 0.</summary>
    public int TryBeginAttack(Vector3 from)
    {
        if (shotPrefab == null || !BasicRulesAllow(from, profile != null ? profile.minFireDistance : 0f))
        {
            return 0;
        }
        if (Time.time - lastAttackTime < profile.minSecondsBetweenAttacks || BusyAttacks >= profile.maxShotsInFlight)
        {
            return 0;
        }
        lastAttackTime = Time.time;
        int id = nextAttackId++;
        pendingAttacks.Add(id);
        AttackStarted?.Invoke(from);
        return id;
    }

    /// <summary>A gun attack finished firing (its shots may still be flying) or its enemy was removed before firing.</summary>
    public void EndAttack(int attackId)
    {
        pendingAttacks.Remove(attackId);
    }

    /// <summary>Where to aim at the player, leading them by 'leadFactor' of their motion during the shot's flight (capped).</summary>
    public Vector3 PredictedAim(Vector3 from, float shotSpeed, float leadFactor, float maxLead)
    {
        Vector3 target = PlayerPosition;
        if (leadFactor <= 0f || shotSpeed <= 0f)
        {
            return target;
        }
        float flight = Vector3.Distance(from, target) / shotSpeed;
        Vector3 lead = Vector3.ClampMagnitude(PlayerVelocity * flight * leadFactor, maxLead);
        lead.y = 0f;
        return target + lead;
    }

    /// <summary>Fires one straight (non-homing) shot from 'from' towards 'aimPoint', as part of attack 'attackId'.</summary>
    public void Fire(Vector3 from, Vector3 aimPoint, float speed, int attackId = 0)
    {
        Vector3 direction = aimPoint - from;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.back;
        GameObject shot = Instantiate(shotPrefab, new Vector3(from.x, 0f, from.z), Quaternion.LookRotation(direction), transform);
        var bullet = shot.GetComponent<TestEnemyBullet>();
        if (bullet != null)
        {
            bullet.Launch(direction * speed, attackId);
        }
        if (TestFxManager.Active != null)
        {
            TestFxManager.Active.PlayEnemyShot(from);
        }
        ShotFired?.Invoke(from, direction, aimPoint);
    }

    /// <summary>Reserves a rocket launch if the rocket rules allow it (cap, gap, distance and the shared rules).</summary>
    public bool TryBeginRocket(Vector3 from)
    {
        if (rocketPrefab == null || !BasicRulesAllow(from, profile != null ? profile.minRocketDistance : 0f))
        {
            return false;
        }
        if (Time.time - lastRocketTime < profile.minSecondsBetweenRockets || ActiveRocketCount + pendingRockets >= profile.maxRocketsInFlight)
        {
            return false;
        }
        lastRocketTime = Time.time;
        pendingRockets++;
        RocketWarningStarted?.Invoke(from);
        return true;
    }

    /// <summary>A rocket carrier was removed during its warning.</summary>
    public void CancelRocket()
    {
        pendingRockets = Mathf.Max(0, pendingRockets - 1);
    }

    /// <summary>Launches a homing rocket from 'from' heading 'direction'; it homes on the player within its limits.</summary>
    public TestHomingRocket LaunchRocket(Vector3 from, Vector3 direction)
    {
        pendingRockets = Mathf.Max(0, pendingRockets - 1);
        direction.y = 0f;
        direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.back;
        GameObject go = Instantiate(rocketPrefab, new Vector3(from.x, 0f, from.z), Quaternion.LookRotation(direction), transform);
        var rocket = go.GetComponent<TestHomingRocket>();
        if (rocket != null)
        {
            rocket.Launch(direction, player);
        }
        if (TestFxManager.Active != null)
        {
            TestFxManager.Active.PlayRocketLaunch(from);
        }
        RocketLaunched?.Invoke(rocket);
        return rocket;
    }

    /// <summary>Removes every enemy shot and rocket and starts a new run seed (restart).</summary>
    public void ClearAll()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        ResetState();
    }
}
