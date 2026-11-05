using UnityEngine;

/// <summary>
/// PLACEHOLDER scenery: drifts a decorative object down the screen and wraps it to the top,
/// suggesting forward travel. Decoration only: no collider, never damages the player.
/// </summary>
public class TestBackgroundDrift : MonoBehaviour
{
    [SerializeField] private float speed = 1f;
    [SerializeField] private float wrapBelowZ = -14f;
    [SerializeField] private float wrapToZ = 16f;
    [SerializeField] private Vector3 spinDegreesPerSecond = new Vector3(0f, 12f, 6f);

    void Update()
    {
        Vector3 p = transform.position;
        p.z -= speed * Time.deltaTime;
        if (p.z < wrapBelowZ)
        {
            p.z += wrapToZ - wrapBelowZ;
        }
        transform.position = p;
        transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
