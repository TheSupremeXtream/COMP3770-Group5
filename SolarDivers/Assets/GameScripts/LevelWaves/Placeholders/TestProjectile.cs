using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PLACEHOLDER projectile for wave testing only (Taha owns real weapons).
/// Needs a trigger collider and a kinematic Rigidbody. Visuals (bolt mesh, trail) are children/extra components.
/// </summary>
public class TestProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 25f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifetime = 2f;

    // Taha - Missile explosion settings
    // A radius of 0 keeps laser and plasma projectiles working normally
    // Missiles use this radius and scale for their impact explosion
    [SerializeField] private float explosionRadius = 0f;
    [SerializeField] private float explosionScale = 1f;

    private bool hasHit;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += transform.forward * (speed * Time.deltaTime);
    }

    // Old collision code kept here so the missile AOE change is easy to review.
    // It damaged nearby enemies once, but it did not show an impact explosion and could run twice.
    //private void OnTriggerEnter(Collider other)
    //{
    //    TestEnemyHealth enemy = other.GetComponentInParent<TestEnemyHealth>();
    //
    //    if (explosionRadius > 0f)
    //    {
    //        var hitEnemies = new HashSet<TestEnemyHealth>();
    //
    //        foreach (Collider hit in Physics.OverlapSphere(transform.position, explosionRadius))
    //        {
    //            TestEnemyHealth target = hit.GetComponentInParent<TestEnemyHealth>();
    //            if (target != null && hitEnemies.Add(target))
    //                target.TakeDamage(damage);
    //        }
    //
    //        Destroy(gameObject);
    //    }
    //    else if (enemy != null)
    //    {
    //        enemy.TakeDamage(damage);
    //        Destroy(gameObject);
    //    }
    //}

    // New collision code:
    // Laser and plasma still damage one enemy, while missiles explode at the exact impact point.
    // The HashSet makes sure an enemy with several colliders only takes AOE damage once.
    private void OnTriggerEnter(Collider other)
    {
        if (hasHit)
        {
            return;
        }

        TestEnemyHealth enemy = other.GetComponentInParent<TestEnemyHealth>();

        if (explosionRadius <= 0f)
        {
            if (enemy == null)
            {
                return;
            }

            hasHit = true;
            if (TestFxManager.Active != null)
            {
                TestFxManager.Active.PlayImpact(other.ClosestPoint(transform.position));
            }
            enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        hasHit = true;
        Vector3 impactPoint = other.ClosestPoint(transform.position);
        if (TestFxManager.Active != null)
        {
            TestFxManager.Active.PlayExplosion(impactPoint, explosionScale);
        }

        var hitEnemies = new HashSet<TestEnemyHealth>();
        foreach (Collider hit in Physics.OverlapSphere(impactPoint, explosionRadius))
        {
            TestEnemyHealth target = hit.GetComponentInParent<TestEnemyHealth>();
            if (target != null && hitEnemies.Add(target))
            {
                target.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }
}
