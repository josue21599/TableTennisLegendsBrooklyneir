using Brooklyneir.Core;
using Brooklyneir.Settings;
using UnityEngine;

namespace Brooklyneir.Tables
{
    /// <summary>Owns the current scenario and table; rebuilds them when another table is chosen.</summary>
    public class ArenaManager : MonoBehaviour
    {
        public TableDefinition Current { get; private set; }
        public EnvironmentBuilder.Result Environment { get; private set; }
        public GameObject Table { get; private set; }
        QualityProfile _quality;
        bool _hasQuality;

        public void Show(TableDefinition def)
        {
            if (def == null) return;
            if (Current != null && Current.Id == def.Id && Environment != null) return;
            Clear();
            Current = def;
            Environment = EnvironmentBuilder.Build(def, transform);
            Table = TableModelBuilder.Build(def, transform);
            if (Environment.MainLight != null) Environment.MainLight.cullingMask = ~(1 << Layers.Preview);
            if (_hasQuality) ApplyQuality(_quality);
        }

        public void Clear()
        {
            if (Environment != null && Environment.Root != null) Destroy(Environment.Root);
            if (Table != null) Destroy(Table);
            Environment = null;
            Table = null;
            Current = null;
        }

        public void ApplyQuality(QualityProfile p)
        {
            _quality = p;
            _hasQuality = true;
            if (Environment == null) return;
            if (Environment.MainLight != null) Environment.MainLight.shadows = p.Shadows;
            for (int i = 0; i < Environment.ExtraLights.Count; i++)
                if (Environment.ExtraLights[i] != null) Environment.ExtraLights[i].enabled = p.ExtraLights;
            if (Environment.Crowd != null) Environment.Crowd.SetCrowdVisible(p.Crowd);
        }

        public void Cheer(float intensity)
        {
            if (Environment != null && Environment.Crowd != null) Environment.Crowd.Cheer(intensity);
        }
    }
}
