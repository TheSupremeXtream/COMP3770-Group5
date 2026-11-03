using UnityEngine;

public enum TestShooterRole
{
    None,
    // Single aimed shot or a burst along a locked aim line (fighters; interceptors fire a quicker single shot).
    Gun,
    // One homing rocket after a red launch warning (heavies, or a group leader when the wave introduces rockets).
    Rocket
}

/// <summary>
/// PLACEHOLDER attacks for the test enemies (not Leonardo's AI or Taha's weapons). Each armed ship attacks once, only when
/// TestAttackDirector allows it, and always with a warning:
/// - Gun: the ship glows orange and shows an aim line that follows the player's predicted position (limited lead), the
///   line locks Aim Lock Seconds before firing, then a single shot or a short burst flies straight along it.
///   Interceptors fire one quick shot with a little more lead; fighters fire bursts.
/// - Rocket: the ship pulses red with a red lock line for the rocket warning time, then launches one homing rocket
///   (TestHomingRocket: limited turn rate, speed and homing time, so it can be dodged).
/// The role comes from the wave's TestDifficultyProfile entry and the ship's class / formation slot.
/// </summary>
public class TestEnemyShooter : MonoBehaviour, IWaveSpawnReceiver
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private LineRenderer aimLine;
    [SerializeField] private Transform model;
    [SerializeField] private Color chargeColour = new Color(1f, 0.45f, 0.1f);
    [SerializeField] private Color rocketColour = new Color(1f, 0.12f, 0.12f);
    [SerializeField] private float aimLineLength = 16f;

    private TestShooterRole role;
    private bool fired;
    private bool charging;
    private bool locked;
    private float timer;
    private float telegraphSeconds = 0.9f;
    private float shotSpeed = 8f;
    private int shotsPerBurst = 1;
    private float burstInterval = 0.14f;
    private float leadFactor;
    private float maxLead = 1.5f;
    private int attackId;
    private int shotsLeft;
    private float nextShotAt;
    private Vector3 lockedAim;
    private MeshRenderer[] renderers;
    private MaterialPropertyBlock block;

    public TestShooterRole Role => role;
    public bool Armed => role != TestShooterRole.None;
    public bool IsCharging => charging;
    public bool HasFired => fired;
    public int ShotsPerBurst => shotsPerBurst;
    public float TelegraphSeconds => telegraphSeconds;

    void Awake()
    {
        renderers = (model != null ? model : transform).GetComponentsInChildren<MeshRenderer>();
        block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        role = TestShooterRole.None;
        fired = charging = locked = false;
        timer = 0f;
        shotsLeft = 0;
        attackId = 0;
        HideTelegraph();
    }

    void OnDisable()
    {
        TestAttackDirector director = TestAttackDirector.Active;
        if (charging && director != null)
        {
            if (role == TestShooterRole.Rocket)
            {
                director.CancelRocket();
            }
            else
            {
                director.EndAttack(attackId);
            }
        }
        charging = false;
        HideTelegraph();
    }

    public void OnWaveSpawn(WaveSpawnInfo info)
    {
        TestDifficultyProfile profile = TestAttackDirector.Active != null ? TestAttackDirector.Active.Profile : null;
        TestWaveDifficulty wave = profile != null ? profile.ForWave(info.waveIndex) : null;
        var mover = GetComponent<TestEnemyMover>();
        TestEnemyClass enemyClass = mover != null ? mover.Class : TestEnemyClass.Fighter;
        Configure(wave, enemyClass, info.slotIndex);
    }

    /// <summary>Chooses this ship's role for a wave (used by OnWaveSpawn and tests).</summary>
    public void Configure(TestWaveDifficulty wave, TestEnemyClass enemyClass, int slotIndex)
    {
        role = TestShooterRole.None;
        if (wave == null)
        {
            return;
        }
        bool rocketClass = (wave.rocketClasses & TestEnemyMover.ToMask(enemyClass)) != 0;
        if (rocketClass || (wave.groupLeaderRocket && slotIndex == 0))
        {
            role = TestShooterRole.Rocket;
            telegraphSeconds = wave.rocketWarningSeconds;
            return;
        }
        int n = wave.aimedShooterEveryNth;
        if (n > 0 && slotIndex % n == n - 1)
        {
            bool interceptor = enemyClass == TestEnemyClass.Interceptor;
            Arm(true, interceptor ? Mathf.Max(0.6f, wave.telegraphSeconds * 0.8f) : wave.telegraphSeconds, wave.shotSpeed,
                interceptor ? 1 : wave.shotsPerBurst, wave.burstInterval,
                interceptor ? Mathf.Min(0.8f, wave.leadFactor + 0.25f) : wave.leadFactor, wave.maxLead);
        }
    }

    /// <summary>Arms a gun attack directly (tests).</summary>
    public void Arm(bool canShoot, float telegraph, float speed, int shots = 1, float interval = 0.14f, float lead = 0f, float leadCap = 1.5f)
    {
        role = canShoot ? TestShooterRole.Gun : TestShooterRole.None;
        telegraphSeconds = telegraph;
        shotSpeed = speed;
        shotsPerBurst = Mathf.Max(1, shots);
        burstInterval = interval;
        leadFactor = lead;
        maxLead = leadCap;
    }

    /// <summary>Arms a rocket attack directly (tests).</summary>
    public void ArmRocket(float warningSeconds)
    {
        role = TestShooterRole.Rocket;
        telegraphSeconds = warningSeconds;
    }

    void Update()
    {
        TestAttackDirector director = TestAttackDirector.Active;
        if (role == TestShooterRole.None || fired || director == null || muzzle == null)
        {
            return;
        }
        if (!charging)
        {
            bool allowed = role == TestShooterRole.Rocket
                ? director.TryBeginRocket(muzzle.position)
                : (attackId = director.TryBeginAttack(muzzle.position)) != 0;
            if (allowed)
            {
                charging = true;
                timer = 0f;
                locked = false;
                if (TestFxManager.Active != null)
                {
                    TestFxManager.Active.PlayEnemyCharge(role == TestShooterRole.Rocket);
                }
            }
            return;
        }

        timer += Time.deltaTime;
        if (role == TestShooterRole.Rocket)
        {
            if (timer >= telegraphSeconds)
            {
                Vector3 dir = director.PlayerPosition - muzzle.position;
                director.LaunchRocket(muzzle.position, dir);
                Finish();
                return;
            }
            ShowTelegraph(director.PlayerPosition, timer / telegraphSeconds, rocketColour);
            return;
        }

        // Gun: follow the (limited) predicted aim, lock it, then fire the shot or burst along the locked line.
        float lockSeconds = director.Profile != null ? director.Profile.aimLockSeconds : 0.3f;
        if (!locked && timer >= telegraphSeconds - lockSeconds)
        {
            locked = true;
            lockedAim = director.PredictedAim(muzzle.position, shotSpeed, leadFactor, maxLead);
            shotsLeft = shotsPerBurst;
            nextShotAt = telegraphSeconds;
        }
        Vector3 aim = locked ? lockedAim : director.PredictedAim(muzzle.position, shotSpeed, leadFactor, maxLead);
        if (locked && timer >= nextShotAt && shotsLeft > 0)
        {
            director.Fire(muzzle.position, aim, shotSpeed, attackId);
            shotsLeft--;
            nextShotAt += burstInterval;
            if (shotsLeft == 0)
            {
                director.EndAttack(attackId);
                Finish();
                return;
            }
        }
        ShowTelegraph(aim, Mathf.Clamp01(timer / telegraphSeconds), chargeColour);
    }

    private void Finish()
    {
        fired = true;
        charging = false;
        HideTelegraph();
    }

    private void ShowTelegraph(Vector3 aim, float progress, Color colour)
    {
        if (aimLine != null)
        {
            Vector3 from = muzzle.position;
            Vector3 dir = aim - from;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.back;
            aimLine.enabled = true;
            aimLine.SetPosition(0, from);
            aimLine.SetPosition(1, from + dir * aimLineLength);
            // Faint and thin while following; brighter, wider and steady once locked. Rockets: red and pulsing.
            bool steady = locked || role == TestShooterRole.Rocket;
            float alpha = steady ? 0.9f : 0.25f + 0.35f * progress;
            if (role == TestShooterRole.Rocket)
            {
                alpha *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(progress * 18f));
            }
            float width = steady ? 0.16f : 0.07f;
            aimLine.startColor = new Color(colour.r, colour.g, colour.b, alpha);
            aimLine.endColor = new Color(colour.r, colour.g, colour.b, 0f);
            aimLine.startWidth = width;
            aimLine.endWidth = width * 0.5f;
        }
        float pulse = 0.6f + 0.4f * Mathf.Sin(progress * 30f);
        foreach (MeshRenderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", colour * (0.6f + 2.4f * progress) * pulse);
            // Tint too: models with a plain material (e.g. Leonardo's default "Material") have no emission enabled.
            block.SetColor("_BaseColor", Color.Lerp(Color.white, colour, 0.65f * progress * pulse));
            r.SetPropertyBlock(block);
        }
    }

    private void HideTelegraph()
    {
        if (aimLine != null)
        {
            aimLine.enabled = false;
        }
        if (renderers == null)
        {
            return;
        }
        foreach (MeshRenderer r in renderers)
        {
            if (r != null)
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}
