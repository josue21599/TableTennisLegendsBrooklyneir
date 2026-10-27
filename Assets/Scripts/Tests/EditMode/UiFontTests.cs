using System.Collections.Generic;
using Brooklyneir.UI;
using NUnit.Framework;
using TMPro;

namespace Brooklyneir.Tests
{
    /// <summary>
    /// Interface font checks. They need Unity (Resources, TextMeshPro) and the real
    /// Jersey10 Raster.asset, so they run only in the Unity Test Runner.
    /// </summary>
    [TestFixture]
    public class UiFontTests
    {
        [TearDown]
        public void TearDown()
        {
            UiFont.ResetForTests();
        }

        [Test]
        public void ProviderReturnsAlwaysTheSameInstance()
        {
            UiFont.ResetForTests();
            TMP_FontAsset a = UiFont.Font;
            TMP_FontAsset b = UiFont.Font;
            TMP_FontAsset c = UIKit.Font;
            Assert.IsNotNull(a);
            Assert.IsTrue(ReferenceEquals(a, b), "same instance on repeated calls");
            Assert.IsTrue(ReferenceEquals(a, c), "UIKit hands out the provider's instance");
            Assert.AreEqual(UiFontSource.Configured, UiFont.Source, "Jersey 10 should load from Resources/" + UiFont.Config.fontResourcePath);
        }

        [Test]
        public void FallbackIsUsedWhenTheResourceIsMissing()
        {
            TMP_FontAsset f = null;
            Assert.DoesNotThrow(() => f = UiFont.ReloadForTests("Fonts/__no_existe__"));
            Assert.IsTrue(UiFont.UsingFallback);
            Assert.AreNotEqual(UiFontSource.Configured, UiFont.Source);
            Assert.IsNotNull(f, "a fallback font (TMP default or operating system) must exist: " + UiFont.Source);
        }

        [Test]
        public void SpanishCharactersAreInTheAtlas()
        {
            UiFont.ResetForTests();
            TMP_FontAsset f = UiFont.Font;
            Assert.AreEqual(UiFontSource.Configured, UiFont.Source);
            List<char> missing;
            bool ok = f.HasCharacters("áéíóúÁÉÍÓÚñÑüÜ¿¡", out missing);
            Assert.IsTrue(ok, "missing in the atlas: " + Describe(missing));
        }

        [Test]
        public void EveryCharacterUsedByTheGameIsInTheAtlas()
        {
            // Printable ASCII plus the non-ASCII characters that appear in the game's texts.
            UiFont.ResetForTests();
            TMP_FontAsset f = UiFont.Font;
            Assert.AreEqual(UiFontSource.Configured, UiFont.Source);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (char ch = ' '; ch <= '~'; ch++) sb.Append(ch);
            sb.Append("¡·¿ÁÉÍÓÚáéíñóú—");
            List<char> missing;
            bool ok = f.HasCharacters(sb.ToString(), out missing);
            Assert.IsTrue(ok, "missing in the atlas: " + Describe(missing));
        }

        static string Describe(List<char> missing)
        {
            if (missing == null || missing.Count == 0) return "(none)";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in missing) sb.Append("'" + c + "' U+" + ((int)c).ToString("X4") + "  ");
            return sb.ToString();
        }
    }
}
