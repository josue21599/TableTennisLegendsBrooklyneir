using System.Collections.Generic;
using UnityEngine;

namespace Brooklyneir.Art
{
    /// <summary>
    /// Procedural mesh generation. All game models (table, net, rackets, hands, ball, scenery) are
    /// built from these primitives at runtime, so the project needs no imported model files.
    /// Every mesh gets proper normals and UVs so URP lighting, shadows and textures work.
    /// </summary>
    public static class MeshFactory
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        static Mesh Cached(string key, System.Func<Mesh> build)
        {
            Mesh m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = build();
            m.name = key;
            Cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ sphere

        public static Mesh Sphere(float radius, int longitude = 24, int latitude = 16)
        {
            return Cached("sphere_" + radius + "_" + longitude + "_" + latitude, () =>
            {
                List<Vector3> v = new List<Vector3>();
                List<Vector3> n = new List<Vector3>();
                List<Vector2> uv = new List<Vector2>();
                List<int> t = new List<int>();
                for (int lat = 0; lat <= latitude; lat++)
                {
                    float a = Mathf.PI * lat / latitude;
                    float y = Mathf.Cos(a), r = Mathf.Sin(a);
                    for (int lon = 0; lon <= longitude; lon++)
                    {
                        float b = 2f * Mathf.PI * lon / longitude;
                        Vector3 dir = new Vector3(r * Mathf.Cos(b), y, r * Mathf.Sin(b));
                        v.Add(dir * radius);
                        n.Add(dir);
                        uv.Add(new Vector2(lon / (float)longitude, 1f - lat / (float)latitude));
                    }
                }
                int row = longitude + 1;
                for (int lat = 0; lat < latitude; lat++)
                    for (int lon = 0; lon < longitude; lon++)
                    {
                        int i0 = lat * row + lon, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                        t.Add(i0); t.Add(i1); t.Add(i2);
                        t.Add(i1); t.Add(i3); t.Add(i2);
                    }
                return Build(v, n, uv, t);
            });
        }

        /// <summary>Inside-facing sphere for sky domes.</summary>
        public static Mesh SkyDome(float radius)
        {
            return Cached("skydome_" + radius, () =>
            {
                Mesh s = Object.Instantiate(Sphere(1f, 32, 20));
                Vector3[] v = s.vertices;
                Vector3[] n = s.normals;
                for (int i = 0; i < v.Length; i++) { v[i] *= radius; n[i] = -n[i]; }
                int[] tri = s.triangles;
                for (int i = 0; i < tri.Length; i += 3) { int tmp = tri[i]; tri[i] = tri[i + 1]; tri[i + 1] = tmp; }
                s.vertices = v;
                s.normals = n;
                s.triangles = tri;
                s.RecalculateBounds();
                return s;
            });
        }

        // ------------------------------------------------------------------ rounded box

        /// <summary>
        /// Box with genuinely rounded edges and corners (bevel radius <paramref name="radius"/>).
        /// Each face is a grid whose rows/columns are concentrated inside the bevel band; every grid
        /// point is projected onto the rounded surface (inner box + radius along the offset).
        /// Adjacent faces meet at 45 degrees with identical positions and normals, so shading is smooth.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 3)
        {
            string key = string.Format("rbox_{0:F3}_{1:F3}_{2:F3}_{3:F3}_{4}", size.x, size.y, size.z, radius, segments);
            return Cached(key, () =>
            {
                float r = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f - 0.0001f);
                if (r < 0.0005f) return Box(size);
                Vector3 half = size * 0.5f;
                Vector3 inner = half - new Vector3(r, r, r);
                List<Vector3> v = new List<Vector3>();
                List<Vector3> n = new List<Vector3>();
                List<Vector2> uv = new List<Vector2>();
                List<int> t = new List<int>();
                Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                for (int f = 0; f < 6; f++)
                {
                    Vector3 nrm = normals[f];
                    Vector3 axisA = new Vector3(Mathf.Abs(nrm.y), Mathf.Abs(nrm.z), Mathf.Abs(nrm.x));
                    Vector3 axisB = Vector3.Cross(nrm, axisA);
                    float ha = Mathf.Abs(Vector3.Dot(half, axisA));
                    float hb = Mathf.Abs(Vector3.Dot(half, new Vector3(Mathf.Abs(axisB.x), Mathf.Abs(axisB.y), Mathf.Abs(axisB.z))));
                    float hn = Mathf.Abs(Vector3.Dot(half, new Vector3(Mathf.Abs(nrm.x), Mathf.Abs(nrm.y), Mathf.Abs(nrm.z))));
                    float[] ca = BevelCoords(ha, r, segments);
                    float[] cb = BevelCoords(hb, r, segments);
                    int start = v.Count;
                    for (int y = 0; y < cb.Length; y++)
                        for (int x = 0; x < ca.Length; x++)
                        {
                            Vector3 p = nrm * hn + axisA * ca[x] + axisB * cb[y];
                            Vector3 clamped = new Vector3(
                                Mathf.Clamp(p.x, -inner.x, inner.x),
                                Mathf.Clamp(p.y, -inner.y, inner.y),
                                Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 dir = p - clamped;
                            Vector3 nn = dir.sqrMagnitude > 1e-12f ? dir.normalized : nrm;
                            v.Add(clamped + nn * r);
                            n.Add(nn);
                            uv.Add(new Vector2(ca[x] / (2f * ha) + 0.5f, cb[y] / (2f * hb) + 0.5f));
                        }
                    int row = ca.Length;
                    for (int y = 0; y < cb.Length - 1; y++)
                        for (int x = 0; x < ca.Length - 1; x++)
                        {
                            int i0 = start + y * row + x, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                            t.Add(i0); t.Add(i1); t.Add(i2);
                            t.Add(i1); t.Add(i3); t.Add(i2);
                        }
                }
                return Build(v, n, uv, t);
            });
        }

        /// <summary>Coordinates from -h to h: 'segments' steps inside each bevel band, one flat span between.</summary>
        static float[] BevelCoords(float h, float r, int segments)
        {
            float[] c = new float[(segments + 1) * 2];
            for (int k = 0; k <= segments; k++)
            {
                float f = k / (float)segments;
                c[k] = -h + r * f;
                c[segments + 1 + k] = (h - r) + r * f;
            }
            return c;
        }

        /// <summary>Plain box with hard edges, UVs per face.</summary>
        public static Mesh Box(Vector3 size)
        {
            string key = string.Format("box_{0:F3}_{1:F3}_{2:F3}", size.x, size.y, size.z);
            return Cached(key, () =>
            {
                List<Vector3> v = new List<Vector3>();
                List<Vector3> n = new List<Vector3>();
                List<Vector2> uv = new List<Vector2>();
                List<int> t = new List<int>();
                Vector3 h = size * 0.5f;
                Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (Vector3 nrm in normals)
                {
                    Vector3 a = new Vector3(nrm.y, nrm.z, nrm.x);
                    Vector3 b = Vector3.Cross(nrm, a);
                    int s = v.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        float fx = (i == 1 || i == 2) ? 1f : -1f;
                        float fy = (i >= 2) ? 1f : -1f;
                        Vector3 p = nrm + a * fx + b * fy;
                        v.Add(Vector3.Scale(p, h));
                        n.Add(nrm);
                        uv.Add(new Vector2(fx * 0.5f + 0.5f, fy * 0.5f + 0.5f));
                    }
                    t.Add(s); t.Add(s + 1); t.Add(s + 3);
                    t.Add(s + 1); t.Add(s + 2); t.Add(s + 3);
                }
                return Build(v, n, uv, t);
            });
        }

        // ------------------------------------------------------------------ cylinders, discs, rings

        /// <summary>Cylinder along Y, pivot at the centre.</summary>
        public static Mesh Cylinder(float radius, float height, int segments = 20, bool caps = true)
        {
            return Cached("cyl_" + radius + "_" + height + "_" + segments + "_" + caps, () =>
                Lathe(new[] { new Vector2(0f, -height * 0.5f), new Vector2(radius, -height * 0.5f), new Vector2(radius, height * 0.5f), new Vector2(0f, height * 0.5f) },
                    segments, caps));
        }

        /// <summary>Tapered cylinder (cone frustum) along Y.</summary>
        public static Mesh Frustum(float bottomRadius, float topRadius, float height, int segments = 20)
        {
            return Cached("frus_" + bottomRadius + "_" + topRadius + "_" + height + "_" + segments, () =>
                Lathe(new[] { new Vector2(0f, -height * 0.5f), new Vector2(bottomRadius, -height * 0.5f), new Vector2(topRadius, height * 0.5f), new Vector2(0f, height * 0.5f) },
                    segments, true));
        }

        /// <summary>
        /// Surface of revolution around Y from a profile (x = radius, y = height), bottom to top.
        /// Hard edges at profile corners are produced by duplicating vertices per segment.
        /// </summary>
        public static Mesh Lathe(Vector2[] profile, int segments, bool closed = true)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> t = new List<int>();
            float total = 0f;
            for (int i = 1; i < profile.Length; i++) total += (profile[i] - profile[i - 1]).magnitude;
            float acc = 0f;
            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                float len = (b - a).magnitude;
                if (len < 1e-6f) continue;
                Vector2 d = (b - a) / len;
                Vector2 nrm2 = new Vector2(d.y, -d.x); // outward for a bottom-to-top profile
                int start = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float ang = 2f * Mathf.PI * s / segments;
                    float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    v.Add(new Vector3(a.x * c, a.y, a.x * sn));
                    v.Add(new Vector3(b.x * c, b.y, b.x * sn));
                    Vector3 nn = new Vector3(nrm2.x * c, nrm2.y, nrm2.x * sn).normalized;
                    n.Add(nn);
                    n.Add(nn);
                    uv.Add(new Vector2(s / (float)segments, acc / Mathf.Max(0.0001f, total)));
                    uv.Add(new Vector2(s / (float)segments, (acc + len) / Mathf.Max(0.0001f, total)));
                }
                for (int s = 0; s < segments; s++)
                {
                    int i0 = start + s * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                    t.Add(i0); t.Add(i1); t.Add(i2);
                    t.Add(i2); t.Add(i1); t.Add(i3);
                }
                acc += len;
            }
            Mesh m = Build(v, n, uv, t);
            if (!closed) m.name = "lathe_open";
            return m;
        }

        /// <summary>Capsule along Y (total height includes the hemispherical caps), pivot at centre.</summary>
        public static Mesh Capsule(float radius, float height, int segments = 16, int capRings = 5)
        {
            return Cached("caps_" + radius + "_" + height + "_" + segments + "_" + capRings, () =>
            {
                float half = Mathf.Max(0f, height * 0.5f - radius);
                List<Vector2> prof = new List<Vector2>();
                for (int i = 0; i <= capRings; i++)
                {
                    float a = -Mathf.PI / 2f + (Mathf.PI / 2f) * i / capRings;
                    prof.Add(new Vector2(Mathf.Cos(a) * radius, -half + Mathf.Sin(a) * radius));
                }
                for (int i = 0; i <= capRings; i++)
                {
                    float a = (Mathf.PI / 2f) * i / capRings;
                    prof.Add(new Vector2(Mathf.Cos(a) * radius, half + Mathf.Sin(a) * radius));
                }
                return SmoothLathe(prof.ToArray(), segments);
            });
        }

        /// <summary>Lathe with smooth (shared) normals along the profile, for organic shapes.</summary>
        public static Mesh SmoothLathe(Vector2[] profile, int segments)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> t = new List<int>();
            int count = profile.Length;
            for (int i = 0; i < count; i++)
            {
                Vector2 prev = profile[Mathf.Max(0, i - 1)], next = profile[Mathf.Min(count - 1, i + 1)];
                Vector2 d = (next - prev).normalized;
                Vector2 nrm2 = new Vector2(d.y, -d.x);
                for (int s = 0; s <= segments; s++)
                {
                    float ang = 2f * Mathf.PI * s / segments;
                    float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    v.Add(new Vector3(profile[i].x * c, profile[i].y, profile[i].x * sn));
                    n.Add(new Vector3(nrm2.x * c, nrm2.y, nrm2.x * sn).normalized);
                    uv.Add(new Vector2(s / (float)segments, i / (float)(count - 1)));
                }
            }
            int row = segments + 1;
            for (int i = 0; i < count - 1; i++)
                for (int s = 0; s < segments; s++)
                {
                    int i0 = i * row + s, i1 = (i + 1) * row + s, i2 = i0 + 1, i3 = i1 + 1;
                    t.Add(i0); t.Add(i1); t.Add(i2);
                    t.Add(i2); t.Add(i1); t.Add(i3);
                }
            return Build(v, n, uv, t);
        }

        /// <summary>Flat disc in the XZ plane facing +Y.</summary>
        public static Mesh Disc(float radius, int segments = 32)
        {
            return Cached("disc_" + radius + "_" + segments, () => Ring(0f, radius, segments));
        }

        /// <summary>Flat ring in the XZ plane facing +Y (UV.x around, UV.y inner to outer).</summary>
        public static Mesh Ring(float inner, float outer, int segments = 48)
        {
            return Cached("ring_" + inner + "_" + outer + "_" + segments, () =>
            {
                List<Vector3> v = new List<Vector3>();
                List<Vector3> n = new List<Vector3>();
                List<Vector2> uv = new List<Vector2>();
                List<int> t = new List<int>();
                for (int s = 0; s <= segments; s++)
                {
                    float a = 2f * Mathf.PI * s / segments;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    v.Add(new Vector3(c * inner, 0f, sn * inner));
                    v.Add(new Vector3(c * outer, 0f, sn * outer));
                    n.Add(Vector3.up); n.Add(Vector3.up);
                    uv.Add(new Vector2(c * 0.5f * (inner / Mathf.Max(outer, 0.0001f)) + 0.5f, sn * 0.5f * (inner / Mathf.Max(outer, 0.0001f)) + 0.5f));
                    uv.Add(new Vector2(c * 0.5f + 0.5f, sn * 0.5f + 0.5f));
                }
                for (int s = 0; s < segments; s++)
                {
                    int i0 = s * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                    t.Add(i0); t.Add(i2); t.Add(i1);
                    t.Add(i1); t.Add(i2); t.Add(i3);
                }
                return Build(v, n, uv, t);
            });
        }

        /// <summary>Quad in the XY plane facing -Z (towards a default camera), pivot at centre.</summary>
        public static Mesh Quad(float width, float height)
        {
            return Cached("quad_" + width + "_" + height, () =>
            {
                float w = width * 0.5f, h = height * 0.5f;
                List<Vector3> v = new List<Vector3> { new Vector3(-w, -h, 0), new Vector3(w, -h, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0) };
                List<Vector3> n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                List<Vector2> uv = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                List<int> t = new List<int> { 0, 2, 1, 0, 3, 2 };
                return Build(v, n, uv, t);
            });
        }

        /// <summary>Horizontal plane in XZ facing +Y with tiled UVs.</summary>
        public static Mesh Plane(float width, float depth, float uvTile = 1f)
        {
            return Cached("plane_" + width + "_" + depth + "_" + uvTile, () =>
            {
                float w = width * 0.5f, d = depth * 0.5f;
                List<Vector3> v = new List<Vector3> { new Vector3(-w, 0, -d), new Vector3(w, 0, -d), new Vector3(w, 0, d), new Vector3(-w, 0, d) };
                List<Vector3> n = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                float tu = width * uvTile, tv = depth * uvTile;
                List<Vector2> uv = new List<Vector2> { new Vector2(0, 0), new Vector2(tu, 0), new Vector2(tu, tv), new Vector2(0, tv) };
                List<int> t = new List<int> { 0, 2, 1, 0, 3, 2 };
                return Build(v, n, uv, t);
            });
        }

        // ------------------------------------------------------------------ torus

        public static Mesh Torus(float major, float minor, int majorSegments = 24, int minorSegments = 10)
        {
            return Cached("torus_" + major + "_" + minor + "_" + majorSegments + "_" + minorSegments, () =>
            {
                List<Vector3> v = new List<Vector3>();
                List<Vector3> n = new List<Vector3>();
                List<Vector2> uv = new List<Vector2>();
                List<int> t = new List<int>();
                for (int i = 0; i <= majorSegments; i++)
                {
                    float a = 2f * Mathf.PI * i / majorSegments;
                    Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    for (int j = 0; j <= minorSegments; j++)
                    {
                        float b = 2f * Mathf.PI * j / minorSegments;
                        Vector3 dir = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        v.Add(c * major + dir * minor);
                        n.Add(dir);
                        uv.Add(new Vector2(i / (float)majorSegments, j / (float)minorSegments));
                    }
                }
                int row = minorSegments + 1;
                for (int i = 0; i < majorSegments; i++)
                    for (int j = 0; j < minorSegments; j++)
                    {
                        int i0 = i * row + j, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                        t.Add(i0); t.Add(i1); t.Add(i2);
                        t.Add(i1); t.Add(i3); t.Add(i2);
                    }
                return Build(v, n, uv, t);
            });
        }

        // ------------------------------------------------------------------ extrusions

        /// <summary>
        /// Extrudes a closed 2D outline (counter-clockwise, in the XY plane) along Z, centred.
        /// Caps are triangulated as a fan from the centroid, valid for star-shaped outlines such as
        /// racket blades. Side walls use smooth normals.
        /// </summary>
        public static Mesh Extrude(Vector2[] outline, float thickness, bool frontCap = true, bool backCap = true)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> t = new List<int>();
            int count = outline.Length;
            Vector2 min = outline[0], max = outline[0], centroid = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                min = new Vector2(Mathf.Min(min.x, outline[i].x), Mathf.Min(min.y, outline[i].y));
                max = new Vector2(Mathf.Max(max.x, outline[i].x), Mathf.Max(max.y, outline[i].y));
                centroid += outline[i];
            }
            centroid /= count;
            Vector2 extent = max - min;
            float hz = thickness * 0.5f;
            for (int side = 0; side < 2; side++)
            {
                bool front = side == 0;
                if (front && !frontCap) continue;
                if (!front && !backCap) continue;
                float z = front ? -hz : hz;
                Vector3 nrm = front ? Vector3.back : Vector3.forward;
                int c = v.Count;
                v.Add(new Vector3(centroid.x, centroid.y, z));
                n.Add(nrm);
                uv.Add(new Vector2((centroid.x - min.x) / extent.x, (centroid.y - min.y) / extent.y));
                for (int i = 0; i < count; i++)
                {
                    v.Add(new Vector3(outline[i].x, outline[i].y, z));
                    n.Add(nrm);
                    uv.Add(new Vector2((outline[i].x - min.x) / extent.x, (outline[i].y - min.y) / extent.y));
                }
                for (int i = 0; i < count; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % count;
                    if (front) { t.Add(c); t.Add(b); t.Add(a); }
                    else { t.Add(c); t.Add(a); t.Add(b); }
                }
            }
            // Side wall.
            int wall = v.Count;
            float perim = 0f;
            for (int i = 0; i <= count; i++)
            {
                Vector2 p = outline[i % count];
                Vector2 prev = outline[(i - 1 + count) % count], next = outline[(i + 1) % count];
                Vector2 tangent = (next - prev).normalized;
                Vector3 nn = new Vector3(tangent.y, -tangent.x, 0f);
                if (i > 0) perim += (outline[i % count] - outline[i - 1]).magnitude;
                v.Add(new Vector3(p.x, p.y, -hz));
                v.Add(new Vector3(p.x, p.y, hz));
                n.Add(nn); n.Add(nn);
                uv.Add(new Vector2(perim * 4f, 0f));
                uv.Add(new Vector2(perim * 4f, 1f));
            }
            for (int i = 0; i < count; i++)
            {
                int i0 = wall + i * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                t.Add(i0); t.Add(i2); t.Add(i1);
                t.Add(i1); t.Add(i2); t.Add(i3);
            }
            return Build(v, n, uv, t);
        }

        /// <summary>Band around an outline (edge tape): an extrusion of the outline's side wall only.</summary>
        public static Mesh OutlineBand(Vector2[] outline, float thickness, float inflate)
        {
            Vector2[] grown = Offset(outline, inflate);
            return Extrude(grown, thickness, false, false);
        }

        /// <summary>Moves every outline point along its normal (positive = outward for CCW outlines).</summary>
        public static Vector2[] Offset(Vector2[] outline, float amount)
        {
            int count = outline.Length;
            Vector2[] r = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 prev = outline[(i - 1 + count) % count], next = outline[(i + 1) % count];
                Vector2 tangent = (next - prev).normalized;
                Vector2 nrm = new Vector2(tangent.y, -tangent.x);
                r[i] = outline[i] + nrm * amount;
            }
            return r;
        }

        // ------------------------------------------------------------------ net

        /// <summary>A flat vertical quad spanning X (net mesh), double-sided via two faces.</summary>
        public static Mesh DoubleSidedQuad(float width, float height, float uvTilesX, float uvTilesY)
        {
            return Cached("dsq_" + width + "_" + height + "_" + uvTilesX + "_" + uvTilesY, () =>
            {
                float w = width * 0.5f;
                List<Vector3> v = new List<Vector3>
                {
                    new Vector3(-w, 0, 0), new Vector3(w, 0, 0), new Vector3(w, height, 0), new Vector3(-w, height, 0),
                    new Vector3(-w, 0, 0), new Vector3(w, 0, 0), new Vector3(w, height, 0), new Vector3(-w, height, 0)
                };
                List<Vector3> n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
                List<Vector2> uv = new List<Vector2>
                {
                    new Vector2(0, 0), new Vector2(uvTilesX, 0), new Vector2(uvTilesX, uvTilesY), new Vector2(0, uvTilesY),
                    new Vector2(0, 0), new Vector2(uvTilesX, 0), new Vector2(uvTilesX, uvTilesY), new Vector2(0, uvTilesY)
                };
                List<int> t = new List<int> { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7 };
                return Build(v, n, uv, t);
            });
        }

        // ------------------------------------------------------------------ helpers

        static Mesh Build(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            Mesh m = new Mesh();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Combines several meshes (with local transforms) into one, for static scenery.</summary>
        public static Mesh Combine(List<CombineInstance> parts, string name)
        {
            Mesh m = new Mesh();
            m.name = name;
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.CombineMeshes(parts.ToArray(), true, true);
            m.RecalculateBounds();
            return m;
        }
    }
}
