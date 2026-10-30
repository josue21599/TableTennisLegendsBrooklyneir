using UnityEngine;

namespace Brooklyneir.Tables
{
    public enum CrowdReaction { None, Clap, Cheer, Wave }

    /// <summary>
    /// Pure curves for the crowd: where each head looks, how much each fan waits before reacting,
    /// applause, jumps and the stadium wave. Pure functions (no state, no allocation), unit-tested.
    /// </summary>
    public static class CrowdMotion
    {
        /// <summary>Yaw and pitch (degrees) for a head looking along a direction in the fan's local space (+Z = forward).</summary>
        public static void LookAngles(Vector3 localDir, float maxYaw, float maxPitch, out float yaw, out float pitch)
        {
            float flat = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
            if (flat < 1e-4f && Mathf.Abs(localDir.y) < 1e-4f)
            {
                yaw = 0f; pitch = 0f;
                return;
            }
            yaw = Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
            // Positive pitch = look down (Unity's X rotation).
            pitch = Mathf.Clamp(-Mathf.Atan2(localDir.y, Mathf.Max(flat, 1e-4f)) * Mathf.Rad2Deg, -maxPitch, maxPitch);
        }

        /// <summary>Delay before a fan reacts, from its hash in [0, 1).</summary>
        public static float ReactionDelay(float hash, float maxDelay)
        {
            return Mathf.Clamp01(hash) * maxDelay;
        }

        /// <summary>Head-turn speed of a fan (1/s), from its hash: some follow the ball faster than others.</summary>
        public static float HeadSpeed(float hash, float min, float max)
        {
            return Mathf.Lerp(min, max, Mathf.Clamp01(hash));
        }

        /// <summary>Angle (degrees, 0..360) of a fan around the table centre, used to order the wave.</summary>
        public static float AngleAround(Vector3 position)
        {
            float a = Mathf.Atan2(position.z, position.x) * Mathf.Rad2Deg;
            return a < 0f ? a + 360f : a;
        }

        /// <summary>Envelope 0..1 of a reaction lasting <paramref name="duration"/> seconds.</summary>
        public static float Envelope(float t, float duration)
        {
            if (t <= 0f || t >= duration) return 0f;
            float rise = Mathf.Clamp01(t / 0.15f);
            float fall = Mathf.Clamp01((duration - t) / 0.4f);
            return Mathf.Min(rise, fall);
        }

        /// <summary>
        /// Applause: arms forward (degrees) and how far they close together (0 = open, 1 = palms
        /// touching), oscillating at clapHz.
        /// </summary>
        public static void Clap(float t, float duration, float clapHz, out float forward, out float close)
        {
            float env = Envelope(t, duration);
            forward = 70f * env;
            close = env * (0.5f + 0.5f * Mathf.Cos(t * clapHz * 2f * Mathf.PI));
        }

        /// <summary>Jump height (m) and arm raise (0..1) of a cheering fan.</summary>
        public static void Cheer(float t, float duration, float phase, out float jump, out float arms)
        {
            float env = Envelope(t, duration);
            jump = Mathf.Abs(Mathf.Sin(t * 9f + phase)) * 0.18f * env;
            arms = env;
        }

        /// <summary>
        /// Stadium wave: 0..1 for a fan at <paramref name="angleDeg"/> around the table, <paramref name="t"/>
        /// seconds after it started. The crest goes round laps times in duration seconds.
        /// </summary>
        public static float Wave(float angleDeg, float t, float duration, float laps, float widthDeg)
        {
            if (t <= 0f || t >= duration) return 0f;
            float crest = t / duration * laps * 360f;
            // How far the crest has travelled past this fan, in degrees (only fans it has reached).
            float behind = crest - angleDeg;
            if (behind < -widthDeg) return 0f;
            float d = Mathf.Repeat(behind + widthDeg, 360f) - widthDeg;
            if (d < -widthDeg || d > widthDeg) return 0f;
            float x = d / widthDeg;
            return 0.5f + 0.5f * Mathf.Cos(x * Mathf.PI);
        }

        public static float Duration(CrowdReaction r, Brooklyneir.Art.AnimationConfig c)
        {
            switch (r)
            {
                case CrowdReaction.Clap: return c.clapSeconds;
                case CrowdReaction.Cheer: return c.cheerSeconds;
                case CrowdReaction.Wave: return c.waveSeconds;
                default: return 0f;
            }
        }
    }
}
