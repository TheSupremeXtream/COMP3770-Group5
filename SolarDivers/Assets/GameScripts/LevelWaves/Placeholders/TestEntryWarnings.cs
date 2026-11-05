using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PLACEHOLDER: shows a blinking chevron strip at the top of the play area where the next enemy group will enter,
/// driven by WaveEncounter.onGroupIncoming. Decoration only (no colliders); markers remove themselves.
/// </summary>
public class TestEntryWarnings : MonoBehaviour
{
    [SerializeField] private WaveEncounter encounter;
    [Tooltip("Flat quad with the warning material; cloned for each warning.")]
    [SerializeField] private GameObject markerPrefab;
    [Tooltip("World Z of the strip (just inside the top edge of the view).")]
    [SerializeField] private float markerZ = 8.2f;
    [SerializeField] private Color colour = new Color(1f, 0.7f, 0.2f, 1f);
    [SerializeField] private float blinkRate = 6f;

    private class Marker
    {
        public GameObject go;
        public Renderer renderer;
        public float timeLeft;
        public float tiles;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private MaterialPropertyBlock block;

    public int ActiveCount => markers.Count;

    void OnEnable()
    {
        block = new MaterialPropertyBlock();
        if (encounter != null)
        {
            encounter.onGroupIncoming.AddListener(Show);
        }
    }

    void OnDisable()
    {
        if (encounter != null)
        {
            encounter.onGroupIncoming.RemoveListener(Show);
        }
        ClearAll();
    }

    public void Show(Vector3 centre, float width, float seconds)
    {
        if (markerPrefab == null || seconds <= 0f)
        {
            return;
        }
        float w = width + 2f; // a little wider than the group's outer ships
        // Placed in this object's space, so the strip stays at the top of the view if this object rides a rail rig.
        GameObject go = Instantiate(markerPrefab, transform);
        go.transform.localPosition = new Vector3(transform.InverseTransformPoint(centre).x, 0f, markerZ);
        go.transform.localRotation = markerPrefab.transform.localRotation;
        Vector3 scale = go.transform.localScale;
        go.transform.localScale = new Vector3(w, scale.y, scale.z);
        go.SetActive(true);
        markers.Add(new Marker { go = go, renderer = go.GetComponent<Renderer>(), timeLeft = seconds, tiles = Mathf.Max(1f, Mathf.Round(w / 1.2f)) });
    }

    public void ClearAll()
    {
        foreach (Marker m in markers)
        {
            if (m.go != null)
            {
                Destroy(m.go);
            }
        }
        markers.Clear();
    }

    void Update()
    {
        if (encounter != null && encounter.IsFailed)
        {
            ClearAll();
            return;
        }
        for (int i = markers.Count - 1; i >= 0; i--)
        {
            Marker m = markers[i];
            m.timeLeft -= Time.deltaTime;
            if (m.go == null || m.timeLeft <= 0f)
            {
                if (m.go != null)
                {
                    Destroy(m.go);
                }
                markers.RemoveAt(i);
                continue;
            }
            Color c = colour;
            c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Cos(m.timeLeft * blinkRate * Mathf.PI * 2f));
            m.renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c * 1.6f);
            block.SetVector("_BaseMap_ST", new Vector4(m.tiles, 1f, 0f, 0f)); // one chevron per ~1.2 units
            m.renderer.SetPropertyBlock(block);
        }
    }
}
