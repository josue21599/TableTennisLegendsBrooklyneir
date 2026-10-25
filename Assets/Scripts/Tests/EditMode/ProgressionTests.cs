using System.Collections.Generic;
using Brooklyneir.Config;
using Brooklyneir.Progression;
using Brooklyneir.Rackets;
using Brooklyneir.SaveSystem;
using Brooklyneir.Tables;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class EconomyTests
    {
        SaveData _data;
        Economy _eco;

        [SetUp]
        public void Setup()
        {
            _data = SaveData.CreateNew(300);
            _eco = new Economy(_data, new EconomyTuning());
        }

        [Test]
        public void CannotBuyWithoutEnoughCoins()
        {
            Assert.AreEqual(PurchaseResult.NotEnoughCoins, _eco.BuyRacket("legend_crown"));
            Assert.AreEqual(300, _eco.Coins);
            Assert.IsFalse(_eco.OwnsRacket("legend_crown"));
        }

        [Test]
        public void PurchaseDeductsAndUnlocks()
        {
            _eco.AddCoins(300);
            int changes = 0;
            _eco.CoinsChanged += (balance, delta) => changes++;
            Assert.AreEqual(PurchaseResult.Success, _eco.BuyRacket("neon_pulse"));
            Assert.AreEqual(100, _eco.Coins);
            Assert.IsTrue(_eco.OwnsRacket("neon_pulse"));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, _eco.BuyRacket("neon_pulse"));
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void TournamentTableIsNotForSale()
        {
            _eco.AddCoins(100000);
            Assert.AreEqual(PurchaseResult.NotForSale, _eco.BuyTable("legend"));
            Assert.IsTrue(_eco.GrantTable("legend"));
            Assert.IsFalse(_eco.GrantTable("legend"));
        }

        [Test]
        public void BalanceIsCapped()
        {
            _eco.AddCoins(int.MaxValue);
            Assert.AreEqual(new EconomyTuning().maxCoins, _eco.Coins);
            _eco.AddCoins(-50);
            Assert.AreEqual(new EconomyTuning().maxCoins, _eco.Coins, "negative grants are ignored");
        }

        [Test]
        public void UnknownItemsAreRejected()
        {
            Assert.AreEqual(PurchaseResult.UnknownItem, _eco.BuyRacket("does_not_exist"));
            Assert.AreEqual(PurchaseResult.UnknownItem, _eco.BuyTable("nope"));
        }
    }

    [TestFixture]
    public class SaveDataTests
    {
        [Test]
        public void NewSaveOwnsFreeContent()
        {
            SaveData d = SaveData.CreateNew(300);
            foreach (RacketDefinition r in RacketCatalog.All)
                if (r.Unlock == UnlockRule.Free) Assert.IsTrue(d.ownedRackets.Contains(r.Id));
            foreach (TableDefinition t in TableCatalog.All)
                if (t.Unlock == UnlockRule.Free) Assert.IsTrue(d.ownedTables.Contains(t.Id));
        }

        [Test]
        public void SanitizeRepairsMissingAndCorruptFields()
        {
            SaveData d = new SaveData();
            d.coins = -40;
            d.ownedRackets = null;
            d.ownedTables = new List<string> { "garbage", "classic", "classic" };
            d.stats = null;
            d.settings = null;
            d.tournament = null;
            d.racketP1 = "legend_crown";
            d.selectedTable = "garbage";
            d.training = new List<TrainingRecord> { null, new TrainingRecord { drillId = "returns", bestStars = 9 } };
            Assert.IsTrue(d.Sanitize(999999));
            Assert.AreEqual(0, d.coins);
            Assert.IsNotNull(d.stats);
            Assert.IsNotNull(d.settings);
            Assert.IsNotNull(d.tournament);
            Assert.IsFalse(d.ownedTables.Contains("garbage"));
            Assert.AreEqual(1, CountOf(d.ownedTables, "classic"), "duplicates removed");
            Assert.IsTrue(d.ownedRackets.Contains(d.racketP1), "selected racket must be owned");
            Assert.IsTrue(d.ownedTables.Contains(d.selectedTable));
            Assert.AreEqual(1, d.training.Count);
            Assert.AreEqual(3, d.training[0].bestStars);
        }

        [Test]
        public void SettingsAreClamped()
        {
            SettingsData s = new SettingsData();
            s.musicVolume = 5f;
            s.sfxVolume = float.NaN;
            s.touchSensitivity = 10f;
            s.quality = 7;
            s.Sanitize();
            Assert.AreEqual(1f, s.musicVolume, 0.0001f);
            Assert.IsFalse(float.IsNaN(s.sfxVolume));
            Assert.AreEqual(2f, s.touchSensitivity, 0.0001f);
            Assert.AreEqual((int)QualityLevel.Medium, s.quality);
        }

        static int CountOf(List<string> l, string v)
        {
            int n = 0;
            foreach (string s in l) if (s == v) n++;
            return n;
        }
    }

    [TestFixture]
    public class TournamentTests
    {
        [Test]
        public void BracketHasPlayerAndSevenDistinctRivals()
        {
            TournamentState s = Tournament.CreateNew(1234);
            Assert.AreEqual(15, s.bracket.Length);
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < 8; i++) Assert.IsTrue(seen.Add(s.bracket[i]));
            Assert.IsTrue(seen.Contains(Tournament.Player));
            Assert.AreEqual(8, seen.Count);
        }

        [Test]
        public void WinningThreeMatchesMakesChampion()
        {
            Tournament t = new Tournament(Tournament.CreateNew(77));
            for (int round = 0; round < 3; round++)
            {
                Assert.AreEqual(round, t.Round);
                Assert.Greater(t.CurrentOpponent(), 0);
                t.RecordPlayerResult(true);
            }
            Assert.IsTrue(t.PlayerIsChampion);
            Assert.IsFalse(t.Active);
        }

        [Test]
        public void LosingEliminatesAndCompletesBracket()
        {
            Tournament t = new Tournament(Tournament.CreateNew(5));
            t.RecordPlayerResult(true);
            t.RecordPlayerResult(false);
            Assert.IsTrue(t.PlayerEliminated);
            Assert.IsFalse(t.Active);
            Assert.IsFalse(t.PlayerIsChampion);
            Assert.AreNotEqual(-1, t.State.bracket[14], "a champion is decided");
            Assert.AreNotEqual(Tournament.Player, t.State.bracket[14]);
        }

        [Test]
        public void SameSeedSameBracket()
        {
            TournamentState a = Tournament.CreateNew(99);
            TournamentState b = Tournament.CreateNew(99);
            for (int i = 0; i < 8; i++) Assert.AreEqual(a.bracket[i], b.bracket[i]);
            Tournament ta = new Tournament(a), tb = new Tournament(b);
            ta.RecordPlayerResult(true);
            tb.RecordPlayerResult(true);
            for (int i = 8; i < 12; i++) Assert.AreEqual(a.bracket[i], b.bracket[i]);
        }

        [Test]
        public void FinalIsAlwaysHard()
        {
            Tournament t = new Tournament(Tournament.CreateNew(3));
            t.RecordPlayerResult(true);
            t.RecordPlayerResult(true);
            Assert.AreEqual(Brooklyneir.Gameplay.AIDifficulty.Hard, t.DifficultyForCurrentRound());
        }
    }

    [TestFixture]
    public class AchievementTests
    {
        [Test]
        public void AchievementsUnlockOncePayOnce()
        {
            SaveData d = SaveData.CreateNew(0);
            Economy eco = new Economy(d, new EconomyTuning());
            d.stats.matchesPlayed = 1;
            List<AchievementDefinition> first = Achievements.Evaluate(d, eco);
            Assert.AreEqual(1, first.Count);
            int coins = eco.Coins;
            Assert.Greater(coins, 0);
            Assert.AreEqual(0, Achievements.Evaluate(d, eco).Count);
            Assert.AreEqual(coins, eco.Coins);
        }
    }

    [TestFixture]
    public class RacketStatTests
    {
        [Test]
        public void StatsChangeRealGameplayValues()
        {
            RacketTuning t = new RacketTuning();
            RacketStatModel low = new RacketStatModel(new RacketStats(1, 1, 1, 1, 1), t);
            RacketStatModel high = new RacketStatModel(new RacketStats(10, 10, 10, 10, 10), t);
            Assert.Greater(high.SpeedMultiplier, low.SpeedMultiplier);
            Assert.Greater(high.Reach, low.Reach);
            Assert.Greater(high.SpinMultiplier, low.SpinMultiplier);
            Assert.Greater(high.FollowSpeed, low.FollowSpeed);
            Assert.Less(high.AimNoiseMultiplier, low.AimNoiseMultiplier);
            Assert.Less(high.VerticalNoiseMultiplier, low.VerticalNoiseMultiplier);
        }

        [Test]
        public void CatalogIsValid()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (RacketDefinition r in RacketCatalog.All)
            {
                Assert.IsTrue(ids.Add(r.Id), "duplicate id " + r.Id);
                Assert.IsFalse(string.IsNullOrEmpty(r.Name));
                Assert.IsTrue(r.Unlock != UnlockRule.Coins || r.Price > 0, r.Id + " needs a price");
            }
            Assert.GreaterOrEqual(RacketCatalog.All.Count, 6);
            Assert.AreEqual(6, TableCatalog.All.Count);
        }
    }
}
