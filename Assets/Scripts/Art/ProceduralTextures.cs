using System.Collections.Generic;
using UnityEngine;

namespace Brooklyneir.Art
{
    /// <summary>
    /// Runtime-generated textures (table top with painted lines, wood grain, net mesh, court
    /// floors, gradients, soft glows). Generated once and cached; mipmaps on for surfaces.
    /// </summary>
    public static class ProceduralTextures
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        static Texture2D Get(string key, int w, int h, bool mips, TextureWrapMode wrap, System.Func<int, int, Color> pixel)
        {
            Texture2D t;
            if (Cache.TryGetValue(key, out t) && t != null) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, mips);
            t.name = key;
            t.wrapMode = wrap;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = mips ? 4 : 0;
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            t.SetPixels32(px);
            t.Apply(mips, true);
            Cache[key] = t;
            return t;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Smooth value noise in [0,1].</summary>
        public static float Noise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        static float Fbm(float x, float y, int seed)
        {
            return Noise(x, y, seed) * 0.55f + Noise(x * 2.1f, y * 2.1f, seed + 1) * 0.3f + Noise(x * 4.3f, y * 4.3f, seed + 2) * 0.15f;
        }

        /// <summary>
        /// Table top: UV (0,0)-(1,1) maps to the whole surface (u across the width, v along the
        /// length). Painted white edge lines (2 cm), centre line, subtle matte noise.
        /// </summary>
        public static Texture2D TableTop(Color top, Color line)
        {
            const int w = 256, h = 512;
            string key = "tabletop_" + ColorUtility.ToHtmlStringRGB(top) + ColorUtility.ToHtmlStringRGB(line);
            float lineU = 0.02f / 1.525f, lineV = 0.02f / 2.74f, centreU = 0.003f / 1.525f;
            return Get(key, w, h, true, TextureWrapMode.Clamp, (x, y) =>
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float n = Fbm(u * 40f, v * 70f, 3) * 0.06f - 0.03f;
                Color c = top * (1f + n);
                bool edge = u < lineU || u > 1f - lineU || v < lineV || v > 1f - lineV;
                bool centre = Mathf.Abs(u - 0.5f) < centreU + 0.5f / w;
                if (edge || centre)
                {
                    float wear = Fbm(u * 120f, v * 120f, 9) * 0.08f;
                    c = Color.Lerp(line, top, wear);
                }
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Wood grain (tileable along V).</summary>
        public static Texture2D Wood(Color light, Color dark)
        {
            string key = "wood_" + ColorUtility.ToHtmlStringRGB(light) + ColorUtility.ToHtmlStringRGB(dark);
            return Get(key, 128, 256, true, TextureWrapMode.Repeat, (x, y) =>
            {
                float u = x / 128f, v = y / 256f;
                float warp = Fbm(u * 3f, v * 6f, 11) * 2.2f;
                float rings = Mathf.Abs(Mathf.Sin((u * 14f + warp) * Mathf.PI));
                float fine = Noise(u * 90f, v * 8f, 5) * 0.25f;
                Color c = Color.Lerp(dark, light, Mathf.Clamp01(rings * 0.8f + fine));
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Net weave with alpha (cutout). Tiles in both directions.</summary>
        public static Texture2D NetMesh(Color color)
        {
            string key = "net_" + ColorUtility.ToHtmlStringRGB(color);
            return Get(key, 64, 64, true, TextureWrapMode.Repeat, (x, y) =>
            {
                int gx = x % 16, gy = y % 16;
                bool thread = gx < 2 || gy < 2;
                Color c = color;
                c.a = thread ? 1f : 0f;
                return c;
            });
        }

        /// <summary>Vertical gradient for sky domes (v = 0 bottom, 1 top).</summary>
        public static Texture2D VerticalGradient(Color bottom, Color top, Color horizonGlow)
        {
            string key = "grad_" + ColorUtility.ToHtmlStringRGB(bottom) + ColorUtility.ToHtmlStringRGB(top) + ColorUtility.ToHtmlStringRGB(horizonGlow);
            return Get(key, 4, 256, false, TextureWrapMode.Clamp, (x, y) =>
            {
                float v = y / 255f;
                Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0.35f, 1f, v));
                float glow = Mathf.Exp(-Mathf.Pow((v - 0.5f) * 9f, 2f));
                c = Color.Lerp(c, horizonGlow, glow * 0.6f);
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Radial soft blob (shadows, glows, particles). White with alpha falloff.</summary>
        public static Texture2D SoftCircle(float hardness = 0.25f)
        {
            string key = "soft_" + hardness;
            return Get(key, 64, 64, false, TextureWrapMode.Clamp, (x, y) =>
            {
                float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 1f - Mathf.SmoothStep(hardness, 1f, d);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>Ring (landing marker), white with alpha.</summary>
        public static Texture2D RingMarker()
        {
            return Get("ringmarker", 128, 128, false, TextureWrapMode.Clamp, (x, y) =>
            {
                float dx = (x + 0.5f) / 64f - 1f, dy = (y + 0.5f) / 64f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.78f) * 14f, 2f));
                float dot = 1f - Mathf.SmoothStep(0.12f, 0.2f, d);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(ring + dot * 0.9f));
            });
        }

        /// <summary>Spark streak for particles.</summary>
        public static Texture2D Spark()
        {
            return Get("spark", 64, 64, false, TextureWrapMode.Clamp, (x, y) =>
            {
                float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                float core = Mathf.Exp(-(dx * dx * 30f + dy * dy * 3f));
                float halo = Mathf.Exp(-(dx * dx + dy * dy) * 6f) * 0.35f;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(core + halo));
            });
        }

        /// <summary>Hexagon grid for the shield (alpha lines).</summary>
        public static Texture2D Hexagons()
        {
            return Get("hex", 128, 128, true, TextureWrapMode.Repeat, (x, y) =>
            {
                float u = x / 128f * 4f, v = y / 128f * 4f * 1.1547f;
                // Distance to nearest hex edge using axial trick.
                float qx = u, qy = v * 0.866f;
                float a = Frac(qx + qy * 0.577f), b = Frac(qx - qy * 0.577f), c = Frac(qy * 1.1547f);
                float e = Mathf.Min(Mathf.Min(Edge(a), Edge(b)), Edge(c));
                float line = 1f - Mathf.SmoothStep(0.02f, 0.07f, e);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(line * 0.9f + 0.12f));
            });
        }

        static float Frac(float v) { return v - Mathf.Floor(v); }
        static float Edge(float f) { return Mathf.Abs(f - 0.5f) > 0.5f ? 0f : 0.5f - Mathf.Abs(f - 0.5f); }

        /// <summary>Sports floor planks.</summary>
        public static Texture2D Planks(Color baseColor)
        {
            string key = "planks_" + ColorUtility.ToHtmlStringRGB(baseColor);
            return Get(key, 256, 256, true, TextureWrapMode.Repeat, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                int plank = Mathf.FloorToInt(u * 8f);
                float offset = Hash(plank, 0, 2) * 0.7f;
                float seam = Mathf.Min(Frac(u * 8f), 1f - Frac(u * 8f));
                float endSeam = Mathf.Min(Frac(v * 2f + offset), 1f - Frac(v * 2f + offset));
                float tone = 0.9f + Hash(plank, Mathf.FloorToInt(v * 2f + offset), 4) * 0.18f;
                float grain = Fbm(u * 30f, v * 3f, 8) * 0.12f;
                Color c = baseColor * (tone + grain - 0.06f);
                if (seam < 0.025f || endSeam < 0.006f) c *= 0.7f;
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Asphalt / concrete with speckles.</summary>
        public static Texture2D Concrete(Color baseColor, int seed)
        {
            string key = "concrete_" + ColorUtility.ToHtmlStringRGB(baseColor) + seed;
            return Get(key, 256, 256, true, TextureWrapMode.Repeat, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                float n = Fbm(u * 8f, v * 8f, seed) * 0.25f + Noise(u * 120f, v * 120f, seed + 7) * 0.12f;
                Color c = baseColor * (0.85f + n);
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Sand with ripples.</summary>
        public static Texture2D Sand(Color baseColor)
        {
            string key = "sand_" + ColorUtility.ToHtmlStringRGB(baseColor);
            return Get(key, 256, 256, true, TextureWrapMode.Repeat, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                float ripple = Mathf.Sin((u * 18f + Fbm(u * 3f, v * 3f, 2) * 4f) * Mathf.PI) * 0.04f;
                float grain = Noise(u * 160f, v * 160f, 3) * 0.12f;
                Color c = baseColor * (0.92f + ripple + grain);
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Glowing grid for the neon floor (emission map style, black background).</summary>
        public static Texture2D NeonGrid(Color line)
        {
            string key = "neongrid_" + ColorUtility.ToHtmlStringRGB(line);
            return Get(key, 128, 128, true, TextureWrapMode.Repeat, (x, y) =>
            {
                float u = Frac(x / 128f * 2f), v = Frac(y / 128f * 2f);
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float g = Mathf.Exp(-d * 60f);
                Color c = line * g;
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Building facade with lit windows (night) or plain windows (day).</summary>
        public static Texture2D Windows(Color wall, Color lit, bool night, int seed)
        {
            string key = "win_" + ColorUtility.ToHtmlStringRGB(wall) + ColorUtility.ToHtmlStringRGB(lit) + night + seed;
            return Get(key, 128, 256, true, TextureWrapMode.Repeat, (x, y) =>
            {
                int cx = x / 16, cy = y / 16;
                int lx = x % 16, ly = y % 16;
                bool window = lx >= 4 && lx < 12 && ly >= 4 && ly < 12;
                Color c = wall * (0.9f + Noise(x * 0.05f, y * 0.05f, seed) * 0.15f);
                if (window)
                {
                    float on = Hash(cx, cy, seed);
                    if (night) c = on > 0.45f ? lit * (0.7f + on * 0.4f) : wall * 0.35f;
                    else c = Color.Lerp(new Color(0.55f, 0.7f, 0.85f), new Color(0.85f, 0.92f, 1f), on);
                }
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Stylised graffiti-ish mural: bold colour blobs and stripes (original, abstract).</summary>
        public static Texture2D Mural(Color a, Color b, Color c, int seed)
        {
            string key = "mural_" + ColorUtility.ToHtmlStringRGB(a) + ColorUtility.ToHtmlStringRGB(b) + seed;
            return Get(key, 256, 128, true, TextureWrapMode.Clamp, (x, y) =>
            {
                float u = x / 256f, v = y / 128f;
                float f1 = Fbm(u * 3f, v * 2f, seed);
                float f2 = Fbm(u * 5f + 3f, v * 3f, seed + 5);
                Color col = new Color(0.86f, 0.84f, 0.8f);
                if (f1 > 0.55f) col = a;
                if (f2 > 0.62f) col = b;
                float stripe = Mathf.Abs(Mathf.Sin((u * 6f + v * 2f + f1) * Mathf.PI));
                if (stripe > 0.94f) col = c;
                if (Mathf.Abs(f1 - 0.55f) < 0.012f || Mathf.Abs(f2 - 0.62f) < 0.012f) col = new Color(0.08f, 0.08f, 0.1f);
                col.a = 1f;
                return col;
            });
        }

        /// <summary>Chain-link fence (alpha cutout).</summary>
        public static Texture2D ChainLink()
        {
            return Get("chainlink", 64, 64, true, TextureWrapMode.Repeat, (x, y) =>
            {
                float u = Frac((x + y) / 32f), v = Frac((x - y + 64) / 32f);
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float a = 1f - Mathf.SmoothStep(0.03f, 0.07f, d);
                return new Color(0.75f, 0.78f, 0.8f, a);
            });
        }

        /// <summary>Billboard sign with a simple original graphic (stripes + circle).</summary>
        public static Texture2D Sign(Color bg, Color accent, int seed)
        {
            string key = "sign_" + ColorUtility.ToHtmlStringRGB(bg) + ColorUtility.ToHtmlStringRGB(accent) + seed;
            return Get(key, 128, 64, true, TextureWrapMode.Clamp, (x, y) =>
            {
                float u = x / 128f, v = y / 64f;
                Color c = bg;
                float cx = 0.22f + Hash(seed, 1, 1) * 0.1f;
                float d = Mathf.Sqrt((u - cx) * (u - cx) * 4f + (v - 0.5f) * (v - 0.5f));
                if (d < 0.32f) c = accent;
                if (d < 0.2f) c = bg;
                if (u > 0.45f && Mathf.Abs(v - 0.62f) < 0.07f) c = Color.white;
                if (u > 0.45f && u < 0.85f && Mathf.Abs(v - 0.38f) < 0.05f) c = accent;
                if (u < 0.02f || u > 0.98f || v < 0.04f || v > 0.96f) c = accent;
                c.a = 1f;
                return c;
            });
        }

        public static Color Hex(string hex)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return Color.magenta;
        }
    }
}
