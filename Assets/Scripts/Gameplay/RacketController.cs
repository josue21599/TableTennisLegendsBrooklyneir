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
    /// model: automatic height towards the incoming ball, forehand/backhand lean, a stroke that
    /// depends on the shot type, an idle sway while waiting, a celebration after the point and the
    /// ice shell while frozen. The animations only change how the racket is drawn: hits are
    /// decided by the logical Position.
    /// </summary>
    public class RacketController : MonoBehaviour
    {
        public const float VisualScale = 2.2f;
        public const float RestHeight = 0.95f;

        public int Player { get; private set; }
        public RacketDefinition Definition { get; private set; }
        public RacketStatModel Stats { get; private set; }
        public Vector3 Position { get; private set; }
        /// <summary>Position at the start of this frame's movement (for swept hit detection).</summary>
        public Vector3 PreviousPosition { get; private set; }
        public Vector3 Target { get; private set; }
        public bool Assist { get; set; }
        public float VisualHeight { get; private set; }

        RacketTuning _t;
        Transform _model;
        GameObject _ice;
        readonly Vector3[] _history = new Vector3[8];
        readonly Vector3[] _targetHistory = new Vector3[8];
        readonly float[] _historyTime = new float[8];
        int _historyIndex;
        float _clock;
        float _swingTimer = -1f;
        float _swingStrength;
        ShotType _swingType = ShotType.Normal;
        float _celebrateTimer = -1f;
        bool _celebrateWon;
        float _idleWeight;
        float _lean;
        Vector3 _smoothedVelocity;
        Vector3 _strokeVelocity;
        Vector3 _syntheticSwing;
        bool _useSynthetic;
        bool _hasCross;
        Vector3 _cross;
        float _crossTime;
        bool _magnet;
        Vector3 _magnetAccum;

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
            PreviousPosition = Home;
            Target = Home;
            for (int i = 0; i < _history.Length; i++) { _history[i] = Position; _targetHistory[i] = Position; _historyTime[i] = _clock - i * 0.02f; }
            _smoothedVelocity = Vector3.zero;
            _strokeVelocity = Vector3.zero;
            _magnetAccum = Vector3.zero;
            _hasCross = false;
            _swingTimer = -1f;
            _celebrateTimer = -1f;
            _idleWeight = 0f;
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

        /// <summary>
        /// The stroke: how the player's finger (the racket's target) has been moving over the last
        /// ~0.1 s (m/s, world), capped at the racket's top speed. The finger leads the racket, so
        /// this is the stroke the player is making at the moment of contact, not the racket's
        /// delayed catch-up movement.
        /// </summary>
        public Vector3 SwingVelocity
        {
            get { return _useSynthetic ? _syntheticSwing : _strokeVelocity; }
        }

        /// <summary>Predicted crossing of the incoming ball through this racket's plane.</summary>
        public void SetPrediction(bool has, Vector3 crossing, float timeToCrossing, bool magnet)
        {
            _hasCross = has;
            _cross = crossing;
            _crossTime = timeToCrossing;
            _magnet = magnet;
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
            PlaySwing(strength, ShotType.Normal);
        }

        /// <summary>Plays the stroke of this shot type (visual only).</summary>
        public void PlaySwing(float strength, ShotType type)
        {
            _swingTimer = 0f;
            _swingStrength = Mathf.Clamp01(strength);
            _swingType = type;
            _celebrateTimer = -1f;
        }

        /// <summary>After a point: raise and pump the racket (won) or let it drop a little (lost).</summary>
        public void PlayCelebration(bool won)
        {
            _celebrateTimer = 0f;
            _celebrateWon = won;
            _swingTimer = -1f;
        }

        /// <summary>Moves the racket. <paramref name="ballPos"/> drives the automatic height.</summary>
        public void Tick(float dt, float followMultiplier, Vector3 ballPos, bool ballIncoming)
        {
            if (dt <= 0f) return;
            _clock += dt;
            PreviousPosition = Position;
            float maxSpeed = FollowSpeed * Mathf.Max(0.05f, followMultiplier);
            Vector3 next = Vector3.MoveTowards(Position, Target, maxSpeed * dt);
            // Exponential smoothing on top of the speed limit removes jitter from noisy touches.
            float k = 1f - Mathf.Exp(-_t.followSharpness * dt);
            next = Vector3.Lerp(Position, next, Mathf.Max(k, 0.5f));
            Position = Clamp(next);

            // Gentle pull onto the ball just before contact (human players). The pull is kept out
            // of the measured swing, so it never changes the shot type or its direction.
            if (_magnet && _hasCross && _crossTime < _t.magnetWindow && followMultiplier > 0.2f)
            {
                float dx = _cross.x - Position.x;
                float range = Reach + _t.magnetRange + (Assist ? 0.08f : 0f);
                if (Mathf.Abs(dx) < range && Mathf.Abs(dx) > 0.005f)
                {
                    float step = Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), _t.magnetSpeed * dt);
                    Vector3 before = Position;
                    Position = Clamp(Position + new Vector3(step, 0f, 0f));
                    Vector3 moved = Position - before;
                    Target = Clamp(Target + moved);
                    _magnetAccum += moved;
                }
            }

            _historyIndex = (_historyIndex + 1) % _history.Length;
            _history[_historyIndex] = Position - _magnetAccum;
            _targetHistory[_historyIndex] = Target - _magnetAccum;
            _historyTime[_historyIndex] = _clock;
            int oldest = _historyIndex;
            for (int i = 1; i < _history.Length; i++)
            {
                int idx = (_historyIndex - i + _history.Length) % _history.Length;
                if (_clock - _historyTime[idx] > 0.08f) break;
                oldest = idx;
            }
            float span = _clock - _historyTime[oldest];
            Vector3 v = span > 1e-4f ? (Position - _magnetAccum - _history[oldest]) / span : Vector3.zero;
            _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, v, 0.6f);

            int oldestT = _historyIndex;
            for (int i = 1; i < _targetHistory.Length; i++)
            {
                int idx = (_historyIndex - i + _targetHistory.Length) % _targetHistory.Length;
                if (_clock - _historyTime[idx] > 0.1f) break;
                oldestT = idx;
            }
            float spanT = _clock - _historyTime[oldestT];
            Vector3 vt = spanT > 1e-4f ? (Target - _magnetAccum - _targetHistory[oldestT]) / spanT : Vector3.zero;
            vt.y = 0f;
            vt = Vector3.ClampMagnitude(vt, maxSpeed);
            _strokeVelocity = Vector3.Lerp(_strokeVelocity, vt, 0.7f);

            // Visual height: rise towards an incoming ball when it is close.
            // With a predicted crossing, the blade goes to the exact height where the ball will
            // pass, so on screen the ball meets the blade whenever the hit will register.
            float targetY = RestHeight;
            float heightSharpness = 14f;
            // Same window as the hit test, so the blade is never drawn where the ball cannot be hit.
            float minY = TableGeometry.TopY + _t.minHittableHeight, maxY = TableGeometry.TopY + _t.maxHittableHeight;
            if (ballIncoming && _hasCross)
            {
                float w = Mathf.Clamp01(1f - (_crossTime - 0.25f) / 0.6f);
                targetY = Mathf.Lerp(RestHeight, Mathf.Clamp(_cross.y, minY, maxY), w);
                heightSharpness = 22f;
            }
            else if (ballIncoming)
            {
                float dz = Mathf.Abs(ballPos.z - Position.z);
                float w = Mathf.Clamp01(1f - dz / 1.6f);
                targetY = Mathf.Lerp(RestHeight, Mathf.Clamp(ballPos.y, minY, maxY), w);
            }
            VisualHeight = Mathf.Lerp(VisualHeight, targetY, 1f - Mathf.Exp(-heightSharpness * dt));
            float leanX = ballIncoming && _hasCross ? _cross.x : ballPos.x;
            float desiredLean = Mathf.Clamp((leanX - Position.x) * 2.2f, -1f, 1f);
            _lean = Mathf.Lerp(_lean, desiredLean, 1f - Mathf.Exp(-10f * dt));
            AnimationConfig anim = AnimationSettings.Config;
            if (_swingTimer >= 0f)
            {
                _swingTimer += dt;
                if (_swingTimer >= anim.StrokeFor(_swingType).duration) _swingTimer = -1f;
            }
            if (_celebrateTimer >= 0f)
            {
                _celebrateTimer += dt;
                if (_celebrateTimer >= RacketAnimation.CelebrationDuration(_celebrateWon, anim)) _celebrateTimer = -1f;
            }
            // The idle sway fades out as soon as a ball comes, so the blade meets it exactly.
            float idleTarget = ballIncoming || _swingTimer >= 0f || _celebrateTimer >= 0f ? 0f : 1f;
            _idleWeight = Mathf.MoveTowards(_idleWeight, idleTarget, dt * (idleTarget > _idleWeight ? 1.5f : 8f));
            ApplyTransform(_smoothedVelocity);
        }

        void ApplyTransform(Vector3 velocity)
        {
            float fwd = TableGeometry.ForwardSign(Player);
            AnimationConfig anim = AnimationSettings.Config;
            RacketPoseOffset o = RacketAnimation.Idle(_clock, Player * 0.37f, _idleWeight, anim);
            if (_swingTimer >= 0f) o = o + RacketAnimation.Stroke(anim.StrokeFor(_swingType), _swingTimer, _swingStrength, anim);
            if (_celebrateTimer >= 0f) o = o + RacketAnimation.Celebration(_celebrateWon, _celebrateTimer, anim);
            Vector3 centre = new Vector3(Position.x, VisualHeight + o.Lift, Position.z);
            // Pitch rises with the swing (blade sweeps up and over the ball), roll follows the ball side.
            float fwdVel = velocity.z * fwd;
            float pitch = 28f + o.Pitch + Mathf.Clamp(fwdVel * 5f, -18f, 22f);
            float roll = (-_lean * 35f + o.Roll) * fwd;
            float yaw = (Mathf.Clamp(velocity.x * 9f, -35f, 35f) + o.Yaw) * fwd;
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
