using Brooklyneir.Gameplay;
using Brooklyneir.Progression;
using Brooklyneir.SaveSystem;

namespace Brooklyneir.Net
{
    public enum LanOutcome { Finished, Void, OpponentLeft, LocalLeft }

    /// <summary>
    /// Coins, stats and achievements for a LAN match, on each phone for its own player, from the
    /// host's final result. A void match (no reconnection in time) gives nothing to anybody. If
    /// one player leaves, the one who stays wins; the one who leaves loses and the match gives it
    /// no coins (achievements already reached still pay: they are milestones, not this match's prize).
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
            else if (outcome == LanOutcome.LocalLeft)
            {
                sum.Lines.Add("Abandonaste el partido");
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
