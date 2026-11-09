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
    [Tooltip("Off = weapon only: another component moves the ship (the integrated demo uses Abel's PlayerController); " +
             "this one only fires and resets the position on restart.")]
    [SerializeField] private bool handleMovement = true;
    [Tooltip("Top speed (units/s). Raised 20% from 8 after playtesting; crossing the 18-unit-wide play area takes about 2 s.")]
    [SerializeField] private float speed = 9.6f;
    [Tooltip("Units/s gained per second while steering. 75 reaches top speed in ~0.13 s.")]
    [SerializeField] private float acceleration = 75f;
    [Tooltip("Units/s lost per second when the stick is released or reversed. Higher = less sliding.")]
    [SerializeField] private float deceleration = 100f;
    [SerializeField] private Vector2 xLimits = new Vector2(-9f, 9f);
    [SerializeField] private Vector2 zLimits = new Vector2(-8f, 0f);
    [SerializeField] private GameObject projectilePrefab;


    // Taha Hammoud, added Nov-03
    // Adding the Plasma weapon settings, after this we have 2/3 weapons
    // Has its own projectile, cooldown, and limited ammo
    // We'll use these when switching between laser and plasma
    [SerializeField] private GameObject plasmaProjectilePrefab;
    [SerializeField] private float plasmaFireCooldown = 0.5f;
    [SerializeField] private int plasmaAmmo = 33;
    private bool usingPlasma = false;
    // Keeps track of whether missiles are selected
    // We'll use this for switching between all three weapons
    private bool usingMissiles = false;
    // Taha, Missile launcher settings
    // Third weapon, with its own projectile and limited ammo
    // Slower firing rate, but deals more damage
    [SerializeField] private GameObject missileProjectilePrefab;
    [SerializeField] private float missileFireCooldown = 2f;
    [SerializeField] private int missileAmmo = 6;








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

    public bool HandlesMovement => handleMovement;

    /// <summary>Back to the start position, at rest, with no shots in flight.</summary>
    public void ResetShip()
    {
        // A CharacterController keeps its own position (auto-sync transforms is off), so it must be off while teleporting.
        var controller = GetComponent<CharacterController>();
        bool controllerWasOn = controller != null && controller.enabled;
        if (controllerWasOn)
        {
            controller.enabled = false;
        }
        transform.position = startPosition;
        if (controllerWasOn)
        {
            controller.enabled = true;
        }
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




    // Taha - manual edit
    // Tracks which weapon is selected, 0 = laser, etc
    // Switching weapons also resets the firing cooldown
    private int CurrentWeapon => usingMissiles ? 2 : usingPlasma ? 1 : 0;
    private void SelectWeapon(int weapon)
    {
        usingPlasma = weapon == 1;
        usingMissiles = weapon == 2;
        cooldown = 0f;
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

        /*
        if (handleMovement)
        {
            Vector2 move = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
            Step(move, Time.deltaTime);
        }
        */



                //// Switching from weapons using 1 and 2 on MNK, eventually 3 for missils. 
                //// Reset the firing timer so switching feels responsive
                //// place holder, need to ADD CONTROLER SUPPORT - done - Taha
                //// The top face button on a controller toggles weapons (Y on Xbox, X on Nintendo, Square on PlayStation)
                //// Reset the cooldown when switching
                //Gamepad gamepad = Gamepad.current;
                //if (keyboard.digit1Key.wasPressedThisFrame)
                //    usingPlasma = false;
                //if (keyboard.digit2Key.wasPressedThisFrame)
                //    usingPlasma = true;
                //if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
                //    usingPlasma = !usingPlasma;
        // Switch between laser, plasma, and missiles
        // Keyboard uses 1, 2, 3
        // Controller's top face button cycles through all three
        Gamepad gamepad = Gamepad.current;

        if (keyboard.digit1Key.wasPressedThisFrame)
            SelectWeapon(0);

        if (keyboard.digit2Key.wasPressedThisFrame)
            SelectWeapon(1);

        if (keyboard.digit3Key.wasPressedThisFrame)
            SelectWeapon(2);

        if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
            SelectWeapon((CurrentWeapon + 1) % 3);




        cooldown -= Time.deltaTime;
        bool fire = keyboard.spaceKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed);
        if (fire && cooldown <= 0f)
        {
            Fire();
        }
    }

    /* ALREADY BEING HANDLED BY PLAYERCONTROLLER
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
    */

    public void Fire()
    {
        if (projectilePrefab == null)
        {
            return;
        }
        Transform from = firePoint != null ? firePoint : transform;
        projectiles.RemoveAll(p => p == null);




        // projectiles.Add(Instantiate(projectilePrefab, from.position, from.rotation)); 
        // updated to implement choosing the right projectile based on the selected weapon, right now in test we have the 1 plaster
        // plasma uses its own prefab and has limited ammo
        // GameObject selectedProjectile = usingPlasma ? plasmaProjectilePrefab : projectilePrefab;
        // Pick the projectile for whichever weapon is selected
        // Laser, plasma, and missiles each have their own prefab
        GameObject selectedProjectile = usingMissiles ? missileProjectilePrefab
            : usingPlasma ? plasmaProjectilePrefab : projectilePrefab;
        //if (selectedProjectile == null || (usingPlasma && plasmaAmmo <= 0))
        //    return;
        //projectiles.Add(Instantiate(selectedProjectile, from.position, from.rotation));
        //if (usingPlasma)
        //    plasmaAmmo--;

    // Check ammo before firing
    // Each special weapon uses its own ammo supply
    if (selectedProjectile == null ||
        (usingPlasma && plasmaAmmo <= 0) ||
        (usingMissiles && missileAmmo <= 0))
        return;

    projectiles.Add(Instantiate(selectedProjectile, from.position, from.rotation));

    if (usingPlasma)
        plasmaAmmo--;
    if (usingMissiles)
        missileAmmo--;

        //cooldown = fireCooldown;
        // implementing the funtion so that each weapon has its own firing speed
        // Plasma fires slower than the regular laser - remember to add MISSILES NEXT
        // Each weapon has its own firing speed
        // Missiles are the slowest, followed by plasma and laser
        cooldown = usingMissiles ? missileFireCooldown
            : usingPlasma ? plasmaFireCooldown : fireCooldown;









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
