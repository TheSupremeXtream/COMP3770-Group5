using UnityEngine;

/// <summary>
/// PLACEHOLDER health so test projectiles can destroy test enemies.
/// Replace with the team's real damage/health system when it exists.
/// Presentation: short white flash when hit, explosion effect when destroyed by damage
/// (enemies that escape or time out are removed silently by WaveEncounter).
/// </summary>
public class TestEnemyHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float hitFlashSeconds = 0.07f;
    [SerializeField] private float explosionScale = 1f;

    private int health;
    private float flashTimer;
    private MeshRenderer[] renderers;
    private MaterialPropertyBlock block;

    void Awake()
    {
        renderers = (visualRoot != null ? visualRoot : transform).GetComponentsInChildren<MeshRenderer>();
        block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        health = maxHealth;
        SetFlash(false);
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
        {
            if (TestFxManager.Active != null)
            {
                TestFxManager.Active.PlayExplosion(transform.position, explosionScale);
            }
            Destroy(gameObject);
            return;
        }
        flashTimer = hitFlashSeconds;
        SetFlash(true);
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                SetFlash(false);
            }
        }
    }

    private void SetFlash(bool on)
    {
        if (renderers == null)
        {
            return;
        }
        foreach (MeshRenderer r in renderers)
        {
            if (on)
            {
                block.Clear();
                block.SetColor("_BaseColor", Color.white);
                block.SetColor("_EmissionColor", Color.white * 1.2f);
                r.SetPropertyBlock(block);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}
