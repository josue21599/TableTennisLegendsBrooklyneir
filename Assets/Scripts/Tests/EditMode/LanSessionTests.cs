using Brooklyneir.Config;
using Brooklyneir.Gameplay;
using Brooklyneir.Net;
using Brooklyneir.Progression;
using Brooklyneir.SaveSystem;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class SessionStateMachineTests
    {
        NetConfig _cfg;
        NetIdentity _me;

        [SetUp]
        public void Setup()
        {
            _cfg = new NetConfig();
            _cfg.Validate();
            _me = new NetIdentity { Protocol = NetProtocol.Version, AppVersion = "1.5.0", BalanceHash = 0xABCD1234u };
        }

        HelloMsg Hello(uint code, string app = "1.5.0", uint hash = 0xABCD1234u, ushort protocol = NetProtocol.Version)
        {
            return new HelloMsg { Protocol = protocol, AppVersion = app, BalanceHash = hash, Name = "J2", RacketId = "x", SessionCode = code };
        }

        SessionStateMachine HostInMatch()
        {
            SessionStateMachine h = new SessionStateMachine(NetRole.Host, _me, _cfg);
            h.StartHosting(777);
            Assert.AreEqual(RejectReason.None, h.HostOnHello(Hello(0)));
            Assert.AreEqual(LinkState.Lobby, h.State);
            h.MatchStarted();
            Assert.AreEqual(LinkState.InMatch, h.State);
            return h;
        }

        [Test]
        public void ADisconnectionDuringTheMatchWaitsAndThenVoidsIt()
        {
            SessionStateMachine h = HostInMatch();
            h.TransportDisconnected(100.0);
            Assert.AreEqual(LinkState.Reconnecting, h.State, "during the match: wait for the other phone");
            h.Update(100.0 + _cfg.reconnectWindowSeconds - 0.5);
            Assert.AreEqual(LinkState.Reconnecting, h.State);
            h.Update(100.0 + _cfg.reconnectWindowSeconds + 0.5);
            Assert.AreEqual(LinkState.Ended, h.State);
            Assert.AreEqual(EndReason.Void, h.Reason);
            Assert.AreEqual(LanOutcome.Void, h.Outcome);
            // Before the match there is nothing to resume.
            SessionStateMachine lobby = new SessionStateMachine(NetRole.Host, _me, _cfg);
            lobby.StartHosting(5);
            lobby.HostOnHello(Hello(0));
            lobby.TransportDisconnected(3.0);
            Assert.AreEqual(EndReason.OpponentLeft, lobby.Reason);
            Assert.AreEqual(LanOutcome.Void, lobby.Outcome, "leaving the lobby is not a match");
        }

        [Test]
        public void TheGuestComesBackWithItsSessionCodeAndTheMatchGoesOn()
        {
            SessionStateMachine h = HostInMatch();
            SessionStateMachine g = new SessionStateMachine(NetRole.Guest, _me, _cfg);
            g.StartConnecting();
            g.GuestOnConnected();
            Assert.AreEqual(LinkState.Handshaking, g.State);
            Assert.AreEqual(0u, g.HelloCode, "a new guest says Hello with code 0");
            g.GuestOnWelcome(new WelcomeMsg { SessionCode = h.SessionCode, PlayerIndex = 1 });
            Assert.AreEqual(LinkState.Lobby, g.State);
            g.MatchStarted();
            // Both lose the connection; the guest reconnects with the code it was given.
            h.TransportDisconnected(50.0);
            g.TransportDisconnected(50.1);
            Assert.AreEqual(h.SessionCode, g.HelloCode, "the guest brings the session code back");
            Assert.AreEqual(RejectReason.None, h.HostOnHello(Hello(g.HelloCode)));
            Assert.IsTrue(h.LastAcceptWasReconnect, "the host knows it must send the full state again");
            Assert.AreEqual(LinkState.InMatch, h.State);
            g.GuestOnWelcome(new WelcomeMsg { SessionCode = h.SessionCode, PlayerIndex = 1 });
            Assert.AreEqual(LinkState.InMatch, g.State, "the guest is back in the same match");
            h.MatchFinished(); g.MatchFinished();
            Assert.AreEqual(LanOutcome.Finished, h.Outcome);
            Assert.AreEqual(LanOutcome.Finished, g.Outcome);
        }

        [Test]
        public void WhoeverLeavesLosesAndTheOneWhoStaysWins()
        {
            SessionStateMachine h = HostInMatch();
            h.OnBye();
            Assert.AreEqual(EndReason.OpponentLeft, h.Reason);
            Assert.AreEqual(LanOutcome.OpponentLeft, h.Outcome, "the host stays: it wins");
            SessionStateMachine g = new SessionStateMachine(NetRole.Guest, _me, _cfg);
            g.StartConnecting(); g.GuestOnConnected();
            g.GuestOnWelcome(new WelcomeMsg { SessionCode = 9, PlayerIndex = 1 });
            g.MatchStarted();
            g.LocalLeave();
            Assert.AreEqual(LanOutcome.LocalLeft, g.Outcome, "the guest left: it loses");
            g.OnBye();
            Assert.AreEqual(EndReason.LocalLeft, g.Reason, "the end does not change afterwards");
        }

        [Test]
        public void AWrongSessionCodeOrVersionIsRejected()
        {
            SessionStateMachine h = new SessionStateMachine(NetRole.Host, _me, _cfg);
            h.StartHosting(42);
            Assert.AreEqual(RejectReason.ProtocolMismatch, h.HostOnHello(Hello(0, protocol: (ushort)(NetProtocol.Version + 1))));
            Assert.AreEqual(RejectReason.AppVersionMismatch, h.HostOnHello(Hello(0, app: "1.4.9")));
            Assert.AreEqual(RejectReason.BalanceMismatch, h.HostOnHello(Hello(0, hash: 1u)));
            Assert.AreEqual(RejectReason.BadSession, h.HostOnHello(Hello(1234)), "a code from another match");
            Assert.AreEqual(LinkState.Hosting, h.State, "rejections do not change the state");
            Assert.AreEqual(RejectReason.None, h.HostOnHello(Hello(0)));
            Assert.AreEqual(RejectReason.Full, h.HostOnHello(Hello(0)), "a second guest while one is in");
            h.MatchStarted();
            h.TransportDisconnected(1.0);
            Assert.AreEqual(RejectReason.BadSession, h.HostOnHello(Hello(h.SessionCode + 1)), "someone else cannot take the place of the guest");
            Assert.AreEqual(RejectReason.BadSession, h.HostOnHello(Hello(0)), "nor a new guest while waiting for the old one");
            Assert.AreEqual(LinkState.Reconnecting, h.State);
            SessionStateMachine g = new SessionStateMachine(NetRole.Guest, _me, _cfg);
            g.StartConnecting(); g.GuestOnConnected();
            g.GuestOnReject(new RejectMsg { Reason = RejectReason.AppVersionMismatch, Text = "Actualizad los dos" });
            Assert.AreEqual(EndReason.Rejected, g.Reason);
            Assert.AreEqual(RejectReason.AppVersionMismatch, g.RejectedBecause);
        }
    }

    [TestFixture]
    public class LanRewardsTests
    {
        static MatchResult Result(int winner)
        {
            MatchResult r = new MatchResult { Config = new MatchConfig { mode = GameMode.QuickMatch }, Winner = winner, LongestRally = 12 };
            r.Points[0] = 7; r.Points[1] = 5;
            r.Hits[0] = 40; r.Hits[1] = 38;
            r.Smashes[0] = 2; r.Smashes[1] = 4;
            return r;
        }

        [Test]
        public void AVoidMatchGivesNothingToAnybody()
        {
            for (int local = 0; local < 2; local++)
            {
                SaveData d = SaveData.CreateNew(0);
                Economy eco = new Economy(d, new EconomyTuning());
                int coins = eco.Coins, played = d.stats.matchesPlayed, hits = d.stats.totalHits;
                RewardSummary s = LanRewards.ForMatch(Result(local), local, LanOutcome.Void, d, eco);
                Assert.AreEqual(0, s.Coins);
                Assert.AreEqual(coins, eco.Coins, "no coins");
                Assert.AreEqual(played, d.stats.matchesPlayed, "not even counted as played");
                Assert.AreEqual(hits, d.stats.totalHits);
                Assert.AreEqual(0, d.stats.lanMatches);
                Assert.AreEqual(0, s.Achievements.Count);
            }
        }

        [Test]
        public void LeavingGivesTheWinToTheOneWhoStaysAndEachPhoneCountsItsOwnPlayer()
        {
            EconomyTuning t = new EconomyTuning();
            // The guest (player 2) stays after the host leaves, although the host was ahead 7-5.
            SaveData stay = SaveData.CreateNew(0);
            Economy ecoStay = new Economy(stay, t);
            RewardSummary s = LanRewards.ForMatch(Result(0), 1, LanOutcome.OpponentLeft, stay, ecoStay);
            Assert.GreaterOrEqual(s.Coins, t.lanWin, "the one who stays wins");
            Assert.AreEqual(1, stay.stats.lanWins);
            Assert.AreEqual(5, stay.stats.pointsWon, "its own points (player 2)");
            Assert.AreEqual(38, stay.stats.totalHits);
            Assert.AreEqual(4, stay.stats.smashes);
            // The host who left: a loss, counted, without coins.
            SaveData left = SaveData.CreateNew(0);
            Economy ecoLeft = new Economy(left, t);
            int before = ecoLeft.Coins;
            RewardSummary leftSum = LanRewards.ForMatch(Result(0), 0, LanOutcome.LocalLeft, left, ecoLeft);
            int milestones = 0;
            for (int i = 0; i < leftSum.Achievements.Count; i++) milestones += leftSum.Achievements[i].Reward;
            Assert.AreEqual(before + milestones, ecoLeft.Coins, "the match itself earns nothing; only achievements already reached pay (they are milestones, not this match's prize)");
            Assert.IsFalse(leftSum.Lines.Contains("Victoria en LAN") || leftSum.Lines.Contains("Premio por participar"));
            Assert.AreEqual(1, left.stats.lanMatches);
            Assert.AreEqual(0, left.stats.lanWins);
            // A normal finish: the guest wins only if player 2 won.
            SaveData g = SaveData.CreateNew(0);
            Economy ecoG = new Economy(g, t);
            Assert.GreaterOrEqual(LanRewards.ForMatch(Result(1), 1, LanOutcome.Finished, g, ecoG).Coins, t.lanWin);
            SaveData g2 = SaveData.CreateNew(0);
            Economy ecoG2 = new Economy(g2, t);
            RewardSummary lost = LanRewards.ForMatch(Result(0), 1, LanOutcome.Finished, g2, ecoG2);
            Assert.AreEqual(0, g2.stats.lanWins);
            Assert.GreaterOrEqual(lost.Coins, t.lanLoss, "the loser still gets the participation coins");
            Assert.Less(lost.Coins, t.lanWin);
        }
    }
}
