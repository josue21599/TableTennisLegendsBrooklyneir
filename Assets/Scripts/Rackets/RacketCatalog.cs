using System.Collections.Generic;

namespace Brooklyneir.Rackets
{
    public enum Rarity { Common, Rare, Epic, Legendary }
    public enum UnlockRule { Free, Coins, TournamentChampion }
    public enum BladeShape { Classic, Wide, Teardrop, Angular }
    public enum HandleShape { Flared, Straight, Anatomic, Futuristic }

    /// <summary>Visual + gameplay description of one racket. Colors are "#RRGGBB" strings.</summary>
    public class RacketDefinition
    {
        public string Id;
        public string Name;
        public string Style;
        public Rarity Rarity;
        public RacketStats Stats;
        public int Price;
        public UnlockRule Unlock;
        public BladeShape Blade;
        public HandleShape Handle;
        public string ForehandRubber;
        public string BackhandRubber;
        public string BladeWood;
        public string EdgeTape;
        public string HandlePrimary;
        public string HandleAccent;
        public string GlowColor;   // empty = no emissive accents
        public float Metallic;
        public string Description;
    }

    /// <summary>All rackets. Original designs; no real brands are referenced.</summary>
    public static class RacketCatalog
    {
        static List<RacketDefinition> _all;

        public static IList<RacketDefinition> All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        public static RacketDefinition Get(string id)
        {
            IList<RacketDefinition> all = All;
            for (int i = 0; i < all.Count; i++) if (all[i].Id == id) return all[i];
            return all[0];
        }

        public static bool Exists(string id)
        {
            IList<RacketDefinition> all = All;
            for (int i = 0; i < all.Count; i++) if (all[i].Id == id) return true;
            return false;
        }

        static List<RacketDefinition> Build()
        {
            List<RacketDefinition> l = new List<RacketDefinition>();
            l.Add(new RacketDefinition
            {
                Id = "classic_oak", Name = "Roble Clásico", Style = "Clásica", Rarity = Rarity.Common,
                Stats = new RacketStats(5, 7, 5, 5, 6), Price = 0, Unlock = UnlockRule.Free,
                Blade = BladeShape.Classic, Handle = HandleShape.Flared,
                ForehandRubber = "#C8202B", BackhandRubber = "#16161A", BladeWood = "#C99A62", EdgeTape = "#2A2A2E",
                HandlePrimary = "#B9864F", HandleAccent = "#7A4E28", GlowColor = "", Metallic = 0f,
                Description = "Equilibrada y fiable. Perfecta para aprender."
            });
            l.Add(new RacketDefinition
            {
                Id = "brooklyn_street", Name = "Brooklyn Street", Style = "Urbana", Rarity = Rarity.Common,
                Stats = new RacketStats(6, 5, 6, 6, 5), Price = 0, Unlock = UnlockRule.Free,
                Blade = BladeShape.Classic, Handle = HandleShape.Straight,
                ForehandRubber = "#E8562A", BackhandRubber = "#1B2A4A", BladeWood = "#D7B27A", EdgeTape = "#F2C230",
                HandlePrimary = "#1B2A4A", HandleAccent = "#F2C230", GlowColor = "", Metallic = 0.1f,
                Description = "Nacida en las pistas del barrio. Algo más de pegada."
            });
            l.Add(new RacketDefinition
            {
                Id = "neon_pulse", Name = "Neon Pulse", Style = "Neón", Rarity = Rarity.Rare,
                Stats = new RacketStats(6, 6, 8, 7, 5), Price = 500, Unlock = UnlockRule.Coins,
                Blade = BladeShape.Teardrop, Handle = HandleShape.Anatomic,
                ForehandRubber = "#FF2E88", BackhandRubber = "#141026", BladeWood = "#2B2440", EdgeTape = "#22E6FF",
                HandlePrimary = "#141026", HandleAccent = "#22E6FF", GlowColor = "#22E6FF", Metallic = 0.3f,
                Description = "Gomas pegajosas para efectos imposibles."
            });
            l.Add(new RacketDefinition
            {
                Id = "tropic_wave", Name = "Tropic Wave", Style = "Tropical", Rarity = Rarity.Rare,
                Stats = new RacketStats(5, 8, 6, 7, 7), Price = 700, Unlock = UnlockRule.Coins,
                Blade = BladeShape.Wide, Handle = HandleShape.Flared,
                ForehandRubber = "#16B58A", BackhandRubber = "#FF8A3D", BladeWood = "#E9C98F", EdgeTape = "#FFFFFF",
                HandlePrimary = "#0E7D78", HandleAccent = "#FFD166", GlowColor = "", Metallic = 0f,
                Description = "Pala ancha y suave: control total."
            });
            l.Add(new RacketDefinition
            {
                Id = "rooftop_viper", Name = "Rooftop Viper", Style = "Urbana nocturna", Rarity = Rarity.Epic,
                Stats = new RacketStats(8, 5, 7, 8, 5), Price = 900, Unlock = UnlockRule.Coins,
                Blade = BladeShape.Angular, Handle = HandleShape.Straight,
                ForehandRubber = "#7CFF3A", BackhandRubber = "#0D0D10", BladeWood = "#3A3A40", EdgeTape = "#7CFF3A",
                HandlePrimary = "#18181C", HandleAccent = "#7CFF3A", GlowColor = "#7CFF3A", Metallic = 0.45f,
                Description = "Rápida y agresiva. Muerde en cada golpe."
            });
            l.Add(new RacketDefinition
            {
                Id = "iron_wall", Name = "Iron Wall", Style = "Defensiva", Rarity = Rarity.Epic,
                Stats = new RacketStats(4, 10, 5, 6, 9), Price = 1200, Unlock = UnlockRule.Coins,
                Blade = BladeShape.Wide, Handle = HandleShape.Anatomic,
                ForehandRubber = "#2F5BFF", BackhandRubber = "#202024", BladeWood = "#8E9AA8", EdgeTape = "#C9D2DC",
                HandlePrimary = "#5D6875", HandleAccent = "#C9D2DC", GlowColor = "", Metallic = 0.8f,
                Description = "Un muro con mango. Todo vuelve."
            });
            l.Add(new RacketDefinition
            {
                Id = "cyber_blade", Name = "Cyber Blade", Style = "Futurista", Rarity = Rarity.Epic,
                Stats = new RacketStats(8, 6, 7, 9, 6), Price = 1500, Unlock = UnlockRule.Coins,
                Blade = BladeShape.Angular, Handle = HandleShape.Futuristic,
                ForehandRubber = "#9B5CFF", BackhandRubber = "#0B0F1A", BladeWood = "#D9E2EC", EdgeTape = "#9B5CFF",
                HandlePrimary = "#E6ECF2", HandleAccent = "#9B5CFF", GlowColor = "#B78CFF", Metallic = 0.9f,
                Description = "Fibra de carbono y respuesta instantánea."
            });
            l.Add(new RacketDefinition
            {
                Id = "legend_crown", Name = "Corona Legendaria", Style = "Legendaria", Rarity = Rarity.Legendary,
                Stats = new RacketStats(9, 8, 9, 8, 8), Price = 3000, Unlock = UnlockRule.Coins,
                Blade = BladeShape.Teardrop, Handle = HandleShape.Futuristic,
                ForehandRubber = "#D4142B", BackhandRubber = "#0A0A0A", BladeWood = "#2A1A0E", EdgeTape = "#F5C542",
                HandlePrimary = "#1A1208", HandleAccent = "#F5C542", GlowColor = "#FFC94A", Metallic = 0.85f,
                Description = "Solo para leyendas de Brooklyneir."
            });
            return l;
        }

        public static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "RARA";
                case Rarity.Epic: return "ÉPICA";
                case Rarity.Legendary: return "LEGENDARIA";
                default: return "COMÚN";
            }
        }
    }
}
