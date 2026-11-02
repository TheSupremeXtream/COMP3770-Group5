using UnityEngine;

/// <summary>
/// PLACEHOLDER rail motion for the integrated demo: moves this rig (camera, player, spawn origin, bounds space) forward
/// at a constant speed, standing in for Abel's LevelCamera until its keyframe movement is finished. Speed 0 = static
/// (the default). Lets us check that spawns, bounds and enemy routes follow a moving rig. ResetToStart is wired to R.
/// Runs before other scripts and syncs physics after moving: a CharacterController child (Abel's player) keeps its own
/// physics position and would otherwise be left behind, because this project does not auto-sync transforms.
/// The real rail camera will need the same Physics.SyncTransforms() call (or to move the player explicitly).
/// </summary>
[DefaultExecutionOrder(-200)]
public class TestRailScroll : MonoBehaviour
{
    [Tooltip("Units per second along this object's forward (+Z). 0 = no movement.")]
    [SerializeField] private float speed = 0f;

    private Vector3 startPosition;
    private bool started;

    public float Speed
    {
        get => speed;
        set => speed = value;
    }

    public float Travelled => started ? Vector3.Distance(startPosition, transform.position) : 0f;

    void Awake()
    {
        startPosition = transform.position;
        started = true;
    }

    void Update()
    {
        if (speed != 0f)
        {
            transform.position += transform.forward * (speed * Time.deltaTime);
            Physics.SyncTransforms();
        }
    }

    /// <summary>Back to where the rig started (restart).</summary>
    public void ResetToStart()
    {
        if (started)
        {
            transform.position = startPosition;
            Physics.SyncTransforms();
        }
    }
}
