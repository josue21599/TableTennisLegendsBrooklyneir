using System.Globalization;
using System.Text;
using Brooklyneir.Config;

namespace Brooklyneir.Gameplay
{
    /// <summary>
    /// Debug text for one hit: the shot type, the stroke that produced it and, for the shots it
    /// did not become, the first condition that was missing ("remate: bola a 0,22 m, mínimo 0,38").
    /// Uses exactly the conditions of ShotModel.Classify. Pure, unit-tested; only shown in the
    /// editor and development builds.
    /// </summary>
    public static class ShotDiagnostics
    {
        public static string Name(ShotType t)
        {
            switch (t)
            {
                case ShotType.Fast: return "RÁPIDO";
                case ShotType.Defensive: return "GLOBO";
                case ShotType.Smash: return "REMATE";
                case ShotType.Spin: return "EFECTO";
                case ShotType.Special: return "ESPECIAL";
                case ShotType.SuperSmash: return "SÚPER REMATE";
                case ShotType.Serve: return "SAQUE";
                default: return "NORMAL";
            }
        }

        static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ','); }
        static string D(float v) { return v.ToString("0", CultureInfo.InvariantCulture); }

        /// <param name="forward">Stroke speed towards the net (m/s), as Classify computes it.</param>
        /// <param name="lateral">Sideways stroke speed (m/s, world x).</param>
        /// <param name="height">Ball height above the table at contact (m).</param>
        public static string Describe(int player, ShotType type, float forward, float lateral, float height, ShotTuning s)
        {
            float speed = (float)System.Math.Sqrt(forward * forward + lateral * lateral);
            float angle = ShotModel.SwingAngle(forward, lateral);
            StringBuilder b = new StringBuilder(160);
            b.Append('J').Append(player + 1).Append(" · ").Append(Name(type));
            b.Append(" · gesto ").Append(F(speed)).Append(" m/s a ").Append(D(angle)).Append('°');
            b.Append(" (delante ").Append(F(forward)).Append(", lado ").Append(F(lateral)).Append(')');
            b.Append(" · bola a ").Append(F(height)).Append(" m");
            if (type == ShotType.Serve || type == ShotType.Special || type == ShotType.SuperSmash) return b.ToString();

            if (type != ShotType.Smash) b.Append(" | remate: ").Append(SmashMissing(forward, angle, height, s));
            if (type != ShotType.Spin) b.Append(" | efecto: ").Append(SpinMissing(lateral, angle, s));
            if (type != ShotType.Fast && type != ShotType.Smash) b.Append(" | rápido: ").Append(FastMissing(forward, angle, s));
            return b.ToString();
        }

        /// <summary>First missing condition for a smash (or why a better type won).</summary>
        public static string SmashMissing(float forward, float angle, float height, ShotTuning s)
        {
            if (forward < s.smashSwipeThreshold) return "hacia delante " + F(forward) + " m/s, mínimo " + F(s.smashSwipeThreshold);
            if (angle > s.smashMaxAngle) return "ángulo " + D(angle) + "°, máximo " + D(s.smashMaxAngle) + "°";
            if (height < s.smashMinHeight) return "bola a " + F(height) + " m, mínimo " + F(s.smashMinHeight);
            return "cumple, pero ganó otro tipo";
        }

        public static string SpinMissing(float lateral, float angle, ShotTuning s)
        {
            if (angle <= s.spinMinAngle) return "ángulo " + D(angle) + "°, necesita más de " + D(s.spinMinAngle) + "° (más de lado)";
            if (angle >= s.defensiveMinAngle) return "ángulo " + D(angle) + "°, demasiado hacia atrás (desde " + D(s.defensiveMinAngle) + "° es globo)";
            float lat = lateral < 0f ? -lateral : lateral;
            if (lat < s.spinSwipeThreshold) return "de lado " + F(lat) + " m/s, mínimo " + F(s.spinSwipeThreshold);
            return "cumple, pero ganó otro tipo";
        }

        public static string FastMissing(float forward, float angle, ShotTuning s)
        {
            if (angle > s.spinMinAngle) return "ángulo " + D(angle) + "°, máximo " + D(s.spinMinAngle) + "°";
            if (forward < s.fastSwipeThreshold) return "hacia delante " + F(forward) + " m/s, mínimo " + F(s.fastSwipeThreshold);
            return "cumple, pero ganó otro tipo";
        }
    }
}
