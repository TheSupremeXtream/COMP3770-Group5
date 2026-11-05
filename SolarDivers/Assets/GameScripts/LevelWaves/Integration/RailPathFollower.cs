using UnityEngine;

/// <summary>
/// Integrated demo: makes the RailRig (camera, player, boundaries, spawns, bounds space) follow a path object that
/// Abel's LevelCamera moves along its KeyFrames. His component is used as-is; this adapter only adds what the
/// encounter needs on top of it:
/// - it runs before other scripts and calls Physics.SyncTransforms() after moving the rig, so the player's
///   CharacterController (Abel's PlayerController) rides along instead of being left behind (auto-sync is off);
/// - ResetToStart (wired to R) puts the path and the rig back at the start, since LevelCamera has no reset.
/// The rig lags the path by at most one frame (it reads the position LevelCamera set last frame).
/// </summary>
[DefaultExecutionOrder(-200)]
public class RailPathFollower : MonoBehaviour
{
    [Tooltip("The object Abel's LevelCamera moves (must not be a child of this rig).")]
    [SerializeField] private Transform path;

    private Vector3 offset;
    private Vector3 pathStart;
    private Vector3 rigStart;
    private bool ready;

    public Transform Path => path;
    public float Travelled => ready ? Vector3.Distance(rigStart, transform.position) : 0f;

    /// <summary>Used by the scene builder and tests.</summary>
    public void Configure(Transform pathObject)
    {
        path = pathObject;
        Capture();
    }

    void Awake()
    {
        Capture();
    }

    private void Capture()
    {
        if (path == null)
        {
            return;
        }
        rigStart = transform.position;
        pathStart = path.position;
        offset = rigStart - pathStart;
        ready = true;
    }

    void Update()
    {
        if (!ready)
        {
            return;
        }
        Vector3 target = path.position + offset;
        if ((target - transform.position).sqrMagnitude > 1e-10f)
        {
            transform.position = target;
            Physics.SyncTransforms();
        }
    }

    /// <summary>Back to where the path and the rig started (restart).</summary>
    public void ResetToStart()
    {
        if (!ready)
        {
            return;
        }
        path.position = pathStart;
        transform.position = rigStart;
        Physics.SyncTransforms();
    }
}
