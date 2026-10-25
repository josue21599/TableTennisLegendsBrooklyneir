using Brooklyneir.Art;
using Brooklyneir.Physics;
using UnityEngine;

namespace Brooklyneir.Tables
{
    /// <summary>
    /// Regulation table built from geometry: board with visible thickness, painted top with lines,
    /// apron, legs with wheels and cross braces, net with posts, clamps, mesh and top tape.
    /// Dimensions come from <see cref="TableGeometry"/>, so every themed table plays identically.
    /// </summary>
    public static class TableModelBuilder
    {
        public static GameObject Build(TableDefinition def, Transform parent, int layer = 0)
        {
            GameObject root = new GameObject("Table_" + def.Id);
            root.layer = layer;
            root.transform.SetParent(parent, false);

            Color top = ProceduralTextures.Hex(def.TopColor);
            Color line = ProceduralTextures.Hex(def.LineColor);
            Color frame = ProceduralTextures.Hex(def.FrameColor);
            Color leg = ProceduralTextures.Hex(def.LegColor);
            Color net = ProceduralTextures.Hex(def.NetColor);
            bool neon = def.Environment == EnvironmentKind.NeonFuture;
            bool legend = def.Environment == EnvironmentKind.LegendArena;

            Material boardMat = MaterialLibrary.Surface(SurfaceKind.Painted, top * 0.92f);
            Material topMat = MaterialLibrary.Surface(SurfaceKind.Painted, Color.white, ProceduralTextures.TableTop(top, line));
            if (neon) topMat = MaterialLibrary.Emissive(Color.white, line * 0.35f, 1f, ProceduralTextures.TableTop(top, line));
            Material frameMat = MaterialLibrary.Surface(SurfaceKind.Metal, frame, null, 1f, 0.6f);
            Material legMat = MaterialLibrary.Surface(SurfaceKind.Metal, leg);
            Material rubberMat = MaterialLibrary.Surface(SurfaceKind.Rubber, new Color(0.08f, 0.08f, 0.09f));
            Material netMat = MaterialLibrary.LitTransparent(Color.white, ProceduralTextures.NetMesh(net * 0.9f), 1f);
            Material tapeMat = neon || legend
                ? MaterialLibrary.Emissive(net, net, neon ? 1.8f : 0.6f)
                : MaterialLibrary.Surface(SurfaceKind.Fabric, Color.Lerp(net, Color.white, 0.6f));
            Material postMat = MaterialLibrary.Surface(SurfaceKind.Metal, Color.Lerp(leg, Color.white, 0.25f), null, 1f, 0.9f);

            float W = TableGeometry.Width, L = TableGeometry.Length, Y = TableGeometry.TopY, T = TableGeometry.BoardThickness;

            // Board (two halves with a small gap underneath like folding tables).
            Part(root, "BoardP1", MeshFactory.RoundedBox(new Vector3(W, T, L * 0.5f - 0.002f), 0.006f, 2), boardMat, new Vector3(0f, Y - T * 0.5f, -L * 0.25f - 0.001f), layer, true);
            Part(root, "BoardP2", MeshFactory.RoundedBox(new Vector3(W, T, L * 0.5f - 0.002f), 0.006f, 2), boardMat, new Vector3(0f, Y - T * 0.5f, L * 0.25f + 0.001f), layer, true);
            // Painted playing surface (separate so the board edges keep a clean colour).
            Part(root, "PlayingSurface", MeshFactory.Plane(W - 0.004f, L - 0.004f, 0f), topMat, new Vector3(0f, Y + 0.0006f, 0f), layer, false)
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FixPlaneUV(root.transform.Find("PlayingSurface").GetComponent<MeshFilter>());

            // Apron / frame under the board.
            float apronY = Y - T - 0.03f;
            Part(root, "ApronL", MeshFactory.RoundedBox(new Vector3(0.03f, 0.05f, L - 0.2f), 0.008f, 2), frameMat, new Vector3(-W * 0.5f + 0.1f, apronY, 0f), layer, true);
            Part(root, "ApronR", MeshFactory.RoundedBox(new Vector3(0.03f, 0.05f, L - 0.2f), 0.008f, 2), frameMat, new Vector3(W * 0.5f - 0.1f, apronY, 0f), layer, true);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(root, "ApronEnd" + s, MeshFactory.RoundedBox(new Vector3(W - 0.2f, 0.05f, 0.03f), 0.008f, 2), frameMat, new Vector3(0f, apronY, s * (L * 0.5f - 0.1f)), layer, true);
                Part(root, "ApronMid" + s, MeshFactory.RoundedBox(new Vector3(W - 0.2f, 0.05f, 0.03f), 0.008f, 2), frameMat, new Vector3(0f, apronY, s * 0.12f), layer, true);
            }

            // Legs (8 legs: two frames per half), braces and wheels.
            float legH = Y - T - 0.05f;
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float zFrame = sz * (L * 0.5f - 0.32f);
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    float x = sx * (W * 0.5f - 0.16f);
                    Part(root, "Leg", MeshFactory.RoundedBox(new Vector3(0.045f, legH, 0.045f), 0.01f, 2), legMat, new Vector3(x, legH * 0.5f + 0.05f, zFrame), layer, true);
                    // Wheel housing + wheel.
                    Part(root, "Caster", MeshFactory.RoundedBox(new Vector3(0.05f, 0.03f, 0.06f), 0.008f, 2), legMat, new Vector3(x, 0.07f, zFrame), layer, false);
                    GameObject wheel = Part(root, "Wheel", MeshFactory.Cylinder(0.032f, 0.026f, 18), rubberMat, new Vector3(x, 0.032f, zFrame), layer, false);
                    wheel.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                }
                // Horizontal brace between the two legs of the frame.
                GameObject brace = Part(root, "Brace", MeshFactory.Cylinder(0.014f, W - 0.32f, 14), legMat, new Vector3(0f, 0.28f, zFrame), layer, true);
                brace.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                // Diagonal struts from the leg frame to the board centre.
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    Vector3 a = new Vector3(sx * (W * 0.5f - 0.16f), 0.3f, zFrame);
                    Vector3 b = new Vector3(sx * (W * 0.5f - 0.2f), Y - T - 0.06f, sz * 0.2f);
                    Strut(root, a, b, 0.011f, legMat, layer);
                }
            }

            // Net assembly.
            float span = TableGeometry.NetHalfSpan;
            float netTop = TableGeometry.NetTopY;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * span;
                Part(root, "NetPost", MeshFactory.Cylinder(0.011f, netTop - Y + 0.01f, 14), postMat, new Vector3(x, (netTop + Y) * 0.5f + 0.005f, 0f), layer, true);
                Part(root, "PostCap", MeshFactory.Sphere(0.014f, 12, 8), postMat, new Vector3(x, netTop + 0.012f, 0f), layer, false);
                // Clamp gripping the table edge.
                Part(root, "ClampArm", MeshFactory.RoundedBox(new Vector3(0.17f, 0.02f, 0.035f), 0.006f, 2), postMat, new Vector3(s * (W * 0.5f + 0.07f), Y - 0.005f, 0f), layer, true);
                Part(root, "ClampJaw", MeshFactory.RoundedBox(new Vector3(0.03f, 0.05f, 0.04f), 0.008f, 2), postMat, new Vector3(s * (W * 0.5f - 0.005f), Y - 0.03f, 0f), layer, true);
                Part(root, "ClampScrew", MeshFactory.Cylinder(0.009f, 0.03f, 12), legMat, new Vector3(s * (W * 0.5f - 0.005f), Y - 0.065f, 0f), layer, false);
            }
            GameObject mesh = Part(root, "NetMesh", MeshFactory.DoubleSidedQuad(span * 2f, netTop - Y - 0.006f, span * 2f / 0.04f, (netTop - Y) / 0.04f), netMat, new Vector3(0f, Y, 0f), layer, false);
            mesh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Part(root, "NetTape", MeshFactory.RoundedBox(new Vector3(span * 2f, 0.016f, 0.006f), 0.003f, 2), tapeMat, new Vector3(0f, netTop - 0.006f, 0f), layer, true);
            Part(root, "NetCord", MeshFactory.Cylinder(0.002f, span * 2f, 6), postMat, new Vector3(0f, Y + 0.004f, 0f), layer, false)
                .transform.localEulerAngles = new Vector3(0f, 0f, 90f);

            // Side branding strips (original, abstract).
            Material sign = MaterialLibrary.Surface(SurfaceKind.Painted, Color.white, ProceduralTextures.Sign(frame, ProceduralTextures.Hex(def.AccentColor), def.Id.GetHashCode() & 7));
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject plate = Part(root, "Brand", MeshFactory.Quad(0.5f, 0.06f), sign, new Vector3(s * (W * 0.5f + 0.0015f), Y - T * 0.5f - 0.002f, -0.7f * s), layer, false);
                plate.transform.localEulerAngles = new Vector3(0f, s > 0 ? -90f : 90f, 0f);
                plate.transform.localScale = new Vector3(1f, 0.45f, 1f);
            }
            return root;
        }

        /// <summary>Re-maps the plane UV to 0..1 over the whole surface (u = width, v = length).</summary>
        static void FixPlaneUV(MeshFilter mf)
        {
            Mesh m = Object.Instantiate(mf.sharedMesh);
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mf.sharedMesh = m;
        }

        static GameObject Part(GameObject root, string name, Mesh mesh, Material mat, Vector3 pos, int layer, bool shadows)
        {
            GameObject g = new GameObject(name);
            g.layer = layer;
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            return g;
        }

        static void Strut(GameObject root, Vector3 a, Vector3 b, float radius, Material mat, int layer)
        {
            Vector3 d = b - a;
            GameObject g = Part(root, "Strut", MeshFactory.Cylinder(radius, d.magnitude, 10), mat, (a + b) * 0.5f, layer, true);
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        }
    }
}
