using UnityEngine;

namespace Brooklyneir.Art
{
    /// <summary>Visual offset added to the racket pose: angles in degrees, lift in metres.</summary>
    public struct RacketPoseOffset
    {
        public float Pitch, Roll, Yaw, Lift;

        public RacketPoseOffset(float pitch, float roll, float yaw, float lift)
        {
            Pitch = pitch; Roll = roll; Yaw = yaw; Lift = lift;
        }

        public static RacketPoseOffset operator +(RacketPoseOffset a, RacketPoseOffset b)
        {
            return new RacketPoseOffset(a.Pitch + b.Pitch, a.Roll + b.Roll, a.Yaw + b.Yaw, a.Lift + b.Lift);
        }

        public RacketPoseOffset Scaled(float k)
        {
            return new RacketPoseOffset(Pitch * k, Roll * k, Yaw * k, Lift * k);
        }
    }

    /// <summary>
    /// Pure curves for the hand/racket animation: the stroke of each shot type, the idle sway and
    /// the celebration after a point. Pure functions of time (no state, no allocation), so they are
    /// deterministic and unit-tested. All curves start and end at zero, so the racket always
    /// returns to its gameplay pose.
    /// </summary>
    public static class RacketAnimation
    {
        /// <summary>0..1..0 stroke shape: fast ease-out to the peak at peakAt, smooth recovery after.</summary>
        public static float StrokeShape(float k, float peakAt)
        {
            if (k <= 0f || k >= 1f) return 0f;
            if (k < peakAt)
            {
                float u = k / peakAt;
                return 1f - (1f - u) * (1f - u);
            }
            float v = (k - peakAt) / (1f - peakAt);
            return 1f - v * v * (3f - 2f * v);
        }

        /// <summary>Stroke offset <paramref name="time"/> seconds after contact (zero once it is over).</summary>
        public static RacketPoseOffset Stroke(StrokeAnim s, float time, float strength, AnimationConfig c)
        {
            if (s == null || time < 0f || time >= s.duration) return default(RacketPoseOffset);
            float shape = StrokeShape(time / s.duration, c.strokePeakAt);
            float scale = Mathf.Lerp(c.strengthScaleMin, c.strengthScaleMax, Mathf.Clamp01(strength));
            return new RacketPoseOffset(s.pitch, s.roll, s.yaw, s.lift).Scaled(shape * scale);
        }

        /// <summary>Gentle breathing sway while waiting; <paramref name="weight"/> 0..1 fades it in.</summary>
        public static RacketPoseOffset Idle(float clock, float phase, float weight, AnimationConfig c)
        {
            if (weight <= 0f) return default(RacketPoseOffset);
            float w = Mathf.Clamp01(weight);
            float a = (clock * c.idleFrequency + phase) * 2f * Mathf.PI;
            return new RacketPoseOffset(
                Mathf.Sin(a) * c.idlePitch * w,
                Mathf.Sin(a * 0.5f + 1.3f) * c.idleRoll * w,
                0f,
                (Mathf.Sin(a) * 0.5f + 0.5f) * c.idleLift * w);
        }

        public static float CelebrationDuration(bool won, AnimationConfig c)
        {
            return won ? c.celebrateSeconds : c.sadSeconds;
        }

        /// <summary>Envelope 0..1: quick rise, hold, smooth fall in the last 30 %.</summary>
        public static float Envelope(float k)
        {
            if (k <= 0f || k >= 1f) return 0f;
            float rise = Mathf.Clamp01(k / 0.15f);
            float fall = Mathf.Clamp01((1f - k) / 0.3f);
            rise = rise * rise * (3f - 2f * rise);
            fall = fall * fall * (3f - 2f * fall);
            return Mathf.Min(rise, fall);
        }

        /// <summary>
        /// After a point: the winner raises the racket and pumps it; the loser lets it drop a little.
        /// </summary>
        public static RacketPoseOffset Celebration(bool won, float time, AnimationConfig c)
        {
            float duration = CelebrationDuration(won, c);
            if (time < 0f || time >= duration) return default(RacketPoseOffset);
            float k = time / duration;
            float env = Envelope(k);
            if (won)
            {
                float pump = Mathf.Sin(time * c.celebratePumpHz * 2f * Mathf.PI);
                return new RacketPoseOffset(pump * c.celebratePitch * env, c.celebrateRoll * env, 0f, c.celebrateLift * env);
            }
            float sway = Mathf.Sin(time * 2.2f) * 4f;
            return new RacketPoseOffset(c.sadPitch * env, 0f, sway * env, c.sadLift * env);
        }
    }
}
