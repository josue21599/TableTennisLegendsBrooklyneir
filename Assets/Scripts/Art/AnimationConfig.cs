using System;

namespace Brooklyneir.Art
{
    /// <summary>
    /// One racket stroke after contact: how long it lasts and how far it turns (degrees) and moves
    /// (metres, up = positive) at its peak. Purely visual.
    /// </summary>
    [Serializable]
    public class StrokeAnim
    {
        public float duration;
        public float pitch;
        public float roll;
        public float yaw;
        public float lift;

        public StrokeAnim() { }

        public StrokeAnim(float duration, float pitch, float roll, float yaw, float lift)
        {
            this.duration = duration;
            this.pitch = pitch;
            this.roll = roll;
            this.yaw = yaw;
            this.lift = lift;
        }
    }

    /// <summary>
    /// Visual animation settings for the hands/rackets and the crowd, loaded from
    /// Resources/Config/animation.json (pure data + validation, unit-tested). Nothing here changes
    /// gameplay: hits are still decided by the racket's logical position, not by its drawing.
    /// </summary>
    [Serializable]
    public class AnimationConfig
    {
        public const string ResourcePath = "Config/animation";

        // ---- strokes by shot type
        public StrokeAnim normal = new StrokeAnim(0.30f, 40f, 0f, 10f, 0f);
        public StrokeAnim fast = new StrokeAnim(0.22f, 55f, 0f, 18f, 0f);
        public StrokeAnim smash = new StrokeAnim(0.32f, 75f, 0f, 0f, -0.07f);
        public StrokeAnim superSmash = new StrokeAnim(0.36f, 90f, 0f, 0f, -0.09f);
        public StrokeAnim special = new StrokeAnim(0.40f, 85f, 25f, 15f, -0.05f);
        public StrokeAnim spin = new StrokeAnim(0.34f, 30f, 45f, 0f, 0.08f);
        public StrokeAnim defensive = new StrokeAnim(0.42f, -35f, 0f, 0f, 0.04f);
        public StrokeAnim serve = new StrokeAnim(0.30f, 25f, 0f, 0f, 0.02f);
        /// <summary>Amplitude multiplier for the weakest and the strongest hit.</summary>
        public float strengthScaleMin = 0.75f;
        public float strengthScaleMax = 1.25f;
        /// <summary>Fraction of the stroke spent going out to the peak (the rest is the recovery).</summary>
        public float strokePeakAt = 0.3f;

        // ---- idle (waiting, ball not coming)
        public float idlePitch = 3f;
        public float idleRoll = 2.5f;
        public float idleLift = 0.012f;
        public float idleFrequency = 0.9f;

        // ---- celebration after the point
        public float celebrateSeconds = 1.1f;
        public float celebrateLift = 0.22f;
        public float celebratePitch = 25f;
        public float celebrateRoll = 12f;
        public float celebratePumpHz = 2.5f;
        public float sadSeconds = 1.0f;
        public float sadPitch = -28f;
        public float sadLift = -0.05f;

        // ---- crowd
        public float crowdHeadMaxYaw = 55f;
        public float crowdHeadMaxPitch = 20f;
        public float crowdHeadSpeedMin = 3f;
        public float crowdHeadSpeedMax = 7f;
        public float crowdReactionMaxDelay = 0.35f;
        public float clapSeconds = 1.8f;
        public float clapHz = 4.5f;
        public float cheerSeconds = 2.2f;
        public float waveSeconds = 4f;
        public float waveLaps = 1.5f;
        public float waveWidthDeg = 50f;

        /// <summary>Replaces missing or out-of-range values with the defaults. Never throws.</summary>
        public void Validate()
        {
            AnimationConfig d = new AnimationConfig();
            normal = Stroke(normal, d.normal);
            fast = Stroke(fast, d.fast);
            smash = Stroke(smash, d.smash);
            superSmash = Stroke(superSmash, d.superSmash);
            special = Stroke(special, d.special);
            spin = Stroke(spin, d.spin);
            defensive = Stroke(defensive, d.defensive);
            serve = Stroke(serve, d.serve);
            if (!(strengthScaleMin > 0f && strengthScaleMin <= 3f)) strengthScaleMin = d.strengthScaleMin;
            if (!(strengthScaleMax >= strengthScaleMin && strengthScaleMax <= 3f)) strengthScaleMax = Math.Max(strengthScaleMin, d.strengthScaleMax);
            if (!(strokePeakAt >= 0.05f && strokePeakAt <= 0.9f)) strokePeakAt = d.strokePeakAt;

            idlePitch = Range(idlePitch, 0f, 15f, d.idlePitch);
            idleRoll = Range(idleRoll, 0f, 15f, d.idleRoll);
            idleLift = Range(idleLift, 0f, 0.05f, d.idleLift);
            idleFrequency = Range(idleFrequency, 0.05f, 4f, d.idleFrequency);

            celebrateSeconds = Range(celebrateSeconds, 0.2f, 3f, d.celebrateSeconds);
            celebrateLift = Range(celebrateLift, 0f, 0.5f, d.celebrateLift);
            celebratePitch = Range(celebratePitch, 0f, 60f, d.celebratePitch);
            celebrateRoll = Range(celebrateRoll, 0f, 45f, d.celebrateRoll);
            celebratePumpHz = Range(celebratePumpHz, 0.2f, 8f, d.celebratePumpHz);
            sadSeconds = Range(sadSeconds, 0.2f, 3f, d.sadSeconds);
            sadPitch = Range(sadPitch, -60f, 0f, d.sadPitch);
            sadLift = Range(sadLift, -0.2f, 0f, d.sadLift);

            crowdHeadMaxYaw = Range(crowdHeadMaxYaw, 0f, 90f, d.crowdHeadMaxYaw);
            crowdHeadMaxPitch = Range(crowdHeadMaxPitch, 0f, 45f, d.crowdHeadMaxPitch);
            crowdHeadSpeedMin = Range(crowdHeadSpeedMin, 0.5f, 30f, d.crowdHeadSpeedMin);
            crowdHeadSpeedMax = Range(crowdHeadSpeedMax, crowdHeadSpeedMin, 30f, Math.Max(crowdHeadSpeedMin, d.crowdHeadSpeedMax));
            crowdReactionMaxDelay = Range(crowdReactionMaxDelay, 0f, 1.5f, d.crowdReactionMaxDelay);
            clapSeconds = Range(clapSeconds, 0.3f, 6f, d.clapSeconds);
            clapHz = Range(clapHz, 0.5f, 10f, d.clapHz);
            cheerSeconds = Range(cheerSeconds, 0.3f, 6f, d.cheerSeconds);
            waveSeconds = Range(waveSeconds, 0.5f, 10f, d.waveSeconds);
            waveLaps = Range(waveLaps, 0.25f, 5f, d.waveLaps);
            waveWidthDeg = Range(waveWidthDeg, 5f, 180f, d.waveWidthDeg);
        }

        /// <summary>Stroke used for a shot type.</summary>
        public StrokeAnim StrokeFor(Brooklyneir.Gameplay.ShotType type)
        {
            switch (type)
            {
                case Brooklyneir.Gameplay.ShotType.Fast: return fast;
                case Brooklyneir.Gameplay.ShotType.Smash: return smash;
                case Brooklyneir.Gameplay.ShotType.SuperSmash: return superSmash;
                case Brooklyneir.Gameplay.ShotType.Special: return special;
                case Brooklyneir.Gameplay.ShotType.Spin: return spin;
                case Brooklyneir.Gameplay.ShotType.Defensive: return defensive;
                case Brooklyneir.Gameplay.ShotType.Serve: return serve;
                default: return normal;
            }
        }


        static StrokeAnim Stroke(StrokeAnim s, StrokeAnim def)
        {
            if (s == null) return new StrokeAnim(def.duration, def.pitch, def.roll, def.yaw, def.lift);
            if (!(s.duration >= 0.05f && s.duration <= 2f)) s.duration = def.duration;
            if (!(Math.Abs(s.pitch) <= 150f)) s.pitch = def.pitch;
            if (!(Math.Abs(s.roll) <= 150f)) s.roll = def.roll;
            if (!(Math.Abs(s.yaw) <= 150f)) s.yaw = def.yaw;
            if (!(Math.Abs(s.lift) <= 0.5f)) s.lift = def.lift;
            return s;
        }

        static float Range(float v, float min, float max, float def)
        {
            return v >= min && v <= max ? v : def; // also rejects NaN
        }
    }
}
