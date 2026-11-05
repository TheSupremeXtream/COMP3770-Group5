using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the prototype's original low-poly meshes in code (no imported or downloaded models).
/// All ships point along +Z. Sub-mesh slots: 0 hull, 1 accent/wings, 2 cockpit, 3 engine glow.
/// </summary>
public static class LowPolyShipMeshes
{
    public const int Hull = 0, Accent = 1, Cockpit = 2, Glow = 3;

    /// <summary>Friendly fighter: long white fuselage, swept delta wings, canards, twin engines.</summary>
    public static Mesh PlayerFighter()
    {
        var b = new MeshBuilder(4);
        b.Loft(Hull, 0f, new[] { S(1.45f, 0f, 0f), S(0.85f, 0.2f, 0.13f), S(0.1f, 0.3f, 0.2f), S(-0.65f, 0.28f, 0.18f), S(-0.9f, 0.2f, 0.14f) }, 6);
        b.Loft(Cockpit, 0f, new[] { S(0.75f, 0f, 0f, 0.12f), S(0.5f, 0.11f, 0.1f, 0.14f), S(0.05f, 0.13f, 0.12f, 0.16f), S(-0.2f, 0f, 0f, 0.14f) }, 6);
        b.PlateMirrored(Accent, new[] { V(0.22f, 0.35f), V(1.15f, -0.45f), V(1.15f, -0.68f), V(0.22f, -0.6f) }, 0f, 0.07f);
        b.PlateMirrored(Accent, new[] { V(0.16f, 0.95f), V(0.42f, 0.62f), V(0.16f, 0.6f) }, 0f, 0.05f);
        b.PlateMirrored(Hull, new[] { V(1.05f, -0.2f), V(1.2f, -0.35f), V(1.2f, -0.72f), V(1.05f, -0.7f) }, 0.02f, 0.12f);
        foreach (float x in new[] { -0.24f, 0.24f })
        {
            b.Loft(Hull, x, new[] { S(-0.3f, 0.09f, 0.08f), S(-0.95f, 0.12f, 0.1f) }, 6, true, true);
            b.Loft(Glow, x, new[] { S(-0.95f, 0.09f, 0.075f), S(-1.0f, 0.09f, 0.075f) }, 6, false, true);
        }
        return b.ToMesh("TEST_Mesh_PlayerFighter");
    }

    /// <summary>Basic enemy "Dart": dark narrow fuselage, forward-swept blade wings, single engine, red eye.</summary>
    public static Mesh EnemyDart()
    {
        var b = new MeshBuilder(4);
        b.Loft(Hull, 0f, new[] { S(1.15f, 0f, 0f), S(0.5f, 0.17f, 0.13f), S(-0.2f, 0.26f, 0.17f), S(-0.75f, 0.2f, 0.13f) }, 6);
        b.Loft(Cockpit, 0f, new[] { S(0.55f, 0f, 0f, 0.1f), S(0.38f, 0.08f, 0.07f, 0.12f), S(0.12f, 0.09f, 0.08f, 0.12f), S(-0.02f, 0f, 0f, 0.1f) }, 6);
        b.PlateMirrored(Accent, new[] { V(0.2f, -0.15f), V(0.95f, 0.4f), V(1.02f, 0.2f), V(0.2f, -0.65f) }, 0f, 0.06f);
        b.PlateMirrored(Hull, new[] { V(0.15f, -0.45f), V(0.45f, -0.85f), V(0.15f, -0.8f) }, 0f, 0.05f);
        b.Loft(Glow, 0f, new[] { S(-0.75f, 0.13f, 0.09f), S(-0.8f, 0.13f, 0.09f) }, 6, false, true);
        return b.ToMesh("TEST_Mesh_EnemyDart");
    }

    /// <summary>Weaving enemy "Manta": wide flying wing, forward pincer prongs, twin engines.</summary>
    public static Mesh EnemyManta()
    {
        var b = new MeshBuilder(4);
        b.Loft(Hull, 0f, new[] { S(0.6f, 0f, 0f), S(0.35f, 0.22f, 0.13f), S(-0.3f, 0.3f, 0.15f), S(-0.6f, 0.2f, 0.1f) }, 6);
        b.Loft(Cockpit, 0f, new[] { S(0.42f, 0f, 0f, 0.1f), S(0.25f, 0.1f, 0.08f, 0.13f), S(-0.05f, 0.11f, 0.08f, 0.13f), S(-0.2f, 0f, 0f, 0.1f) }, 6);
        b.PlateMirrored(Accent, new[] { V(0.15f, 0.3f), V(1.3f, -0.2f), V(1.25f, -0.48f), V(0.15f, -0.45f) }, 0f, 0.08f);
        foreach (float x in new[] { -0.55f, 0.55f })
        {
            b.Loft(Hull, x, new[] { S(0.75f, 0f, 0f), S(0.25f, 0.08f, 0.06f), S(-0.2f, 0.1f, 0.07f) }, 6, false, true);
        }
        foreach (float x in new[] { -0.3f, 0.3f })
        {
            b.Loft(Glow, x, new[] { S(-0.5f, 0.08f, 0.06f), S(-0.55f, 0.08f, 0.06f) }, 6, false, true);
        }
        return b.ToMesh("TEST_Mesh_EnemyManta");
    }

    /// <summary>Laser bolt: thin pointed spindle.</summary>
    public static Mesh LaserBolt()
    {
        var b = new MeshBuilder(1);
        b.Loft(0, 0f, new[] { S(0.4f, 0f, 0f), S(0.22f, 0.05f, 0.05f), S(-0.22f, 0.05f, 0.05f), S(-0.4f, 0f, 0f) }, 6);
        return b.ToMesh("TEST_Mesh_LaserBolt");
    }

    // Section: z position, half width, half height, optional y offset.
    static Vector4 S(float z, float w, float h, float y = 0f) => new Vector4(z, w, h, y);
    static Vector2 V(float x, float z) => new Vector2(x, z);

    private class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<int>[] subMeshes;

        public MeshBuilder(int subMeshCount)
        {
            subMeshes = new List<int>[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
            {
                subMeshes[i] = new List<int>();
            }
        }

        /// <summary>Flat-shaded triangle, flipped if needed so it faces away from 'inside'.</summary>
        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f)
            {
                return;
            }
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
            {
                (b, c) = (c, b);
                n = -n;
            }
            n.Normalize();
            foreach (Vector3 v in new[] { a, b, c })
            {
                subMeshes[sub].Add(vertices.Count);
                vertices.Add(v);
                normals.Add(n);
            }
        }

        /// <summary>Convex hull piece lofted along Z through elliptical sections (width 0 = point).</summary>
        public void Loft(int sub, float x, Vector4[] sections, int sides, bool capStart = false, bool capEnd = false)
        {
            var rings = new Vector3[sections.Length][];
            Vector3 centre = Vector3.zero;
            for (int s = 0; s < sections.Length; s++)
            {
                rings[s] = new Vector3[sides];
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    rings[s][k] = new Vector3(x + Mathf.Cos(a) * sections[s].y, sections[s].w + Mathf.Sin(a) * sections[s].z, sections[s].x);
                }
                centre += new Vector3(x, sections[s].w, sections[s].x);
            }
            centre /= sections.Length;
            for (int s = 0; s < sections.Length - 1; s++)
            {
                Vector3 axis = new Vector3(x, (sections[s].w + sections[s + 1].w) * 0.5f, (sections[s].x + sections[s + 1].x) * 0.5f);
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    Tri(sub, rings[s][k], rings[s + 1][k], rings[s + 1][k2], axis);
                    Tri(sub, rings[s][k], rings[s + 1][k2], rings[s][k2], axis);
                }
            }
            if (capStart)
            {
                Cap(sub, rings[0], new Vector3(x, sections[0].w, sections[0].x), centre);
            }
            if (capEnd)
            {
                int last = sections.Length - 1;
                Cap(sub, rings[last], new Vector3(x, sections[last].w, sections[last].x), centre);
            }
        }

        private void Cap(int sub, Vector3[] ring, Vector3 middle, Vector3 inside)
        {
            for (int k = 0; k < ring.Length; k++)
            {
                Tri(sub, middle, ring[k], ring[(k + 1) % ring.Length], inside);
            }
        }

        /// <summary>Flat convex plate (top-view polygon in X/Z) with thickness, plus its mirror across X = 0.</summary>
        public void PlateMirrored(int sub, Vector2[] polygon, float y, float thickness)
        {
            Plate(sub, polygon, y, thickness);
            var mirrored = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                mirrored[i] = new Vector2(-polygon[i].x, polygon[i].y);
            }
            Plate(sub, mirrored, y, thickness);
        }

        private void Plate(int sub, Vector2[] polygon, float y, float thickness)
        {
            int n = polygon.Length;
            var top = new Vector3[n];
            var bottom = new Vector3[n];
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                top[i] = new Vector3(polygon[i].x, y + thickness * 0.5f, polygon[i].y);
                bottom[i] = new Vector3(polygon[i].x, y - thickness * 0.5f, polygon[i].y);
                centre += new Vector3(polygon[i].x, y, polygon[i].y);
            }
            centre /= n;
            for (int i = 1; i < n - 1; i++)
            {
                Tri(sub, top[0], top[i], top[i + 1], centre);
                Tri(sub, bottom[0], bottom[i], bottom[i + 1], centre);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(sub, top[i], bottom[i], bottom[j], centre);
                Tri(sub, top[i], bottom[j], top[j], centre);
            }
        }

        public Mesh ToMesh(string meshName)
        {
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = subMeshes.Length;
            for (int i = 0; i < subMeshes.Length; i++)
            {
                mesh.SetTriangles(subMeshes[i], i);
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
