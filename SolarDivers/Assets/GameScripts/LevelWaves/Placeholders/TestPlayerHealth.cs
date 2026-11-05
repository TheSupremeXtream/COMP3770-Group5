using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PLACEHOLDER player health for the wave prototype only (no team damage system exists yet).
/// Needs a trigger collider and a kinematic Rigidbody on the same object.
/// Values (3 hits, 1 s invulnerability) are proposals to tune with the team.
/// </summary>
public class TestPlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHits = 3;
    [SerializeField] private float invulnerabilitySeconds = 1f;
    [SerializeField] private Color hitFlashColour = new Color(1f, 0.15f, 0.15f);
    [SerializeField] private Color defeatedColour = new Color(0.25f, 0.25f, 0.25f);
    [SerializeField] private float blinkRate = 12f;
    [Tooltip("Length of the bright red flash at the moment of a hit.")]
    [SerializeField] private float hitFlashSeconds = 0.12f;
    [Tooltip("Visual model that flashes/blinks. Empty = this object. Colliders stay on the root.")]
    [SerializeField] private Transform visualRoot;

    public UnityEvent onDamaged = new UnityEvent();
    public UnityEvent onDefeated = new UnityEvent();

    private MeshRenderer[] renderers;
    private ParticleSystem[] engines;
    private MaterialPropertyBlock block;
    private float invulnerableTimer;

    public int MaxHits => maxHits;
    public int HitsRemaining { get; private set; }
    public bool IsDefeated { get; private set; }
    public bool IsInvulnerable => invulnerableTimer > 0f;

    /// <summary>Used by the scene builder and tests.</summary>
    public void Configure(int hits, float invulnerability)
    {
        maxHits = hits;
        invulnerabilitySeconds = invulnerability;
        ResetHealth();
    }

    void Awake()
    {
        Transform root = visualRoot != null ? visualRoot : transform;
        renderers = root.GetComponentsInChildren<MeshRenderer>();
        engines = GetComponentsInChildren<ParticleSystem>();
        block = new MaterialPropertyBlock();
        // A sleeping kinematic body stops sending OnTriggerStay; keep it awake while the ship sits still.
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.sleepThreshold = 0f;
        }
    }

    void OnEnable()
    {
        ResetHealth();
    }

    public void ResetHealth()
    {
        HitsRemaining = maxHits;
        IsDefeated = false;
        invulnerableTimer = 0f;
        ApplyLook(false, false, true);
    }

    /// <summary>Returns true if the hit counted (not invulnerable and not already defeated).</summary>
    public bool TryTakeHit(int amount)
    {
        if (IsDefeated || IsInvulnerable || amount <= 0)
        {
            return false;
        }
        HitsRemaining = Mathf.Max(0, HitsRemaining - amount);
        if (HitsRemaining == 0)
        {
            IsDefeated = true;
            invulnerableTimer = 0f;
            ApplyLook(false, true, true);
            Debug.Log("[TestPlayerHealth] Player defeated.", this);
            onDefeated.Invoke();
            return true;
        }
        invulnerableTimer = invulnerabilitySeconds;
        ApplyLook(true, false, true);
        Debug.Log($"[TestPlayerHealth] Hit! {HitsRemaining}/{maxHits} hits left.", this);
        onDamaged.Invoke();
        return true;
    }

    void Update()
    {
        if (invulnerableTimer <= 0f)
        {
            return;
        }
        invulnerableTimer -= Time.deltaTime;
        if (invulnerableTimer <= 0f)
        {
            ApplyLook(false, false, true);
            return;
        }
        // A short, bright red pop, then the normal colours blinking until vulnerable again.
        bool justHit = invulnerabilitySeconds - invulnerableTimer < hitFlashSeconds;
        bool visible = justHit || Mathf.Repeat(invulnerableTimer * blinkRate, 1f) > 0.35f;
        ApplyLook(justHit, false, visible);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider other)
    {
        // Trigger messages also reach disabled components, so check explicitly.
        if (!isActiveAndEnabled)
        {
            return;
        }
        TestContactDamage source = other.GetComponentInParent<TestContactDamage>();
        if (source != null)
        {
            TryTakeHit(source.Damage);
        }
    }

    private void ApplyLook(bool flashing, bool defeated, bool visible)
    {
        if (renderers == null)
        {
            return;
        }
        SetEngines(!defeated);
        foreach (MeshRenderer r in renderers)
        {
            if (r == null)
            {
                continue;
            }
            r.enabled = visible;
            r.GetPropertyBlock(block);
            if (flashing || defeated)
            {
                Color c = defeated ? defeatedColour : hitFlashColour;
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                block.SetColor("_EmissionColor", defeated ? Color.black : c * 1.5f);
                r.SetPropertyBlock(block);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }

    private void SetEngines(bool running)
    {
        if (engines == null)
        {
            return;
        }
        foreach (ParticleSystem engine in engines)
        {
            if (engine == null)
            {
                continue;
            }
            if (running && !engine.isPlaying)
            {
                engine.Play();
            }
            else if (!running && engine.isPlaying)
            {
                engine.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
