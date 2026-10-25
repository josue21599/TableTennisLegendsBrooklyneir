using System.Collections.Generic;
using Brooklyneir.Gameplay;
using Brooklyneir.SaveSystem;

namespace Brooklyneir.Progression
{
    public class RewardSummary
    {
        public int Coins;
        public readonly List<string> Lines = new List<string>();
        public readonly List<AchievementDefinition> Achievements = new List<AchievementDefinition>();
        public readonly List<string> Unlocks = new List<string>();
        public bool TournamentChampion;
        public bool TournamentEliminated;
    }

    /// <summary>
    /// Turns a finished match or drill into coins, stats, achievements and tournament progress.
    /// Pure logic over the save data (unit-tested). The caller saves afterwards.
    /// </summary>
    public static class Rewards
    {
        public static RewardSummary ForMatch(MatchResult r, SaveData data, Economy eco)
        {
            RewardSummary sum = new RewardSummary();
            if (r == null || r.Abandoned || r.Config == null || r.Config.mode == GameMode.Demo) return sum;
            MatchConfig c = r.Config;
            StatsData st = data.stats;
            int before = eco.Coins;
            bool vsCpu = c.player2IsAI && !c.player1IsAI;
            bool p1Won = r.Winner == 0;

            st.matchesPlayed++;
            st.pointsWon += r.Points[0];
            st.totalHits += r.Hits[0] + (vsCpu ? 0 : r.Hits[1]);
            st.smashes += r.Smashes[0] + (vsCpu ? 0 : r.Smashes[1]);
            st.powersUsed += r.PowersUsed[0] + (vsCpu ? 0 : r.PowersUsed[1]);
            if (r.LongestRally > st.longestRally) st.longestRally = r.LongestRally;

            if (c.mode == GameMode.Tournament)
            {
                Tournament t = new Tournament(data.tournament);
                int round = t.Round;
                t.RecordPlayerResult(p1Won);
                if (p1Won)
                {
                    eco.AddCoins(eco.Tuning.tournamentRoundWin * (round + 1));
                    sum.Lines.Add("Victoria en " + Tournament.RoundNames[round].ToLowerInvariant());
                }
                else
                {
                    eco.AddCoins(eco.Tuning.lossConsolation);
                    sum.Lines.Add("Eliminado en " + Tournament.RoundNames[round].ToLowerInvariant());
                    sum.TournamentEliminated = true;
                }
                if (t.PlayerIsChampion)
                {
                    st.tournamentsWon++;
                    eco.AddCoins(eco.Tuning.tournamentChampion);
                    sum.Lines.Add("¡CAMPEÓN DEL TORNEO!");
                    sum.TournamentChampion = true;
                    if (eco.GrantTable("legend")) sum.Unlocks.Add("Mesa: Arena de Leyendas");
                }
                if (p1Won) st.matchesWonVsCpu++; else st.matchesLostVsCpu++;
            }
            else if (vsCpu)
            {
                if (p1Won)
                {
                    st.matchesWonVsCpu++;
                    if (c.aiDifficulty == AIDifficulty.Hard) st.hardWins++;
                    eco.AddCoins(eco.RewardForCpuWin((int)c.aiDifficulty));
                    sum.Lines.Add("Victoria contra la máquina");
                }
                else
                {
                    st.matchesLostVsCpu++;
                    eco.AddCoins(eco.Tuning.lossConsolation);
                    sum.Lines.Add("Premio por participar");
                }
            }
            else
            {
                st.localMatches++;
                eco.AddCoins(eco.Tuning.localMatchPlayed);
                sum.Lines.Add("Partido de 2 jugadores");
            }

            sum.Achievements.AddRange(Achievements.Evaluate(data, eco));
            for (int i = 0; i < sum.Achievements.Count; i++) sum.Lines.Add("Logro: " + sum.Achievements[i].Title);
            sum.Coins = eco.Coins - before;
            return sum;
        }

        public static RewardSummary ForTraining(TrainingResult r, SaveData data, Economy eco)
        {
            RewardSummary sum = new RewardSummary();
            if (r == null || r.Drill == null) return sum;
            int before = eco.Coins;
            data.stats.trainingSessions++;
            TrainingRecord rec = data.GetTraining(r.Drill.Id);
            if (rec == null)
            {
                rec = new TrainingRecord { drillId = r.Drill.Id };
                data.training.Add(rec);
            }
            int newStars = r.Stars > rec.bestStars ? r.Stars - rec.bestStars : 0;
            if (newStars > 0)
            {
                eco.AddCoins(newStars * eco.Tuning.trainingStar);
                sum.Lines.Add("Nuevas estrellas: " + newStars);
            }
            else if (r.Stars > 0)
            {
                eco.AddCoins(eco.Tuning.trainingRepeat);
                sum.Lines.Add("Práctica completada");
            }
            if (r.Stars > rec.bestStars) rec.bestStars = r.Stars;
            if (r.Successes > rec.bestScore) rec.bestScore = r.Successes;
            sum.Achievements.AddRange(Achievements.Evaluate(data, eco));
            for (int i = 0; i < sum.Achievements.Count; i++) sum.Lines.Add("Logro: " + sum.Achievements[i].Title);
            sum.Coins = eco.Coins - before;
            return sum;
        }
    }
}
