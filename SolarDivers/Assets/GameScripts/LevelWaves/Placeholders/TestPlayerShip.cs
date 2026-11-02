using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PLACEHOLDER ship so the wave prototype can be played on main, where the team's player
/// (Abel, movement-rail-system branch) and weapons (Taha) are not merged yet. Swap it out when they are.
/// Disable the component to stop movement and shooting.
/// Handling: velocity eases towards the input (acceleration / deceleration per axis, exact over any frame rate);
/// limits and fire rate are the same as the first prototype; top speed is 20% higher.
/// </summary>
public class TestPlayerShip : MonoBehaviour
{
    [Tooltip("Top speed (units/s). Raised 20% from 8 after playtesting; crossing the 18-unit-wide play area takes about 2 s.")]
    [SerializeField] private float speed = 9.6f;
    [Tooltip("Units/s gained per second while steering. 75 reaches top speed in ~0.13 s.")]
    [SerializeField] private float acceleration = 75f;
    [Tooltip("Units/s lost per second when the stick is released or reversed. Higher = less sliding.")]
    [SerializeField] private float deceleration = 100f;
    [SerializeField] private Vector2 xLimits = new Vector2(-9f, 9f);
    [SerializeField] private Vector2 zLimits = new Vector2(-8f, 0f);
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCooldown = 0.2f;
    [Tooltip("Presentation only: brief flash object at the fire point (starts inactive).")]
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private float muzzleFlashSeconds = 0.06f;

    private readonly List<GameObject> projectiles = new List<GameObject>();
    private Vector3 startPosition;
    private Vector3 velocity;
    private float cooldown;
    private float flashTimer;
    private Vector3 baseFlashScale = Vector3.one;
    private Vector3 flashScale = Vector3.one;

    public Vector3 Velocity => velocity;
    public float MaxSpeed => speed;
    public Vector2 XLimits => xLimits;
    public Vector2 ZLimits => zLimits;

    void Awake()
    {
        startPosition = transform.position;
        if (muzzleFlash != null)
        {
            baseFlashScale = muzzleFlash.transform.localScale;
        }
    }

    void OnEnable()
    {
        cooldown = 0f;
        velocity = Vector3.zero;
        HideMuzzleFlash();
    }

    void OnDisable()
    {
        velocity = Vector3.zero;
        HideMuzzleFlash();
    }

    private void HideMuzzleFlash()
    {
        flashTimer = 0f;
        if (muzzleFlash != null)
        {
            muzzleFlash.SetActive(false);
        }
    }

    /// <summary>Back to the start position, at rest, with no shots in flight.</summary>
    public void ResetShip()
    {
        transform.position = startPosition;
        velocity = Vector3.zero;
        ClearProjectiles();
        cooldown = 0f;
        HideMuzzleFlash();
    }

    public void ClearProjectiles()
    {
        foreach (GameObject shot in projectiles)
        {
            if (shot != null)
            {
                Destroy(shot);
            }
        }
        projectiles.Clear();
    }

    public int ActiveProjectileCount
    {
        get
        {
            projectiles.RemoveAll(p => p == null);
            return projectiles.Count;
        }
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                HideMuzzleFlash();
            }
            else if (muzzleFlash != null)
            {
                // Shrinks over its short life so it reads as a pop, not a blob.
                muzzleFlash.transform.localScale = flashScale * (0.6f + 0.4f * flashTimer / muzzleFlashSeconds);
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector2 move = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
        Step(move, Time.deltaTime);

        cooldown -= Time.deltaTime;
        bool fire = keyboard.spaceKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed);
        if (fire && cooldown <= 0f)
        {
            Fire();
        }
    }

    /// <summary>One movement step for a given input (x = right, y = up the screen). Public so tests can drive it.</summary>
    public void Step(Vector2 input, float dt)
    {
        if (dt <= 0f)
        {
            return;
        }
        Vector2 target = Vector2.ClampMagnitude(input, 1f) * speed; // diagonals are not faster
        Vector3 start = transform.position;
        float dx = Integrate(ref velocity.x, target.x, dt);
        float dz = Integrate(ref velocity.z, target.y, dt);
        Vector3 position = start + new Vector3(dx, 0f, dz);
        // Hitting a limit stops movement into it, so there is no "sticky" push against the edge.
        if (position.x < xLimits.x || position.x > xLimits.y)
        {
            position.x = Mathf.Clamp(position.x, xLimits.x, xLimits.y);
            velocity.x = 0f;
        }
        if (position.z < zLimits.x || position.z > zLimits.y)
        {
            position.z = Mathf.Clamp(position.z, zLimits.x, zLimits.y);
            velocity.z = 0f;
        }
        transform.position = position;
    }

    // Moves v towards target at a constant rate and returns the exact distance covered this step,
    // so the result is the same at 30 or 240 fps.
    private float Integrate(ref float v, float target, float dt)
    {
        bool braking = Mathf.Approximately(target, 0f) || (v != 0f && Mathf.Sign(target) != Mathf.Sign(v));
        float rate = braking ? deceleration : acceleration;
        float gap = target - v;
        float rampTime = Mathf.Abs(gap) / rate;
        float distance;
        if (rampTime >= dt)
        {
            float newV = v + Mathf.Sign(gap) * rate * dt;
            distance = (v + newV) * 0.5f * dt;
            v = newV;
        }
        else
        {
            distance = (v + target) * 0.5f * rampTime + target * (dt - rampTime);
            v = target;
        }
        return distance;
    }

    public void Fire()
    {
        if (projectilePrefab == null)
        {
            return;
        }
        Transform from = firePoint != null ? firePoint : transform;
        projectiles.RemoveAll(p => p == null);
        projectiles.Add(Instantiate(projectilePrefab, from.position, from.rotation));
        cooldown = fireCooldown;
        if (muzzleFlash != null)
        {
            muzzleFlash.SetActive(true);
            // Small random spin and size so rapid fire does not look stamped.
            muzzleFlash.transform.localRotation = Quaternion.Euler(90f, 0f, Random.Range(-25f, 25f));
            flashScale = baseFlashScale * Random.Range(0.85f, 1.1f);
            muzzleFlash.transform.localScale = flashScale;
            flashTimer = muzzleFlashSeconds;
        }
        if (TestFxManager.Active != null)
        {
            TestFxManager.Active.PlayShot();
        }
    }
}
