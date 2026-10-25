using Brooklyneir.Config;
using Brooklyneir.Gameplay;
using Brooklyneir.Progression;
using Brooklyneir.SaveSystem;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class RewardsTests
    {
        static MatchResult Result(GameMode mode, bool vsCpu, AIDifficulty diff, int winner)
        {
            MatchConfig c = new MatchConfig { mode = mode, player2IsAI = vsCpu, aiDifficulty = diff };
            MatchResult r = new MatchResult { Config = c, Winner = winner, LongestRally = 9 };
            r.Points[0] = 7;
            r.Points[1] = 4;
            r.Hits[0] = 30;
            r.Hits[1] = 28;
            r.Smashes[0] = 3;
            return r;
        }

        [Test]
        public void CpuWinPaysByDifficultyAndUpdatesStats()
        {
            SaveData d = SaveData.CreateNew(0);
            Economy eco = new Economy(d, new EconomyTuning());
            RewardSummary s = Rewards.ForMatch(Result(GameMode.VersusCpu, true, AIDifficulty.Hard, 0), d, eco);
            Assert.GreaterOrEqual(s.Coins, new EconomyTuning().winVsHard);
            Assert.AreEqual(1, d.stats.matchesWonVsCpu);
            Assert.AreEqual(1, d.stats.hardWins);
            Assert.AreEqual(9, d.stats.longestRally);
            Assert.AreEqual(30, d.stats.totalHits, "CPU hits are not counted");
        }

        [Test]
        public void LocalMatchRewardsTheSharedProfile()
        {
            SaveData d = SaveData.CreateNew(0);
            Economy eco = new Economy(d, new EconomyTuning());
            Rewards.ForMatch(Result(GameMode.QuickMatch, false, AIDifficulty.Normal, 1), d, eco);
            Assert.AreEqual(1, d.stats.localMatches);
            Assert.AreEqual(58, d.stats.totalHits);
            Assert.Greater(eco.Coins, 0);
        }

        [Test]
        public void TournamentChampionUnlocksLegendTable()
        {
            SaveData d = SaveData.CreateNew(0);
            Economy eco = new Economy(d, new EconomyTuning());
            d.tournament = Tournament.CreateNew(11);
            for (int i = 0; i < 3; i++) Rewards.ForMatch(Result(GameMode.Tournament, true, AIDifficulty.Normal, 0), d, eco);
            Assert.IsTrue(d.ownedTables.Contains("legend"));
            Assert.AreEqual(1, d.stats.tournamentsWon);
            Assert.IsFalse(d.tournament.active);
        }

        [Test]
        public void DemoAndAbandonedGiveNothing()
        {
            SaveData d = SaveData.CreateNew(0);
            Economy eco = new Economy(d, new EconomyTuning());
            Rewards.ForMatch(Result(GameMode.Demo, true, AIDifficulty.Normal, 0), d, eco);
            MatchResult ab = Result(GameMode.VersusCpu, true, AIDifficulty.Normal, 0);
            ab.Abandoned = true;
            Rewards.ForMatch(ab, d, eco);
            Assert.AreEqual(0, eco.Coins);
            Assert.AreEqual(0, d.stats.matchesPlayed);
        }

        [Test]
        public void TrainingStarsPayOnlyOnce()
        {
            SaveData d = SaveData.CreateNew(0);
            Economy eco = new Economy(d, new EconomyTuning());
            DrillDefinition drill = TrainingDrills.Get("returns");
            TrainingResult r = new TrainingResult { Drill = drill, Successes = 11, Balls = 12, Stars = 3 };
            RewardSummary first = Rewards.ForTraining(r, d, eco);
            RewardSummary second = Rewards.ForTraining(r, d, eco);
            Assert.AreEqual(3 * new EconomyTuning().trainingStar, first.Coins - AchievementCoins(first));
            Assert.AreEqual(new EconomyTuning().trainingRepeat, second.Coins - AchievementCoins(second));
            Assert.AreEqual(3, d.GetTraining("returns").bestStars);
        }

        static int AchievementCoins(RewardSummary s)
        {
            int c = 0;
            for (int i = 0; i < s.Achievements.Count; i++) c += s.Achievements[i].Reward;
            return c;
        }
    }
}
