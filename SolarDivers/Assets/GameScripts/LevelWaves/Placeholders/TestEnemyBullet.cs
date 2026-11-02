using UnityEngine;

/// <summary>
/// PLACEHOLDER enemy shot: flies in a straight line (no homing), hurts through TestContactDamage on the same object,
/// disappears on touching the player, after its lifetime, or once it leaves the play area.
/// Needs a trigger collider and a kinematic Rigidbody.
/// </summary>
public class TestEnemyBullet : MonoBehaviour
{
    [SerializeField] private float lifetime = 4f;
    [Tooltip("Removed when it leaves this world-space area.")]
    [SerializeField] private Bounds area = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(36f, 10f, 32f));

    private float age;

    public Vector3 Velocity { get; private set; }

    public void Launch(Vector3 velocity)
    {
        Velocity = velocity;
        age = 0f;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position += Velocity * Time.deltaTime;
        if (age > lifetime || !area.Contains(transform.position))
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // TestPlayerHealth applies the damage from its own trigger callback; the shot just ends here.
        if (other.GetComponentInParent<TestPlayerHealth>() != null)
        {
            Destroy(gameObject);
        }
    }
}
