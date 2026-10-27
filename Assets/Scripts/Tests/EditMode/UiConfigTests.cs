using Brooklyneir.UI;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class UiConfigTests
    {
        [Test]
        public void SizeHintsMapToTheFourFixedSizes()
        {
            UiConfig c = new UiConfig();
            c.Validate();
            Assert.AreEqual(32, c.SizeForHint(22));
            Assert.AreEqual(32, c.SizeForHint(34));
            Assert.AreEqual(32, c.SizeForHint(46));
            Assert.AreEqual(64, c.SizeForHint(48));
            Assert.AreEqual(64, c.SizeForHint(72));
            Assert.AreEqual(96, c.SizeForHint(90));
            Assert.AreEqual(96, c.SizeForHint(120));
            Assert.AreEqual(128, c.SizeForHint(150));
            Assert.AreEqual(128, c.SizeForHint(170));
            for (int hint = 1; hint <= 400; hint++)
            {
                int size = c.SizeForHint(hint);
                Assert.IsTrue(size == c.sizeBody || size == c.sizeTitle || size == c.sizeDisplay || size == c.sizeHuge, "free size " + size + " for hint " + hint);
            }
        }

        [Test]
        public void ValidateRepairsBadConfiguration()
        {
            UiConfig c = new UiConfig();
            c.fontResourcePath = "   ";
            c.fallbackOsFonts = new string[0];
            c.sizeBody = 0;
            c.sizeTitle = 10;
            c.displayFromHint = 5;
            c.shadowAlpha = 3f;
            c.logoLineGap = -1f;
            c.logoPlaquePadding = 5f;
            c.Validate();
            Assert.AreEqual("Fonts/Jersey10 Raster", c.fontResourcePath);
            Assert.Greater(c.fallbackOsFonts.Length, 0);
            Assert.GreaterOrEqual(c.sizeBody, 12);
            Assert.GreaterOrEqual(c.sizeTitle, c.sizeBody);
            Assert.Greater(c.displayFromHint, c.titleFromHint);
            Assert.LessOrEqual(c.shadowAlpha, 1f);
            Assert.IsTrue(c.logoLineGap >= 0f && c.logoLineGap <= 1f);
            Assert.IsTrue(c.logoPlaquePadding >= 0f && c.logoPlaquePadding <= 1f);
        }

        [Test]
        public void LogoLinesStackWithTheConfiguredGapAndFitBothSizes()
        {
            UiConfig c = new UiConfig();
            // Same sizes (96/128/64) on the title (1500x520, centred) and in the menu corner (900x290).
            float[] ratios = { 0.55f, 0.625f, 0.7f };
            for (int i = 0; i < ratios.Length; i++)
            {
                CheckLogo(c, ratios[i], c.sizeDisplay, c.sizeHuge, c.sizeTitle, 520f);
                CheckLogo(c, ratios[i], c.sizeDisplay, c.sizeHuge, c.sizeTitle, 290f);
                LogoLayout.Result corner = LogoLayout.Compute(ratios[i], c.sizeDisplay, c.sizeHuge, c.sizeTitle, c.logoLineGap, c.logoPlaquePadding, 0f);
                Assert.AreEqual(corner.TopCap * 0.5f, corner.TopCenter, 1e-3f, "corner logo must start at the top edge");
            }
        }

        static void CheckLogo(UiConfig c, float ratio, int top, int legends, int plaque, float parentH)
        {
            LogoLayout.Result r = LogoLayout.Compute(ratio, top, legends, plaque, c.logoLineGap, c.logoPlaquePadding, parentH);
            float topBottom = r.TopCenter + r.TopCap * 0.5f;
            float legendsTop = r.LegendsCenter - r.LegendsCap * 0.5f;
            float legendsBottom = r.LegendsCenter + r.LegendsCap * 0.5f;
            float plaqueTop = r.PlaqueCenter - r.PlaqueHeight * 0.5f;
            float expectedGap = c.logoLineGap * legends * ratio;
            Assert.AreEqual(expectedGap, legendsTop - topBottom, 1e-3f, "gap TABLE TENNIS / LEGENDS");
            Assert.AreEqual(expectedGap, plaqueTop - legendsBottom, 1e-3f, "gap LEGENDS / plaque");
            Assert.LessOrEqual(r.TotalHeight, parentH, "logo taller than its box");
            float top0 = r.TopCenter - r.TopCap * 0.5f;
            float bottom = r.PlaqueCenter + r.PlaqueHeight * 0.5f;
            Assert.AreEqual(parentH - bottom, top0, 1e-3f, "block not centred");
        }

        [Test]
        public void CapRatioComesFromTheFontOrFallsBack()
        {
            Assert.AreEqual(0.625f, LogoLayout.CapRatio(20f, 32f), 1e-5f);
            Assert.AreEqual(LogoLayout.DefaultCapRatio, LogoLayout.CapRatio(0f, 32f), 1e-5f);
            Assert.AreEqual(LogoLayout.DefaultCapRatio, LogoLayout.CapRatio(20f, 0f), 1e-5f);
            Assert.AreEqual(LogoLayout.DefaultCapRatio, LogoLayout.CapRatio(200f, 32f), 1e-5f);
        }
    }
}
