using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PLACEHOLDER game-flow glue for the wave test scene (Ronit owns real game management).
/// Player defeated -> encounter fails, ship stops. R -> full restart of ship, health, shots (player and enemy), effects, sounds and encounter.
/// </summary>
public class EncounterTestHarness : MonoBehaviour
{
    [SerializeField] private WaveEncounter encounter;
    [SerializeField] private TestPlayerHealth playerHealth;
    [SerializeField] private TestPlayerShip playerShip;
    [Tooltip("Optional placeholder effects/sound; cleared on restart.")]
    [SerializeField] private TestFxManager fx;
    [Tooltip("Optional camera shake on player damage/defeat.")]
    [SerializeField] private TestCameraShake cameraShake;
    [Tooltip("Optional entry warnings; cleared on restart.")]
    [SerializeField] private TestEntryWarnings entryWarnings;
    [Tooltip("Optional test enemy attack referee; its shots are removed on restart.")]
    [SerializeField] private TestAttackDirector attackDirector;
    [Tooltip("Optional engine / flyby sound; every loop is stopped on restart.")]
    [SerializeField] private TestFlightAudio flightAudio;
    [Tooltip("Extra components turned off on defeat and back on at restart, e.g. a teammate's player controller.")]
    [SerializeField] private Behaviour[] disableOnDefeat = new Behaviour[0];
    [Tooltip("Called first on every restart (R), before the ship and encounter reset, e.g. to put a rail rig back at its start.")]
    public UnityEngine.Events.UnityEvent onRestart = new UnityEngine.Events.UnityEvent();
    [Range(0f, 1f)] [SerializeField] private float damageShake = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float defeatShake = 0.9f;

    public WaveEncounter Encounter => encounter;
    public TestPlayerHealth PlayerHealth => playerHealth;

    /// <summary>Used by the scene builder and tests.</summary>
    public void Configure(WaveEncounter newEncounter, TestPlayerHealth health, TestPlayerShip ship, TestFxManager effects = null,
        TestCameraShake shake = null, TestEntryWarnings warnings = null, TestAttackDirector director = null, TestFlightAudio flight = null)
    {
        OnDisable();
        encounter = newEncounter;
        playerHealth = health;
        playerShip = ship;
        fx = effects;
        cameraShake = shake;
        entryWarnings = warnings;
        attackDirector = director;
        flightAudio = flight;
        OnEnable();
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.onDefeated.AddListener(HandleDefeat);
            playerHealth.onDamaged.AddListener(HandleDamaged);
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.onDefeated.RemoveListener(HandleDefeat);
            playerHealth.onDamaged.RemoveListener(HandleDamaged);
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartAll();
        }
    }

    private void HandleDamaged()
    {
        if (fx != null)
        {
            fx.PlayPlayerHit(playerHealth.transform.position);
        }
        if (cameraShake != null)
        {
            cameraShake.Shake(damageShake);
        }
    }

    private void HandleDefeat()
    {
        if (fx != null)
        {
            fx.PlayPlayerDestroyed(playerHealth.transform.position);
        }
        if (cameraShake != null)
        {
            cameraShake.Shake(defeatShake);
        }
        if (playerShip != null)
        {
            playerShip.enabled = false;
        }
        SetExtraControls(false);
        if (encounter != null)
        {
            encounter.FailEncounter("player defeated");
        }
    }

    /// <summary>Used by the demo scene builder: components switched off on defeat and back on at restart.</summary>
    public void SetDisableOnDefeat(params Behaviour[] behaviours)
    {
        disableOnDefeat = behaviours ?? new Behaviour[0];
    }

    private void SetExtraControls(bool on)
    {
        foreach (Behaviour b in disableOnDefeat)
        {
            if (b != null)
            {
                b.enabled = on;
            }
        }
    }

    public void RestartAll()
    {
        onRestart.Invoke();
        if (fx != null)
        {
            fx.ClearAll();
        }
        if (cameraShake != null)
        {
            cameraShake.ResetShake();
        }
        if (entryWarnings != null)
        {
            entryWarnings.ClearAll();
        }
        if (attackDirector != null)
        {
            attackDirector.ClearAll();
        }
        if (flightAudio != null)
        {
            flightAudio.ClearAll();
        }
        if (playerShip != null)
        {
            playerShip.ResetShip();
            playerShip.enabled = true;
        }
        SetExtraControls(true);
        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
        }
        if (encounter != null)
        {
            encounter.RestartEncounter();
        }
    }
}
