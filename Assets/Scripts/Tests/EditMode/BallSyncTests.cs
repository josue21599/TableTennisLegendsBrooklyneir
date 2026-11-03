using System.Collections.Generic;
using Brooklyneir.Config;
using Brooklyneir.Net;
using Brooklyneir.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class BallSyncTests
    {
        PhysicsConfig _p;

        [SetUp]
        public void Setup()
        {
            BalanceConfig c = new BalanceConfig();
            c.Validate();
            _p = c.physics;
        }

        /// <summary>A rally-like shot from player 1's side towards player 2.</summary>
        static NetBallState Shot(float vx = 0.6f)
        {
            NetBallState s = new NetBallState();
            s.Position = new Vector3(0.1f, TableGeometry.TopY + 0.3f, -1.6f);
            s.Velocity = new Vector3(vx, 1.4f, 6.2f);
            s.Spin = new Vector3(0.8f, -1.5f, 0f);
            s.Active = true;
            return s;
        }

        /// <summary>Exact, component by component (Unity's Vector3 == is approximate).</summary>
        static bool Exact(Vector3 a, Vector3 b) { return a.x == b.x && a.y == b.y && a.z == b.z; }

        static bool Same(BallSimulation a, BallSimulation b)
        {
            return Exact(a.Position, b.Position) && Exact(a.Velocity, b.Velocity) && Exact(a.SpinAccel, b.SpinAccel) && a.Active == b.Active;
        }

        [Test]
        public void ASmallNumericDifferenceIsGoneAfterTheNextHit()
        {
            BallSync host = new BallSync(_p), guest = new BallSync(_p);
            NetBallState serve = Shot();
            host.Apply(serve, 1000, 1000, 1f, 1f, null);
            NetBallState onGuest = serve;
            onGuest.Velocity += new Vector3(1e-4f, 0f, 0f); // another CPU/libm rounding differently
            guest.Apply(onGuest, 1000, 1000, 1f, 1f, null);

            host.AdvanceTo(1100, 1f, 1f, null);
            guest.AdvanceTo(1100, 1f, 1f, null);
            float drift = (host.Sim.Position - guest.Sim.Position).magnitude;
            Assert.Greater(drift, 1e-6f, "during the flight the two balls drift apart");
            Assert.Greater(drift, 1e-5f, "far enough to matter (and above Unity's approximate Vector3 equality)");
            Assert.IsFalse(Same(host.Sim, guest.Sim));

            // Player 2 (on the host here) hits at tick 1100: new state, sent with its tick.
            NetBallState hit = host.Capture();
            hit.Velocity = new Vector3(-0.4f, 1.2f, -6.5f);
            hit.Spin = new Vector3(-0.5f, 1.0f, 0f);
            host.Apply(hit, 1100, 1100, 1f, 1f, null);
            host.AdvanceTo(1107, 1f, 1f, null);            // the message takes 7 ticks (~29 ms)
            guest.AdvanceTo(1107, 1f, 1f, null);           // meanwhile the guest's old ball kept flying
            guest.Apply(hit, 1100, 1107, 1f, 1f, null);    // HitLaunch arrives
            Assert.IsTrue(Same(host.Sim, guest.Sim), "after the hit both balls are bit-for-bit identical");
            Assert.AreEqual(host.Tick, guest.Tick);
            host.AdvanceTo(1300, 1f, 1f, null);
            guest.AdvanceTo(1300, 1f, 1f, null);
            Assert.IsTrue(Same(host.Sim, guest.Sim), "and they stay identical afterwards");
        }

        [Test]
        public void TheSameStateGivesTheSameBallAt60_90And120Hz()
        {
            float[] rates = { 60f, 90f, 120f };
            BallSync[] devices = new BallSync[rates.Length];
            System.Random jitter = new System.Random(5);
            const uint start = 500, end = start + 240 * 2;
            for (int d = 0; d < rates.Length; d++)
            {
                devices[d] = new BallSync(_p);
                devices[d].Apply(Shot(), start, start, 1f, 1f, null);
                double t = start / 240.0;
                while (devices[d].Tick < end)
                {
                    t += (1.0 / rates[d]) * (0.8 + 0.4 * jitter.NextDouble()); // irregular frames
                    uint target = (uint)System.Math.Min(end, System.Math.Floor(t * 240.0));
                    devices[d].AdvanceTo(target, 1f, 1f, null);
                }
            }
            Assert.IsTrue(Same(devices[0].Sim, devices[1].Sim) && Same(devices[0].Sim, devices[2].Sim),
                "a 60, a 90 and a 120 Hz phone end with the identical ball at the same tick");
        }

        [Test]
        public void TheFastForwardPutsTheBallWhereTheOtherPhoneHasIt()
        {
            BallSync sender = new BallSync(_p), receiver = new BallSync(_p);
            List<BallContactEvent> senderEvents = new List<BallContactEvent>(), receiverEvents = new List<BallContactEvent>();
            NetBallState shot = Shot();
            sender.Apply(shot, 2000, 2000, 1f, 1f, senderEvents);
            sender.AdvanceTo(2000 + 120, 1f, 1f, senderEvents); // half a second: the ball bounces meanwhile
            Assert.Greater(senderEvents.Count, 0, "the sender saw the bounce");
            receiver.Sim.Hold(new Vector3(0f, 1.2f, 1f));
            receiver.SetTick(2000 + 120);
            receiver.Apply(shot, 2000, 2000 + 120, 1f, 1f, receiverEvents); // a very late message
            Assert.IsTrue(Same(sender.Sim, receiver.Sim), "same position, velocity and spin at the same tick");
            Assert.AreEqual(senderEvents.Count, receiverEvents.Count, "the receiver also gets the bounce, for sound and rules");
            Assert.AreEqual(senderEvents[0].Type, receiverEvents[0].Type);
            Assert.AreEqual(senderEvents[0].Side, receiverEvents[0].Side);
            // The drawn ball does not teleport: it starts where it was and slides in.
            float jump = receiver.VisualOffset.magnitude;
            Assert.Greater(jump, 0.01f);
            for (int f = 0; f < 30; f++) receiver.UpdateVisual(1f / 60f);
            Assert.Less(receiver.VisualOffset.magnitude, jump * 0.01f, "after half a second less than 1 % of the jump is left");
            for (int f = 0; f < 30; f++) receiver.UpdateVisual(1f / 60f);
            Assert.Less(receiver.VisualOffset.magnitude, 0.001f, "after one second the drawing has caught up (< 1 mm)");
            // A state from the "future" waits for the clock instead of running ahead.
            BallSync early = new BallSync(_p);
            early.Apply(Shot(), 3000, 2990, 1f, 1f, null);
            Assert.AreEqual(3000u, early.Tick);
            Assert.AreEqual(0, early.AdvanceTo(2995, 1f, 1f, null));
            Assert.IsTrue(Exact(Shot().Position, early.Sim.Position));
        }

        [Test]
        public void SlowMotionByTicksMatchesOnBothPhonesAndOnlySlowsTheBallHeadingToTheSlowedPlayer()
        {
            BallSync a = new BallSync(_p), b = new BallSync(_p), normal = new BallSync(_p);
            NetBallState shot = Shot(); // heading to player 2 (+z)
            a.Apply(shot, 0, 0, 1f, 0.5f, null);
            b.Apply(shot, 0, 0, 1f, 0.5f, null);
            normal.Apply(shot, 0, 0, 1f, 1f, null);
            a.AdvanceTo(60, 1f, 0.5f, null);
            for (uint t = 1; t <= 60; t++) b.AdvanceTo(t, 1f, 0.5f, null); // one tick per call
            normal.AdvanceTo(60, 1f, 1f, null);
            Assert.IsTrue(Same(a.Sim, b.Sim), "both phones slow the ball identically");
            float slowTravel = a.Sim.Position.z - shot.Position.z, normalTravel = normal.Sim.Position.z - shot.Position.z;
            Assert.AreEqual(0.5f, slowTravel / normalTravel, 0.02f, "half speed towards the slowed player");
            // A ball heading to player 1 is not slowed by player 2's slow-motion.
            NetBallState back = shot;
            back.Velocity = new Vector3(0f, 1.4f, -6.2f);
            BallSync c = new BallSync(_p), d = new BallSync(_p);
            c.Apply(back, 0, 0, 1f, 0.5f, null);
            d.Apply(back, 0, 0, 1f, 1f, null);
            c.AdvanceTo(60, 1f, 0.5f, null);
            d.AdvanceTo(60, 1f, 1f, null);
            Assert.IsTrue(Same(c.Sim, d.Sim));
            Assert.AreEqual(240, a.TickRate);
        }
    }
}
