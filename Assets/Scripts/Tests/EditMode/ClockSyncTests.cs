using System;
using Brooklyneir.Net;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class ClockSyncTests
    {
        const int Rate = 240;

        /// <summary>One Ping/Pong exchange between a guest clock and a host clock that is offset seconds ahead.</summary>
        static void Exchange(ClockSync c, double guestNow, double offset, double up, double down)
        {
            double t0 = guestNow;
            double t1 = guestNow + up + offset;      // host clock when the ping arrives
            double t2 = guestNow + up + down;        // guest clock when the pong arrives
            c.AddSample(t0, t1, t2);
        }

        [Test]
        public void SymmetricDelayGivesTheExactOffsetAndTheHostsTick()
        {
            ClockSync guest = new ClockSync(Rate);
            ClockSync host = ClockSync.ForHost(Rate);
            const double offset = 5.3; // host clock is 5,3 s ahead of the guest's
            Assert.IsFalse(guest.IsSynced);
            for (int i = 0; i < 5; i++) Exchange(guest, 10.0 + i * 2, offset, 0.020, 0.020);
            Assert.IsTrue(guest.IsSynced);
            Assert.AreEqual(offset, guest.EstimatedOffset, 1e-9);
            Assert.AreEqual(0.040, guest.RoundTrip, 1e-9);
            double guestNow = 25.0;
            guest.Update(guestNow);
            uint gt = guest.Tick(guestNow);
            uint ht = host.Tick(guestNow + offset);
            Assert.LessOrEqual(Math.Abs((long)gt - (long)ht), 1, "both phones agree on the tick");
            Assert.AreEqual((uint)Math.Floor((guestNow + offset) * Rate), ht);
            Assert.AreEqual(0.0, host.RoundTrip);
            Assert.AreEqual(1.0, guest.TicksToSeconds(Rate), 1e-12);
        }

        [Test]
        public void IrregularWifiDelaysKeepTheErrorBelowHalfTheBestRoundTrip()
        {
            Random rnd = new Random(11);
            const double offset = -123.456;
            for (int run = 0; run < 200; run++)
            {
                ClockSync c = new ClockSync(Rate, 16);
                double now = 1.0;
                for (int i = 0; i < 40; i++)
                {
                    // 4–60 ms each way, asymmetric, plus occasional 150–400 ms spikes on either leg.
                    double up = 0.004 + rnd.NextDouble() * 0.056;
                    double down = 0.004 + rnd.NextDouble() * 0.056;
                    if (rnd.NextDouble() < 0.15) up += 0.15 + rnd.NextDouble() * 0.25;
                    if (rnd.NextDouble() < 0.15) down += 0.15 + rnd.NextDouble() * 0.25;
                    Exchange(c, now, offset, up, down);
                    now += 2.0;
                }
                double error = Math.Abs(c.EstimatedOffset - offset);
                Assert.LessOrEqual(error, c.RoundTrip / 2 + 1e-9, "error bounded by half the best round trip (run " + run + ")");
                Assert.Less(error, 0.03, "a few milliseconds on a normal Wi-Fi");
            }
            // Glitches are ignored: a negative round trip does not change anything.
            ClockSync g = new ClockSync(Rate);
            Exchange(g, 1.0, 2.0, 0.01, 0.01);
            g.AddSample(5.0, 7.0, 4.0);
            Assert.AreEqual(1, g.SampleCount);
            Assert.AreEqual(2.0, g.EstimatedOffset, 1e-9);
        }

        [Test]
        public void AnOffsetChangeMidMatchConvergesWithoutTheTickGoingBack()
        {
            ClockSync c = new ClockSync(Rate, 4);
            const double frame = 1.0 / 60.0;
            double now = 100.0, offset = 2.0;
            Exchange(c, now, offset, 0.01, 0.01);
            c.Update(now);
            uint last = c.Tick(now);
            double lastHost = c.HostTime(now);
            int changesSeen = 0;
            for (int f = 1; f <= 60 * 20; f++)
            {
                now += frame;
                if (f == 60 * 3) offset += 0.040;   // the clocks drift 40 ms apart...
                if (f == 60 * 9) offset -= 0.080;   // ...then 80 ms the other way
                if (f % 120 == 0) { Exchange(c, now, offset, 0.01, 0.01); changesSeen++; }
                c.Update(now);
                uint tick = c.Tick(now);
                double host = c.HostTime(now);
                Assert.GreaterOrEqual(tick, last, "the tick never goes back (frame " + f + ")");
                double rate = (host - lastHost) / frame;
                Assert.IsTrue(rate >= 0.9 - 1e-6 && rate <= 1.1 + 1e-6, "host time runs between 0.9x and 1.1x real time, was " + rate);
                last = tick;
                lastHost = host;
            }
            Assert.Greater(changesSeen, 5);
            Assert.AreEqual(offset, c.AppliedOffset, 1e-6, "converged to the final offset");
        }

        [Test]
        public void TheFirstSyncCanMoveTheTickBackOnceWhenTheGuestsClockIsAhead()
        {
            // The guest's app has been open 10 minutes longer than the host's: before syncing, its
            // ticks are far ahead. Once synced it must follow the host at once, not wait 10 minutes.
            ClockSync c = new ClockSync(Rate);
            const double offset = -600.0;
            double now = 700.0;
            c.Update(now);
            uint unsynced = c.Tick(now);
            Assert.AreEqual((uint)(700.0 * Rate), unsynced);
            Exchange(c, now, offset, 0.01, 0.01);
            now += 0.02;
            c.Update(now);
            uint synced = c.Tick(now);
            ClockSync host = ClockSync.ForHost(Rate);
            Assert.LessOrEqual(Math.Abs((long)synced - (long)host.Tick(now + offset)), 1, "follows the host right after the first sample");
            // From then on it never goes back, even when a better sample moves the estimate back a little.
            uint last = synced;
            for (int i = 0; i < 300; i++)
            {
                now += 1.0 / 60.0;
                if (i == 10) Exchange(c, now, offset - 0.05, 0.002, 0.002);
                c.Update(now);
                uint t = c.Tick(now);
                Assert.GreaterOrEqual(t, last);
                last = t;
            }
        }
    }
}
