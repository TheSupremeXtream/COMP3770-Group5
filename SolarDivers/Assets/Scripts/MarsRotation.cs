using UnityEngine;

public class MarsRotation : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 1f, 0f);
    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }
}
