using UnityEngine;

/// <summary>
/// PLACEHOLDER camera shake for the wave test scene. Short, decaying, optional (untick Shake Enabled).
/// The offset is added on top of wherever the camera is and removed again, so it never drifts the camera.
/// The HUD renders through its own camera and does not shake.
/// </summary>
public class TestCameraShake : MonoBehaviour
{
    [SerializeField] private bool shakeEnabled = true;
    [Tooltip("Largest offset at full strength (world units).")]
    [SerializeField] private float maxOffset = 0.3f;
    [SerializeField] private float frequency = 24f;
    [Tooltip("Seconds for a full-strength shake to die away.")]
    [SerializeField] private float duration = 0.35f;

    private float trauma;
    private Vector3 appliedOffset;
    private float seed;

    public bool ShakeEnabled
    {
        get => shakeEnabled;
        set
        {
            shakeEnabled = value;
            if (!value)
            {
                ResetShake();
            }
        }
    }

    public float Strength => trauma;
    public Vector3 CurrentOffset => appliedOffset;

    void OnEnable()
    {
        seed = Random.Range(0f, 100f);
        trauma = 0f;
        appliedOffset = Vector3.zero;
    }

    void OnDisable()
    {
        ResetShake();
    }

    /// <summary>0..1. Strength is squared, so small hits stay subtle.</summary>
    public void Shake(float strength)
    {
        if (shakeEnabled && isActiveAndEnabled)
        {
            trauma = Mathf.Clamp01(Mathf.Max(trauma, strength));
        }
    }

    public void ResetShake()
    {
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
        trauma = 0f;
    }

    void LateUpdate()
    {
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
        if (trauma <= 0f)
        {
            return;
        }
        trauma = Mathf.Max(0f, trauma - Time.deltaTime / Mathf.Max(0.01f, duration));
        float t = Time.time * frequency;
        float amount = maxOffset * trauma * trauma;
        // Shake in the screen plane only (camera right/up), never towards the scene.
        appliedOffset = Vector3.ClampMagnitude((transform.right * (Mathf.PerlinNoise(seed, t) * 2f - 1f) +
                                                transform.up * (Mathf.PerlinNoise(seed + 17f, t) * 2f - 1f)) * amount, amount);
        transform.position += appliedOffset;
    }
}
