using System.Collections.Generic;
using Brooklyneir.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brooklyneir.Tables
{
    /// <summary>
    /// Builds the six themed scenarios procedurally (floor, sky, lights, props, stylised crowd).
    /// Scenery stays outside the play volume so it can never affect the ball.
    /// </summary>
    public static class EnvironmentBuilder
    {
        public class Result
        {
            public GameObject Root;
            /// <summary>Non-moving scenery: statically batched after building to save draw calls.</summary>
            public GameObject Static;
            /// <summary>Animated scenery (crowd, palms, flames, rings).</summary>
            public GameObject Dynamic;
            public Light MainLight;
            public CrowdAnimator Crowd;
            public List<Light> ExtraLights = new List<Light>();
        }

        public static Result Build(TableDefinition def, Transform parent)
        {
            Result r = new Result();
            r.Root = new GameObject("Environment_" + def.Id);
            r.Root.transform.SetParent(parent, false);
            r.Crowd = r.Root.AddComponent<CrowdAnimator>();
            r.Static = new GameObject("Static");
            r.Static.transform.SetParent(r.Root.transform, false);
            r.Dynamic = new GameObject("Dynamic");
            r.Dynamic.transform.SetParent(r.Root.transform, false);

            Color sky0 = ProceduralTextures.Hex(def.SkyBottom), sky1 = ProceduralTextures.Hex(def.SkyTop);
            Color ambient = ProceduralTextures.Hex(def.AmbientColor);
            Color accent = ProceduralTextures.Hex(def.AccentColor);
            Color floor = ProceduralTextures.Hex(def.FloorColor);

            // Sky dome.
            GameObject sky = Prop(r.Static, "Sky", MeshFactory.SkyDome(60f),
                MaterialLibrary.Unlit(Color.white, ProceduralTextures.VerticalGradient(sky0, sky1, Color.Lerp(sky0, accent, 0.25f))),
                Vector3.zero, Vector3.zero, Vector3.one, false);
            sky.GetComponent<MeshRenderer>().receiveShadows = false;

            // Main light.
            GameObject lightGo = new GameObject("MainLight");
            lightGo.transform.SetParent(r.Root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(def.Night ? 68f : 58f, -30f, 0f);
            Light main = lightGo.AddComponent<Light>();
            main.type = LightType.Directional;
            main.color = ProceduralTextures.Hex(def.LightColor);
            main.intensity = def.LightIntensity;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.75f;
            main.shadowBias = 0.03f;
            main.shadowNormalBias = 0.3f;
            r.MainLight = main;
            RenderSettings.sun = main;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(ambient, sky1, 0.3f);
            RenderSettings.ambientEquatorColor = ambient;
            RenderSettings.ambientGroundColor = Color.Lerp(ambient, floor, 0.5f) * 0.6f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(sky0, sky1, 0.3f);
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 55f;

            switch (def.Environment)
            {
                case EnvironmentKind.ClassicArena: ClassicArena(r, def, floor, accent); break;
                case EnvironmentKind.BrooklynCourt: BrooklynCourt(r, def, floor, accent); break;
                case EnvironmentKind.NeonFuture: NeonFuture(r, def, floor, accent); break;
                case EnvironmentKind.Rooftop: Rooftop(r, def, floor, accent); break;
                case EnvironmentKind.Tropical: Tropical(r, def, floor, accent); break;
                default: LegendArena(r, def, floor, accent); break;
            }
            StaticBatchingUtility.Combine(r.Static);
            return r;
        }

        // ---------------------------------------------------------------- scenarios

        static void ClassicArena(Result r, TableDefinition def, Color floor, Color accent)
        {
            Prop(r.Static, "Floor", MeshFactory.Plane(40f, 40f, 0.25f), MaterialLibrary.Surface(SurfaceKind.Ground, Color.white, ProceduralTextures.Planks(new Color(0.78f, 0.58f, 0.36f)), 1f), Vector3.zero, Vector3.zero, Vector3.one, false);
            Prop(r.Static, "Court", MeshFactory.Plane(7f, 12f, 0.5f), MaterialLibrary.Surface(SurfaceKind.Rubber, floor), new Vector3(0f, 0.004f, 0f), Vector3.zero, Vector3.one, false);
            Surrounds(r, 3.6f, 6.2f, accent, new Color(0.12f, 0.16f, 0.28f));
            Stands(r, 9f, 4, 14f, new Color(0.22f, 0.26f, 0.36f), 0.42f);
            Stands(r, -9f, 4, 14f, new Color(0.22f, 0.26f, 0.36f), 0.42f);
            Scoreboard(r, new Vector3(0f, 4.5f, 10f), accent);
            LightRig(r, 5.5f, new Color(1f, 0.95f, 0.85f), 2);
        }

        static void BrooklynCourt(Result r, TableDefinition def, Color floor, Color accent)
        {
            Material asphalt = MaterialLibrary.Surface(SurfaceKind.Ground, Color.white, ProceduralTextures.Concrete(floor, 4), 0.3f);
            Prop(r.Static, "Floor", MeshFactory.Plane(40f, 40f, 0.3f), asphalt, Vector3.zero, Vector3.zero, Vector3.one, false);
            // Painted court rectangle and circle (original markings).
            Material paint = MaterialLibrary.Surface(SurfaceKind.Painted, new Color(0.95f, 0.92f, 0.85f));
            Material zone = MaterialLibrary.Surface(SurfaceKind.Painted, Color.Lerp(floor, accent, 0.35f));
            Prop(r.Static, "CourtZone", MeshFactory.Plane(6f, 10f, 0f), zone, new Vector3(0f, 0.003f, 0f), Vector3.zero, Vector3.one, false);
            LineBox(r, 6f, 10f, 0.08f, paint);
            Prop(r.Static, "CentreCircle", MeshFactory.Ring(1.1f, 1.18f, 48), paint, new Vector3(0f, 0.006f, 0f), Vector3.zero, Vector3.one, false);
            // Brick walls with murals.
            Material brick = MaterialLibrary.Surface(SurfaceKind.Ground, Color.white, ProceduralTextures.Windows(new Color(0.55f, 0.24f, 0.18f), Color.white, false, 3), 0.2f);
            for (int s = -1; s <= 1; s += 2)
            {
                Prop(r.Static, "Wall", MeshFactory.RoundedBox(new Vector3(0.5f, 4.5f, 18f), 0.04f, 1), brick, new Vector3(s * 8.5f, 2.25f, 0f), Vector3.zero, Vector3.one, true);
                Material mural = MaterialLibrary.Surface(SurfaceKind.Painted, Color.white, ProceduralTextures.Mural(accent, new Color(0.18f, 0.55f, 0.9f), new Color(0.95f, 0.3f, 0.45f), 7 + s));
                GameObject m = Prop(r.Static, "Mural", MeshFactory.Quad(9f, 3.2f), mural, new Vector3(s * 8.24f, 2.1f, -1f), new Vector3(0f, s > 0 ? -90f : 90f, 0f), Vector3.one, false);
                m.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // Chain-link fences.
            Material fence = MaterialLibrary.LitTransparent(Color.white, ProceduralTextures.ChainLink(), 1f);
            Material pole = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.45f, 0.48f, 0.5f));
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject f = Prop(r.Static, "Fence", MeshFactory.DoubleSidedQuad(14f, 3f, 14f / 0.25f, 3f / 0.25f), fence, new Vector3(0f, 0f, s * 6.5f), Vector3.zero, Vector3.one, false);
                f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                for (int i = -3; i <= 3; i++)
                    Prop(r.Static, "FencePole", MeshFactory.Cylinder(0.04f, 3.1f, 10), pole, new Vector3(i * 2.3f, 1.55f, s * 6.5f), Vector3.zero, Vector3.one, true);
            }
            // Street lamps, benches, hydrant.
            for (int s = -1; s <= 1; s += 2)
            {
                StreetLamp(r, new Vector3(s * 5.2f, 0f, -4.5f * s), accent);
                Bench(r, new Vector3(s * 5.5f, 0f, 2.5f * s), s > 0 ? -90f : 90f);
            }
            Hydrant(r, new Vector3(-6.2f, 0f, 4.8f));
            Crowd(r, new Vector3(6.6f, 0f, -3f), new Vector3(6.6f, 0f, 3f), 7, 0, 0.3f);
            Crowd(r, new Vector3(-6.6f, 0f, -3f), new Vector3(-6.6f, 0f, 3f), 7, 0, 0.3f);
        }

        static void NeonFuture(Result r, TableDefinition def, Color floor, Color accent)
        {
            Color cyan = ProceduralTextures.Hex(def.LineColor);
            Material grid = MaterialLibrary.Emissive(floor, Color.white, 0.9f, ProceduralTextures.NeonGrid(cyan));
            Prop(r.Static, "Floor", MeshFactory.Plane(40f, 40f, 0.5f), grid, Vector3.zero, Vector3.zero, Vector3.one, false);
            Material glossy = MaterialLibrary.Surface(SurfaceKind.Glass, new Color(0.05f, 0.06f, 0.12f));
            Prop(r.Static, "Platform", MeshFactory.RoundedBox(new Vector3(7f, 0.08f, 11f), 0.04f, 2), glossy, new Vector3(0f, 0.04f, 0f), Vector3.zero, Vector3.one, false);
            Material pink = MaterialLibrary.Emissive(accent, accent, 3f);
            Material blue = MaterialLibrary.Emissive(cyan, cyan, 3f);
            // Platform edge strips.
            for (int s = -1; s <= 1; s += 2)
            {
                Prop(r.Static, "Strip", MeshFactory.Capsule(0.035f, 11f, 10, 3), s > 0 ? pink : blue, new Vector3(s * 3.5f, 0.09f, 0f), new Vector3(90f, 0f, 0f), Vector3.one, false);
                Prop(r.Static, "Strip", MeshFactory.Capsule(0.035f, 7f, 10, 3), s > 0 ? blue : pink, new Vector3(0f, 0.09f, s * 5.5f), new Vector3(0f, 0f, 90f), Vector3.one, false);
            }
            // Neon arches framing both ends of the court. Kept beyond the racket zones (|z| > 4) so
            // they never come between the match camera and the table.
            for (int i = 0; i < 4; i++)
            {
                float z = (i < 2 ? -1f : 1f) * (4.4f + (i % 2) * 2.6f);
                Arch(r, z, 6.2f, 4.2f, i % 2 == 0 ? pink : blue);
            }
            // Floating holo rings.
            Material holo = MaterialLibrary.Additive(new Color(cyan.r, cyan.g, cyan.b, 0.55f), ProceduralTextures.SoftCircle(0.8f));
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject ring = Prop(r.Dynamic, "HoloRing", MeshFactory.Torus(1.1f, 0.04f, 40, 8), blue, new Vector3(s * 6.5f, 3f, 0f), new Vector3(0f, 0f, 90f), Vector3.one, false);
                r.Crowd.AddSpinner(ring.transform, new Vector3(0f, 25f * s, 0f));
                Prop(r.Static, "HoloGlow", MeshFactory.Disc(1.05f, 32), holo, new Vector3(s * 6.5f, 3f, 0f), new Vector3(0f, 0f, 90f), Vector3.one, false);
            }
            // Pillars.
            Material pillar = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.12f, 0.13f, 0.2f));
            for (int s = -1; s <= 1; s += 2)
                for (int i = -2; i <= 2; i++)
                {
                    Prop(r.Static, "Pillar", MeshFactory.RoundedBox(new Vector3(0.5f, 7f, 0.5f), 0.08f, 2), pillar, new Vector3(s * 9f, 3.5f, i * 4f), Vector3.zero, Vector3.one, true);
                    Prop(r.Static, "PillarGlow", MeshFactory.Capsule(0.05f, 6.4f, 8, 2), i % 2 == 0 ? pink : blue, new Vector3(s * 8.72f, 3.5f, i * 4f), Vector3.zero, Vector3.one, false);
                }
            Crowd(r, new Vector3(7.2f, 0f, -4f), new Vector3(7.2f, 0f, 4f), 9, 0, 0.35f);
            Crowd(r, new Vector3(-7.2f, 0f, -4f), new Vector3(-7.2f, 0f, 4f), 9, 0, 0.35f);
            AddPointLight(r, new Vector3(0f, 3.5f, -3f), accent, 2.5f, 9f);
            AddPointLight(r, new Vector3(0f, 3.5f, 3f), cyan, 2.5f, 9f);
        }

        static void Rooftop(Result r, TableDefinition def, Color floor, Color accent)
        {
            Prop(r.Static, "Floor", MeshFactory.Plane(18f, 18f, 0.35f), MaterialLibrary.Surface(SurfaceKind.Ground, Color.white, ProceduralTextures.Concrete(floor, 9), 0.35f), Vector3.zero, Vector3.zero, Vector3.one, false);
            Material parapet = MaterialLibrary.Surface(SurfaceKind.Ground, Color.white, ProceduralTextures.Concrete(new Color(0.42f, 0.42f, 0.46f), 12), 0.5f);
            for (int s = -1; s <= 1; s += 2)
            {
                Prop(r.Static, "Parapet", MeshFactory.RoundedBox(new Vector3(0.35f, 1f, 18f), 0.04f, 1), parapet, new Vector3(s * 9f, 0.5f, 0f), Vector3.zero, Vector3.one, true);
                Prop(r.Static, "Parapet", MeshFactory.RoundedBox(new Vector3(18f, 1f, 0.35f), 0.04f, 1), parapet, new Vector3(0f, 0.5f, s * 9f), Vector3.zero, Vector3.one, true);
            }
            // Water tower.
            WaterTower(r, new Vector3(6.8f, 1f, 5.5f));
            // AC units.
            Material metal = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.6f, 0.62f, 0.66f));
            Prop(r.Static, "ACUnit", MeshFactory.RoundedBox(new Vector3(1.2f, 0.9f, 1f), 0.06f, 2), metal, new Vector3(-6.5f, 0.45f, -5.5f), Vector3.zero, Vector3.one, true);
            Prop(r.Static, "ACUnit", MeshFactory.RoundedBox(new Vector3(1.2f, 0.9f, 1f), 0.06f, 2), metal, new Vector3(-6.5f, 0.45f, -3.8f), Vector3.zero, Vector3.one, true);
            // Skyline with lit windows.
            Skyline(r, 22f, 30, ProceduralTextures.Hex(def.LightColor), true);
            // String lights across the court.
            Material bulb = MaterialLibrary.Emissive(new Color(1f, 0.85f, 0.6f), new Color(1f, 0.75f, 0.45f), 3f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = new Vector3(-6f, 3.2f, s * 4.2f), b = new Vector3(6f, 3.2f, s * 4.2f);
                for (int i = 0; i <= 16; i++)
                {
                    float t = i / 16f;
                    Vector3 p = Vector3.Lerp(a, b, t);
                    p.y -= Mathf.Sin(t * Mathf.PI) * 0.6f;
                    Prop(r.Static, "Bulb", MeshFactory.Sphere(0.06f, 10, 6), bulb, p, Vector3.zero, Vector3.one, false);
                }
                Prop(r.Static, "LightPole", MeshFactory.Cylinder(0.05f, 3.3f, 10), metal, new Vector3(-6f, 1.65f, s * 4.2f), Vector3.zero, Vector3.one, true);
                Prop(r.Static, "LightPole", MeshFactory.Cylinder(0.05f, 3.3f, 10), metal, new Vector3(6f, 1.65f, s * 4.2f), Vector3.zero, Vector3.one, true);
            }
            AddPointLight(r, new Vector3(0f, 2.8f, -4f), new Color(1f, 0.78f, 0.5f), 1.8f, 7f);
            AddPointLight(r, new Vector3(0f, 2.8f, 4f), new Color(1f, 0.78f, 0.5f), 1.8f, 7f);
            Crowd(r, new Vector3(-7.5f, 0f, -1.5f), new Vector3(-7.5f, 0f, 2.5f), 5, 0, 0.25f);
            Crowd(r, new Vector3(7.5f, 0f, -3f), new Vector3(7.5f, 0f, 2f), 5, 0, 0.25f);
        }

        static void Tropical(Result r, TableDefinition def, Color floor, Color accent)
        {
            Prop(r.Static, "Sand", MeshFactory.Plane(40f, 40f, 0.25f), MaterialLibrary.Surface(SurfaceKind.Ground, Color.white, ProceduralTextures.Sand(floor), 0.25f), Vector3.zero, Vector3.zero, Vector3.one, false);
            Material wood = MaterialLibrary.Surface(SurfaceKind.Wood, Color.white, ProceduralTextures.Wood(new Color(0.78f, 0.62f, 0.42f), new Color(0.55f, 0.4f, 0.26f)), 1f);
            Prop(r.Static, "Deck", MeshFactory.RoundedBox(new Vector3(6.5f, 0.1f, 10f), 0.03f, 2), wood, new Vector3(0f, 0.05f, 0f), Vector3.zero, Vector3.one, false);
            Material sea = MaterialLibrary.LitTransparent(new Color(0.1f, 0.6f, 0.75f, 0.85f));
            GameObject ocean = Prop(r.Dynamic, "Ocean", MeshFactory.Plane(80f, 30f, 0f), sea, new Vector3(30f, -0.05f, 0f), new Vector3(0f, 90f, 0f), Vector3.one, false);
            r.Crowd.AddBobber(ocean.transform, 0.05f, 0.4f);
            for (int s = -1; s <= 1; s += 2)
            {
                Palm(r, new Vector3(s * 5.4f, 0f, -4.2f), s * 12f);
                Palm(r, new Vector3(s * 6.6f, 0f, 3.4f), -s * 9f);
                Torch(r, new Vector3(s * 3.8f, 0.1f, -5.2f));
                Torch(r, new Vector3(s * 3.8f, 0.1f, 5.2f));
            }
            Umbrella(r, new Vector3(-6.2f, 0f, 0f), accent);
            Umbrella(r, new Vector3(6.4f, 0f, -0.8f), new Color(0.1f, 0.7f, 0.6f));
            Crowd(r, new Vector3(-7.8f, 0f, -2.5f), new Vector3(-7.8f, 0f, 2.5f), 6, 0, 0.3f);
            Crowd(r, new Vector3(8f, 0f, -2f), new Vector3(8f, 0f, 3f), 5, 0, 0.3f);
        }

        static void LegendArena(Result r, TableDefinition def, Color floor, Color accent)
        {
            Material dark = MaterialLibrary.Surface(SurfaceKind.Glass, floor);
            Prop(r.Static, "Floor", MeshFactory.Plane(40f, 40f, 0f), dark, Vector3.zero, Vector3.zero, Vector3.one, false);
            Material court = MaterialLibrary.Surface(SurfaceKind.Rubber, new Color(0.07f, 0.07f, 0.09f)); // dark court: the maroon table must stand out
            Prop(r.Static, "Court", MeshFactory.Plane(7f, 12f, 0f), court, new Vector3(0f, 0.004f, 0f), Vector3.zero, Vector3.one, false);
            Material gold = MaterialLibrary.Emissive(accent, accent, 1.2f);
            LineBox(r, 7f, 12f, 0.06f, gold);
            Surrounds(r, 3.6f, 6.2f, accent, new Color(0.1f, 0.06f, 0.06f));
            Stands(r, 9f, 5, 16f, new Color(0.16f, 0.12f, 0.14f), 0.42f);
            Stands(r, -9f, 5, 16f, new Color(0.16f, 0.12f, 0.14f), 0.42f);
            Scoreboard(r, new Vector3(0f, 5f, 10.5f), accent);
            // Spotlight beams.
            Material beam = MaterialLibrary.Additive(new Color(1f, 0.9f, 0.6f, 0.07f), ProceduralTextures.SoftCircle(0.1f));
            for (int s = -1; s <= 1; s += 2)
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector3 top = new Vector3(s * 6f, 8f, i * 5f);
                    Vector3 dir = (new Vector3(0f, 0.8f, 0f) - top).normalized;
                    GameObject b = Prop(r.Static, "Beam", MeshFactory.Frustum(1.2f, 0.12f, 9f, 16), beam, top + dir * 4.5f, Vector3.zero, Vector3.one, false);
                    b.transform.rotation = Quaternion.FromToRotation(Vector3.down, dir);
                    b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            // Banners.
            for (int s = -1; s <= 1; s += 2)
            {
                Material banner = MaterialLibrary.Surface(SurfaceKind.Fabric, Color.white, ProceduralTextures.Sign(new Color(0.3f, 0.05f, 0.08f), accent, 3 + s));
                GameObject g = Prop(r.Static, "Banner", MeshFactory.Quad(3f, 1.5f), banner, new Vector3(s * 8.7f, 4.2f, 0f), new Vector3(0f, s > 0 ? -90f : 90f, 0f), Vector3.one, false);
                g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            LightRig(r, 6f, new Color(1f, 0.92f, 0.75f), 4);
        }

        // ---------------------------------------------------------------- props

        static void Surrounds(Result r, float halfX, float halfZ, Color accent, Color panel)
        {
            Material panelMat = MaterialLibrary.Surface(SurfaceKind.Fabric, panel);
            Material stripe = MaterialLibrary.Surface(SurfaceKind.Painted, accent);
            for (int s = -1; s <= 1; s += 2)
            {
                for (float z = -halfZ + 1f; z <= halfZ - 0.9f; z += 2f)
                {
                    Barrier(r, new Vector3(s * halfX, 0f, z), 90f, panelMat, stripe);
                }
                for (float x = -halfX + 1f; x <= halfX - 0.9f; x += 2f)
                {
                    Barrier(r, new Vector3(x, 0f, s * halfZ), 0f, panelMat, stripe);
                }
            }
        }

        static void Barrier(Result r, Vector3 pos, float yaw, Material panel, Material stripe)
        {
            GameObject g = Prop(r.Static, "Barrier", MeshFactory.RoundedBox(new Vector3(1.96f, 0.7f, 0.06f), 0.025f, 2), panel, pos + Vector3.up * 0.35f, new Vector3(0f, yaw, 0f), Vector3.one, true);
            GameObject st = Prop(g, "Stripe", MeshFactory.RoundedBox(new Vector3(1.9f, 0.08f, 0.065f), 0.02f, 1), stripe, new Vector3(0f, 0.22f, 0f), Vector3.zero, Vector3.one, false);
            st.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            Material feet = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.25f, 0.25f, 0.28f));
            Prop(g, "Foot", MeshFactory.RoundedBox(new Vector3(0.06f, 0.04f, 0.3f), 0.01f, 1), feet, new Vector3(-0.85f, -0.33f, 0f), Vector3.zero, Vector3.one, false);
            Prop(g, "Foot", MeshFactory.RoundedBox(new Vector3(0.06f, 0.04f, 0.3f), 0.01f, 1), feet, new Vector3(0.85f, -0.33f, 0f), Vector3.zero, Vector3.one, false);
        }

        static void Stands(Result r, float x, int rows, float length, Color concrete, float density)
        {
            float s = Mathf.Sign(x);
            Material step = MaterialLibrary.Surface(SurfaceKind.Ground, concrete);
            Material seat = MaterialLibrary.Surface(SurfaceKind.Plastic, Color.Lerp(concrete, new Color(0.2f, 0.45f, 0.9f), 0.6f));
            for (int i = 0; i < rows; i++)
            {
                float px = x + s * i * 0.9f;
                float py = 0.4f + i * 0.55f;
                Prop(r.Static, "Step", MeshFactory.RoundedBox(new Vector3(0.9f, py * 2f, length), 0.03f, 1), step, new Vector3(px, py * 0.5f, 0f), Vector3.zero, Vector3.one, true);
                Prop(r.Static, "Seats", MeshFactory.RoundedBox(new Vector3(0.45f, 0.12f, length - 0.4f), 0.03f, 1), seat, new Vector3(px - s * 0.15f, py + 0.06f, 0f), Vector3.zero, Vector3.one, false);
                int people = Mathf.RoundToInt(length / 0.75f * density);
                Crowd(r, new Vector3(px, py, -length * 0.45f), new Vector3(px, py, length * 0.45f), people, (i * 37) % 11, 0.5f);
            }
        }

        static void Crowd(Result r, Vector3 from, Vector3 to, int count, int seed, float jitter)
        {
            Material skinA = MaterialLibrary.Surface(SurfaceKind.Fabric, new Color(0.96f, 0.8f, 0.66f));
            Material skinB = MaterialLibrary.Surface(SurfaceKind.Fabric, new Color(0.72f, 0.5f, 0.36f));
            Material skinC = MaterialLibrary.Surface(SurfaceKind.Fabric, new Color(0.45f, 0.3f, 0.2f));
            Material[] skins = { skinA, skinB, skinC };
            Color[] shirts =
            {
                new Color(0.9f, 0.25f, 0.2f), new Color(0.2f, 0.45f, 0.9f), new Color(0.98f, 0.78f, 0.2f), new Color(0.2f, 0.7f, 0.45f),
                new Color(0.95f, 0.95f, 0.95f), new Color(0.6f, 0.3f, 0.8f), new Color(0.15f, 0.15f, 0.18f), new Color(1f, 0.5f, 0.15f)
            };
            Mesh body = MeshFactory.Capsule(0.17f, 0.62f, 12, 4);
            Mesh head = MeshFactory.Sphere(0.12f, 12, 8);
            Mesh arm = MeshFactory.Capsule(0.05f, 0.42f, 8, 3);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float h = Hash(i, seed);
                Vector3 p = Vector3.Lerp(from, to, t) + new Vector3((h - 0.5f) * jitter, 0f, (Hash(i + 7, seed) - 0.5f) * jitter);
                GameObject person = new GameObject("Fan");
                person.transform.SetParent(r.Dynamic.transform, false);
                person.transform.localPosition = p;
                Material shirt = MaterialLibrary.Surface(SurfaceKind.Fabric, shirts[(i + seed) % shirts.Length]);
                Prop(person, "Body", body, shirt, new Vector3(0f, 0.36f, 0f), Vector3.zero, Vector3.one, false);
                GameObject headGo = Prop(person, "Head", head, skins[(i * 3 + seed) % 3], new Vector3(0f, 0.8f, 0f), Vector3.zero, Vector3.one, false);
                GameObject armL = Prop(person, "ArmL", arm, shirt, new Vector3(-0.2f, 0.5f, 0f), new Vector3(0f, 0f, -15f), Vector3.one, false);
                GameObject armR = Prop(person, "ArmR", arm, shirt, new Vector3(0.2f, 0.5f, 0f), new Vector3(0f, 0f, 15f), Vector3.one, false);
                person.transform.LookAt(new Vector3(0f, p.y, 0f));
                r.Crowd.AddFan(person.transform, armL.transform, armR.transform, headGo.transform, h);
            }
        }

        static void Scoreboard(Result r, Vector3 pos, Color accent)
        {
            Material frame = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.12f, 0.12f, 0.14f));
            Material screen = MaterialLibrary.Emissive(new Color(0.05f, 0.08f, 0.15f), accent, 0.8f, ProceduralTextures.Sign(new Color(0.04f, 0.06f, 0.12f), accent, 11));
            Prop(r.Static, "BoardFrame", MeshFactory.RoundedBox(new Vector3(4.4f, 2.2f, 0.3f), 0.08f, 2), frame, pos, Vector3.zero, Vector3.one, false);
            Prop(r.Static, "BoardScreen", MeshFactory.Quad(4f, 1.8f), screen, pos + new Vector3(0f, 0f, -0.16f), Vector3.zero, Vector3.one, false);
        }

        static void LightRig(Result r, float height, Color color, int count)
        {
            Material housing = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.2f, 0.2f, 0.22f));
            Material lamp = MaterialLibrary.Emissive(Color.white, color, 4f);
            // Trusses sit beyond both ends of the table (|z| >= 4.2) so they never occlude the play
            // area from the overhead match camera.
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = side * (4.2f + (i / 2) * 1.6f);
                Prop(r.Static, "Truss", MeshFactory.RoundedBox(new Vector3(8f, 0.15f, 0.15f), 0.03f, 1), housing, new Vector3(0f, height + 0.3f, z), Vector3.zero, Vector3.one, false);
                for (int s = -1; s <= 1; s += 2)
                {
                    Prop(r.Static, "Lamp", MeshFactory.Frustum(0.25f, 0.18f, 0.3f, 14), housing, new Vector3(s * 1.6f, height, z), Vector3.zero, Vector3.one, false);
                    Prop(r.Static, "LampGlow", MeshFactory.Disc(0.22f, 20), lamp, new Vector3(s * 1.6f, height - 0.16f, z), new Vector3(180f, 0f, 0f), Vector3.one, false);
                }
            }
        }

        static void LineBox(Result r, float w, float d, float lw, Material m)
        {
            Prop(r.Static, "Line", MeshFactory.Plane(w, lw, 0f), m, new Vector3(0f, 0.006f, d * 0.5f), Vector3.zero, Vector3.one, false);
            Prop(r.Static, "Line", MeshFactory.Plane(w, lw, 0f), m, new Vector3(0f, 0.006f, -d * 0.5f), Vector3.zero, Vector3.one, false);
            Prop(r.Static, "Line", MeshFactory.Plane(lw, d, 0f), m, new Vector3(w * 0.5f, 0.006f, 0f), Vector3.zero, Vector3.one, false);
            Prop(r.Static, "Line", MeshFactory.Plane(lw, d, 0f), m, new Vector3(-w * 0.5f, 0.006f, 0f), Vector3.zero, Vector3.one, false);
        }

        static void StreetLamp(Result r, Vector3 pos, Color accent)
        {
            Material metal = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.15f, 0.32f, 0.22f));
            Material lamp = MaterialLibrary.Emissive(Color.white, new Color(1f, 0.85f, 0.55f), 2.5f);
            Prop(r.Static, "LampPost", MeshFactory.Cylinder(0.07f, 4f, 12), metal, pos + Vector3.up * 2f, Vector3.zero, Vector3.one, true);
            Prop(r.Static, "LampBase", MeshFactory.Frustum(0.16f, 0.09f, 0.4f, 12), metal, pos + Vector3.up * 0.2f, Vector3.zero, Vector3.one, true);
            Prop(r.Static, "LampHead", MeshFactory.Frustum(0.12f, 0.3f, 0.35f, 14), metal, pos + Vector3.up * 4.1f, new Vector3(180f, 0f, 0f), Vector3.one, true);
            Prop(r.Static, "LampBulb", MeshFactory.Sphere(0.11f, 12, 8), lamp, pos + Vector3.up * 3.95f, Vector3.zero, Vector3.one, false);
        }

        static void Bench(Result r, Vector3 pos, float yaw)
        {
            Material wood = MaterialLibrary.Surface(SurfaceKind.Wood, Color.white, ProceduralTextures.Wood(new Color(0.7f, 0.5f, 0.3f), new Color(0.45f, 0.3f, 0.18f)), 1f);
            Material metal = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.15f, 0.15f, 0.17f));
            GameObject b = new GameObject("Bench");
            b.transform.SetParent(r.Static.transform, false);
            b.transform.localPosition = pos;
            b.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            for (int i = 0; i < 3; i++)
                Prop(b, "Slat", MeshFactory.RoundedBox(new Vector3(1.8f, 0.04f, 0.12f), 0.015f, 1), wood, new Vector3(0f, 0.45f, -0.14f + i * 0.14f), Vector3.zero, Vector3.one, true);
            Prop(b, "Back", MeshFactory.RoundedBox(new Vector3(1.8f, 0.3f, 0.04f), 0.015f, 1), wood, new Vector3(0f, 0.72f, 0.2f), new Vector3(-10f, 0f, 0f), Vector3.one, true);
            Prop(b, "LegL", MeshFactory.RoundedBox(new Vector3(0.05f, 0.45f, 0.42f), 0.01f, 1), metal, new Vector3(-0.8f, 0.225f, 0f), Vector3.zero, Vector3.one, true);
            Prop(b, "LegR", MeshFactory.RoundedBox(new Vector3(0.05f, 0.45f, 0.42f), 0.01f, 1), metal, new Vector3(0.8f, 0.225f, 0f), Vector3.zero, Vector3.one, true);
        }

        static void Hydrant(Result r, Vector3 pos)
        {
            Material red = MaterialLibrary.Surface(SurfaceKind.Painted, new Color(0.85f, 0.15f, 0.12f));
            Prop(r.Static, "HydrantBody", MeshFactory.Cylinder(0.14f, 0.6f, 16), red, pos + Vector3.up * 0.3f, Vector3.zero, Vector3.one, true);
            Prop(r.Static, "HydrantTop", MeshFactory.Sphere(0.15f, 14, 8), red, pos + Vector3.up * 0.62f, Vector3.zero, Vector3.one, true);
            Prop(r.Static, "HydrantNozzle", MeshFactory.Cylinder(0.06f, 0.42f, 12), red, pos + Vector3.up * 0.4f, new Vector3(0f, 0f, 90f), Vector3.one, true);
        }

        static void WaterTower(Result r, Vector3 pos)
        {
            Material wood = MaterialLibrary.Surface(SurfaceKind.Wood, Color.white, ProceduralTextures.Wood(new Color(0.55f, 0.38f, 0.25f), new Color(0.35f, 0.24f, 0.16f)), 2f);
            Material metal = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.3f, 0.3f, 0.32f));
            Prop(r.Static, "TowerTank", MeshFactory.Cylinder(1.1f, 2.2f, 24), wood, pos + Vector3.up * 2.6f, Vector3.zero, Vector3.one, true);
            Prop(r.Static, "TowerRoof", MeshFactory.Frustum(1.2f, 0.05f, 0.9f, 24), metal, pos + Vector3.up * 4.15f, Vector3.zero, Vector3.one, true);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.785f;
                Vector3 leg = pos + new Vector3(Mathf.Cos(a) * 0.8f, 0.75f, Mathf.Sin(a) * 0.8f);
                Prop(r.Static, "TowerLeg", MeshFactory.Cylinder(0.06f, 1.5f, 10), metal, leg, Vector3.zero, Vector3.one, true);
            }
            for (int i = 0; i < 3; i++)
                Prop(r.Static, "Hoop", MeshFactory.Torus(1.12f, 0.025f, 32, 6), metal, pos + Vector3.up * (1.8f + i * 0.8f), Vector3.zero, Vector3.one, false);
        }

        static void Skyline(Result r, float radius, int count, Color windowColor, bool night)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f;
                float h = 6f + Hash(i, 3) * 14f;
                float w = 2.5f + Hash(i, 5) * 3f;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius * (1f + Hash(i, 9) * 0.3f), h * 0.5f - 6f, Mathf.Sin(a) * radius * (1f + Hash(i, 11) * 0.3f));
                Color wall = Color.Lerp(new Color(0.1f, 0.11f, 0.16f), new Color(0.22f, 0.2f, 0.28f), Hash(i, 13));
                Material m = night
                    ? MaterialLibrary.Emissive(Color.white, Color.white, 0.9f, ProceduralTextures.Windows(wall, windowColor, true, i % 4))
                    : MaterialLibrary.Surface(SurfaceKind.Painted, Color.white, ProceduralTextures.Windows(wall, windowColor, false, i % 4));
                GameObject b = Prop(r.Static, "Building", MeshFactory.Box(new Vector3(w, h, w)), m, p, new Vector3(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one, false);
                b.GetComponent<MeshRenderer>().receiveShadows = false;
                // Antenna on some roofs.
                if (Hash(i, 17) > 0.6f)
                    Prop(r.Static, "Antenna", MeshFactory.Cylinder(0.05f, 2f, 6), MaterialLibrary.Emissive(Color.red, Color.red, 2f), p + Vector3.up * (h * 0.5f + 1f), Vector3.zero, Vector3.one, false);
            }
        }

        static void Palm(Result r, Vector3 pos, float lean)
        {
            Material trunk = MaterialLibrary.Surface(SurfaceKind.Wood, Color.white, ProceduralTextures.Wood(new Color(0.62f, 0.48f, 0.32f), new Color(0.4f, 0.3f, 0.2f)), 1f);
            Material leaf = MaterialLibrary.Surface(SurfaceKind.Fabric, new Color(0.18f, 0.6f, 0.28f));
            GameObject palm = new GameObject("Palm");
            palm.transform.SetParent(r.Dynamic.transform, false);
            palm.transform.localPosition = pos;
            Vector3 p = Vector3.zero;
            float angle = 0f;
            for (int i = 0; i < 7; i++)
            {
                angle += lean / 7f;
                float rr = 0.2f - i * 0.015f;
                Prop(palm, "Trunk", MeshFactory.Frustum(rr, rr - 0.02f, 0.75f, 12), trunk, p + Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.37f, 0f), new Vector3(0f, 0f, angle), Vector3.one, true);
                p += Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.72f, 0f);
            }
            GameObject crown = new GameObject("Crown");
            crown.transform.SetParent(palm.transform, false);
            crown.transform.localPosition = p;
            for (int i = 0; i < 8; i++)
            {
                GameObject frond = Prop(crown, "Frond", MeshFactory.Capsule(0.13f, 1.9f, 10, 3), leaf, Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 0.25f), true);
                frond.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f) * Quaternion.Euler(0f, 0f, 62f + (i % 2) * 14f);
                frond.transform.localPosition = frond.transform.localRotation * new Vector3(0f, 0.85f, 0f);
            }
            Material coco = MaterialLibrary.Surface(SurfaceKind.Wood, new Color(0.35f, 0.24f, 0.12f));
            for (int i = 0; i < 3; i++)
                Prop(crown, "Coconut", MeshFactory.Sphere(0.11f, 10, 8), coco, new Vector3(Mathf.Cos(i * 2.1f) * 0.15f, -0.12f, Mathf.Sin(i * 2.1f) * 0.15f), Vector3.zero, Vector3.one, true);
            r.Crowd.AddSway(crown.transform, 4f, 0.7f);
        }

        static void Torch(Result r, Vector3 pos)
        {
            Material bamboo = MaterialLibrary.Surface(SurfaceKind.Wood, new Color(0.75f, 0.62f, 0.35f));
            Material flame = MaterialLibrary.Emissive(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.45f, 0.1f), 4f);
            Prop(r.Static, "TorchPole", MeshFactory.Cylinder(0.04f, 1.6f, 8), bamboo, pos + Vector3.up * 0.8f, Vector3.zero, Vector3.one, true);
            Prop(r.Static, "TorchCup", MeshFactory.Frustum(0.06f, 0.1f, 0.15f, 10), bamboo, pos + Vector3.up * 1.65f, Vector3.zero, Vector3.one, true);
            GameObject f = Prop(r.Dynamic, "Flame", MeshFactory.Capsule(0.06f, 0.22f, 10, 3), flame, pos + Vector3.up * 1.82f, Vector3.zero, Vector3.one, false);
            r.Crowd.AddFlicker(f.transform);
        }

        static void Umbrella(Result r, Vector3 pos, Color color)
        {
            Material pole = MaterialLibrary.Surface(SurfaceKind.Wood, new Color(0.85f, 0.8f, 0.7f));
            Material canopy = MaterialLibrary.Surface(SurfaceKind.Fabric, color);
            Prop(r.Static, "UmbrellaPole", MeshFactory.Cylinder(0.03f, 2.3f, 8), pole, pos + Vector3.up * 1.15f, new Vector3(0f, 0f, 6f), Vector3.one, true);
            Prop(r.Static, "Canopy", MeshFactory.Frustum(1.2f, 0.05f, 0.45f, 16), canopy, pos + new Vector3(-0.12f, 2.3f, 0f), new Vector3(0f, 0f, 6f), Vector3.one, true);
        }

        static void Arch(Result r, float z, float width, float height, Material m)
        {
            int segs = 14;
            Vector3 prev = new Vector3(-width * 0.5f, 0f, z);
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                float a = Mathf.PI * t;
                Vector3 p = new Vector3(-Mathf.Cos(a) * width * 0.5f, Mathf.Sin(a) * height, z);
                Vector3 d = p - prev;
                GameObject g = Prop(r.Static, "ArchSeg", MeshFactory.Capsule(0.04f, d.magnitude + 0.08f, 8, 2), m, (p + prev) * 0.5f, Vector3.zero, Vector3.one, false);
                g.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                prev = p;
            }
        }

        static void AddPointLight(Result r, Vector3 pos, Color c, float intensity, float range)
        {
            GameObject g = new GameObject("PointLight");
            g.transform.SetParent(r.Root.transform, false);
            g.transform.localPosition = pos;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            r.ExtraLights.Add(l);
        }

        static GameObject Prop(GameObject parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale, bool shadows)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent.transform, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return g;
        }

        static float Hash(int i, int seed)
        {
            unchecked
            {
                int h = i * 73856093 ^ seed * 19349663;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }
    }
}
