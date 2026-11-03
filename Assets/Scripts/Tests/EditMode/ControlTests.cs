using Brooklyneir.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class ControlSetupTests
    {
        static MatchConfig Config(GameMode mode, bool p1Ai = false, bool p2Ai = false)
        {
            MatchConfig c = new MatchConfig();
            c.mode = mode;
            c.player1IsAI = p1Ai;
            c.player2IsAI = p2Ai;
            c.Validate();
            return c;
        }

        /// <summary>The rules the match used before ControlSetup existed (Human[] and the AI creation).</summary>
        static void OldRules(MatchConfig c, out bool human0, out bool human1, out bool ai0, out bool ai1)
        {
            bool training = c.mode == GameMode.Training;
            human0 = !c.player1IsAI && c.mode != GameMode.Demo;
            human1 = !c.player2IsAI && c.mode != GameMode.Demo && !training;
            ai0 = !human0;
            ai1 = !(human1 || training);
        }

        [Test]
        public void EveryModeGetsTheSameControlsAsBefore()
        {
            GameMode[] modes = { GameMode.QuickMatch, GameMode.VersusCpu, GameMode.Tournament, GameMode.Training, GameMode.Custom, GameMode.Demo };
            for (int m = 0; m < modes.Length; m++)
                for (int a = 0; a < 4; a++)
                {
                    MatchConfig c = Config(modes[m], (a & 1) != 0, (a & 2) != 0);
                    bool h0, h1, ai0, ai1;
                    OldRules(c, out h0, out h1, out ai0, out ai1);
                    PlayerControl[] got = ControlSetup.For(c);
                    string where = modes[m] + " p1Ai=" + c.player1IsAI + " p2Ai=" + c.player2IsAI;
                    Assert.AreEqual(h0, got[0] == PlayerControl.Local, where + " J1 local");
                    Assert.AreEqual(ai0, got[0] == PlayerControl.Cpu, where + " J1 CPU");
                    Assert.AreEqual(h1, got[1] == PlayerControl.Local, where + " J2 local");
                    Assert.AreEqual(ai1, got[1] == PlayerControl.Cpu, where + " J2 CPU");
                }
        }

        [Test]
        public void DemoIsCpuAgainstCpuAndTrainingHasNoSecondPlayer()
        {
            PlayerControl[] demo = ControlSetup.For(Config(GameMode.Demo));
            Assert.AreEqual(PlayerControl.Cpu, demo[0]);
            Assert.AreEqual(PlayerControl.Cpu, demo[1]);
            PlayerControl[] training = ControlSetup.For(Config(GameMode.Training, false, true));
            Assert.AreEqual(PlayerControl.Local, training[0]);
            Assert.AreEqual(PlayerControl.None, training[1]);
            PlayerControl[] cpu = ControlSetup.For(Config(GameMode.VersusCpu));
            Assert.AreEqual(PlayerControl.Local, cpu[0]);
            Assert.AreEqual(PlayerControl.Cpu, cpu[1]);
        }

        [Test]
        public void RemotePlayerOnlyInQuickAndCustomMatches()
        {
            Assert.AreEqual(PlayerControl.Remote, ControlSetup.For(Config(GameMode.QuickMatch), 1)[1]);
            Assert.AreEqual(PlayerControl.Local, ControlSetup.For(Config(GameMode.QuickMatch), 1)[0]);
            Assert.AreEqual(PlayerControl.Remote, ControlSetup.For(Config(GameMode.Custom), 0)[0]);
            GameMode[] noLan = { GameMode.VersusCpu, GameMode.Tournament, GameMode.Training, GameMode.Demo };
            for (int m = 0; m < noLan.Length; m++)
                for (int p = -1; p <= 1; p++)
                {
                    PlayerControl[] c = ControlSetup.For(Config(noLan[m]), p);
                    Assert.IsTrue(c[0] != PlayerControl.Remote && c[1] != PlayerControl.Remote, noLan[m] + " must never have a network player");
                }
            PlayerControl[] bad = ControlSetup.For(Config(GameMode.QuickMatch), 5);
            Assert.IsTrue(bad[0] != PlayerControl.Remote && bad[1] != PlayerControl.Remote, "invalid index ignored");
        }
    }

    [TestFixture]
    public class RemotePaddleBufferTests
    {
        [Test]
        public void InterpolatesBetweenTheTwoSamplesAroundTheRenderTime()
        {
            RemotePaddleBuffer b = new RemotePaddleBuffer();
            Vector3 p;
            Assert.IsFalse(b.TrySample(1.0, out p), "nothing received yet");
            b.Push(1, 1.00, new Vector3(0f, 0.95f, 1f));
            b.Push(2, 1.05, new Vector3(0.5f, 0.95f, 1f));
            b.Push(3, 1.10, new Vector3(0.5f, 0.95f, 2f));
            Assert.IsTrue(b.TrySample(1.025, out p));
            Assert.AreEqual(0.25f, p.x, 1e-4f);
            Assert.IsTrue(b.TrySample(1.075, out p));
            Assert.AreEqual(1.5f, p.z, 1e-4f);
            Assert.IsTrue(b.TrySample(0.5, out p));
            Assert.AreEqual(0f, p.x, 1e-6f, "before the oldest sample: hold it");
        }

        [Test]
        public void RepeatedAndOutOfOrderPacketsAreDropped()
        {
            RemotePaddleBuffer b = new RemotePaddleBuffer();
            Assert.IsTrue(b.Push(10, 2.00, new Vector3(1f, 0f, 0f)));
            Assert.IsFalse(b.Push(10, 2.03, new Vector3(9f, 0f, 0f)), "same sequence");
            Assert.IsFalse(b.Push(9, 1.97, new Vector3(9f, 0f, 0f)), "older sequence");
            Assert.IsTrue(b.Push(12, 2.06, new Vector3(2f, 0f, 0f)), "a lost packet in between is fine");
            Assert.IsFalse(b.Push(11, 2.03, new Vector3(9f, 0f, 0f)), "arrived late, after a newer one");
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(3, b.Dropped);
            // Sequence numbers wrap around without breaking the order.
            RemotePaddleBuffer w = new RemotePaddleBuffer();
            Assert.IsTrue(w.Push(uint.MaxValue, 1.0, Vector3.zero));
            Assert.IsTrue(w.Push(0u, 1.03, Vector3.one));
            Vector3 p;
            w.TrySample(5.0, out p);
            Assert.IsTrue(p.x < 9f, "no absurd value after wrap-around");
        }

        [Test]
        public void ExtrapolationPastTheNewestSampleIsLimited()
        {
            RemotePaddleBuffer b = new RemotePaddleBuffer();
            b.MaxExtrapolation = 0.1f;
            b.Push(1, 1.00, new Vector3(0f, 0f, 0f));
            b.Push(2, 1.05, new Vector3(0.1f, 0f, 0f)); // 2 m/s
            Vector3 p;
            b.TrySample(1.10, out p);
            Assert.AreEqual(0.2f, p.x, 1e-4f, "0.05 s ahead");
            b.TrySample(3.00, out p);
            Assert.AreEqual(0.3f, p.x, 1e-4f, "never more than 0.1 s ahead, even if packets stop");
        }

        [Test]
        public void ALongGapIsNotSlidAcross()
        {
            RemotePaddleBuffer b = new RemotePaddleBuffer();
            b.MaxGap = 0.25f;
            b.Push(1, 1.0, new Vector3(-0.5f, 0f, 0f));
            b.Push(2, 2.0, new Vector3(0.5f, 0f, 0f)); // 1 s without packets
            Vector3 p;
            b.TrySample(1.5, out p);
            Assert.AreEqual(-0.5f, p.x, 1e-6f, "waits at the last known position");
            b.TrySample(2.0, out p);
            Assert.AreEqual(0.5f, p.x, 1e-6f, "then jumps cleanly");
            b.TrySample(2.5, out p);
            Assert.AreEqual(0.5f, p.x, 1e-6f, "no extrapolation from samples across a gap");
            b.Clear();
            Assert.AreEqual(0, b.Count);
            Assert.IsTrue(b.Push(1, 0.1, Vector3.zero), "clear also forgets the last sequence");
        }
    }
}
