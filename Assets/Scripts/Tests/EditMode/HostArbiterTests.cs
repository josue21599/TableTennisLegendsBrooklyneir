using Brooklyneir.Gameplay;
using Brooklyneir.Net;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class HostArbiterTests
    {
        const int Host = 0, Guest = 1, Grace = 48; // 48 ticks = 200 ms

        /// <summary>Host serves; the ball bounces once on the guest's half (tick 100), then a second time (tick 130).</summary>
        static HostArbiter DoubleBounceOnGuestSide()
        {
            HostArbiter a = new HostArbiter(Guest, Grace);
            a.StartRally(Host);
            a.OnTableBounce(Guest, 100);
            Assert.IsFalse(a.PointPending);
            a.OnTableBounce(Guest, 130);
            return a;
        }

        [Test]
        public void AGuestHitArrivingWithinTheGraceUndoesThePendingPoint()
        {
            HostArbiter a = DoubleBounceOnGuestSide();
            Assert.IsTrue(a.PointPending, "second bounce on the guest's side: point pending, not confirmed");
            Assert.IsFalse(a.PointConfirmed);
            Assert.AreEqual(Host, a.Winner);
            Assert.IsFalse(a.Update(140), "still inside the grace");
            Assert.AreEqual(GuestHitDecision.Accepted, a.GuestHit(120), "the guest hit at tick 120, between the two bounces");
            Assert.IsFalse(a.PointPending);
            Assert.IsFalse(a.Referee.RallyOver, "the rally goes on");
            Assert.AreEqual(Guest, a.Referee.LastHitter);
            Assert.AreEqual(1, a.Referee.HitCount);
            Assert.IsFalse(a.Update(1000), "nothing is confirmed later");
        }

        [Test]
        public void ALateGuestHitDoesNotUndoAConfirmedPoint()
        {
            HostArbiter a = DoubleBounceOnGuestSide();
            Assert.IsFalse(a.Update(130 + Grace - 1));
            Assert.IsTrue(a.Update(130 + Grace), "grace over: point confirmed");
            Assert.IsTrue(a.PointConfirmed);
            Assert.AreEqual(GuestHitDecision.RejectedPointConfirmed, a.GuestHit(120));
            Assert.AreEqual(Host, a.Winner);
            Assert.AreEqual(PointReason.DoubleBounce, a.Reason);
        }

        [Test]
        public void AGuestHitAfterTheSecondBounceOrBeforeTheFirstIsRejected()
        {
            HostArbiter a = DoubleBounceOnGuestSide();
            Assert.AreEqual(GuestHitDecision.RejectedNotAllowed, a.GuestHit(131), "contact after the second bounce");
            Assert.AreEqual(GuestHitDecision.RejectedNotAllowed, a.GuestHit(90), "volley before the first bounce");
            Assert.IsTrue(a.PointPending, "the point is still pending");
            Assert.AreEqual(GuestHitDecision.RejectedNotAllowed, a.GuestHit(130), "same tick as the second bounce: the bounce counts as first, so the hit is late");
            Assert.AreEqual(GuestHitDecision.Accepted, a.GuestHit(129), "one tick earlier the hit is valid");
        }

        [Test]
        public void TheRefereeCopyRestoresEverythingAndHostSidePointsAreImmediate()
        {
            RallyReferee r = new RallyReferee();
            r.StartRally(1);
            r.OnTableBounce(0);
            r.OnNetContact();
            r.RegisterHit(0);
            r.OnNetContact();
            RallyReferee c = new RallyReferee();
            c.CopyFrom(r);
            Assert.IsTrue(c.LastHitter == r.LastHitter && c.BouncesOnReceiverSide == r.BouncesOnReceiverSide && c.RallyOver == r.RallyOver &&
                          c.Winner == r.Winner && c.Reason == r.Reason && c.HitCount == r.HitCount && c.NetTouchedThisShot == r.NetTouchedThisShot);
            r.OnTableBounce(0);
            c.CopyFrom(r);
            Assert.IsTrue(c.RallyOver && c.Winner == r.Winner && c.Reason == r.Reason, "a finished rally is copied too");

            // The guest returns: the ball now heads to the host, whose hits are local. A double
            // bounce on the host's side is confirmed at once, without grace.
            HostArbiter a = new HostArbiter(Guest, Grace);
            a.StartRally(Host);
            a.OnTableBounce(Guest, 100);
            Assert.AreEqual(GuestHitDecision.Accepted, a.GuestHit(110));
            Assert.IsFalse(a.HostHit(Host), "the host cannot hit before the ball bounces on its side");
            a.OnTableBounce(Host, 200);
            Assert.IsTrue(a.HostHit(Host), "after the bounce it can");
            a.OnTableBounce(Guest, 300);
            Assert.AreEqual(GuestHitDecision.Accepted, a.GuestHit(305));
            a.OnTableBounce(Host, 400);
            a.OnTableBounce(Host, 420);
            Assert.IsTrue(a.PointConfirmed, "on the host's side the point is immediate");
            Assert.IsFalse(a.PointPending);
            Assert.AreEqual(Guest, a.Winner);
            Assert.AreEqual(3, a.Referee.HitCount);
        }

        [Test]
        public void TheGuestNeverShowsADifferentScoreFromTheHost()
        {
            System.Random rnd = new System.Random(3);
            for (int match = 0; match < 50; match++)
            {
                MatchScore host = new MatchScore(11, 2, true, match % 2);
                ScoreMirror guest = new ScoreMirror(match % 2);
                int points = 0;
                while (!host.MatchOver && points < 500)
                {
                    int winner = rnd.Next(2);
                    ScoreEvent ev = host.AwardPoint(winner);
                    PointResultMsg m = ScoreMirror.Build(host, ev, PointReason.MissedReturn, points * 3f);
                    // Through the real protocol, as on the network.
                    byte[] buf = new byte[NetProtocol.MaxPacket];
                    NetWriter w = new NetWriter(buf);
                    NetProtocol.Write(w, m);
                    PointResultMsg back;
                    Assert.IsTrue(NetProtocol.TryRead(new NetReader(buf, w.Length), out back));
                    guest.Apply(back);
                    points++;
                    Assert.IsTrue(guest.Points[0] == host.Points[0] && guest.Points[1] == host.Points[1], "points after point " + points);
                    Assert.IsTrue(guest.Sets[0] == host.Sets[0] && guest.Sets[1] == host.Sets[1], "sets");
                    Assert.AreEqual(host.Server, guest.Server, "next server");
                    Assert.AreEqual(host.MatchOver, guest.MatchOver);
                    Assert.AreEqual(winner, guest.LastWinner);
                }
                Assert.IsTrue(host.MatchOver && guest.MatchOver && guest.MatchWinner == host.MatchWinner, "same winner (match " + match + ")");
            }
        }
    }
}
