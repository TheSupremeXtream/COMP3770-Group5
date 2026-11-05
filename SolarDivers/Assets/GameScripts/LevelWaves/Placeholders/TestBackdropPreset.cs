using UnityEngine;

/// <summary>
/// PLACEHOLDER scenery switch for the wave test scene: one backdrop at a time (planet + moon, or the Sun),
/// with the key light turned to match it. Decoration only; nothing here has colliders.
/// </summary>
public class TestBackdropPreset : MonoBehaviour
{
    public enum Preset
    {
        PlanetAndMoon,
        Sun
    }

    [SerializeField] private Preset preset = Preset.PlanetAndMoon;
    [SerializeField] private GameObject planetAndMoon;
    [SerializeField] private GameObject sun;
    [SerializeField] private Light keyLight;
    [SerializeField] private Vector3 planetLightEuler = new Vector3(52f, 150f, 0f);
    [SerializeField] private Vector3 sunLightEuler = new Vector3(52f, 215f, 0f);

    public Preset Current => preset;

    void OnEnable()
    {
        Apply(preset);
    }

    [ContextMenu("Apply Preset")]
    private void ApplyFromMenu()
    {
        Apply(preset);
    }

    public void Apply(Preset newPreset)
    {
        preset = newPreset;
        if (planetAndMoon != null)
        {
            planetAndMoon.SetActive(preset == Preset.PlanetAndMoon);
        }
        if (sun != null)
        {
            sun.SetActive(preset == Preset.Sun);
        }
        if (keyLight != null)
        {
            keyLight.transform.rotation = Quaternion.Euler(preset == Preset.Sun ? sunLightEuler : planetLightEuler);
        }
    }
}
