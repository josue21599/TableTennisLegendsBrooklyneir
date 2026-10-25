using System.Collections.Generic;
using UnityEngine;

namespace Brooklyneir.Art
{
    public enum IconId
    {
        Play, Pause, Back, Gear, Coin, Trophy, Star, StarEmpty, Lock, Racket, Table, Fire, Slow, Curve, Smash,
        Shield, Freeze, Random, Special, Profile, Shop, Target, Music, Sound, Vibration, Check, Cross, Plus, Minus,
        ArrowLeft, ArrowRight, TwoPlayers, Cpu, Training, Bracket, Restart, Home, Info, Ball
    }

    /// <summary>
    /// Vector-style icons rasterised at runtime with signed-distance shapes and anti-aliasing.
    /// White on transparent: the UI tints them. Original designs drawn in code.
    /// </summary>
    public static class IconFactory
    {
        const int Size = 128;
        static readonly Dictionary<IconId, Sprite> Icons = new Dictionary<IconId, Sprite>();
        static readonly Dictionary<string, Sprite> Panels = new Dictionary<string, Sprite>();

        class Painter
        {
            public readonly float[] A = new float[Size * Size];

            static float Cov(float d) { return Mathf.Clamp01(0.5f - d * Size / 1.6f); } // d in [0..1] icon units

            public void Shape(System.Func<float, float, float> sdf, bool subtract = false)
            {
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        float c = Cov(sdf(u, v));
                        int i = y * Size + x;
                        A[i] = subtract ? Mathf.Min(A[i], 1f - c) : Mathf.Max(A[i], c);
                    }
            }

            public void Circle(float cx, float cy, float r, bool sub = false) { Shape((u, v) => Dist(u, v, cx, cy) - r, sub); }
            public void Ring(float cx, float cy, float r, float w, bool sub = false) { Shape((u, v) => Mathf.Abs(Dist(u, v, cx, cy) - r) - w * 0.5f, sub); }
            public void Capsule(float ax, float ay, float bx, float by, float r, bool sub = false) { Shape((u, v) => SegDist(u, v, ax, ay, bx, by) - r, sub); }
            public void Box(float cx, float cy, float hw, float hh, float round, bool sub = false)
            {
                Shape((u, v) =>
                {
                    float qx = Mathf.Abs(u - cx) - hw + round, qy = Mathf.Abs(v - cy) - hh + round;
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
                }, sub);
            }
            public void Poly(Vector2[] pts, bool sub = false) { Shape((u, v) => PolyDist(u, v, pts), sub); }
            public void Arc(float cx, float cy, float r, float w, float a0, float a1, bool sub = false)
            {
                Shape((u, v) =>
                {
                    float ang = Mathf.Atan2(v - cy, u - cx);
                    float best = float.MaxValue;
                    // distance to the arc's ring if inside the angle range, else to the end caps
                    if (AngleIn(ang, a0, a1)) best = Mathf.Abs(Dist(u, v, cx, cy) - r) - w * 0.5f;
                    float e0x = cx + Mathf.Cos(a0) * r, e0y = cy + Mathf.Sin(a0) * r;
                    float e1x = cx + Mathf.Cos(a1) * r, e1y = cy + Mathf.Sin(a1) * r;
                    best = Mathf.Min(best, Dist(u, v, e0x, e0y) - w * 0.5f);
                    best = Mathf.Min(best, Dist(u, v, e1x, e1y) - w * 0.5f);
                    return best;
                }, sub);
            }

            static bool AngleIn(float a, float a0, float a1)
            {
                float span = Mathf.Repeat(a1 - a0, Mathf.PI * 2f);
                float rel = Mathf.Repeat(a - a0, Mathf.PI * 2f);
                return rel <= span;
            }

            public Sprite ToSprite(string name)
            {
                Texture2D t = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                t.name = name;
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Trilinear;
                Color32[] px = new Color32[Size * Size];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(A[i] * 255f));
                t.SetPixels32(px);
                t.Apply(true, true);
                return Sprite.Create(t, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        static float Dist(float u, float v, float x, float y) { float dx = u - x, dy = v - y; return Mathf.Sqrt(dx * dx + dy * dy); }

        static float SegDist(float u, float v, float ax, float ay, float bx, float by)
        {
            float px = u - ax, py = v - ay, dx = bx - ax, dy = by - ay;
            float h = Mathf.Clamp01((px * dx + py * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
            float ex = px - dx * h, ey = py - dy * h;
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        static float PolyDist(float u, float v, Vector2[] p)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                d = Mathf.Min(d, SegDist(u, v, p[j].x, p[j].y, p[i].x, p[i].y));
                if (((p[i].y > v) != (p[j].y > v)) && (u < (p[j].x - p[i].x) * (v - p[i].y) / (p[j].y - p[i].y) + p[i].x)) inside = !inside;
            }
            return inside ? -d : d;
        }

        static Vector2[] Star(float cx, float cy, float outer, float inner, int points, float rot)
        {
            Vector2[] s = new Vector2[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float a = rot + Mathf.PI * i / points;
                float r = i % 2 == 0 ? outer : inner;
                s[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return s;
        }

        public static Sprite Get(IconId id)
        {
            Sprite s;
            if (Icons.TryGetValue(id, out s) && s != null) return s;
            Painter p = new Painter();
            Draw(id, p);
            s = p.ToSprite("icon_" + id);
            Icons[id] = s;
            return s;
        }

        static void Draw(IconId id, Painter p)
        {
            const float H = Mathf.PI / 2f;
            switch (id)
            {
                case IconId.Play:
                    p.Poly(new[] { new Vector2(0.32f, 0.2f), new Vector2(0.82f, 0.5f), new Vector2(0.32f, 0.8f) });
                    break;
                case IconId.Pause:
                    p.Box(0.36f, 0.5f, 0.08f, 0.28f, 0.04f);
                    p.Box(0.64f, 0.5f, 0.08f, 0.28f, 0.04f);
                    break;
                case IconId.Back:
                case IconId.ArrowLeft:
                    p.Capsule(0.3f, 0.5f, 0.72f, 0.5f, 0.06f);
                    p.Capsule(0.3f, 0.5f, 0.52f, 0.72f, 0.06f);
                    p.Capsule(0.3f, 0.5f, 0.52f, 0.28f, 0.06f);
                    break;
                case IconId.ArrowRight:
                    p.Capsule(0.7f, 0.5f, 0.28f, 0.5f, 0.06f);
                    p.Capsule(0.7f, 0.5f, 0.48f, 0.72f, 0.06f);
                    p.Capsule(0.7f, 0.5f, 0.48f, 0.28f, 0.06f);
                    break;
                case IconId.Gear:
                    p.Poly(Star(0.5f, 0.5f, 0.42f, 0.32f, 8, 0f));
                    p.Circle(0.5f, 0.5f, 0.34f);
                    p.Circle(0.5f, 0.5f, 0.13f, true);
                    break;
                case IconId.Coin:
                    p.Circle(0.5f, 0.5f, 0.4f);
                    p.Ring(0.5f, 0.5f, 0.3f, 0.05f, true);
                    p.Box(0.5f, 0.5f, 0.05f, 0.16f, 0.02f, true);
                    break;
                case IconId.Trophy:
                    p.Box(0.5f, 0.74f, 0.24f, 0.12f, 0.02f);
                    p.Ring(0.25f, 0.68f, 0.09f, 0.05f);
                    p.Ring(0.75f, 0.68f, 0.09f, 0.05f);
                    p.Box(0.5f, 0.35f, 0.05f, 0.08f, 0.01f);
                    p.Box(0.5f, 0.2f, 0.2f, 0.06f, 0.03f);
                    p.Shape((u, v) => { float d = Dist(u, v, 0.5f, 0.62f) - 0.24f; return Mathf.Max(d, v - 0.62f); });
                    break;
                case IconId.Star:
                    p.Poly(Star(0.5f, 0.52f, 0.44f, 0.19f, 5, H));
                    break;
                case IconId.StarEmpty:
                    p.Poly(Star(0.5f, 0.52f, 0.44f, 0.19f, 5, H));
                    p.Poly(Star(0.5f, 0.52f, 0.32f, 0.12f, 5, H), true);
                    break;
                case IconId.Lock:
                    p.Box(0.5f, 0.38f, 0.28f, 0.22f, 0.05f);
                    p.Arc(0.5f, 0.6f, 0.17f, 0.07f, 0f, Mathf.PI);
                    p.Capsule(0.33f, 0.6f, 0.33f, 0.5f, 0.035f);
                    p.Capsule(0.67f, 0.6f, 0.67f, 0.5f, 0.035f);
                    p.Circle(0.5f, 0.4f, 0.05f, true);
                    p.Box(0.5f, 0.32f, 0.02f, 0.06f, 0.01f, true);
                    break;
                case IconId.Racket:
                    p.Shape((u, v) => (Mathf.Sqrt(Mathf.Pow((u - 0.42f) / 0.28f, 2f) + Mathf.Pow((v - 0.6f) / 0.3f, 2f)) - 1f) * 0.26f);
                    p.Capsule(0.6f, 0.36f, 0.78f, 0.14f, 0.06f);
                    p.Circle(0.78f, 0.78f, 0.08f);
                    break;
                case IconId.Table:
                    p.Poly(new[] { new Vector2(0.12f, 0.5f), new Vector2(0.88f, 0.5f), new Vector2(0.76f, 0.7f), new Vector2(0.24f, 0.7f) });
                    p.Box(0.5f, 0.6f, 0.015f, 0.12f, 0.005f, true);
                    p.Box(0.5f, 0.72f, 0.02f, 0.06f, 0.01f);
                    p.Box(0.24f, 0.35f, 0.035f, 0.15f, 0.015f);
                    p.Box(0.76f, 0.35f, 0.035f, 0.15f, 0.015f);
                    break;
                case IconId.Fire:
                    p.Shape((u, v) =>
                    {
                        float x = u - 0.5f, y = v - 0.38f;
                        float body = Mathf.Sqrt(x * x / 0.07f + y * y / 0.06f) - 1f;
                        float tip = SegDist(u, v, 0.5f, 0.45f, 0.56f, 0.9f) - 0.08f;
                        return Mathf.Min(body * 0.2f, tip);
                    });
                    p.Shape((u, v) => Mathf.Sqrt(Mathf.Pow(u - 0.5f, 2f) / 0.015f + Mathf.Pow(v - 0.33f, 2f) / 0.02f) * 0.1f - 0.1f, true);
                    break;
                case IconId.Slow:
                    p.Ring(0.5f, 0.5f, 0.36f, 0.08f);
                    p.Capsule(0.5f, 0.5f, 0.5f, 0.74f, 0.045f);
                    p.Capsule(0.5f, 0.5f, 0.66f, 0.42f, 0.045f);
                    break;
                case IconId.Curve:
                    p.Arc(0.5f, 0.38f, 0.3f, 0.08f, 0.15f, Mathf.PI - 0.15f);
                    p.Poly(new[] { new Vector2(0.1f, 0.48f), new Vector2(0.32f, 0.5f), new Vector2(0.2f, 0.3f) });
                    p.Circle(0.8f, 0.43f, 0.08f);
                    break;
                case IconId.Smash:
                    p.Poly(new[] { new Vector2(0.58f, 0.92f), new Vector2(0.22f, 0.46f), new Vector2(0.46f, 0.46f), new Vector2(0.38f, 0.08f), new Vector2(0.78f, 0.56f), new Vector2(0.54f, 0.56f) });
                    break;
                case IconId.Shield:
                    p.Shape((u, v) =>
                    {
                        float x = Mathf.Abs(u - 0.5f);
                        float top = v - 0.86f;
                        float side = x - 0.34f;
                        float bottom = Dist(u, v, 0.5f, 0.55f) - 0.42f;
                        return Mathf.Max(Mathf.Max(top, side), v < 0.55f ? bottom : -1f);
                    });
                    p.Shape((u, v) =>
                    {
                        float x = Mathf.Abs(u - 0.5f);
                        float top = v - 0.78f;
                        float side = x - 0.26f;
                        float bottom = Dist(u, v, 0.5f, 0.55f) - 0.34f;
                        return Mathf.Max(Mathf.Max(top, side), v < 0.55f ? bottom : -1f);
                    }, true);
                    p.Box(0.5f, 0.52f, 0.04f, 0.18f, 0.02f);
                    p.Box(0.5f, 0.56f, 0.15f, 0.04f, 0.02f);
                    break;
                case IconId.Freeze:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        float cx = Mathf.Cos(a) * 0.38f, cy = Mathf.Sin(a) * 0.38f;
                        p.Capsule(0.5f - cx, 0.5f - cy, 0.5f + cx, 0.5f + cy, 0.035f);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            float bx = 0.5f + cx * 0.65f * s, by = 0.5f + cy * 0.65f * s;
                            float pa = a + Mathf.PI / 2f;
                            p.Capsule(bx, by, bx + Mathf.Cos(pa) * 0.1f + cx * 0.25f * s, by + Mathf.Sin(pa) * 0.1f + cy * 0.25f * s, 0.025f);
                            p.Capsule(bx, by, bx - Mathf.Cos(pa) * 0.1f + cx * 0.25f * s, by - Mathf.Sin(pa) * 0.1f + cy * 0.25f * s, 0.025f);
                        }
                    }
                    break;
                case IconId.Random:
                    p.Box(0.5f, 0.5f, 0.34f, 0.34f, 0.08f);
                    p.Circle(0.34f, 0.66f, 0.06f, true);
                    p.Circle(0.66f, 0.34f, 0.06f, true);
                    p.Circle(0.5f, 0.5f, 0.06f, true);
                    p.Circle(0.66f, 0.66f, 0.06f, true);
                    p.Circle(0.34f, 0.34f, 0.06f, true);
                    break;
                case IconId.Special:
                    p.Poly(new[] { new Vector2(0.14f, 0.3f), new Vector2(0.86f, 0.3f), new Vector2(0.92f, 0.72f), new Vector2(0.7f, 0.52f), new Vector2(0.5f, 0.84f), new Vector2(0.3f, 0.52f), new Vector2(0.08f, 0.72f) });
                    p.Box(0.5f, 0.22f, 0.36f, 0.05f, 0.02f);
                    break;
                case IconId.Profile:
                    p.Circle(0.5f, 0.66f, 0.17f);
                    p.Shape((u, v) => Mathf.Max(Dist(u, v, 0.5f, 0.12f) - 0.36f, -(v - 0.15f)));
                    break;
                case IconId.Shop:
                    p.Poly(new[] { new Vector2(0.14f, 0.7f), new Vector2(0.86f, 0.7f), new Vector2(0.78f, 0.36f), new Vector2(0.26f, 0.36f) });
                    p.Capsule(0.08f, 0.8f, 0.16f, 0.7f, 0.035f);
                    p.Circle(0.32f, 0.22f, 0.07f);
                    p.Circle(0.72f, 0.22f, 0.07f);
                    break;
                case IconId.Target:
                    p.Ring(0.5f, 0.5f, 0.38f, 0.07f);
                    p.Ring(0.5f, 0.5f, 0.22f, 0.07f);
                    p.Circle(0.5f, 0.5f, 0.07f);
                    break;
                case IconId.Music:
                    p.Circle(0.33f, 0.28f, 0.11f);
                    p.Circle(0.7f, 0.36f, 0.11f);
                    p.Capsule(0.42f, 0.28f, 0.42f, 0.8f, 0.035f);
                    p.Capsule(0.79f, 0.36f, 0.79f, 0.86f, 0.035f);
                    p.Poly(new[] { new Vector2(0.39f, 0.82f), new Vector2(0.82f, 0.9f), new Vector2(0.82f, 0.76f), new Vector2(0.39f, 0.68f) });
                    break;
                case IconId.Sound:
                    p.Poly(new[] { new Vector2(0.14f, 0.4f), new Vector2(0.3f, 0.4f), new Vector2(0.52f, 0.2f), new Vector2(0.52f, 0.8f), new Vector2(0.3f, 0.6f), new Vector2(0.14f, 0.6f) });
                    p.Arc(0.52f, 0.5f, 0.16f, 0.05f, -0.9f, 0.9f);
                    p.Arc(0.52f, 0.5f, 0.3f, 0.05f, -0.9f, 0.9f);
                    break;
                case IconId.Vibration:
                    p.Box(0.5f, 0.5f, 0.16f, 0.3f, 0.05f);
                    p.Box(0.5f, 0.5f, 0.1f, 0.22f, 0.02f, true);
                    p.Capsule(0.2f, 0.36f, 0.2f, 0.64f, 0.03f);
                    p.Capsule(0.8f, 0.36f, 0.8f, 0.64f, 0.03f);
                    p.Capsule(0.1f, 0.42f, 0.1f, 0.58f, 0.03f);
                    p.Capsule(0.9f, 0.42f, 0.9f, 0.58f, 0.03f);
                    break;
                case IconId.Check:
                    p.Capsule(0.2f, 0.5f, 0.42f, 0.28f, 0.07f);
                    p.Capsule(0.42f, 0.28f, 0.82f, 0.74f, 0.07f);
                    break;
                case IconId.Cross:
                    p.Capsule(0.24f, 0.24f, 0.76f, 0.76f, 0.07f);
                    p.Capsule(0.24f, 0.76f, 0.76f, 0.24f, 0.07f);
                    break;
                case IconId.Plus:
                    p.Capsule(0.2f, 0.5f, 0.8f, 0.5f, 0.07f);
                    p.Capsule(0.5f, 0.2f, 0.5f, 0.8f, 0.07f);
                    break;
                case IconId.Minus:
                    p.Capsule(0.2f, 0.5f, 0.8f, 0.5f, 0.07f);
                    break;
                case IconId.TwoPlayers:
                    p.Circle(0.34f, 0.66f, 0.13f);
                    p.Shape((u, v) => Mathf.Max(Dist(u, v, 0.34f, 0.2f) - 0.26f, -(v - 0.2f)));
                    p.Circle(0.68f, 0.7f, 0.12f);
                    p.Shape((u, v) => Mathf.Max(Dist(u, v, 0.7f, 0.26f) - 0.24f, -(v - 0.26f)));
                    break;
                case IconId.Cpu:
                    p.Box(0.5f, 0.48f, 0.3f, 0.26f, 0.06f);
                    p.Circle(0.38f, 0.52f, 0.06f, true);
                    p.Circle(0.62f, 0.52f, 0.06f, true);
                    p.Box(0.5f, 0.34f, 0.12f, 0.025f, 0.01f, true);
                    p.Capsule(0.5f, 0.74f, 0.5f, 0.86f, 0.03f);
                    p.Circle(0.5f, 0.88f, 0.05f);
                    break;
                case IconId.Training:
                    p.Poly(new[] { new Vector2(0.5f, 0.88f), new Vector2(0.72f, 0.22f), new Vector2(0.28f, 0.22f) });
                    p.Box(0.5f, 0.16f, 0.34f, 0.05f, 0.02f);
                    p.Box(0.5f, 0.42f, 0.2f, 0.04f, 0.01f, true);
                    p.Box(0.5f, 0.62f, 0.12f, 0.035f, 0.01f, true);
                    break;
                case IconId.Bracket:
                    p.Capsule(0.12f, 0.8f, 0.38f, 0.8f, 0.035f);
                    p.Capsule(0.12f, 0.56f, 0.38f, 0.56f, 0.035f);
                    p.Capsule(0.38f, 0.8f, 0.38f, 0.56f, 0.035f);
                    p.Capsule(0.38f, 0.68f, 0.62f, 0.68f, 0.035f);
                    p.Capsule(0.12f, 0.44f, 0.38f, 0.44f, 0.035f);
                    p.Capsule(0.12f, 0.2f, 0.38f, 0.2f, 0.035f);
                    p.Capsule(0.38f, 0.44f, 0.38f, 0.2f, 0.035f);
                    p.Capsule(0.38f, 0.32f, 0.62f, 0.32f, 0.035f);
                    p.Capsule(0.62f, 0.68f, 0.62f, 0.32f, 0.035f);
                    p.Capsule(0.62f, 0.5f, 0.88f, 0.5f, 0.035f);
                    break;
                case IconId.Restart:
                    p.Arc(0.5f, 0.5f, 0.3f, 0.08f, 0.6f, Mathf.PI * 2f - 0.2f);
                    p.Poly(new[] { new Vector2(0.86f, 0.7f), new Vector2(0.66f, 0.54f), new Vector2(0.9f, 0.48f) });
                    break;
                case IconId.Home:
                    p.Poly(new[] { new Vector2(0.5f, 0.86f), new Vector2(0.88f, 0.52f), new Vector2(0.12f, 0.52f) });
                    p.Box(0.5f, 0.36f, 0.28f, 0.2f, 0.03f);
                    p.Box(0.5f, 0.26f, 0.07f, 0.11f, 0.02f, true);
                    break;
                case IconId.Info:
                    p.Ring(0.5f, 0.5f, 0.38f, 0.07f);
                    p.Circle(0.5f, 0.7f, 0.055f);
                    p.Box(0.5f, 0.42f, 0.045f, 0.16f, 0.02f);
                    break;
                case IconId.Ball:
                    p.Circle(0.5f, 0.5f, 0.36f);
                    p.Arc(0.5f, 0.5f, 0.24f, 0.045f, 2.2f, 3.6f, true);
                    break;
            }
        }

        /// <summary>
        /// 9-sliced rounded panel sprite (radius in pixels). Optional outline for buttons.
        /// </summary>
        public static Sprite RoundedPanel(int radius, int outline = 0)
        {
            string key = radius + "_" + outline;
            Sprite s;
            if (Panels.TryGetValue(key, out s) && s != null) return s;
            int size = radius * 2 + 8;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.name = "panel_" + key;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - 1f) + radius;
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - 1f) + radius;
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (outline > 0)
                    {
                        float inner = Mathf.Clamp01(0.5f - (d + outline));
                        a = Mathf.Clamp01(a - inner);
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            float b = radius + 2;
            s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            Panels[key] = s;
            return s;
        }

        /// <summary>Horizontal gradient strip sprite for fancy bars/headers.</summary>
        public static Sprite Gradient(Color left, Color right)
        {
            string key = "grad_" + ColorUtility.ToHtmlStringRGBA(left) + ColorUtility.ToHtmlStringRGBA(right);
            Sprite s;
            if (Panels.TryGetValue(key, out s) && s != null) return s;
            Texture2D t = new Texture2D(64, 4, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[64 * 4];
            for (int x = 0; x < 64; x++)
            {
                Color c = Color.Lerp(left, right, x / 63f);
                for (int y = 0; y < 4; y++) px[y * 64 + x] = c;
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            s = Sprite.Create(t, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f), 100f);
            Panels[key] = s;
            return s;
        }

        /// <summary>Circle sprite (for radial fills / dots).</summary>
        public static Sprite CircleSprite()
        {
            Sprite s;
            if (Panels.TryGetValue("circle", out s) && s != null) return s;
            Painter p = new Painter();
            p.Circle(0.5f, 0.5f, 0.48f);
            s = p.ToSprite("circle");
            Panels["circle"] = s;
            return s;
        }

        public static Sprite RingSprite()
        {
            Sprite s;
            if (Panels.TryGetValue("ring", out s) && s != null) return s;
            Painter p = new Painter();
            p.Ring(0.5f, 0.5f, 0.44f, 0.06f);
            s = p.ToSprite("ring");
            Panels["ring"] = s;
            return s;
        }
    }
}
