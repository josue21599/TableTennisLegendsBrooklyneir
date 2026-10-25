using System;
using System.Collections.Generic;
using Brooklyneir.SaveSystem;

namespace Brooklyneir.Progression
{
    public class AchievementDefinition
    {
        public string Id;
        public string Title;
        public string Description;
        public int Reward;
        public Func<SaveData, bool> Condition;
        public Func<SaveData, float> Progress; // 0..1 for the profile screen
    }

    /// <summary>Local achievements evaluated against the saved stats.</summary>
    public static class Achievements
    {
        static List<AchievementDefinition> _all;

        public static IList<AchievementDefinition> All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        static float Ratio(int v, int target) { return target <= 0 ? 1f : Math.Min(1f, v / (float)target); }

        static List<AchievementDefinition> Build()
        {
            List<AchievementDefinition> l = new List<AchievementDefinition>();
            l.Add(Make("first_match", "Primer saque", "Juega tu primer partido.", 50, d => d.stats.matchesPlayed >= 1, d => Ratio(d.stats.matchesPlayed, 1)));
            l.Add(Make("first_win", "Primera victoria", "Gana a la máquina.", 80, d => d.stats.matchesWonVsCpu >= 1, d => Ratio(d.stats.matchesWonVsCpu, 1)));
            l.Add(Make("hard_win", "Sin miedo", "Gana a la máquina en difícil.", 200, d => d.stats.hardWins >= 1, d => Ratio(d.stats.hardWins, 1)));
            l.Add(Make("rally_15", "Peloteo infinito", "Consigue un intercambio de 15 golpes.", 100, d => d.stats.longestRally >= 15, d => Ratio(d.stats.longestRally, 15)));
            l.Add(Make("smash_25", "Martillo", "Conecta 25 remates.", 120, d => d.stats.smashes >= 25, d => Ratio(d.stats.smashes, 25)));
            l.Add(Make("powers_20", "Poder total", "Usa 20 poderes.", 100, d => d.stats.powersUsed >= 20, d => Ratio(d.stats.powersUsed, 20)));
            l.Add(Make("local_5", "En el mismo móvil", "Juega 5 partidos de 2 jugadores.", 120, d => d.stats.localMatches >= 5, d => Ratio(d.stats.localMatches, 5)));
            l.Add(Make("champion", "Leyenda de Brooklyneir", "Gana un torneo.", 300, d => d.stats.tournamentsWon >= 1, d => Ratio(d.stats.tournamentsWon, 1)));
            l.Add(Make("collector", "Coleccionista", "Consigue 5 raquetas.", 150, d => d.ownedRackets.Count >= 5, d => Ratio(d.ownedRackets.Count, 5)));
            l.Add(Make("trainee", "Alumno aplicado", "Consigue 9 estrellas en entrenamiento.", 150, d => TotalStars(d) >= 9, d => Ratio(TotalStars(d), 9)));
            return l;
        }

        public static int TotalStars(SaveData d)
        {
            int s = 0;
            for (int i = 0; i < d.training.Count; i++) s += d.training[i].bestStars;
            return s;
        }

        static AchievementDefinition Make(string id, string title, string desc, int reward, Func<SaveData, bool> cond, Func<SaveData, float> prog)
        {
            return new AchievementDefinition { Id = id, Title = title, Description = desc, Reward = reward, Condition = cond, Progress = prog };
        }

        /// <summary>Unlocks every newly satisfied achievement, pays its reward, returns them.</summary>
        public static List<AchievementDefinition> Evaluate(SaveData data, Economy economy)
        {
            List<AchievementDefinition> unlocked = new List<AchievementDefinition>();
            IList<AchievementDefinition> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                AchievementDefinition a = all[i];
                if (data.achievements.Contains(a.Id)) continue;
                bool ok;
                try { ok = a.Condition(data); }
                catch (Exception) { ok = false; }
                if (!ok) continue;
                data.achievements.Add(a.Id);
                if (economy != null) economy.AddCoins(a.Reward);
                unlocked.Add(a);
            }
            return unlocked;
        }
    }
}
