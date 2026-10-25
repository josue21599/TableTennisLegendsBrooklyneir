using Brooklyneir.Art;
using UnityEngine;

namespace Brooklyneir.Rackets
{
    /// <summary>
    /// Builds a detailed 3D racket from a <see cref="RacketDefinition"/>: wooden blade core with
    /// visible thickness, two rubber sheets of different colours (slightly inset, as on real
    /// rackets), edge tape, shoulder, a shaped handle with accent rings and optional emissive trim.
    ///
    /// Local space: blade in the XY plane (forehand rubber faces -Z), handle pointing -Y, origin at
    /// the blade centre. Real-world size (about 27 cm long); the game scales it up for legibility.
    /// </summary>
    public static class RacketModelBuilder
    {
        public const float BladeCentreY = 0.085f;

        /// <summary>
        /// In-game pose: blade top pointing towards the net and tilted up by <paramref name="pitchDeg"/>,
        /// handle towards the player, forehand rubber facing up towards the camera (so the blade reads
        /// as a full, colourful shape from the overhead view). Roll leans the blade towards the ball
        /// side, yaw turns it with lateral movement.
        /// </summary>
        public static Quaternion Pose(float forwardSign, float pitchDeg, float rollDeg, float yawDeg)
        {
            Vector3 f = new Vector3(0f, 0f, forwardSign);
            float p = pitchDeg * Mathf.Deg2Rad;
            Vector3 top = f * Mathf.Cos(p) + Vector3.up * Mathf.Sin(p);
            Vector3 forehandNormal = Vector3.up * Mathf.Cos(p) - f * Mathf.Sin(p);
            Quaternion rot = Quaternion.LookRotation(-forehandNormal, top);
            rot = Quaternion.AngleAxis(rollDeg, top) * rot;
            rot = Quaternion.AngleAxis(yawDeg, Vector3.up) * rot;
            return rot;
        }

        public static GameObject Build(RacketDefinition def, Transform parent, int layer = 0)
        {
            GameObject root = new GameObject("Racket_" + def.Id);
            root.layer = layer;
            if (parent != null) root.transform.SetParent(parent, false);

            Color wood = ProceduralTextures.Hex(def.BladeWood);
            Texture2D woodTex = ProceduralTextures.Wood(wood, wood * 0.72f);
            Material woodMat = MaterialLibrary.Surface(SurfaceKind.Wood, Color.white, woodTex, 1f, def.Metallic > 0.5f ? def.Metallic * 0.6f : -1f);
            Material fh = MaterialLibrary.Surface(SurfaceKind.Rubber, ProceduralTextures.Hex(def.ForehandRubber));
            Material bh = MaterialLibrary.Surface(SurfaceKind.Rubber, ProceduralTextures.Hex(def.BackhandRubber));
            bool glow = !string.IsNullOrEmpty(def.GlowColor);
            Material edge = glow
                ? MaterialLibrary.Emissive(ProceduralTextures.Hex(def.EdgeTape), ProceduralTextures.Hex(def.GlowColor), 2.2f)
                : MaterialLibrary.Surface(SurfaceKind.Plastic, ProceduralTextures.Hex(def.EdgeTape));
            Material handleMat = MaterialLibrary.Surface(def.Metallic > 0.5f ? SurfaceKind.Metal : SurfaceKind.Wood,
                ProceduralTextures.Hex(def.HandlePrimary), def.Metallic > 0.5f ? null : ProceduralTextures.Wood(ProceduralTextures.Hex(def.HandlePrimary), ProceduralTextures.Hex(def.HandlePrimary) * 0.75f),
                1f, def.Metallic);
            Material accent = glow
                ? MaterialLibrary.Emissive(ProceduralTextures.Hex(def.HandleAccent), ProceduralTextures.Hex(def.GlowColor), 1.6f)
                : MaterialLibrary.Surface(SurfaceKind.Plastic, ProceduralTextures.Hex(def.HandleAccent));

            Vector2[] outline = BladeOutline(def.Blade, 56);
            const float core = 0.0065f, rubber = 0.0022f;

            // Blade core.
            Part(root, "BladeCore", MeshFactory.Extrude(outline, core), woodMat, new Vector3(0f, BladeCentreY, 0f), layer);
            // Rubbers (inset 1.5 mm like real sheets).
            Vector2[] rubberOutline = MeshFactory.Offset(outline, -0.0015f);
            Part(root, "RubberForehand", MeshFactory.Extrude(rubberOutline, rubber), fh, new Vector3(0f, BladeCentreY, -(core + rubber) * 0.5f), layer);
            Part(root, "RubberBackhand", MeshFactory.Extrude(rubberOutline, rubber), bh, new Vector3(0f, BladeCentreY, (core + rubber) * 0.5f), layer);
            // Edge tape.
            Part(root, "EdgeTape", MeshFactory.OutlineBand(outline, core + rubber * 2f + 0.0008f, 0.0009f), edge, new Vector3(0f, BladeCentreY, 0f), layer);

            // Shoulder (where blade meets handle).
            Part(root, "Shoulder", MeshFactory.RoundedBox(new Vector3(0.032f, 0.018f, core + 0.006f), 0.004f, 2), handleMat, new Vector3(0f, 0.004f, 0f), layer);

            // Handle.
            BuildHandle(root, def.Handle, handleMat, accent, layer);
            return root;
        }

        static GameObject Part(GameObject root, string name, Mesh mesh, Material mat, Vector3 pos, int layer)
        {
            GameObject g = new GameObject(name);
            g.layer = layer;
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            return g;
        }

        static void BuildHandle(GameObject root, HandleShape shape, Material handle, Material accent, int layer)
        {
            switch (shape)
            {
                case HandleShape.Flared:
                {
                    Vector2[] prof =
                    {
                        new Vector2(0f, -0.1f), new Vector2(0.0165f, -0.1f), new Vector2(0.0172f, -0.094f), new Vector2(0.0148f, -0.075f),
                        new Vector2(0.0128f, -0.045f), new Vector2(0.0122f, -0.015f), new Vector2(0.0118f, -0.002f), new Vector2(0f, -0.002f)
                    };
                    GameObject g = Part(root, "Handle", MeshFactory.SmoothLathe(prof, 20), handle, Vector3.zero, layer);
                    g.transform.localScale = new Vector3(1f, 1f, 0.72f);
                    GameObject ring = Part(root, "AccentRing", MeshFactory.Torus(0.0158f, 0.0022f, 24, 8), accent, new Vector3(0f, -0.088f, 0f), layer);
                    ring.transform.localScale = new Vector3(1f, 1f, 0.72f);
                    break;
                }
                case HandleShape.Anatomic:
                {
                    Vector2[] prof =
                    {
                        new Vector2(0f, -0.1f), new Vector2(0.014f, -0.1f), new Vector2(0.0155f, -0.09f), new Vector2(0.0165f, -0.06f),
                        new Vector2(0.0145f, -0.03f), new Vector2(0.0122f, -0.008f), new Vector2(0.0118f, -0.002f), new Vector2(0f, -0.002f)
                    };
                    GameObject g = Part(root, "Handle", MeshFactory.SmoothLathe(prof, 20), handle, Vector3.zero, layer);
                    g.transform.localScale = new Vector3(1f, 1f, 0.75f);
                    GameObject ring = Part(root, "AccentRing", MeshFactory.Torus(0.0162f, 0.002f, 24, 8), accent, new Vector3(0f, -0.06f, 0f), layer);
                    ring.transform.localScale = new Vector3(1f, 1f, 0.75f);
                    break;
                }
                case HandleShape.Futuristic:
                {
                    Part(root, "Handle", MeshFactory.RoundedBox(new Vector3(0.026f, 0.098f, 0.02f), 0.007f, 3), handle, new Vector3(0f, -0.051f, 0f), layer);
                    Part(root, "TrimFront", MeshFactory.RoundedBox(new Vector3(0.006f, 0.07f, 0.004f), 0.0018f, 2), accent, new Vector3(0f, -0.05f, -0.0098f), layer);
                    Part(root, "TrimBack", MeshFactory.RoundedBox(new Vector3(0.006f, 0.07f, 0.004f), 0.0018f, 2), accent, new Vector3(0f, -0.05f, 0.0098f), layer);
                    Part(root, "EndCap", MeshFactory.RoundedBox(new Vector3(0.03f, 0.01f, 0.024f), 0.004f, 2), accent, new Vector3(0f, -0.101f, 0f), layer);
                    break;
                }
                default:
                {
                    Part(root, "Handle", MeshFactory.RoundedBox(new Vector3(0.026f, 0.1f, 0.021f), 0.0085f, 3), handle, new Vector3(0f, -0.052f, 0f), layer);
                    Part(root, "Band", MeshFactory.RoundedBox(new Vector3(0.0275f, 0.012f, 0.0225f), 0.006f, 2), accent, new Vector3(0f, -0.088f, 0f), layer);
                    Part(root, "Band2", MeshFactory.RoundedBox(new Vector3(0.0275f, 0.006f, 0.0225f), 0.003f, 2), accent, new Vector3(0f, -0.072f, 0f), layer);
                    break;
                }
            }
        }

        /// <summary>Counter-clockwise blade outline in metres, centred on (0,0).</summary>
        public static Vector2[] BladeOutline(BladeShape shape, int points)
        {
            float a, b, n;
            switch (shape)
            {
                case BladeShape.Wide: a = 0.083f; b = 0.077f; n = 2.5f; break;
                case BladeShape.Teardrop: a = 0.073f; b = 0.083f; n = 2.2f; break;
                case BladeShape.Angular: a = 0.077f; b = 0.08f; n = 3.6f; break;
                default: a = 0.076f; b = 0.079f; n = 2.25f; break;
            }
            Vector2[] o = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                float t = 2f * Mathf.PI * i / points;
                float c = Mathf.Cos(t), s = Mathf.Sin(t);
                float x = a * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / n);
                float y = b * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / n);
                if (s < 0f)
                {
                    // Narrow the bottom towards the shoulder.
                    float k = Mathf.Pow(-s, 2.2f);
                    x *= 1f - 0.22f * k;
                }
                if (shape == BladeShape.Teardrop) x *= 1f + 0.08f * s;
                if (shape == BladeShape.Angular && s > 0.2f) x *= 1f + 0.04f * s;
                o[i] = new Vector2(x, y);
            }
            return o;
        }
    }
}
