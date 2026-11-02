
using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifetime = 5f;

    private Vector3 direction;
    private float damage;

    public void Initialize(Vector3 direction, float damage)
    {
        this.direction = direction.normalized;
        this.damage = damage;

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }

    /*void OnTriggerEnter(Collider other)
    {
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            player.TakeDamage(damage);
            Destroy(gameObject);
        }
    }*/
}
