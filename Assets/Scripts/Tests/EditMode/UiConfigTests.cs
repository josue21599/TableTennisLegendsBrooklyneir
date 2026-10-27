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
            c.Validate();
            Assert.AreEqual("Fonts/Jersey10 Raster", c.fontResourcePath);
            Assert.Greater(c.fallbackOsFonts.Length, 0);
            Assert.GreaterOrEqual(c.sizeBody, 12);
            Assert.GreaterOrEqual(c.sizeTitle, c.sizeBody);
            Assert.Greater(c.displayFromHint, c.titleFromHint);
            Assert.LessOrEqual(c.shadowAlpha, 1f);
        }
    }
}
