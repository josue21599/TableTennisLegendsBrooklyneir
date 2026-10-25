using System;
using Brooklyneir.Config;

namespace Brooklyneir.Rackets
{
    /// <summary>Racket attributes on a 1..10 scale, as shown in the selection screen.</summary>
    [Serializable]
    public struct RacketStats
    {
        public int power;
        public int control;
        public int spin;
        public int response;
        public int precision;

        public RacketStats(int power, int control, int spin, int response, int precision)
        {
            this.power = Clamp(power);
            this.control = Clamp(control);
            this.spin = Clamp(spin);
            this.response = Clamp(response);
            this.precision = Clamp(precision);
        }

        public static int Clamp(int v) { return v < 1 ? 1 : (v > 10 ? 10 : v); }

        public int Total { get { return power + control + spin + response + precision; } }
    }

    /// <summary>
    /// Turns the 1..10 stats into the multipliers the gameplay actually uses. This is the single
    /// place where stats affect play, which keeps them honest and testable.
    /// </summary>
    public class RacketStatModel
    {
        public readonly RacketStats Stats;
        public readonly float SpeedMultiplier;
        public readonly float Reach;
        public readonly float SpinMultiplier;
        public readonly float FollowSpeed;
        public readonly float AimNoiseMultiplier;
        public readonly float VerticalNoiseMultiplier;

        public RacketStatModel(RacketStats stats, RacketTuning t)
        {
            Stats = stats;
            float p = RacketStats.Clamp(stats.power) / 10f;
            float c = RacketStats.Clamp(stats.control) / 10f;
            float s = RacketStats.Clamp(stats.spin) / 10f;
            float r = RacketStats.Clamp(stats.response) / 10f;
            float a = RacketStats.Clamp(stats.precision) / 10f;
            SpeedMultiplier = 1f + (p - 0.5f) * t.statPowerSpeedRange;
            Reach = t.baseReach + t.reachPerControl * c;
            SpinMultiplier = 1f + (s - 0.5f) * t.statSpinRange;
            FollowSpeed = t.baseFollowSpeed + t.followSpeedPerResponse * r;
            AimNoiseMultiplier = 1f + (0.5f - a) * t.statPrecisionNoiseRange;
            VerticalNoiseMultiplier = 1f + (0.5f - c) * 0.6f;
        }
    }
}
