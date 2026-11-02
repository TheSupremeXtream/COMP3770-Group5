using System;
using UnityEngine;

/// <summary>
/// PLACEHOLDER: referee for the test enemies' aimed shots (not Leonardo's AI). Enemies ask before charging a shot;
/// the director enforces the fairness rules of the difficulty profile (shots in flight, gap between attacks, distance,
/// on-screen window, no shots while the player recovers or after defeat). Enemy shots are parented here so a restart
/// can remove them. Also tells the test movers where the player is.
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

    /// <summary>An enemy started charging (enemy position).</summary>
    public event Action<Vector3> AttackStarted;
    /// <summary>A shot left (origin, direction, aim point).</summary>
    public event Action<Vector3, Vector3, Vector3> ShotFired;

    private float lastAttackTime = float.NegativeInfinity;
    private int pending;

    public TestDifficultyProfile Profile => profile;
    public bool HasPlayer => player != null;
    public Vector3 PlayerPosition => player != null ? player.position : Vector3.zero;
    public int ActiveShotCount => transform.childCount;
    public int PendingAttacks => pending;

    /// <summary>Used by the scene builder and tests.</summary>
    public void Configure(TestDifficultyProfile newProfile, Transform target, TestPlayerHealth health, WaveEncounter newEncounter, GameObject shot)
    {
        profile = newProfile;
        player = target;
        playerHealth = health;
        encounter = newEncounter;
        shotPrefab = shot;
    }

    void OnEnable()
    {
        Active = this;
        lastAttackTime = float.NegativeInfinity;
        pending = 0;
    }

    void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
        ClearAll();
    }

    /// <summary>Reserves an attack if every fairness rule allows it right now.</summary>
    public bool TryBeginAttack(Vector3 from)
    {
        if (profile == null || player == null || shotPrefab == null)
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
        if (Time.time - lastAttackTime < profile.minSecondsBetweenAttacks || ActiveShotCount + pending >= profile.maxShotsInFlight)
        {
            return false;
        }
        if (from.z < profile.fireWindowZ.x || from.z > profile.fireWindowZ.y || Mathf.Abs(from.x) > profile.fireWindowX)
        {
            return false;
        }
        if (Vector3.Distance(from, player.position) < profile.minFireDistance)
        {
            return false;
        }
        lastAttackTime = Time.time;
        pending++;
        AttackStarted?.Invoke(from);
        return true;
    }

    /// <summary>A charging enemy was destroyed or removed before it fired.</summary>
    public void CancelAttack()
    {
        pending = Mathf.Max(0, pending - 1);
    }

    /// <summary>Fires one straight (non-homing) shot from 'from' towards 'aimPoint'.</summary>
    public void Fire(Vector3 from, Vector3 aimPoint, float speed)
    {
        pending = Mathf.Max(0, pending - 1);
        Vector3 direction = aimPoint - from;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.back;
        GameObject shot = Instantiate(shotPrefab, new Vector3(from.x, 0f, from.z), Quaternion.LookRotation(direction), transform);
        shot.GetComponent<TestEnemyBullet>()?.Launch(direction * speed);
        if (TestFxManager.Active != null)
        {
            TestFxManager.Active.PlayEnemyShot();
        }
        ShotFired?.Invoke(from, direction, aimPoint);
    }

    /// <summary>Removes every enemy shot (restart).</summary>
    public void ClearAll()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        pending = 0;
        lastAttackTime = float.NegativeInfinity;
    }
}
