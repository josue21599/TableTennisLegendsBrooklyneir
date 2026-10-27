using System;

namespace Brooklyneir.UI
{
    public enum TextTier { Body, Title, Display, Huge }

    /// <summary>
    /// Interface typography settings, loaded from Resources/Config/ui.json (pure data + validation,
    /// unit-tested). The UI uses only four fixed sizes so the raster pixel font stays crisp; the
    /// numeric sizes written in the screens are hints that map to one of these tiers.
    /// </summary>
    [Serializable]
    public class UiConfig
    {
        public const string ResourcePath = "Config/ui";

        public string fontResourcePath = "Fonts/Jersey10 Raster";
        public string[] fallbackOsFonts = { "Roboto", "Noto Sans", "Arial" };
        public int sizeBody = 32;
        public int sizeTitle = 64;
        public int sizeDisplay = 96;
        public int sizeHuge = 128;
        /// <summary>Size hints from this value up use the Title tier (below: Body).</summary>
        public int titleFromHint = 47;
        public int displayFromHint = 81;
        public int hugeFromHint = 125;
        public float shadowOffsetPx = 2f;
        public float shadowAlpha = 0.6f;
        /// <summary>Logo: gap between lines as a fraction of the cap height of "LEGENDS".</summary>
        public float logoLineGap = 0.12f;
        /// <summary>Logo: padding of the blue plaque above/below its text, as a fraction of that text's cap height.</summary>
        public float logoPlaquePadding = 0.3f;

        /// <summary>Repairs bad values from an edited file. Never throws.</summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(fontResourcePath) || fontResourcePath.Trim().Length == 0) fontResourcePath = "Fonts/Jersey10 Raster";
            if (fallbackOsFonts == null || fallbackOsFonts.Length == 0) fallbackOsFonts = new[] { "Roboto", "Noto Sans", "Arial" };
            sizeBody = Clamp(sizeBody, 12, 200);
            sizeTitle = Clamp(sizeTitle, sizeBody, 300);
            sizeDisplay = Clamp(sizeDisplay, sizeTitle, 400);
            sizeHuge = Clamp(sizeHuge, sizeDisplay, 500);
            titleFromHint = Clamp(titleFromHint, 1, 1000);
            displayFromHint = Clamp(displayFromHint, titleFromHint + 1, 1000);
            hugeFromHint = Clamp(hugeFromHint, displayFromHint + 1, 1000);
            if (shadowOffsetPx < 0f || shadowOffsetPx > 16f) shadowOffsetPx = 2f;
            if (shadowAlpha < 0f || shadowAlpha > 1f) shadowAlpha = 0.6f;
            if (logoLineGap < 0f || logoLineGap > 1f) logoLineGap = 0.12f;
            if (logoPlaquePadding < 0f || logoPlaquePadding > 1f) logoPlaquePadding = 0.3f;
        }

        public TextTier TierForHint(int hint)
        {
            if (hint >= hugeFromHint) return TextTier.Huge;
            if (hint >= displayFromHint) return TextTier.Display;
            if (hint >= titleFromHint) return TextTier.Title;
            return TextTier.Body;
        }

        public int SizeOf(TextTier tier)
        {
            switch (tier)
            {
                case TextTier.Huge: return sizeHuge;
                case TextTier.Display: return sizeDisplay;
                case TextTier.Title: return sizeTitle;
                default: return sizeBody;
            }
        }

        public int SizeForHint(int hint) { return SizeOf(TierForHint(hint)); }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
