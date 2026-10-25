using System.Collections.Generic;
using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Physics;
using Brooklyneir.Rackets;
using NUnit.Framework;
using UnityEngine;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class BallPhysicsTests
    {
        BalanceConfig _cfg;
        List<BallContactEvent> _events;

        [SetUp]
        public void Setup()
        {
            _cfg = new BalanceConfig();
            _cfg.Validate();
            _events = new List<BallContactEvent>();
        }

        BallContactEvent? FirstOf(BallContactType type)
        {
            for (int i = 0; i < _events.Count; i++) if (_events[i].Type == type) return _events[i];
            return null;
        }

        [Test]
        public void DroppedBallBouncesOnTableWithRestitution()
        {
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Launch(new Vector3(0.2f, 1.2f, -0.6f), Vector3.zero, Vector3.zero);
            sim.Step(1f, _events);
            BallContactEvent? hit = FirstOf(BallContactType.Table);
            Assert.IsTrue(hit.HasValue, "the ball must touch the table");
            Assert.AreEqual(0, hit.Value.Side);
            Assert.AreEqual(TableGeometry.TopY + _cfg.physics.ballRadius, hit.Value.Point.y, 0.002f);
            // Bounce height check: simulate up to the apex after the first bounce.
            BallSimulation sim2 = new BallSimulation(_cfg.physics);
            sim2.Launch(new Vector3(0f, 1.2f, -0.6f), Vector3.zero, Vector3.zero);
            float maxAfterBounce = 0f;
            bool bounced = false;
            for (int i = 0; i < 600; i++)
            {
                _events.Clear();
                sim2.Substep(1f / 240f, _events);
                if (_events.Count > 0) bounced = true;
                if (bounced) maxAfterBounce = Mathf.Max(maxAfterBounce, sim2.Position.y);
                if (bounced && sim2.Velocity.y < 0f) break;
            }
            float drop = 1.2f - (TableGeometry.TopY + _cfg.physics.ballRadius);
            float rise = maxAfterBounce - (TableGeometry.TopY + _cfg.physics.ballRadius);
            float e = _cfg.physics.tableRestitution;
            Assert.AreEqual(drop * e * e, rise, 0.01f, "bounce height follows the restitution coefficient");
        }

        [Test]
        public void FastBallNeverTunnelsThroughTable()
        {
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Launch(new Vector3(0f, 0.9f, -0.5f), new Vector3(0f, -25f, 0f), Vector3.zero);
            sim.Step(0.1f, _events);
            Assert.IsTrue(FirstOf(BallContactType.Table).HasValue);
            Assert.GreaterOrEqual(sim.Position.y, TableGeometry.TopY + _cfg.physics.ballRadius - 0.001f);
        }

        [Test]
        public void LowBallHitsNetAndDoesNotCross()
        {
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Launch(new Vector3(0f, TableGeometry.TopY + 0.08f, -0.5f), new Vector3(0f, 0f, 12f), Vector3.zero);
            sim.Step(0.2f, _events);
            Assert.IsTrue(FirstOf(BallContactType.Net).HasValue, "net contact expected");
            Assert.Less(sim.Position.z, 0f, "ball must stay on the hitter's side");
        }

        [Test]
        public void HighBallClearsNet()
        {
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Launch(new Vector3(0f, TableGeometry.NetTopY + 0.15f, -0.4f), new Vector3(0f, 0.5f, 4f), Vector3.zero);
            sim.Step(0.25f, _events);
            Assert.IsFalse(FirstOf(BallContactType.Net).HasValue);
            Assert.IsFalse(FirstOf(BallContactType.NetCord).HasValue);
            Assert.Greater(sim.Position.z, 0f);
        }

        [Test]
        public void NetCordKeepsBallGoingOver()
        {
            BallSimulation sim = new BallSimulation(_cfg.physics);
            float y = TableGeometry.NetTopY + _cfg.physics.ballRadius * 0.5f;
            sim.Launch(new Vector3(0f, y, -0.3f), new Vector3(0f, 0f, 6f), Vector3.zero);
            sim.Step(0.08f, _events);
            Assert.IsTrue(FirstOf(BallContactType.NetCord).HasValue);
            Assert.Greater(sim.Position.z, 0f);
        }

        [Test]
        public void BallOutsideTableFallsToFloorAndComesToRest()
        {
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Launch(new Vector3(1.5f, 1.0f, 0f), new Vector3(0.2f, 0f, 0f), Vector3.zero);
            sim.Step(6f, _events);
            Assert.IsTrue(FirstOf(BallContactType.Floor).HasValue);
            Assert.IsFalse(FirstOf(BallContactType.Table).HasValue);
            Assert.AreEqual(_cfg.physics.ballRadius, sim.Position.y, 0.001f);
            Assert.Less(sim.Velocity.magnitude, 0.05f);
        }

        [Test]
        public void SideSpinCurvesTheBall()
        {
            BallSimulation a = new BallSimulation(_cfg.physics);
            BallSimulation b = new BallSimulation(_cfg.physics);
            a.Launch(new Vector3(0f, 1f, -1.5f), new Vector3(0f, 1.5f, 4f), Vector3.zero);
            b.Launch(new Vector3(0f, 1f, -1.5f), new Vector3(0f, 1.5f, 4f), new Vector3(3f, 0f, 0f));
            a.Step(0.3f, null);
            b.Step(0.3f, null);
            Assert.AreEqual(0f, a.Position.x, 0.0001f);
            Assert.AreEqual(0.5f * 3f * 0.09f, b.Position.x, 0.002f);
        }
    }

    [TestFixture]
    public class ShotSolverTests
    {
        [Test]
        public void SolvedShotLandsOnTarget()
        {
            BalanceConfig cfg = new BalanceConfig();
            cfg.Validate();
            BallSimulation sim = new BallSimulation(cfg.physics);
            BallSimulation scratch = new BallSimulation(cfg.physics);
            List<BallContactEvent> buf = new List<BallContactEvent>();
            GameRandom rng = new GameRandom(7);
            for (int i = 0; i < 60; i++)
            {
                Vector3 start = new Vector3(rng.Range(-0.6f, 0.6f), TableGeometry.TopY + rng.Range(0.15f, 0.6f), -rng.Range(1.45f, 2.2f));
                Vector3 target = new Vector3(rng.Range(-0.6f, 0.6f), TableGeometry.TopY + cfg.physics.ballRadius, rng.Range(0.4f, 1.2f));
                Vector3 accel = new Vector3(rng.Range(-2f, 2f), -cfg.physics.gravity + rng.Range(-3f, 0.5f), 0f);
                ShotSolution s = ShotSolver.Solve(start, target, rng.Range(0.35f, 0.9f), accel, cfg.physics);
                Assert.IsTrue(s.ClearsNet, "solver must find a net-clearing arc");
                sim.Launch(start, s.Velocity, new Vector3(accel.x, accel.y + cfg.physics.gravity, 0f));
                Vector3 landing;
                float t;
                Assert.IsTrue(ShotSolver.PredictFirstBounce(sim, scratch, buf, 3f, out landing, out t));
                Assert.AreEqual(target.x, landing.x, 0.02f);
                Assert.AreEqual(target.z, landing.z, 0.02f);
            }
        }

        [Test]
        public void FlatteningReducesNetClearance()
        {
            BalanceConfig cfg = new BalanceConfig();
            cfg.Validate();
            Vector3 start = new Vector3(0f, TableGeometry.TopY + 0.45f, -1.7f);
            Vector3 target = new Vector3(0.2f, TableGeometry.TopY + cfg.physics.ballRadius, 0.9f);
            Vector3 accel = new Vector3(0f, -cfg.physics.gravity, 0f);
            ShotSolution loopy = ShotSolver.Solve(start, target, 0.8f, accel, cfg.physics);
            ShotSolution flat = ShotSolver.SolveFlattened(start, target, 0.8f, accel, cfg.physics, 0.15f, 0.6f);
            Assert.Less(flat.NetClearance, loopy.NetClearance);
            Assert.IsTrue(flat.ClearsNet);
            Assert.LessOrEqual(flat.Velocity.magnitude, cfg.physics.maxBallSpeed + 0.001f);
        }
    }

    [TestFixture]
    public class ShotModelTests
    {
        BalanceConfig _cfg;
        RacketStatModel _stats;

        [SetUp]
        public void Setup()
        {
            _cfg = new BalanceConfig();
            _cfg.Validate();
            _stats = new RacketStatModel(new RacketStats(5, 5, 5, 5, 5), _cfg.racket);
        }

        ShotContext Ctx(Vector3 swing, float height)
        {
            ShotContext c = new ShotContext();
            c.Hitter = 0;
            c.ContactPoint = new Vector3(0f, TableGeometry.TopY + height, -1.7f);
            c.RacketVelocity = swing;
            c.Stats = _stats;
            c.SwingMultiplier = 1f;
            c.NoiseMultiplier = 1f;
            return c;
        }

        [Test]
        public void SwipeDecidesShotClass()
        {
            ShotModel m = new ShotModel(_cfg, new GameRandom(1));
            float f, l;
            Assert.AreEqual(ShotType.Normal, m.Classify(Ctx(new Vector3(0, 0, 0.2f), 0.3f), out f, out l));
            Assert.AreEqual(ShotType.Fast, m.Classify(Ctx(new Vector3(0, 0, _cfg.shots.fastSwipeThreshold + 0.1f), 0.3f), out f, out l));
            Assert.AreEqual(ShotType.Smash, m.Classify(Ctx(new Vector3(0, 0, _cfg.shots.smashSwipeThreshold + 0.1f), 0.4f), out f, out l));
            Assert.AreEqual(ShotType.Fast, m.Classify(Ctx(new Vector3(0, 0, _cfg.shots.smashSwipeThreshold + 0.1f), 0.1f), out f, out l), "low balls cannot be smashed");
            Assert.AreEqual(ShotType.Defensive, m.Classify(Ctx(new Vector3(0, 0, _cfg.shots.defensiveSwipeThreshold - 0.1f), 0.3f), out f, out l));
        }

        [Test]
        public void PlayerTwoForwardIsNegativeZ()
        {
            ShotModel m = new ShotModel(_cfg, new GameRandom(1));
            ShotContext c = Ctx(new Vector3(0, 0, -(_cfg.shots.fastSwipeThreshold + 0.2f)), 0.3f);
            c.Hitter = 1;
            c.ContactPoint = new Vector3(0, TableGeometry.TopY + 0.3f, 1.7f);
            float f, l;
            Assert.AreEqual(ShotType.Fast, m.Classify(c, out f, out l));
            ShotResult r = m.Compute(c);
            Assert.Less(r.Velocity.z, 0f);
            Assert.Less(r.IntendedTarget.z, 0f);
        }

        [Test]
        public void NormalCentralShotsLandInMostOfTheTime()
        {
            ShotModel m = new ShotModel(_cfg, new GameRandom(99));
            BallSimulation sim = new BallSimulation(_cfg.physics);
            BallSimulation scratch = new BallSimulation(_cfg.physics);
            List<BallContactEvent> buf = new List<BallContactEvent>();
            int inCount = 0, n = 300;
            for (int i = 0; i < n; i++)
            {
                ShotResult r = m.Compute(Ctx(new Vector3(0, 0, 0.3f), 0.3f));
                sim.Launch(new Vector3(0f, TableGeometry.TopY + 0.3f, -1.7f), r.Velocity, r.SpinAccel);
                Vector3 p;
                float t;
                if (ShotSolver.PredictFirstBounce(sim, scratch, buf, 3f, out p, out t) && p.z > 0f) inCount++;
            }
            Assert.Greater(inCount / (float)n, 0.9f);
        }

        [Test]
        public void HigherPowerStatHitsFaster()
        {
            RacketStatModel weak = new RacketStatModel(new RacketStats(1, 5, 5, 5, 5), _cfg.racket);
            RacketStatModel strong = new RacketStatModel(new RacketStats(10, 5, 5, 5, 5), _cfg.racket);
            BalanceConfig noNoise = new BalanceConfig();
            noNoise.Validate();
            noNoise.shots.normal.aimNoise = 0f;
            noNoise.shots.normal.verticalNoise = 0f;
            noNoise.shots.normal.maxNetClearance = 2f;
            ShotModel m = new ShotModel(noNoise, new GameRandom(3));
            ShotContext a = Ctx(new Vector3(0, 0, 0.3f), 0.3f);
            a.Stats = weak;
            ShotContext b = Ctx(new Vector3(0, 0, 0.3f), 0.3f);
            b.Stats = strong;
            Assert.Less(m.Compute(b).FlightTime, m.Compute(a).FlightTime);
        }

        [Test]
        public void LateralSwipeProducesSideSpin()
        {
            ShotModel m = new ShotModel(_cfg, new GameRandom(5));
            ShotResult r = m.Compute(Ctx(new Vector3(_cfg.shots.spinSwipeThreshold + 1.5f, 0, 0.3f), 0.3f));
            Assert.Greater(r.SideSpin, 0f);
            Assert.AreEqual(ShotType.Spin, r.Type);
        }

        [Test]
        public void SuperSmashIsCappedAndReturnable()
        {
            ShotModel m = new ShotModel(_cfg, new GameRandom(11));
            ShotContext c = Ctx(new Vector3(0, 0, 4f), 0.5f);
            c.SuperSmashArmed = true;
            ShotResult r = m.Compute(c);
            Assert.AreEqual(ShotType.SuperSmash, r.Type);
            Assert.IsTrue(r.ConsumedSuperSmash);
            Assert.LessOrEqual(r.Velocity.magnitude, _cfg.physics.maxBallSpeed + 0.001f);
            Assert.GreaterOrEqual(r.FlightTime, _cfg.physics.minFlightTime - 0.001f);
        }

        [Test]
        public void CurvePowerBendsTowardsTarget()
        {
            ShotModel m = new ShotModel(_cfg, new GameRandom(13));
            ShotContext c = Ctx(new Vector3(0, 0, 0.3f), 0.3f);
            c.ContactOffset = 0.8f; // aims to +x
            c.CurveArmed = true;
            ShotResult r = m.Compute(c);
            Assert.IsTrue(r.ConsumedCurve);
            Assert.Less(r.SideSpin, 0f, "aiming right, the curve pulls left, so the ball starts wide");
        }
    }
}
