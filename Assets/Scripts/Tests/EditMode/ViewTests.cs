using Brooklyneir.CameraSystem;
using Brooklyneir.Config;
using Brooklyneir.Controls;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Physics;
using Brooklyneir.Rackets;
using NUnit.Framework;
using UnityEngine;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class ViewTests
    {
        BalanceConfig _cfg;

        [SetUp]
        public void Setup()
        {
            _cfg = new BalanceConfig();
            _cfg.Validate();
        }

        static TouchZoneRouter RouterFor(PlayerControl[] c)
        {
            TouchZoneRouter r = new TouchZoneRouter();
            r.SetHumanControlled(0, ControlSetup.IsLocal(c[0]));
            r.SetHumanControlled(1, ControlSetup.IsLocal(c[1]));
            return r;
        }

        [Test]
        public void TheOnlyLocalPlayerUsesTheWholeScreenAndTheGuestsCameraIsTurned()
        {
            MatchConfig quick = new MatchConfig { mode = GameMode.QuickMatch };
            quick.Validate();
            // LAN host (player 1 local, player 2 on the other phone).
            PlayerControl[] host = ControlSetup.For(quick, 1);
            TouchZoneRouter h = RouterFor(host);
            Assert.AreEqual(0, h.Began(1, 100f, 1920f, false), "host: left half");
            h.Ended(1);
            Assert.AreEqual(0, h.Began(2, 1800f, 1920f, false), "host: right half too");
            Assert.AreEqual(ViewSide.PlusX, ViewMapping.SideFor(host));
            // LAN guest (player 2 local, player 1 on the other phone).
            PlayerControl[] guest = ControlSetup.For(quick, 0);
            TouchZoneRouter g = RouterFor(guest);
            Assert.AreEqual(1, g.Began(3, 100f, 1920f, false), "guest: left half");
            g.Ended(3);
            Assert.AreEqual(1, g.Began(4, 1800f, 1920f, false), "guest: right half too");
            Assert.AreEqual(ViewSide.MinusX, ViewMapping.SideFor(guest), "the guest's camera is turned 180°");
            // Two players on one phone keep a half each and the normal camera.
            PlayerControl[] local = ControlSetup.For(quick);
            TouchZoneRouter two = RouterFor(local);
            Assert.AreEqual(0, two.Began(5, 100f, 1920f, false));
            Assert.AreEqual(1, two.Began(6, 1800f, 1920f, false));
            Assert.AreEqual(ViewSide.PlusX, ViewMapping.SideFor(local));
            // The editor's "ver como invitado" gives the same controls and view as the LAN guest.
            MatchConfig cpu = new MatchConfig { mode = GameMode.VersusCpu, racketP1 = "mine", racketP2 = "theirs" };
            cpu.Validate();
            MatchConfig asGuest = ControlSetup.AsSecondPlayer(cpu);
            asGuest.Validate();
            PlayerControl[] dbg = ControlSetup.For(asGuest);
            Assert.AreEqual(PlayerControl.Cpu, dbg[0]);
            Assert.AreEqual(PlayerControl.Local, dbg[1]);
            Assert.AreEqual(ViewSide.MinusX, ViewMapping.SideFor(dbg));
            Assert.AreEqual("mine", asGuest.racketP2, "you keep your racket");
            Assert.AreEqual(GameMode.VersusCpu, cpu.mode, "the original configuration is not touched");
        }

        /// <summary>
        /// A stroke drawn on the screen (towards the opponent and up), turned into a world velocity
        /// for that camera side, through the real shot model: where does the ball land on that screen?
        /// </summary>
        Vector2 LandingOnScreen(int player, ViewSide side, float screenUp)
        {
            ShotModel m = new ShotModel(_cfg, new GameRandom(1));
            float toward = 3.0f; // m/s towards the opponent on screen (to the right: you are on the left)
            Vector3 velocity = ViewMapping.ScreenToWorld(new Vector2(toward, screenUp), side);
            ShotContext c = new ShotContext();
            c.Hitter = player;
            c.ContactPoint = new Vector3(0f, TableGeometry.TopY + 0.3f, -TableGeometry.ForwardSign(player) * 1.7f);
            c.RacketVelocity = velocity;
            c.Stats = new RacketStatModel(new RacketStats(5, 7, 5, 5, 6), _cfg.racket);
            c.SwingMultiplier = 1f;
            c.NoiseMultiplier = 0.0001f;
            ShotResult r = m.Compute(c);
            return ViewMapping.WorldToScreen(r.IntendedTarget, side);
        }

        [Test]
        public void HostTheBallGoesWhereThePlayerMovesTheRacketOnTheScreen()
        {
            Vector2 up = LandingOnScreen(0, ViewSide.PlusX, 1.5f), down = LandingOnScreen(0, ViewSide.PlusX, -1.5f);
            Assert.Greater(up.x, 0f, "towards the opponent: the right half of the host's screen");
            Assert.Greater(up.y, 0.1f, "racket moved up on the screen: the ball lands up on the screen");
            Assert.Less(down.y, -0.1f, "racket moved down: the ball lands down");
            // ViewMapping agrees with the camera's own maths (LookRotation: right = up × forward).
            Vector3 forward = new Vector3(-ViewMapping.CameraXSign(ViewSide.PlusX), 0f, 0f);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Assert.IsTrue(right == ViewMapping.ScreenRight(ViewSide.PlusX));
            Assert.AreEqual(0f, Vector3.Dot(ViewMapping.ScreenUp(ViewSide.PlusX), right), 1e-6f);
            Assert.Greater(Vector3.Dot(ViewMapping.ScreenUp(ViewSide.PlusX), forward), 0f, "screen up = away from the camera");
        }

        [Test]
        public void GuestWithTheCameraTurnedTheBallStillGoesWhereThePlayerMovesTheRacket()
        {
            Vector2 up = LandingOnScreen(1, ViewSide.MinusX, 1.5f), down = LandingOnScreen(1, ViewSide.MinusX, -1.5f);
            Assert.Greater(up.x, 0f, "towards the opponent: the right half of the guest's screen");
            Assert.Greater(up.y, 0.1f, "racket moved up on the guest's screen: the ball lands up on the guest's screen");
            Assert.Less(down.y, -0.1f, "racket moved down: the ball lands down");
            Vector3 forward = new Vector3(-ViewMapping.CameraXSign(ViewSide.MinusX), 0f, 0f);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Assert.IsTrue(right == ViewMapping.ScreenRight(ViewSide.MinusX), "turned camera: right is −z, player 2 on the left");
            Assert.Greater(Vector3.Dot(ViewMapping.ScreenUp(ViewSide.MinusX), forward), 0f);
            // Same stroke, same physical result: the guest's view is the host's mirrored by 180°.
            Vector2 hostUp = LandingOnScreen(0, ViewSide.PlusX, 1.5f);
            Assert.AreEqual(hostUp.x, up.x, 1e-4f);
            Assert.AreEqual(hostUp.y, up.y, 1e-4f);
        }
    }
}
