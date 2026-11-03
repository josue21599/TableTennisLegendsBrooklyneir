using Brooklyneir.Config;
using Brooklyneir.Controls;
using Brooklyneir.Gameplay;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class ShotDiagnosticsTests
    {
        static ShotTuning Tuning()
        {
            BalanceConfig c = new BalanceConfig();
            c.Validate();
            return c.shots;
        }

        [Test]
        public void FastShotAtALowBallSaysTheBallWasTooLowForASmash()
        {
            ShotTuning s = Tuning();
            // Strong straight stroke, ball 0,20 m above the table: smash needs smashMinHeight.
            string text = ShotDiagnostics.Describe(0, ShotType.Fast, 5f, 0.2f, 0.20f, s);
            StringAssert.Contains("RÁPIDO", text);
            StringAssert.Contains("remate: bola a 0,20 m", text);
            Assert.AreEqual("bola a 0,20 m, mínimo " + s.smashMinHeight.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ','),
                ShotDiagnostics.SmashMissing(5f, 2f, 0.20f, s));
        }

        [Test]
        public void SpinNeedsASidewaysStrokeFastEnough()
        {
            ShotTuning s = Tuning();
            StringAssert.Contains("más de lado", ShotDiagnostics.SpinMissing(0.5f, 10f, s));
            StringAssert.Contains("de lado 1,00 m/s", ShotDiagnostics.SpinMissing(-1f, 90f, s));
            StringAssert.Contains("globo", ShotDiagnostics.SpinMissing(1f, 150f, s));
            StringAssert.Contains("ganó otro tipo", ShotDiagnostics.SpinMissing(s.spinSwipeThreshold + 1f, 90f, s));
        }

        [Test]
        public void TheExplanationMatchesTheRealClassification()
        {
            ShotTuning s = Tuning();
            float[] fwds = { -3f, -1f, 0f, 1f, 2.5f, 4.5f, 6f };
            float[] lats = { -3f, -1f, 0f, 1f, 3f };
            float[] heights = { 0.1f, 0.5f };
            for (int i = 0; i < fwds.Length; i++)
                for (int j = 0; j < lats.Length; j++)
                    for (int k = 0; k < heights.Length; k++)
                    {
                        float f = fwds[i], l = lats[j], h = heights[k];
                        float angle = ShotModel.SwingAngle(f, l);
                        bool smashOk = ShotDiagnostics.SmashMissing(f, angle, h, s).StartsWith("cumple");
                        bool spinOk = ShotDiagnostics.SpinMissing(l, angle, s).StartsWith("cumple");
                        bool fastOk = ShotDiagnostics.FastMissing(f, angle, s).StartsWith("cumple");
                        // Same conditions as ShotModel.Classify.
                        Assert.AreEqual(f >= s.smashSwipeThreshold && angle <= s.smashMaxAngle && h >= s.smashMinHeight, smashOk, "smash f=" + f + " l=" + l + " h=" + h);
                        Assert.AreEqual(angle > s.spinMinAngle && angle < s.defensiveMinAngle && System.Math.Abs(l) >= s.spinSwipeThreshold, spinOk, "spin f=" + f + " l=" + l);
                        Assert.AreEqual(f >= s.fastSwipeThreshold && angle <= s.spinMinAngle, fastOk, "fast f=" + f + " l=" + l);
                    }
        }
    }

    [TestFixture]
    public class MousePointerTests
    {
        [Test]
        public void AQuickClickInsideOneFrameIsAlsoReleased()
        {
            MousePointer.Phases p = MousePointer.FromButton(true, true, false, false);
            Assert.IsTrue(p.Began && p.Ended && !p.Moved, "pressed and released in the same frame: both phases");
            p = MousePointer.FromButton(true, false, true, false);
            Assert.IsTrue(p.Began && !p.Ended);
            p = MousePointer.FromButton(false, false, true, true);
            Assert.IsTrue(p.Moved && !p.Began && !p.Ended);
            p = MousePointer.FromButton(false, true, false, true);
            Assert.IsTrue(p.Ended && !p.Began);
        }

        [Test]
        public void TheMouseNeverStaysStuckToAPlayer()
        {
            // Release lost (button up, but the router still owns the pointer): release it now.
            MousePointer.Phases p = MousePointer.FromButton(false, false, false, true);
            Assert.IsTrue(p.Ended && !p.Moved);
            // Nothing tracked and button up: nothing to do.
            p = MousePointer.FromButton(false, false, false, false);
            Assert.IsFalse(p.Began || p.Moved || p.Ended);
            // Dragging something the router rejected (e.g. started on a button): no movement.
            p = MousePointer.FromButton(false, false, true, false);
            Assert.IsFalse(p.Moved);
            // With the router, two-player mode: a quick click on the right half (pressed and
            // released in one frame) must free the mouse, so the next click on the left half
            // goes to player 1. Before the fix the release was lost and it stayed with player 2.
            TouchZoneRouter r = new TouchZoneRouter();
            p = MousePointer.FromButton(true, true, false, r.OwnerOf(-1) >= 0);
            if (p.Began) Assert.AreEqual(1, r.Began(-1, 1500f, 1920f, false));
            if (p.Ended) r.Ended(-1);
            Assert.AreEqual(0, r.Began(-1, 300f, 1920f, false), "left half belongs to player 1 again");
        }
    }
}
