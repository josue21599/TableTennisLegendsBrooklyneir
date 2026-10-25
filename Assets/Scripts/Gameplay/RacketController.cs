using Brooklyneir.Art;
using Brooklyneir.Config;
using Brooklyneir.Physics;
using Brooklyneir.Rackets;
using UnityEngine;

namespace Brooklyneir.Gameplay
{
    /// <summary>
    /// One player's racket. Holds the logical position (on the horizontal play plane, clamped to
    /// the player's zone), follows its target with a speed limited by the racket's response stat
    /// (and the freeze penalty), measures its own velocity for the swing, and animates the 3D
    /// model: automatic height towards the incoming ball, forehand/backhand lean, swing on hit,
    /// ice shell while frozen.
    /// </summary>
    public class RacketController : MonoBehaviour
    {
        public const float VisualScale = 2.2f;
        public const float RestHeight = 0.95f;

        public int Player { get; private set; }
        public RacketDefinition Definition { get; private set; }
        public RacketStatModel Stats { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Target { get; private set; }
        public bool Assist { get; set; }
        public float VisualHeight { get; private set; }

        RacketTuning _t;
        Transform _model;
        GameObject _ice;
        readonly Vector3[] _history = new Vector3[8];
        readonly float[] _historyTime = new float[8];
        int _historyIndex;
        float _clock;
        float _swingTimer = -1f;
        float _swingStrength;
        float _lean;
        Vector3 _smoothedVelocity;
        Vector3 _syntheticSwing;
        bool _useSynthetic;

        public void Setup(int player, RacketDefinition def, RacketTuning tuning, Color playerColor, Color skin)
        {
            Player = player;
            _t = tuning;
            Definition = def;
            Stats = new RacketStatModel(def.Stats, tuning);
            if (_model != null) Destroy(_model.gameObject);
            GameObject racket = RacketModelBuilder.Build(def, transform);
            HandModelBuilder.Build(racket.transform, playerColor, skin);
            _model = racket.transform;
            _model.localScale = Vector3.one * VisualScale;

            if (_ice == null)
            {
                _ice = new GameObject("IceShell");
                _ice.transform.SetParent(transform, false);
                _ice.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Sphere(0.22f, 18, 12);
                MeshRenderer r = _ice.AddComponent<MeshRenderer>();
                r.sharedMaterial = MaterialLibrary.LitTransparent(new Color(0.7f, 0.9f, 1f, 0.35f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _ice.SetActive(false);
            ResetToHome();
        }

        public Vector3 Home
        {
            get { return new Vector3(0f, RestHeight, -TableGeometry.ForwardSign(Player) * _t.defaultZ); }
        }

        public float Reach
        {
            get { return Stats.Reach + (Assist ? _t.assistReach : 0f); }
        }

        public float FollowSpeed { get { return Stats.FollowSpeed; } }

        public void ResetToHome()
        {
            Position = Home;
            Target = Home;
            for (int i = 0; i < _history.Length; i++) { _history[i] = Position; _historyTime[i] = _clock - i * 0.02f; }
            _smoothedVelocity = Vector3.zero;
            _swingTimer = -1f;
            VisualHeight = RestHeight;
            ApplyTransform(Vector3.zero);
        }

        public Vector3 Clamp(Vector3 p)
        {
            float s = -TableGeometry.ForwardSign(Player); // -1 for player 1 (z < 0)
            float near = _t.zoneNearZ, far = _t.zoneFarZ;
            float z = s < 0f ? Mathf.Clamp(p.z, -far, -near) : Mathf.Clamp(p.z, near, far);
            return new Vector3(Mathf.Clamp(p.x, -_t.zoneHalfWidth, _t.zoneHalfWidth), RestHeight, z);
        }

        public void SetTarget(Vector3 target)
        {
            Target = Clamp(target);
        }

        public void MoveTargetBy(Vector3 delta)
        {
            Target = Clamp(Target + new Vector3(delta.x, 0f, delta.z));
        }

        /// <summary>AI rackets report a decided swing instead of a measured one.</summary>
        public void SetSyntheticSwing(Vector3 swing)
        {
            _syntheticSwing = swing;
            _useSynthetic = true;
        }

        public void ClearSyntheticSwing()
        {
            _useSynthetic = false;
        }

        /// <summary>Velocity of the racket over the last ~0.08 s (m/s, world).</summary>
        public Vector3 SwingVelocity
        {
            get { return _useSynthetic ? _syntheticSwing : _smoothedVelocity; }
        }

        public void SetFrozen(bool frozen)
        {
            if (_ice != null) _ice.SetActive(frozen);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void PlaySwing(float strength)
        {
            _swingTimer = 0f;
            _swingStrength = Mathf.Clamp01(strength);
        }

        /// <summary>Moves the racket. <paramref name="ballPos"/> drives the automatic height.</summary>
        public void Tick(float dt, float followMultiplier, Vector3 ballPos, bool ballIncoming)
        {
            if (dt <= 0f) return;
            _clock += dt;
            float maxSpeed = FollowSpeed * Mathf.Max(0.05f, followMultiplier);
            Vector3 next = Vector3.MoveTowards(Position, Target, maxSpeed * dt);
            // Exponential smoothing on top of the speed limit removes jitter from noisy touches.
            float k = 1f - Mathf.Exp(-_t.followSharpness * dt);
            next = Vector3.Lerp(Position, next, Mathf.Max(k, 0.5f));
            Position = Clamp(next);

            _historyIndex = (_historyIndex + 1) % _history.Length;
            _history[_historyIndex] = Position;
            _historyTime[_historyIndex] = _clock;
            int oldest = _historyIndex;
            for (int i = 1; i < _history.Length; i++)
            {
                int idx = (_historyIndex - i + _history.Length) % _history.Length;
                if (_clock - _historyTime[idx] > 0.08f) break;
                oldest = idx;
            }
            float span = _clock - _historyTime[oldest];
            Vector3 v = span > 1e-4f ? (Position - _history[oldest]) / span : Vector3.zero;
            _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, v, 0.6f);

            // Visual height: rise towards an incoming ball when it is close.
            float targetY = RestHeight;
            if (ballIncoming)
            {
                float dz = Mathf.Abs(ballPos.z - Position.z);
                float w = Mathf.Clamp01(1f - dz / 1.6f);
                targetY = Mathf.Lerp(RestHeight, Mathf.Clamp(ballPos.y, TableGeometry.TopY + 0.08f, TableGeometry.TopY + 1.1f), w);
            }
            VisualHeight = Mathf.Lerp(VisualHeight, targetY, 1f - Mathf.Exp(-14f * dt));
            float desiredLean = Mathf.Clamp((ballPos.x - Position.x) * 2.2f, -1f, 1f);
            _lean = Mathf.Lerp(_lean, desiredLean, 1f - Mathf.Exp(-10f * dt));
            if (_swingTimer >= 0f)
            {
                _swingTimer += dt;
                if (_swingTimer > 0.3f) _swingTimer = -1f;
            }
            ApplyTransform(_smoothedVelocity);
        }

        void ApplyTransform(Vector3 velocity)
        {
            float fwd = TableGeometry.ForwardSign(Player);
            Vector3 centre = new Vector3(Position.x, VisualHeight, Position.z);
            float swing = 0f;
            if (_swingTimer >= 0f)
            {
                float k = _swingTimer / 0.3f;
                swing = Mathf.Sin(k * Mathf.PI) * (25f + 30f * _swingStrength);
            }
            // Pitch rises with the swing (blade sweeps up and over the ball), roll follows the ball side.
            float pitch = 28f + swing;
            float roll = -_lean * 35f * fwd;
            float yaw = Mathf.Clamp(velocity.x * 5f, -25f, 25f) * fwd;
            Quaternion rot = RacketModelBuilder.Pose(fwd, pitch, roll, yaw);
            transform.position = centre;
            if (_model != null)
            {
                _model.localRotation = rot;
                _model.localPosition = -(rot * new Vector3(0f, RacketModelBuilder.BladeCentreY * VisualScale, 0f));
            }
            if (_ice != null) _ice.transform.localPosition = Vector3.zero;
        }
    }
}
