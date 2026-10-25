using UnityEngine;

namespace Brooklyneir.Art
{
    /// <summary>
    /// All particle effects, created in code. Each effect is a single world-space particle system
    /// reused for every occurrence (emit at a position), so there is no runtime instantiation, no
    /// garbage and nothing to clean up between points. Particle counts scale with the quality
    /// profile. Effects are short and kept away from the ball's line so they never hide it.
    /// </summary>
    public class VfxSystem : MonoBehaviour
    {
        ParticleSystem _sparks, _dust, _burst, _ring, _pickup, _ice, _confetti, _fire, _embers;
        float _multiplier = 1f;

        public void Init()
        {
            Texture2D soft = ProceduralTextures.SoftCircle(0.2f);
            Texture2D spark = ProceduralTextures.Spark();
            _sparks = Make("Sparks", MaterialLibrary.Additive(Color.white, spark), 120, true);
            ConfigureBurst(_sparks, 0.18f, 0.38f, 2.5f, 5.5f, 0.02f, 0.05f, 0.6f, ParticleSystemShapeType.Sphere, 0.02f);
            Stretch(_sparks, 0.06f);

            _dust = Make("BounceDust", MaterialLibrary.ParticleAlpha(new Color(1f, 1f, 1f, 0.6f), soft), 60, false);
            ConfigureBurst(_dust, 0.25f, 0.4f, 0.2f, 0.6f, 0.03f, 0.06f, 0f, ParticleSystemShapeType.Circle, 0.03f);

            _burst = Make("SmashBurst", MaterialLibrary.Additive(Color.white, spark), 200, true);
            ConfigureBurst(_burst, 0.3f, 0.6f, 4f, 9f, 0.03f, 0.08f, 0.4f, ParticleSystemShapeType.Sphere, 0.05f);
            Stretch(_burst, 0.08f);

            _ring = Make("Shockwave", MaterialLibrary.Additive(new Color(1f, 1f, 1f, 0.8f), ProceduralTextures.RingMarker()), 8, false);
            ConfigureBurst(_ring, 0.35f, 0.35f, 0f, 0f, 0.1f, 0.1f, 0f, ParticleSystemShapeType.Sphere, 0.001f);
            ParticleSystem.SizeOverLifetimeModule ringSize = _ring.sizeOverLifetime;
            ringSize.enabled = true;
            ringSize.size = new ParticleSystem.MinMaxCurve(6f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));

            _pickup = Make("Pickup", MaterialLibrary.Additive(Color.white, soft), 80, false);
            ConfigureBurst(_pickup, 0.4f, 0.8f, 0.6f, 1.8f, 0.04f, 0.09f, -0.2f, ParticleSystemShapeType.Sphere, 0.08f);

            _ice = Make("Ice", MaterialLibrary.Additive(new Color(0.7f, 0.9f, 1f, 1f), spark), 80, true);
            ConfigureBurst(_ice, 0.4f, 0.8f, 0.5f, 1.6f, 0.03f, 0.07f, 0.3f, ParticleSystemShapeType.Sphere, 0.12f);

            _confetti = Make("Confetti", MaterialLibrary.ParticleAlpha(Color.white, soft), 400, false);
            ConfigureBurst(_confetti, 2.5f, 4f, 2f, 5f, 0.04f, 0.08f, 0.35f, ParticleSystemShapeType.Cone, 0.6f);
            ParticleSystem.MainModule cm = _confetti.main;
            cm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.2f), new Color(0.2f, 0.8f, 1f));
            ParticleSystem.ShapeModule cs = _confetti.shape;
            cs.angle = 35f;
            cs.rotation = new Vector3(-90f, 0f, 0f);

            _fire = Make("FireTrail", MaterialLibrary.Additive(new Color(1f, 0.55f, 0.15f, 1f), soft), 160, false);
            ParticleSystem.MainModule fm = _fire.main;
            fm.loop = true;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            fm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.1f));
            ParticleSystem.EmissionModule fe = _fire.emission;
            fe.rateOverTime = 0f;
            fe.rateOverDistance = 90f;
            ParticleSystem.ShapeModule fs = _fire.shape;
            fs.shapeType = ParticleSystemShapeType.Sphere;
            fs.radius = 0.02f;
            FadeOut(_fire, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.2f, 0.05f));

            _embers = Make("Embers", MaterialLibrary.Additive(new Color(1f, 0.6f, 0.2f, 1f), spark), 60, false);
            ParticleSystem.MainModule em = _embers.main;
            em.loop = true;
            em.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            em.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            em.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
            em.gravityModifier = -0.1f;
            ParticleSystem.EmissionModule ee = _embers.emission;
            ee.rateOverTime = 0f;
            ee.rateOverDistance = 25f;

            ParticleSystem[] bursts = { _sparks, _dust, _burst, _ring, _pickup, _ice, _confetti };
            for (int i = 0; i < bursts.Length; i++) bursts[i].Play();
        }

        public void SetQualityMultiplier(float m)
        {
            _multiplier = Mathf.Clamp(m, 0.2f, 1f);
            if (_fire != null)
            {
                ParticleSystem.EmissionModule fe = _fire.emission;
                fe.rateOverDistance = 90f * _multiplier;
            }
        }

        ParticleSystem Make(string name, Material mat, int max, bool stretched)
        {
            GameObject g = new GameObject("FX_" + name);
            g.transform.SetParent(transform, false);
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.duration = 1f;
            // Looping with zero emission: the system stays "playing" so Emit() particles always simulate.
            main.loop = true;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -10f;
            return ps;
        }

        static void ConfigureBurst(ParticleSystem ps, float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax,
            float gravity, ParticleSystemShapeType shape, float radius)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.gravityModifier = gravity;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = shape;
            sh.radius = radius;
            FadeOut(ps, Color.white, Color.white);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        }

        static void FadeOut(ParticleSystem ps, Color from, Color to)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static void Stretch(ParticleSystem ps, float velocityScale)
        {
            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.velocityScale = velocityScale;
            r.lengthScale = 1.2f;
        }

        void EmitAt(ParticleSystem ps, Vector3 pos, Color color, int count)
        {
            if (ps == null) return;
            if (!ps.isPlaying) ps.Play();
            int n = Mathf.Max(1, Mathf.RoundToInt(count * _multiplier));
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = pos;
            p.startColor = color;
            p.applyShapeToPosition = true;
            ps.Emit(p, n);
        }

        public void Hit(Vector3 pos, Color color, float strength)
        {
            EmitAt(_sparks, pos, color, Mathf.RoundToInt(Mathf.Lerp(8f, 28f, strength)));
        }

        public void Bounce(Vector3 pos, Color color)
        {
            EmitAt(_dust, pos + Vector3.up * 0.005f, color, 6);
        }

        public void Smash(Vector3 pos, Color color)
        {
            EmitAt(_burst, pos, color, 50);
            EmitAt(_ring, pos, new Color(color.r, color.g, color.b, 0.9f), 1);
        }

        public void Pickup(Vector3 pos, Color color)
        {
            EmitAt(_pickup, pos, color, 30);
            EmitAt(_ring, pos, color, 1);
        }

        public void Freeze(Vector3 pos)
        {
            EmitAt(_ice, pos, new Color(0.75f, 0.92f, 1f), 40);
        }

        public void Confetti(Vector3 pos)
        {
            if (_confetti == null) return;
            _confetti.transform.position = pos;
            int n = Mathf.RoundToInt(180 * _multiplier);
            for (int i = 0; i < n; i++)
            {
                ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
                p.position = pos + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-1.2f, 1.2f));
                p.velocity = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(2.5f, 5f), Random.Range(-1.5f, 1.5f));
                p.startColor = Color.HSVToRGB(Random.value, 0.75f, 1f);
                p.startSize = Random.Range(0.04f, 0.08f);
                p.startLifetime = Random.Range(2.5f, 4f);
                _confetti.Emit(p, 1);
            }
        }

        /// <summary>Attaches the continuous fire trail to the ball (or detaches it).</summary>
        public void SetFireTrail(Transform ball, bool on)
        {
            if (_fire == null) return;
            if (on && ball != null)
            {
                _fire.transform.SetParent(ball, false);
                _fire.transform.localPosition = Vector3.zero;
                _embers.transform.SetParent(ball, false);
                _embers.transform.localPosition = Vector3.zero;
                if (!_fire.isPlaying) _fire.Play();
                if (!_embers.isPlaying) _embers.Play();
            }
            else
            {
                _fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Removes every live particle (used when a match is reset or left).</summary>
        public void ClearAll()
        {
            ParticleSystem[] all = { _sparks, _dust, _burst, _ring, _pickup, _ice, _confetti, _fire, _embers };
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) all[i].Clear(true);
            if (_fire != null) _fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_embers != null) _embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_fire != null) _fire.transform.SetParent(transform, false);
            if (_embers != null) _embers.transform.SetParent(transform, false);
        }
    }
}
