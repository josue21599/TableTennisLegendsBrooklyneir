using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.Physics;
using Brooklyneir.Rackets;
using UnityEngine;

namespace Brooklyneir.Gameplay
{
    public enum ShotType { Normal, Fast, Defensive, Smash, Spin, Special, SuperSmash, Serve }

    public struct ShotContext
    {
        public int Hitter;
        public Vector3 ContactPoint;
        /// <summary>(ball.x - racket.x) / reach, in [-1, 1]: where on the blade the ball was met.</summary>
        public float ContactOffset;
        /// <summary>Racket velocity in world space (m/s) at contact.</summary>
        public Vector3 RacketVelocity;
        public RacketStatModel Stats;
        public bool Assist;
        public bool IsServe;
        public bool SpecialArmed;
        public bool SuperSmashArmed;
        public bool CurveArmed;
        public bool FireActive;
        /// <summary>Multiplies the swing (freeze penalty = less than 1).</summary>
        public float SwingMultiplier;
        /// <summary>Extra noise factor (AI difficulty); 1 for humans.</summary>
        public float NoiseMultiplier;
    }

    public struct ShotResult
    {
        public ShotType Type;
        public Vector3 Velocity;
        public Vector3 SpinAccel;
        public Vector3 IntendedTarget;
        public Vector3 NoisyTarget;
        public float FlightTime;
        public float SideSpin;
        public float CameraShake;
        public bool ConsumedSpecial;
        public bool ConsumedSuperSmash;
        public bool ConsumedCurve;
    }

    /// <summary>
    /// Arcade shot model. The player decides WHAT (shot class, direction, spin) with the swipe
    /// and the contact point; the model decides HOW (launch velocity) with the inverse solver,
    /// then applies controlled, stat-dependent error. Results are consistent: the same swipe on
    /// the same ball gives the same intent, and error is bounded by Gaussian noise whose size
    /// depends on the shot risk and on the racket's precision/control.
    /// </summary>
    public class ShotModel
    {
        readonly BalanceConfig _cfg;
        readonly IRandomSource _rng;

        public ShotModel(BalanceConfig cfg, IRandomSource rng)
        {
            _cfg = cfg;
            _rng = rng;
        }

        public ShotType Classify(ShotContext ctx, out float forwardSpeed, out float lateralSpeed)
        {
            float swing = ctx.SwingMultiplier <= 0f ? 1f : ctx.SwingMultiplier;
            float fwdSign = TableGeometry.ForwardSign(ctx.Hitter);
            forwardSpeed = ctx.RacketVelocity.z * fwdSign * swing;
            lateralSpeed = ctx.RacketVelocity.x * swing;
            ShotTuning s = _cfg.shots;
            if (ctx.IsServe) return ShotType.Serve;
            if (ctx.SuperSmashArmed) return ShotType.SuperSmash;
            if (ctx.SpecialArmed) return ShotType.Special;
            float height = ctx.ContactPoint.y - TableGeometry.TopY;
            // The direction of the swing decides the shot (0 deg = straight at the net,
            // 90 = sideways, 180 = backwards); its speed decides how strong it is.
            float speed = Mathf.Sqrt(forwardSpeed * forwardSpeed + lateralSpeed * lateralSpeed);
            float angle = SwingAngle(forwardSpeed, lateralSpeed);
            if (angle >= s.defensiveMinAngle && speed >= -s.defensiveSwipeThreshold) return ShotType.Defensive;
            if (angle > s.spinMinAngle && angle < s.defensiveMinAngle && Mathf.Abs(lateralSpeed) >= s.spinSwipeThreshold) return ShotType.Spin;
            if (forwardSpeed >= s.smashSwipeThreshold && angle <= s.smashMaxAngle && height >= s.smashMinHeight) return ShotType.Smash;
            if (forwardSpeed >= s.fastSwipeThreshold && angle <= s.spinMinAngle) return ShotType.Fast;
            return ShotType.Normal;
        }

        /// <summary>
        /// Sideways shift (m) of the landing point for a stroke with this sideways speed (m/s,
        /// world x). Same sign as the stroke, exactly 0 inside the deadzone, monotonic and never
        /// larger than maxShiftX. Pure and allocation-free; used by the shot model and the AI.
        /// </summary>
        public static float LateralShift(float lateralSpeed, ShotTuning s)
        {
            float v = Mathf.Abs(lateralSpeed) - s.lateralDeadzone;
            if (v <= 0f) return 0f;
            float x = v / Mathf.Max(0.01f, s.lateralRefSpeed);
            float e = (float)System.Math.Exp(-2.0 * x);
            float tanh = (1f - e) / (1f + e);
            return Mathf.Sign(lateralSpeed) * s.maxShiftX * tanh;
        }

        /// <summary>Angle of the swing in degrees: 0 = towards the net, 90 = sideways, 180 = backwards.</summary>
        public static float SwingAngle(float forward, float lateral)
        {
            if (Mathf.Abs(forward) < 1e-4f && Mathf.Abs(lateral) < 1e-4f) return 0f;
            return Mathf.Atan2(Mathf.Abs(lateral), forward) * Mathf.Rad2Deg;
        }

        public ShotTypeTuning TuningFor(ShotType type)
        {
            ShotTuning s = _cfg.shots;
            switch (type)
            {
                case ShotType.Fast: return s.fast;
                case ShotType.Smash: return s.smash;
                case ShotType.Defensive: return s.defensive;
                case ShotType.Special: return s.special;
                case ShotType.SuperSmash: return s.superSmash;
                case ShotType.Serve: return s.serve;
                default: return s.normal;
            }
        }

        public ShotResult Compute(ShotContext ctx)
        {
            float fwd, lat;
            ShotType type = Classify(ctx, out fwd, out lat);
            ShotTypeTuning t = TuningFor(type);
            ShotTuning s = _cfg.shots;
            PhysicsConfig phys = _cfg.physics;
            RacketStatModel stats = ctx.Stats;
            float fwdSign = TableGeometry.ForwardSign(ctx.Hitter);

            // --- Direction: the sideways speed of the stroke shifts the landing point towards the
            // side the racket moves to (player's left -> opponent's right: the same half of the
            // screen). Continuous, proportional and bounded by maxShiftX. Spin shots use the same
            // target: the solver compensates the curve, so the spin changes the path, not the landing.
            float shift = LateralShift(lat, s);
            float baseX = 0f;
            if (type == ShotType.Serve)
            {
                baseX = ctx.ContactPoint.x * 0.5f; // straight down the side you serve from
                shift *= s.lobShiftFactor;
            }
            else if (type == ShotType.Defensive)
            {
                shift *= s.lobShiftFactor;
            }
            float aimX = Mathf.Clamp(baseX + shift + ctx.ContactOffset * s.contactSteer, -t.maxAimX, t.maxAimX);

            // --- Depth: a forward stroke sends a normal shot deeper.
            float depth = t.targetDepth;
            if (type == ShotType.Normal)
                depth = s.normalBaseDepth + s.depthGain * Mathf.Clamp01(fwd / s.depthRefSpeed);

            // --- Spin.
            float spinMul = stats != null ? stats.SpinMultiplier : 1f;
            float sideSpin = 0f;
            if (type == ShotType.Spin)
            {
                // Sideways swing: the ball curves towards the side of the swing.
                float excess = Mathf.Abs(lat) - s.spinSwipeThreshold;
                sideSpin = Mathf.Sign(lat) * (s.spinLabelThreshold + Mathf.Max(0f, excess) * s.sideSpinPerSwipe) * spinMul;
            }
            bool consumedCurve = false;
            if (ctx.CurveArmed && type != ShotType.Serve)
            {
                // Curve power: start wide, bend back towards the target (banana shot).
                float dir = Mathf.Abs(aimX) > 0.05f ? -Mathf.Sign(aimX) : (_rng.Value() < 0.5f ? -1f : 1f);
                sideSpin = dir * s.curveSideAccel * spinMul;
                consumedCurve = true;
            }
            else if (type == ShotType.Special)
            {
                float dir = Mathf.Abs(aimX) > 0.05f ? -Mathf.Sign(aimX) : 1f;
                sideSpin = dir * 1.6f * spinMul;
            }
            float maxSpin = consumedCurve ? Mathf.Max(s.maxSideSpin, s.curveSideAccel * 1.5f) : s.maxSideSpin;
            sideSpin = Mathf.Clamp(sideSpin, -maxSpin, maxSpin);

            float verticalAccel = t.verticalAccel;
            if (verticalAccel < 0f) verticalAccel *= spinMul; // topspin scales with the spin stat

            // --- Target + noise.
            float r = phys.ballRadius;
            Vector3 intended = new Vector3(aimX, TableGeometry.TopY + r, fwdSign * depth);
            float noiseMul = (stats != null ? stats.AimNoiseMultiplier : 1f) * (ctx.Assist ? s.assistNoiseMultiplier : 1f) *
                             (ctx.NoiseMultiplier <= 0f ? 1f : ctx.NoiseMultiplier);
            Vector3 noisy = intended;
            noisy.x += _rng.Gaussian() * t.aimNoise * noiseMul;
            noisy.z += _rng.Gaussian() * t.aimNoise * 1.2f * noiseMul * fwdSign;

            // --- Speed -> flight time.
            float speed = t.horizontalSpeed * (stats != null ? stats.SpeedMultiplier : 1f);
            if (ctx.FireActive) speed *= s.fireSpeedMultiplier;
            if (type == ShotType.Smash || type == ShotType.Fast) speed *= Mathf.Lerp(0.95f, 1.08f, Mathf.InverseLerp(s.fastSwipeThreshold, s.smashSwipeThreshold * 1.5f, fwd));
            Vector3 flat = noisy - ctx.ContactPoint;
            flat.y = 0f;
            float flightTime = flat.magnitude / Mathf.Max(0.5f, speed);

            Vector3 accel = new Vector3(sideSpin, -phys.gravity + verticalAccel, 0f);
            ShotSolution sol = ShotSolver.SolveFlattened(ctx.ContactPoint, noisy, flightTime, accel, phys, t.maxNetClearance, t.minTimeFactor);

            // --- Vertical execution error (the only source of net / long misses).
            float vNoiseMul = (stats != null ? stats.VerticalNoiseMultiplier : 1f) * (ctx.Assist ? s.assistNoiseMultiplier : 1f) *
                              (ctx.NoiseMultiplier <= 0f ? 1f : ctx.NoiseMultiplier);
            Vector3 v = sol.Velocity;
            v.y += _rng.Gaussian() * t.verticalNoise * vNoiseMul;
            v = Vector3.ClampMagnitude(v, phys.maxBallSpeed);

            ShotResult res = new ShotResult();
            res.Type = type;
            res.Velocity = v;
            res.SpinAccel = new Vector3(sideSpin, verticalAccel, 0f);
            res.IntendedTarget = intended;
            res.NoisyTarget = noisy;
            res.FlightTime = sol.FlightTime;
            res.SideSpin = sideSpin;
            res.CameraShake = t.cameraShake;
            res.ConsumedSpecial = type == ShotType.Special;
            res.ConsumedSuperSmash = type == ShotType.SuperSmash;
            res.ConsumedCurve = consumedCurve;
            return res;
        }
    }
}
