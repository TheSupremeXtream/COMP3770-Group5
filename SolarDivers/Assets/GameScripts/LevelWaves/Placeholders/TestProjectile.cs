using UnityEngine;

/// <summary>
/// PLACEHOLDER projectile for wave testing only (Taha owns real weapons).
/// Needs a trigger collider and a kinematic Rigidbody. Visuals (bolt mesh, trail) are children/extra components;
/// speed, damage and lifetime are unchanged from the first prototype.
/// </summary>
public class TestProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 25f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifetime = 2f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += transform.forward * (speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        TestEnemyHealth enemy = other.GetComponentInParent<TestEnemyHealth>();
        if (enemy != null)
        {
            if (TestFxManager.Active != null)
            {
                TestFxManager.Active.PlayImpact(other.ClosestPoint(transform.position));
            }
            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
