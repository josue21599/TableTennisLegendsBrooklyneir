using Brooklyneir.Gameplay;
using Brooklyneir.Progression;
using Brooklyneir.SaveSystem;

namespace Brooklyneir.Net
{
    public enum LanOutcome { Finished, Void, OpponentLeft, LocalLeft }

    /// <summary>
    /// Coins, stats and achievements for a LAN match, on each phone for its own player, from the
    /// host's final result. A void match (no reconnection in time) gives nothing to anybody. If
    /// one player leaves, the one who stays wins. The one who leaves gets no coins and no
    /// achievements: the match only counts in lanMatches, as played and lost (lanWins unchanged),
    /// and no other stat moves, so no achievement can be reached later from this match either.
    /// Pure logic over the save data, unit-tested; the caller saves afterwards.
    /// </summary>
    public static class LanRewards
    {
        public static RewardSummary ForMatch(MatchResult r, int localPlayer, LanOutcome outcome, SaveData data, Economy eco)
        {
            RewardSummary sum = new RewardSummary();
            if (outcome == LanOutcome.Void || r == null) return sum;
            int before = eco.Coins;
            StatsData st = data.stats;
            if (outcome == LanOutcome.LocalLeft)
            {
                st.lanMatches++;
                sum.Lines.Add("Abandonaste el partido: cuenta como perdido");
                return sum;
            }
            int me = localPlayer == 1 ? 1 : 0;
            bool won = outcome == LanOutcome.OpponentLeft || (outcome == LanOutcome.Finished && r.Winner == me);

            st.matchesPlayed++;
            st.lanMatches++;
            st.pointsWon += r.Points[me];
            st.totalHits += r.Hits[me];
            st.smashes += r.Smashes[me];
            st.powersUsed += r.PowersUsed[me];
            if (r.LongestRally > st.longestRally) st.longestRally = r.LongestRally;

            if (won)
            {
                st.lanWins++;
                eco.AddCoins(eco.Tuning.lanWin);
                sum.Lines.Add(outcome == LanOutcome.OpponentLeft ? "Victoria: el rival abandonó" : "Victoria en LAN");
            }
            else
            {
                eco.AddCoins(eco.Tuning.lanLoss);
                sum.Lines.Add("Premio por participar");
            }

            sum.Achievements.AddRange(Achievements.Evaluate(data, eco));
            for (int i = 0; i < sum.Achievements.Count; i++) sum.Lines.Add("Logro: " + sum.Achievements[i].Title);
            sum.Coins = eco.Coins - before;
            return sum;
        }
    }
}
