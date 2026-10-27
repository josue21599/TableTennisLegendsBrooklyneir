using TMPro;
using UnityEngine;

namespace Brooklyneir.UI
{
    public enum UiFontSource { None, Configured, TmpDefault, OperatingSystem }

    /// <summary>
    /// Single source of the interface font. The configured TMP font asset (Jersey 10, SIL OFL,
    /// licence in Assets/Resources/Fonts/OFL.txt) is loaded once with Resources.Load and the very
    /// same instance is handed to every text. If it cannot be loaded the game keeps running with a
    /// fallback: TextMeshPro's default font asset, then a font created at runtime from the
    /// operating system. Nothing here throws.
    /// </summary>
    public static class UiFont
    {
        static TMP_FontAsset _font;
        static UiConfig _config;
        static bool _resolved;

        public static UiFontSource Source { get; private set; }
        public static bool UsingFallback { get { return Source != UiFontSource.Configured; } }

        /// <summary>Interface settings (ui.json), loaded once.</summary>
        public static UiConfig Config
        {
            get
            {
                if (_config == null) _config = LoadConfig();
                return _config;
            }
        }

        /// <summary>The interface font; always the same instance once resolved.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (!_resolved) Resolve(Config.fontResourcePath);
                return _font;
            }
        }

        public static int Size(TextTier tier) { return Config.SizeOf(tier); }
        public static int SizeForHint(int hint) { return Config.SizeForHint(hint); }

        /// <summary>Tests: forget the cached font and resolve again from another resource path.</summary>
        public static TMP_FontAsset ReloadForTests(string resourcePath)
        {
            _resolved = false;
            _font = null;
            Source = UiFontSource.None;
            Resolve(resourcePath);
            return _font;
        }

        /// <summary>Tests: back to the configured font.</summary>
        public static void ResetForTests()
        {
            _resolved = false;
            _font = null;
            _config = null;
            Source = UiFontSource.None;
        }

        static void Resolve(string path)
        {
            _resolved = true;
            TMP_FontAsset f = null;
            try { f = Resources.Load<TMP_FontAsset>(path); }
            catch (System.Exception e) { Debug.LogWarning("[TTLB] Error cargando la fuente '" + path + "': " + e.Message); }
            if (f != null)
            {
                _font = f;
                Source = UiFontSource.Configured;
                return;
            }
            Debug.LogWarning("[TTLB] No se encontró la fuente de interfaz en Resources/" + path + ". Se usa una fuente de respaldo.");

            try { f = TMP_Settings.defaultFontAsset; }
            catch (System.Exception) { f = null; }
            if (f != null)
            {
                _font = f;
                Source = UiFontSource.TmpDefault;
                return;
            }

            string[] names = Config.fallbackOsFonts;
            try
            {
                Font os = UnityEngine.Font.CreateDynamicFontFromOSFont(names, 32);
                if (os != null) f = TMP_FontAsset.CreateFontAsset(os);
            }
            catch (System.Exception e) { Debug.LogWarning("[TTLB] Fuente del sistema no disponible: " + e.Message); f = null; }
            if (f != null)
            {
                _font = f;
                Source = UiFontSource.OperatingSystem;
                return;
            }
            Source = UiFontSource.None;
            Debug.LogError("[TTLB] No hay ninguna fuente disponible para la interfaz. El juego sigue, pero los textos no se verán.");
        }

        static UiConfig LoadConfig()
        {
            UiConfig c = new UiConfig();
            try
            {
                TextAsset json = Resources.Load<TextAsset>(UiConfig.ResourcePath);
                if (json != null) JsonUtility.FromJsonOverwrite(json.text, c);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TTLB] ui.json no válido, se usan los valores por defecto: " + e.Message);
            }
            c.Validate();
            return c;
        }
    }
}
