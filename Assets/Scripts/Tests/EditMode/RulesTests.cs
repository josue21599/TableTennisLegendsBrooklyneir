using Brooklyneir.Gameplay;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class RallyRefereeTests
    {
        [Test]
        public void ReceiverMustLetItBounceOnceThenReturn()
        {
            RallyReferee r = new RallyReferee();
            r.StartRally(0);
            Assert.IsFalse(r.CanHit(1), "no hit before the bounce");
            r.OnTableBounce(1);
            Assert.IsTrue(r.CanHit(1));
            Assert.IsFalse(r.CanHit(0), "the hitter cannot hit twice");
            r.RegisterHit(1);
            Assert.AreEqual(1, r.LastHitter);
            Assert.IsFalse(r.RallyOver);
        }

        [Test]
        public void DoubleBounceGivesPointToHitter()
        {
            RallyReferee r = new RallyReferee();
            r.StartRally(0);
            r.OnTableBounce(1);
            r.OnTableBounce(1);
            Assert.IsTrue(r.RallyOver);
            Assert.AreEqual(0, r.Winner);
            Assert.AreEqual(PointReason.DoubleBounce, r.Reason);
        }

        [Test]
        public void MissedBallAfterBounceGivesPointToHitter()
        {
            RallyReferee r = new RallyReferee();
            r.StartRally(1);
            r.OnTableBounce(0);
            r.OnBallDead(false);
            Assert.AreEqual(1, r.Winner);
            Assert.AreEqual(PointReason.MissedReturn, r.Reason);
        }

        [Test]
        public void BallOutWithoutBounceGivesPointToReceiver()
        {
            RallyReferee r = new RallyReferee();
            r.StartRally(0);
            r.OnBallDead(true);
            Assert.AreEqual(1, r.Winner);
            Assert.AreEqual(PointReason.OutSide, r.Reason);
        }

        [Test]
        public void NetThenOwnSideIsANetFault()
        {
            RallyReferee r = new RallyReferee();
            r.StartRally(0);
            r.OnNetContact();
            r.OnTableBounce(0);
            Assert.AreEqual(1, r.Winner);
            Assert.AreEqual(PointReason.IntoNet, r.Reason);
        }

        [Test]
        public void NetCordThatLandsIsStillInPlay()
        {
            RallyReferee r = new RallyReferee();
            r.StartRally(0);
            r.OnNetContact();
            r.OnTableBounce(1);
            Assert.IsFalse(r.RallyOver);
            Assert.IsTrue(r.CanHit(1));
        }

        [Test]
        public void EventsAfterRallyEndAreIgnored()
        {
            RallyReferee r = new RallyReferee();
            r.StartRally(0);
            r.OnBallDead(false);
            int winner = r.Winner;
            r.OnTableBounce(1);
            r.OnBallDead(true);
            Assert.AreEqual(winner, r.Winner);
        }
    }

    [TestFixture]
    public class MatchScoreTests
    {
        [Test]
        public void WinsAtElevenWithTwoPointLead()
        {
            MatchScore s = new MatchScore(11, 1, true, 0);
            for (int i = 0; i < 10; i++) s.AwardPoint(0);
            for (int i = 0; i < 9; i++) s.AwardPoint(1);
            ScoreEvent e = s.AwardPoint(0);
            Assert.IsTrue(e.MatchWon);
            Assert.AreEqual(0, s.MatchWinner);
        }

        [Test]
        public void DeuceRequiresTwoPointLead()
        {
            MatchScore s = new MatchScore(11, 1, true, 0);
            for (int i = 0; i < 10; i++) { s.AwardPoint(0); s.AwardPoint(1); }
            Assert.IsTrue(s.IsDeuce);
            Assert.IsFalse(s.AwardPoint(0).MatchWon, "11-10 is not enough");
            Assert.IsFalse(s.AwardPoint(1).MatchWon, "11-11");
            Assert.IsFalse(s.AwardPoint(1).MatchWon, "11-12 is not enough either");
            Assert.IsTrue(s.AwardPoint(1).MatchWon, "11-13 wins");
            Assert.IsTrue(s.MatchOver);
            Assert.AreEqual(1, s.MatchWinner);
        }

        [Test]
        public void WithoutWinByTwoFirstToTargetWins()
        {
            MatchScore s = new MatchScore(5, 1, false, 0);
            for (int i = 0; i < 4; i++) { s.AwardPoint(0); s.AwardPoint(1); }
            Assert.IsTrue(s.AwardPoint(1).MatchWon);
        }

        [Test]
        public void ServeRotatesEveryTwoPointsAndEveryPointAtDeuce()
        {
            MatchScore s = new MatchScore(11, 1, true, 0);
            Assert.AreEqual(0, s.Server);
            s.AwardPoint(0);
            Assert.AreEqual(0, s.Server);
            s.AwardPoint(0);
            Assert.AreEqual(1, s.Server);
            s.AwardPoint(1);
            Assert.AreEqual(1, s.Server);
            s.AwardPoint(1);
            Assert.AreEqual(0, s.Server);
            // Bring to 10-10.
            for (int i = 0; i < 8; i++) { s.AwardPoint(0); s.AwardPoint(1); }
            Assert.IsTrue(s.IsDeuce);
            int server = s.Server;
            s.AwardPoint(0);
            Assert.AreNotEqual(server, s.Server, "serve alternates every point at deuce");
        }

        [Test]
        public void BestOfThreeNeedsTwoSetsAndAlternatesFirstServer()
        {
            MatchScore s = new MatchScore(3, 2, false, 0);
            for (int i = 0; i < 3; i++) s.AwardPoint(0);
            Assert.AreEqual(1, s.Sets[0]);
            Assert.IsFalse(s.MatchOver);
            Assert.AreEqual(0, s.Points[0]);
            Assert.AreEqual(1, s.Server, "second set starts with the other server");
            for (int i = 0; i < 3; i++) s.AwardPoint(0);
            Assert.IsTrue(s.MatchOver);
        }

        [Test]
        public void TimeUpEndsMatchForLeaderOrGoldenPoint()
        {
            MatchScore s = new MatchScore(11, 1, true, 0);
            s.AwardPoint(0);
            s.AwardPoint(1);
            s.SignalTimeUp();
            ScoreEvent e = s.AwardPoint(1);
            Assert.IsTrue(e.MatchWon);
            Assert.AreEqual(1, s.MatchWinner);

            MatchScore t = new MatchScore(11, 1, true, 0);
            t.AwardPoint(0);
            t.SignalTimeUp();
            ScoreEvent tie = t.AwardPoint(1);
            Assert.IsTrue(tie.GoldenPoint);
            Assert.IsFalse(t.MatchOver);
            Assert.IsTrue(t.AwardPoint(0).MatchWon);
        }

        [Test]
        public void NoPointsAfterMatchOver()
        {
            MatchScore s = new MatchScore(3, 1, false, 0);
            for (int i = 0; i < 3; i++) s.AwardPoint(0);
            s.AwardPoint(1);
            Assert.AreEqual(0, s.Points[1]);
        }
    }

    [TestFixture]
    public class MatchConfigTests
    {
        [Test]
        public void ValidationFixesContradictions()
        {
            MatchConfig c = new MatchConfig();
            c.pointsToWin = 1;
            c.bestOfSets = 4;
            c.timeLimitMinutes = 3f;
            c.powerUpsEnabled = false;
            c.powerFrequency = PowerFrequency.High;
            c.mode = GameMode.VersusCpu;
            c.player2IsAI = false;
            c.Validate();
            Assert.AreEqual(3, c.pointsToWin);
            Assert.AreEqual(1, c.bestOfSets, "time limit forces a single set");
            Assert.AreEqual(PowerFrequency.Off, c.powerFrequency);
            Assert.IsTrue(c.player2IsAI);
        }

        [Test]
        public void SetsToWinFromBestOf()
        {
            MatchConfig c = new MatchConfig();
            c.bestOfSets = 5;
            Assert.AreEqual(3, c.SetsToWin);
            c.bestOfSets = 3;
            Assert.AreEqual(2, c.SetsToWin);
        }
    }
}
