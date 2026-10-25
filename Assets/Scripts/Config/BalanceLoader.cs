using UnityEngine;

namespace Brooklyneir.Config
{
    /// <summary>Loads Resources/Config/balance.json over the compiled defaults.</summary>
    public static class BalanceLoader
    {
        public static BalanceConfig Load()
        {
            BalanceConfig cfg = new BalanceConfig();
            TextAsset json = Resources.Load<TextAsset>("Config/balance");
            if (json != null && !string.IsNullOrEmpty(json.text))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(json.text, cfg);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[TTLB] balance.json is invalid, using defaults: " + e.Message);
                    cfg = new BalanceConfig();
                }
            }
            cfg.Validate();
            return cfg;
        }
    }
}
