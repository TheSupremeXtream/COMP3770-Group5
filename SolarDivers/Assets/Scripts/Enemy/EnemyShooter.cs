
using UnityEngine;
using System.Collections;

public class EnemyShooter : MonoBehaviour
{
    [Header("Shooting")]
    public GameObject projectilePrefab;

    [Header("Settings")]
    public float shootingInterval = 2f;
    public float projectileDamage = 10f;

    void Start()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("Missing projectile prefab!");
            enabled = false;
            return;
        }

        StartCoroutine(ShootingLoop());
    }

    IEnumerator ShootingLoop()
    {
        while (true)
        {
            Shoot();

            yield return new WaitForSeconds(shootingInterval);
        }
    }

    void Shoot()
    {
        Vector3 direction = -transform.up;

        GameObject projectile = Instantiate(
            projectilePrefab,
            transform.position,
            transform.rotation
        );

        EnemyProjectile projectileScript =
            projectile.GetComponent<EnemyProjectile>();

        if (projectileScript != null)
        {
            projectileScript.Initialize(direction, projectileDamage);
        }
    }
}