using UnityEngine;

namespace Brooklyneir.Art
{
    /// <summary>Loads Resources/Config/animation.json once (defaults if missing or invalid). Never throws.</summary>
    public static class AnimationSettings
    {
        static AnimationConfig _config;

        public static AnimationConfig Config
        {
            get
            {
                if (_config == null) _config = Load();
                return _config;
            }
        }

        static AnimationConfig Load()
        {
            AnimationConfig c = new AnimationConfig();
            try
            {
                TextAsset json = Resources.Load<TextAsset>(AnimationConfig.ResourcePath);
                if (json != null) JsonUtility.FromJsonOverwrite(json.text, c);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TTLB] animation.json no válido, se usan los valores por defecto: " + e.Message);
            }
            c.Validate();
            return c;
        }
    }
}
