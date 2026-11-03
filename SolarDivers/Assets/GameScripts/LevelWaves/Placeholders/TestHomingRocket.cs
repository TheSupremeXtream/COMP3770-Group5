using UnityEngine;

/// <summary>
/// PLACEHOLDER homing rocket for the test enemies (not Taha's weapons or Leonardo's AI). Dodgeable by design:
/// it flies straight for Arming Seconds, then turns towards the player at no more than Turn Rate while it speeds up to
/// Max Speed, stops homing after Homing Seconds (burn-out: flies straight) and is removed after Lifetime or when it
/// leaves its area. A sharp sideways move inside its turning circle makes it miss. It can be shot down (TestEnemyHealth
/// on the same object) and hurts on contact (TestContactDamage). Moves in its parent's space, so it follows a moving rig.
/// Needs a trigger collider and a kinematic Rigidbody.
/// </summary>
public class TestHomingRocket : MonoBehaviour
{
    [SerializeField] private float launchSpeed = 3f;
    [SerializeField] private float maxSpeed = 6.5f;
    [Tooltip("Speed gained per second after launch.")]
    [SerializeField] private float acceleration = 4f;
    [Tooltip("Largest turn (degrees per second) while homing. 70 gives a ~5-unit turning circle at full speed.")]
    [SerializeField] private float turnRate = 70f;
    [Tooltip("Flies straight this long after launch before it starts homing.")]
    [SerializeField] private float armingSeconds = 0.35f;
    [Tooltip("Homing stops after this long (burn-out); it then flies straight.")]
    [SerializeField] private float homingSeconds = 2.6f;
    [SerializeField] private float lifetime = 5f;
    [Tooltip("Removed when it leaves this area (relative to the parent, i.e. the attack director).")]
    [SerializeField] private Bounds area = new Bounds(Vector3.zero, new Vector3(36f, 10f, 32f));

    [Header("Look and sound")]
    [Tooltip("Blinking warning light (optional).")]
    [SerializeField] private GameObject beacon;
    [SerializeField] private float beaconRate = 7f;
    [Tooltip("Looping motor sound (optional); its level and stereo position follow the rocket relative to the player. " +
             "It stops with the rocket (detonation, shot down, expiry, leaving the area, restart).")]
    [SerializeField] private AudioSource loop;
    [Range(0f, 1f)] [SerializeField] private float loopVolume = 0.5f;
    [SerializeField] private float hearingDistance = 14f;
    [Tooltip("Motor level (fraction) when the rocket is at the edge of hearing, so the motor is heard from the launch.")]
    [Range(0f, 1f)] [SerializeField] private float farLevel = 0.35f;
    [Tooltip("The motor fades in over this long, under the launch blast (which already contains the first burst of thrust).")]
    [SerializeField] private float motorFadeIn = 0.6f;

    private Transform target;
    private Vector2 heading;
    private float speed;
    private float age;
    private bool launched;

    public float Speed => speed;
    public float MaxSpeed => maxSpeed;
    public float TurnRate => turnRate;
    public float Age => age;
    public float Lifetime => lifetime;
    public float ArmingSeconds => armingSeconds;
    public float HomingSeconds => homingSeconds;
    public bool IsHoming => launched && age >= armingSeconds && age < armingSeconds + homingSeconds;
    public Vector2 Heading => heading;

    /// <summary>Starts the rocket heading 'direction' (world) towards 'homeOn'.</summary>
    public void Launch(Vector3 direction, Transform homeOn)
    {
        Vector3 local = transform.parent != null ? transform.parent.InverseTransformDirection(direction) : direction;
        heading = new Vector2(local.x, local.z);
        heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : Vector2.down;
        target = homeOn;
        speed = launchSpeed;
        age = 0f;
        launched = true;
        if (loop != null)
        {
            loop.volume = 0f;
            loop.Play();
        }
    }

    void Update()
    {
        Step(Time.deltaTime);
    }

    /// <summary>Advances the rocket by dt (fixed sub-steps, so the path is the same at any frame rate). Public for tests.</summary>
    public void Step(float dt)
    {
        if (!launched || dt <= 0f)
        {
            return;
        }
        int steps = Mathf.CeilToInt(dt / (1f / 120f));
        float h = dt / steps;
        Vector3 p = transform.localPosition;
        var pos = new Vector2(p.x, p.z);
        for (int i = 0; i < steps; i++)
        {
            age += h;
            speed = Mathf.MoveTowards(speed, maxSpeed, acceleration * h);
            if (IsHoming && target != null)
            {
                Vector3 t = transform.parent != null ? transform.parent.InverseTransformPoint(target.position) : target.position;
                Vector2 to = new Vector2(t.x, t.z) - pos;
                if (to.sqrMagnitude > 1e-4f)
                {
                    float turn = Mathf.Clamp(Vector2.SignedAngle(heading, to), -turnRate * h, turnRate * h);
                    float r = turn * Mathf.Deg2Rad;
                    heading = new Vector2(heading.x * Mathf.Cos(r) - heading.y * Mathf.Sin(r), heading.x * Mathf.Sin(r) + heading.y * Mathf.Cos(r)).normalized;
                }
            }
            pos += heading * (speed * h);
        }
        transform.localPosition = new Vector3(pos.x, p.y, pos.y);
        transform.localRotation = Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.y), Vector3.up);

        if (beacon != null)
        {
            beacon.SetActive(Mathf.Repeat(age * beaconRate, 1f) < 0.5f);
        }
        UpdateSound();
        if (age > lifetime || !area.Contains(transform.localPosition))
        {
            Destroy(gameObject);
        }
    }

    private void UpdateSound()
    {
        if (loop == null || target == null)
        {
            return;
        }
        Vector3 rel = transform.position - target.position;
        rel.y = 0f;
        float g = Mathf.Clamp01(1f - rel.magnitude / Mathf.Max(1f, hearingDistance));
        float engines = TestFxManager.Active != null ? TestFxManager.Active.EngineVolume : 1f;
        float fadeIn = motorFadeIn > 0f ? Mathf.Clamp01(age / motorFadeIn) : 1f;
        loop.volume = loopVolume * engines * fadeIn * Mathf.Lerp(farLevel, 1f, g * g);
        loop.panStereo = Mathf.Clamp(rel.x / 8f, -1f, 1f) * 0.8f;
        // Slightly higher while closing in, lower once it has passed (subtle, capped).
        Vector3 v = transform.forward * speed;
        float closing = rel.magnitude > 0.01f ? -Vector3.Dot(rel.normalized, v) : 0f;
        loop.pitch = 1f + 0.04f * Mathf.Clamp(closing / Mathf.Max(0.1f, maxSpeed), -1f, 1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        // TestPlayerHealth applies the damage from its own trigger callback; the rocket detonates here.
        if (other.GetComponentInParent<TestPlayerHealth>() != null)
        {
            if (TestFxManager.Active != null)
            {
                TestFxManager.Active.PlayRocketBlast(transform.position);
            }
            Destroy(gameObject);
        }
    }
}
