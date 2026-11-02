using UnityEngine;

/// <summary>
/// PLACEHOLDER visual: rolls (and optionally yaws/pitches) the ship model into its movement and eases back to level.
/// Reads how the ship actually moves, so it works for the player and the test enemies without touching their movement.
/// Only the visual child rotates; the root and its hitbox never do.
/// </summary>
public class TestShipBanking : MonoBehaviour
{
    [SerializeField] private Transform model;
    [SerializeField] private float maxBankDegrees = 25f;
    [Tooltip("Sideways speed (units/s) that gives full bank.")]
    [SerializeField] private float fullBankSpeed = 6f;
    [Tooltip("Turn the nose into sideways movement (0 = off).")]
    [SerializeField] private float maxYawDegrees = 0f;
    [Tooltip("Tilt the nose with forward/back movement (0 = off).")]
    [SerializeField] private float maxPitchDegrees = 0f;
    [SerializeField] private float fullPitchSpeed = 8f;
    [Tooltip("For ships whose root turns to face its heading (test enemies): turn rate (degrees/s) that gives full bank. 0 = off.")]
    [SerializeField] private float fullBankTurnRate = 0f;
    [Tooltip("How quickly the model follows (higher = snappier). Frame-rate independent.")]
    [SerializeField] private float responsiveness = 9f;

    private Vector3 lastPosition;
    private Vector3 lastForward;
    private float roll;
    private float yaw;
    private float pitch;

    public float Roll => roll;
    public float Yaw => yaw;
    public Transform Model => model;

    void OnEnable()
    {
        lastPosition = transform.position;
        lastForward = transform.forward;
        roll = yaw = pitch = 0f;
        if (model != null)
        {
            model.localRotation = Quaternion.identity;
        }
    }

    void LateUpdate()
    {
        if (model == null || Time.deltaTime <= 0f)
        {
            return;
        }
        Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
        // Ignore teleports (restart) so the ship does not snap into a full bank.
        if (velocity.sqrMagnitude > 2500f)
        {
            velocity = Vector3.zero;
        }
        float lateral = Vector3.Dot(velocity, transform.right);
        float ahead = Vector3.Dot(velocity, transform.forward);
        float targetRoll = -Mathf.Clamp(lateral / fullBankSpeed, -1f, 1f) * maxBankDegrees;
        if (fullBankTurnRate > 0f)
        {
            // Roll into the turn (yaw rate of the root, which faces the heading).
            float turn = Vector3.SignedAngle(lastForward, transform.forward, Vector3.up) / Time.deltaTime;
            targetRoll = Mathf.Clamp(targetRoll - Mathf.Clamp(turn / fullBankTurnRate, -1f, 1f) * maxBankDegrees, -maxBankDegrees, maxBankDegrees);
        }
        lastForward = transform.forward;
        float targetYaw = maxYawDegrees > 0f
            ? Mathf.Clamp(Mathf.Atan2(lateral, Mathf.Max(0.5f, Mathf.Abs(ahead))) * Mathf.Rad2Deg, -maxYawDegrees, maxYawDegrees)
            : 0f;
        float targetPitch = -Mathf.Clamp(ahead / fullPitchSpeed, -1f, 1f) * maxPitchDegrees;
        float blend = 1f - Mathf.Exp(-responsiveness * Time.deltaTime);
        roll = Mathf.Lerp(roll, targetRoll, blend);
        yaw = Mathf.Lerp(yaw, targetYaw, blend);
        pitch = Mathf.Lerp(pitch, targetPitch, blend);
        model.localRotation = Quaternion.Euler(pitch, yaw, roll);
    }
}
