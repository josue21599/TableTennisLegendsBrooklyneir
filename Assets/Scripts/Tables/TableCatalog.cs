using System.Collections.Generic;
using Brooklyneir.Rackets;

namespace Brooklyneir.Tables
{
    public enum EnvironmentKind { ClassicArena, BrooklynCourt, NeonFuture, Rooftop, Tropical, LegendArena }

    /// <summary>One table + scenario. Dimensions and collisions are identical for every entry.</summary>
    public class TableDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public EnvironmentKind Environment;
        public int Price;
        public UnlockRule Unlock;
        public string TopColor;
        public string LineColor;
        public string FrameColor;
        public string LegColor;
        public string NetColor;
        public string FloorColor;
        public string AccentColor;
        public string SkyTop;
        public string SkyBottom;
        public string AmbientColor;
        public string LightColor;
        public float LightIntensity;
        public bool Night;
    }

    public static class TableCatalog
    {
        static List<TableDefinition> _all;

        public static IList<TableDefinition> All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        public static TableDefinition Get(string id)
        {
            IList<TableDefinition> all = All;
            for (int i = 0; i < all.Count; i++) if (all[i].Id == id) return all[i];
            return all[0];
        }

        public static bool Exists(string id)
        {
            IList<TableDefinition> all = All;
            for (int i = 0; i < all.Count; i++) if (all[i].Id == id) return true;
            return false;
        }

        static List<TableDefinition> Build()
        {
            List<TableDefinition> l = new List<TableDefinition>();
            l.Add(new TableDefinition
            {
                Id = "classic", Name = "Arena Clásica", Description = "Pabellón de competición con gradas y focos.",
                Environment = EnvironmentKind.ClassicArena, Price = 0, Unlock = UnlockRule.Free,
                TopColor = "#1F4E8C", LineColor = "#F4F6F8", FrameColor = "#14325C", LegColor = "#2B2F36", NetColor = "#F4F6F8",
                FloorColor = "#B5462E", AccentColor = "#2E8BFF", SkyTop = "#1A2333", SkyBottom = "#2C3A52",
                AmbientColor = "#6A7A99", LightColor = "#FFF4E0", LightIntensity = 1.25f, Night = false
            });
            l.Add(new TableDefinition
            {
                Id = "brooklyn", Name = "Cancha de Brooklyn", Description = "Pista de barrio con muros pintados y vallas.",
                Environment = EnvironmentKind.BrooklynCourt, Price = 0, Unlock = UnlockRule.Free,
                TopColor = "#2E7D4F", LineColor = "#F7F3E8", FrameColor = "#1E5636", LegColor = "#3B3F45", NetColor = "#F7F3E8",
                FloorColor = "#4A5560", AccentColor = "#F2C230", SkyTop = "#7FB2E5", SkyBottom = "#F4C9A0",
                AmbientColor = "#9AA6B4", LightColor = "#FFE7C2", LightIntensity = 1.3f, Night = false
            });
            l.Add(new TableDefinition
            {
                Id = "rooftop", Name = "Azotea Nocturna", Description = "Sobre los tejados, con la ciudad encendida.",
                Environment = EnvironmentKind.Rooftop, Price = 600, Unlock = UnlockRule.Coins,
                TopColor = "#263A5E", LineColor = "#F0F4FF", FrameColor = "#161F33", LegColor = "#20242C", NetColor = "#E8ECF5",
                FloorColor = "#2B2E36", AccentColor = "#FF9F43", SkyTop = "#070B1A", SkyBottom = "#2A2350",
                AmbientColor = "#3B4466", LightColor = "#FFD9A8", LightIntensity = 1.0f, Night = true
            });
            l.Add(new TableDefinition
            {
                Id = "tropical", Name = "Laguna Tropical", Description = "Arena blanca, palmeras y brisa del mar.",
                Environment = EnvironmentKind.Tropical, Price = 900, Unlock = UnlockRule.Coins,
                TopColor = "#0F7C8C", LineColor = "#FFFFFF", FrameColor = "#0A5560", LegColor = "#E9E2D0", NetColor = "#FFFFFF",
                FloorColor = "#E8D7AE", AccentColor = "#FF7A59", SkyTop = "#3FA7E0", SkyBottom = "#BDEBFF",
                AmbientColor = "#A8C8D8", LightColor = "#FFF3D6", LightIntensity = 1.4f, Night = false
            });
            l.Add(new TableDefinition
            {
                Id = "neon", Name = "Neo Distrito", Description = "Arena futurista con luces de neón.",
                Environment = EnvironmentKind.NeonFuture, Price = 1400, Unlock = UnlockRule.Coins,
                TopColor = "#243A78", LineColor = "#22E6FF", FrameColor = "#0B0E1A", LegColor = "#1C2133", NetColor = "#FF2E88",
                FloorColor = "#0D1020", AccentColor = "#FF2E88", SkyTop = "#05060F", SkyBottom = "#1A0F33",
                AmbientColor = "#2A2F55", LightColor = "#D8E4FF", LightIntensity = 0.9f, Night = true
            });
            l.Add(new TableDefinition
            {
                Id = "legend", Name = "Arena de Leyendas", Description = "La final. Focos de torneo y público en pie.",
                Environment = EnvironmentKind.LegendArena, Price = 0, Unlock = UnlockRule.TournamentChampion,
                TopColor = "#7A1428", LineColor = "#F5C542", FrameColor = "#2A0810", LegColor = "#1A1A1E", NetColor = "#F5C542",
                FloorColor = "#141418", AccentColor = "#F5C542", SkyTop = "#09090C", SkyBottom = "#24161C",
                AmbientColor = "#4A3A40", LightColor = "#FFF0D0", LightIntensity = 1.35f, Night = true
            });
            return l;
        }
    }
}
