using UnityEngine;

/// <summary>
/// Original textures for the wave test scene, generated in code (no downloads). Deterministic: the same seed always
/// gives the same pixels, so rebuilding does not change the PNGs. Backdrop bodies are "impostors": a flat disc image
/// with surface detail, lighting, the day/night edge and the atmosphere painted in for a fixed light direction,
/// because the camera always looks straight down at them.
/// Disc space: +x = screen right, +y = screen up, +z = towards the camera.
/// </summary>
public static class ProceduralSpaceTextures
{
    // ---- Noise ----

    static float Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            int h = seed + x * 73856093 ^ y * 19349663 ^ z * 83492791;
            h = (h << 13) ^ h;
            return 1f - ((h * (h * h * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
        }
    }

    static float Fade(float t) => t * t * (3f - 2f * t);

    /// <summary>Value noise in [-1, 1].</summary>
    static float Noise(Vector3 p, int seed)
    {
        int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
        float fx = Fade(p.x - xi), fy = Fade(p.y - yi), fz = Fade(p.z - zi);
        float a = Mathf.Lerp(Hash(xi, yi, zi, seed), Hash(xi + 1, yi, zi, seed), fx);
        float b = Mathf.Lerp(Hash(xi, yi + 1, zi, seed), Hash(xi + 1, yi + 1, zi, seed), fx);
        float c = Mathf.Lerp(Hash(xi, yi, zi + 1, seed), Hash(xi + 1, yi, zi + 1, seed), fx);
        float d = Mathf.Lerp(Hash(xi, yi + 1, zi + 1, seed), Hash(xi + 1, yi + 1, zi + 1, seed), fx);
        return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
    }

    /// <summary>Fractal noise, roughly in [-1, 1].</summary>
    static float Fbm(Vector3 p, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(p, seed + i * 131) * amp;
            norm += amp;
            p = p * 2.03f + new Vector3(5.2f, 1.3f, 7.7f);
            amp *= 0.5f;
        }
        return sum / norm;
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Color32 ToColor32(Color c)
    {
        return new Color32((byte)(Mathf.Clamp01(c.r) * 255f + 0.5f), (byte)(Mathf.Clamp01(c.g) * 255f + 0.5f),
            (byte)(Mathf.Clamp01(c.b) * 255f + 0.5f), (byte)(Mathf.Clamp01(c.a) * 255f + 0.5f));
    }

    /// <summary>Disc radius in pixels for a square texture of 'size' with 'margin' extra room for glow (e.g. 0.1 = 10%).</summary>
    static float DiscRadius(int size, float margin) => size * 0.5f / (1f + margin);

    // ---- Bodies ----

    /// <summary>Ocean world with continents, ice caps, clouds, ocean glint, a soft terminator and a lit atmosphere rim.</summary>
    public static Color32[] Planet(int size, Vector3 light, int seed)
    {
        var px = new Color32[size * size];
        Vector3 L = light.normalized;
        Vector3 H = (L + Vector3.forward).normalized; // half vector towards the camera (+z)
        float R = DiscRadius(size, 0.1f);
        float c0 = size * 0.5f;
        Quaternion tilt = Quaternion.Euler(-62f, 28f, 14f); // so the poles and continents are not face-on
        Color atmosphere = new Color(0.38f, 0.62f, 1f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - c0) / R, v = (y + 0.5f - c0) / R;
                float r = Mathf.Sqrt(u * u + v * v);
                Color halo = Color.clear;
                if (r >= 0.97f && r < 1.1f)
                {
                    float glow = Mathf.Exp(-Mathf.Max(0f, r - 1f) / 0.02f);
                    float side = Smooth(-0.35f, 0.55f, (u * L.x + v * L.y) / Mathf.Max(r, 1e-4f));
                    halo = atmosphere;
                    halo.a = glow * side * 0.9f;
                }
                if (r >= 1f)
                {
                    px[y * size + x] = ToColor32(halo);
                    continue;
                }
                var n = new Vector3(u, v, Mathf.Sqrt(Mathf.Max(0f, 1f - r * r)));
                Vector3 p = tilt * n;
                Vector3 warp = new Vector3(Fbm(p * 2.2f, 4, seed + 11), Fbm(p * 2.2f, 4, seed + 23), Fbm(p * 2.2f, 4, seed + 37));
                float height = Fbm(p * 1.8f + warp * 0.6f, 7, seed);
                float moisture = Fbm(p * 3.2f, 4, seed + 51);
                const float seaLevel = -0.03f;
                bool land = height > seaLevel;
                Color surface;
                if (land)
                {
                    float h = Smooth(seaLevel, 0.35f, height);
                    Color low = Color.Lerp(new Color(0.15f, 0.3f, 0.12f), new Color(0.52f, 0.43f, 0.26f), Smooth(-0.05f, 0.2f, -moisture));
                    surface = Color.Lerp(low, new Color(0.4f, 0.35f, 0.3f), h * h);
                    surface = Color.Lerp(new Color(0.55f, 0.5f, 0.36f), surface, Smooth(0f, 0.025f, height - seaLevel)); // beaches
                }
                else
                {
                    surface = Color.Lerp(new Color(0.02f, 0.06f, 0.17f), new Color(0.06f, 0.25f, 0.38f), Smooth(-0.25f, seaLevel, height));
                }
                float ice = Smooth(0.93f, 0.96f, Mathf.Abs(p.y) + 0.05f * Fbm(p * 7f, 3, seed + 71)); // small polar caps
                surface = Color.Lerp(surface, new Color(0.88f, 0.92f, 0.97f), ice);
                // Clouds: smaller, streaky systems (stretched along latitude and swirled), about a third of the surface.
                Vector3 cp = new Vector3(p.x * 3.6f, p.y * 8f, p.z * 3.6f) + warp * 1.6f;
                float cloudNoise = Fbm(cp, 6, seed + 97);
                float cloud = Smooth(0.12f, 0.38f, cloudNoise) * (0.55f + 0.45f * Smooth(0f, 0.4f, Fbm(p * 9f, 3, seed + 101))) * 0.9f;
                surface = Color.Lerp(surface, new Color(0.93f, 0.95f, 0.98f), cloud);

                float ndl = Vector3.Dot(n, L);
                float diffuse = Mathf.Clamp01(ndl);
                float twilight = Smooth(-0.06f, 0.03f, ndl) * (1f - Smooth(0.03f, 0.12f, ndl));
                Color col = surface * (Mathf.Pow(diffuse, 0.85f) * 1.12f + 0.03f); // night side lifted slightly so the disc reads
                col += new Color(0.35f, 0.14f, 0.05f) * twilight * 0.18f;             // thin warm band at the day/night edge
                float glint = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(n, H)), 60f) * (land ? 0f : 1f) * (1f - cloud) * diffuse;
                col += new Color(0.9f, 0.85f, 0.7f) * glint * 0.5f;                   // sun glint on the oceans
                float rim = Mathf.Pow(1f - n.z, 2.4f);
                col += atmosphere * rim * 0.75f * Smooth(-0.25f, 0.4f, ndl);          // haze thickening towards the limb
                col.a = 1f;
                float edge = Mathf.Clamp01((1f - r) * R);                              // 1 px anti-aliased edge
                px[y * size + x] = ToColor32(Color.Lerp(halo, col, edge));
            }
        }
        return px;
    }

    /// <summary>Airless grey moon with maria and craters, same light direction, a hard terminator.</summary>
    public static Color32[] Moon(int size, Vector3 light, int seed)
    {
        var px = new Color32[size * size];
        Vector3 L = light.normalized;
        float R = DiscRadius(size, 0.02f);
        float c0 = size * 0.5f;
        var random = new System.Random(seed);
        const int craterCount = 34;
        var centres = new Vector3[craterCount];
        var radii = new float[craterCount];
        for (int i = 0; i < craterCount; i++)
        {
            centres[i] = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f).normalized;
            radii[i] = Mathf.Lerp(0.04f, 0.2f, Mathf.Pow((float)random.NextDouble(), 2.2f));
        }
        Quaternion tilt = Quaternion.Euler(-30f, 40f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - c0) / R, v = (y + 0.5f - c0) / R;
                float r = Mathf.Sqrt(u * u + v * v);
                if (r >= 1f)
                {
                    px[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }
                var n = new Vector3(u, v, Mathf.Sqrt(Mathf.Max(0f, 1f - r * r)));
                Vector3 p = tilt * n;
                float albedo = 0.55f + 0.12f * Fbm(p * 5f, 5, seed);
                albedo -= 0.16f * Smooth(0.0f, 0.3f, Fbm(p * 1.3f, 3, seed + 7));       // dark maria
                for (int i = 0; i < craterCount; i++)
                {
                    float d = Mathf.Acos(Mathf.Clamp(Vector3.Dot(p, centres[i]), -1f, 1f)) / radii[i];
                    if (d < 1.25f)
                    {
                        albedo -= 0.08f * (1f - Smooth(0.6f, 0.95f, d));                 // floor
                        albedo += 0.1f * Mathf.Exp(-Mathf.Pow((d - 1f) / 0.12f, 2f));      // bright rim
                    }
                }
                float ndl = Vector3.Dot(n, L);
                float lit = Smooth(-0.03f, 0.08f, ndl) * Mathf.Clamp01(ndl * 1.1f + 0.05f);
                Color col = new Color(0.62f, 0.6f, 0.58f) * albedo * (lit * 2.1f + 0.05f);
                col.a = Mathf.Clamp01((1f - r) * R);
                px[y * size + x] = ToColor32(col);
            }
        }
        return px;
    }

    /// <summary>Sun disc with limb darkening, granulation and a soft corona (use with an additive, HDR-tinted material).</summary>
    public static Color32[] Sun(int size, int seed)
    {
        var px = new Color32[size * size];
        float R = DiscRadius(size, 0.9f);
        float c0 = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - c0) / R, v = (y + 0.5f - c0) / R;
                float r = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);
                float rays = 0.75f + 0.25f * Noise(new Vector3(Mathf.Cos(angle) * 4f, Mathf.Sin(angle) * 4f, r * 1.5f), seed);
                float corona = Mathf.Exp(-Mathf.Max(0f, r - 1f) / 0.16f) * 0.55f * rays + Mathf.Exp(-Mathf.Max(0f, r - 1f) / 0.5f) * 0.18f;
                corona *= 1f - Smooth(1.2f, 1.85f, r); // exactly zero before the quad's edge (no visible square)
                Color col = new Color(1f, 0.78f, 0.5f, Mathf.Clamp01(corona));
                if (r < 1f)
                {
                    float mu = Mathf.Sqrt(1f - r * r);
                    float limb = 1f - 0.55f * (1f - mu);
                    float grain = 1f + 0.06f * Fbm(new Vector3(u, v, mu) * 18f, 3, seed + 3);
                    Color disc = Color.Lerp(new Color(1f, 0.62f, 0.3f), new Color(1f, 0.97f, 0.88f), mu) * limb * grain;
                    disc.a = 1f;
                    col = Color.Lerp(col, disc, Mathf.Clamp01((1f - r) * R));
                }
                px[y * size + x] = ToColor32(col);
            }
        }
        return px;
    }

    /// <summary>Soft cloud of gas fading to nothing at the edges (no seams when it drifts and wraps).</summary>
    public static Color32[] Nebula(int size, Color a, Color b, int seed)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                var p = new Vector3(u * 2.2f, v * 2.2f, seed * 0.37f);
                float warp = Fbm(p * 1.4f, 4, seed + 5);
                float density = Smooth(-0.15f, 0.6f, Fbm(p + new Vector3(warp, -warp, 0f) * 0.9f, 6, seed));
                float falloff = 1f - Smooth(0.35f, 1f, r);
                Color col = Color.Lerp(a, b, Smooth(-0.3f, 0.4f, Fbm(p * 0.8f, 3, seed + 9)));
                col.a = density * falloff;
                px[y * size + x] = ToColor32(col);
            }
        }
        return px;
    }

    // ---- Effects ----

    /// <summary>Four-point star with a hot core: muzzle flashes and impact flashes.</summary>
    public static Color32[] Flare(int size)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float core = Mathf.Exp(-r * r / 0.035f);
                float spikes = Mathf.Exp(-Mathf.Abs(u) * 22f) * Mathf.Exp(-Mathf.Abs(v) * 2.6f) +
                               Mathf.Exp(-Mathf.Abs(v) * 22f) * Mathf.Exp(-Mathf.Abs(u) * 3.4f) * 0.7f;
                float a = Mathf.Clamp01(core + spikes * 0.8f) * (1f - Smooth(0.8f, 1f, r));
                px[y * size + x] = ToColor32(new Color(1f, 1f, 1f, a));
            }
        }
        return px;
    }

    /// <summary>Thin soft ring: explosion shock waves.</summary>
    public static Color32[] Ring(int size)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.07f, 2f)) * (1f - Smooth(0.92f, 1f, r));
                px[y * size + x] = ToColor32(new Color(1f, 1f, 1f, a));
            }
        }
        return px;
    }

    /// <summary>Downward chevron, tiles horizontally: entry warnings.</summary>
    public static Color32[] Chevron(int size)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float line = 0.22f + Mathf.Abs(u - 0.5f) * 1.1f; // tip at the bottom centre
                float a = 1f - Smooth(0.07f, 0.12f, Mathf.Abs(v - line));
                a *= 1f - Smooth(0.36f, 0.48f, Mathf.Abs(u - 0.5f));
                px[y * size + x] = ToColor32(new Color(1f, 1f, 1f, a));
            }
        }
        return px;
    }
}
