using UnityEngine;

/// <summary>
/// Integrated demo only: holds the frame rate at a fixed value while the scene is running. Abel's PlayerController
/// currently moves by Speed per frame (not per second), so without a fixed frame rate the ship would be faster on
/// high-refresh monitors. Remove this once that movement is scaled by Time.deltaTime. Restores the previous settings
/// when disabled (leaving Play mode or unloading the scene).
/// </summary>
public class DemoFrameRateCap : MonoBehaviour
{
    [SerializeField] private int framesPerSecond = 60;

    private int previousTarget;
    private int previousVSync;

    public int FramesPerSecond => framesPerSecond;

    void OnEnable()
    {
        previousTarget = Application.targetFrameRate;
        previousVSync = QualitySettings.vSyncCount;
        QualitySettings.vSyncCount = 0; // otherwise VSync overrides the target frame rate
        Application.targetFrameRate = framesPerSecond;
    }

    void OnDisable()
    {
        Application.targetFrameRate = previousTarget;
        QualitySettings.vSyncCount = previousVSync;
    }
}
