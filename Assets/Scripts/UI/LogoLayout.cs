namespace Brooklyneir.UI
{
    /// <summary>
    /// Vertical layout of the three-line logo ("TABLE TENNIS" / "LEGENDS" / "BROOKLYNEIR" plaque)
    /// from the font's real metrics instead of fixed fractions: each line takes its cap height
    /// (all-caps text), separated by a gap proportional to the "LEGENDS" cap height, and the
    /// block is centred in its parent. Pure math, unit-tested.
    /// </summary>
    public static class LogoLayout
    {
        public struct Result
        {
            /// <summary>Centres measured from the parent's top edge, in canvas units (down = positive).</summary>
            public float TopCenter, LegendsCenter, PlaqueCenter;
            public float TopCap, LegendsCap, PlaqueCap;
            public float PlaqueHeight, Gap, TotalHeight;
        }

        public const float DefaultCapRatio = 0.7f;

        /// <summary>Cap height / point size from the font (falls back to 0.7 if the metrics are unusable).</summary>
        public static float CapRatio(float capLine, float pointSize)
        {
            if (pointSize <= 0f || capLine <= 0f) return DefaultCapRatio;
            float r = capLine / pointSize;
            return r > 0.2f && r < 1.2f ? r : DefaultCapRatio;
        }

        public static Result Compute(float capRatio, int topSize, int legendsSize, int plaqueSize,
            float gapFraction, float plaquePaddingFraction, float parentHeight)
        {
            Result r = new Result();
            r.TopCap = topSize * capRatio;
            r.LegendsCap = legendsSize * capRatio;
            r.PlaqueCap = plaqueSize * capRatio;
            r.Gap = gapFraction * r.LegendsCap;
            r.PlaqueHeight = r.PlaqueCap * (1f + 2f * plaquePaddingFraction);
            r.TotalHeight = r.TopCap + r.Gap + r.LegendsCap + r.Gap + r.PlaqueHeight;
            float y0 = (parentHeight - r.TotalHeight) * 0.5f;
            if (y0 < 0f) y0 = 0f;
            r.TopCenter = y0 + r.TopCap * 0.5f;
            r.LegendsCenter = y0 + r.TopCap + r.Gap + r.LegendsCap * 0.5f;
            r.PlaqueCenter = y0 + r.TopCap + r.Gap + r.LegendsCap + r.Gap + r.PlaqueHeight * 0.5f;
            return r;
        }
    }
}
