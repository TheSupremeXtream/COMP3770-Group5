using UnityEngine;

/// <summary>
/// PLACEHOLDER aimed attack for the test enemies (not Leonardo's AI). Only armed when the wave's difficulty says so.
/// One shot per enemy, never more. When TestAttackDirector allows it, the ship glows and shows an aim line that follows
/// the player, the line locks Aim Lock Seconds before firing, and the shot flies straight at that locked point.
/// </summary>
public class TestEnemyShooter : MonoBehaviour, IWaveSpawnReceiver
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private LineRenderer aimLine;
    [SerializeField] private Transform model;
    [SerializeField] private Color chargeColour = new Color(1f, 0.45f, 0.1f);
    [SerializeField] private float aimLineLength = 16f;

    private bool armed;
    private bool fired;
    private bool charging;
    private bool locked;
    private float timer;
    private float telegraphSeconds = 0.9f;
    private float shotSpeed = 8f;
    private Vector3 lockedAim;
    private MeshRenderer[] renderers;
    private MaterialPropertyBlock block;

    public bool Armed => armed;
    public bool IsCharging => charging;
    public bool HasFired => fired;

    void Awake()
    {
        renderers = (model != null ? model : transform).GetComponentsInChildren<MeshRenderer>();
        block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        armed = fired = charging = locked = false;
        timer = 0f;
        HideTelegraph();
    }

    void OnDisable()
    {
        if (charging && TestAttackDirector.Active != null)
        {
            TestAttackDirector.Active.CancelAttack();
        }
        charging = false;
        HideTelegraph();
    }

    public void OnWaveSpawn(WaveSpawnInfo info)
    {
        TestDifficultyProfile profile = TestAttackDirector.Active != null ? TestAttackDirector.Active.Profile : null;
        TestWaveDifficulty difficulty = profile != null ? profile.ForWave(info.waveIndex) : null;
        if (difficulty == null || difficulty.aimedShooterEveryNth <= 0)
        {
            armed = false;
            return;
        }
        int n = difficulty.aimedShooterEveryNth;
        Arm(info.slotIndex % n == n - 1, difficulty.telegraphSeconds, difficulty.shotSpeed);
    }

    /// <summary>Used by OnWaveSpawn and tests.</summary>
    public void Arm(bool canShoot, float telegraph, float speed)
    {
        armed = canShoot;
        telegraphSeconds = telegraph;
        shotSpeed = speed;
    }

    void Update()
    {
        TestAttackDirector director = TestAttackDirector.Active;
        if (!armed || fired || director == null || muzzle == null)
        {
            return;
        }
        if (!charging)
        {
            if (director.TryBeginAttack(muzzle.position))
            {
                charging = true;
                timer = 0f;
                locked = false;
                if (TestFxManager.Active != null)
                {
                    TestFxManager.Active.PlayEnemyCharge();
                }
            }
            return;
        }

        timer += Time.deltaTime;
        float lockSeconds = director.Profile != null ? director.Profile.aimLockSeconds : 0.3f;
        if (!locked && timer >= telegraphSeconds - lockSeconds)
        {
            locked = true;
            lockedAim = director.PlayerPosition;
        }
        Vector3 aim = locked ? lockedAim : director.PlayerPosition;
        if (timer >= telegraphSeconds)
        {
            director.Fire(muzzle.position, aim, shotSpeed);
            fired = true;
            charging = false;
            HideTelegraph();
            return;
        }
        ShowTelegraph(aim, timer / telegraphSeconds);
    }

    private void ShowTelegraph(Vector3 aim, float progress)
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
            // Faint and thin while following; brighter, wider and steady once locked.
            float alpha = locked ? 0.9f : 0.25f + 0.35f * progress;
            float width = locked ? 0.16f : 0.07f;
            var start = new Color(chargeColour.r, chargeColour.g, chargeColour.b, alpha);
            aimLine.startColor = start;
            aimLine.endColor = new Color(chargeColour.r, chargeColour.g, chargeColour.b, 0f);
            aimLine.startWidth = width;
            aimLine.endWidth = width * 0.5f;
        }
        float pulse = 0.6f + 0.4f * Mathf.Sin(progress * 30f);
        foreach (MeshRenderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", chargeColour * (0.6f + 2.4f * progress) * pulse);
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
