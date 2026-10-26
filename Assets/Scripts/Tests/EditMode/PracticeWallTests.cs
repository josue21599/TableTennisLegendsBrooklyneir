using System.Collections.Generic;
using Brooklyneir.Config;
using Brooklyneir.Gameplay;
using Brooklyneir.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class PracticeWallTests
    {
        BalanceConfig _cfg;

        [SetUp]
        public void Setup()
        {
            _cfg = new BalanceConfig();
            _cfg.Validate();
        }

        WallCollision NewWall()
        {
            PracticeWallTuning w = _cfg.practiceWall;
            WallCollision wall = new WallCollision(TableGeometry.HalfLength + w.wallOffset, w.wallHalfWidth, w.wallHeight,
                w.wallRestitution, w.wallFriction, _cfg.physics.spinDecayOnBounce);
            wall.MaxReturnSpeed = w.wallMaxReturnSpeed;
            wall.Physics = _cfg.physics;
            wall.ReturnBounceDepth = w.returnBounceDepth;
            wall.ReturnNetClearance = w.returnNetClearance;
            wall.ReturnVariation = w.returnVariation;
            wall.Seed = 1234;
            return wall;
        }

        // ------------------------------------------------------------------ collision

        [Test]
        public void WallBounceGivesExpectedVelocity()
        {
            // Plain bounce (no physics config): exact restitution and friction, vertical kept.
            WallCollision wall = NewWall();
            wall.Physics = null;
            float r = _cfg.physics.ballRadius;
            Vector3 pos = new Vector3(0f, 1.2f, wall.FaceZ(r));
            Vector3 vel = new Vector3(1f, 0.5f, 4f);
            Vector3 spin = new Vector3(2f, -1f, 0f);
            wall.Resolve(ref pos, ref vel, ref spin, r);
            Assert.AreEqual(1f * wall.Friction, vel.x, 1e-5f, "sideways speed loses wallFriction");
            Assert.AreEqual(-4f * wall.Restitution, vel.z, 1e-5f, "z speed reverses with wallRestitution");
            Assert.AreEqual(0.5f, vel.y, 1e-5f);
            Assert.AreEqual(0f, spin.x, "spin is removed");

            // Training-partner return: z speed never above restitution x impact (it can only be
            // slower, if a higher arc is needed to clear the net), friction still applies to x.
            wall = NewWall();
            pos = new Vector3(0f, 1.2f, wall.FaceZ(r));
            vel = new Vector3(0f, 0.5f, 4f);
            wall.Resolve(ref pos, ref vel, ref spin, r);
            Assert.Less(vel.z, 0f);
            Assert.LessOrEqual(-vel.z, 4f * wall.Restitution + 1e-4f);
            Assert.AreEqual(0f, vel.x, 1e-5f, "no sideways speed in -> straight back");
            Assert.Less(pos.z, wall.FaceZ(r), "the ball is left in front of the wall");
        }

        [Test]
        public void HardImpactsReturnAtCappedSpeed()
        {
            WallCollision wall = NewWall();
            float r = _cfg.physics.ballRadius;
            for (float speed = 4f; speed <= 30f; speed += 2f)
            {
                Vector3 pos = new Vector3(0f, 1.2f, wall.FaceZ(r));
                Vector3 vel = new Vector3(0f, 0f, speed);
                Vector3 spin = Vector3.zero;
                wall.Resolve(ref pos, ref vel, ref spin, r);
                Assert.LessOrEqual(-vel.z, wall.MaxReturnSpeed + 1e-5f, "return speed never exceeds the cap");
                Assert.Less(vel.z, 0f, "always comes back");
            }
        }

        [Test]
        public void SimulatedBallBouncesOffTheWall()
        {
            WallCollision wall = NewWall();
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Wall = wall;
            List<BallContactEvent> events = new List<BallContactEvent>(8);
            sim.Launch(new Vector3(0f, TableGeometry.TopY + 0.6f, 1.0f), new Vector3(0f, 0f, 6f), Vector3.zero);
            bool hitWall = false;
            for (int i = 0; i < 240 && !hitWall; i++)
            {
                events.Clear();
                sim.Substep(_cfg.physics.substep, events);
                for (int e = 0; e < events.Count; e++)
                {
                    if (events[e].Type != BallContactType.Wall) continue;
                    hitWall = true;
                    Assert.AreEqual(6f, events[e].VelocityBefore.z, 1e-4f);
                }
            }
            Assert.IsTrue(hitWall);
            Assert.Less(sim.Velocity.z, 0f, "comes back");
            Assert.LessOrEqual(-sim.Velocity.z, Mathf.Min(6f * wall.Restitution, wall.MaxReturnSpeed) + 1e-4f);
            Assert.AreEqual(0, wall.ClampActivations, "the safety clamp is not needed in a normal bounce");
        }

        [Test]
        public void BallNeverCrossesTheWallAtHighSpeed()
        {
            WallCollision wall = NewWall();
            float r = _cfg.physics.ballRadius;
            float face = wall.FaceZ(r);
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Wall = wall;
            List<BallContactEvent> events = new List<BallContactEvent>(8);
            float[] steps = { _cfg.physics.substep, 1f / 60f, 1f / 30f };
            int launches = 0;
            for (float speed = 10f; speed <= 40f; speed += 2.5f)
            {
                for (int a = -2; a <= 2; a++)
                {
                    for (int s = 0; s < steps.Length; s++)
                    {
                        float angle = a * 12f * Mathf.Deg2Rad;
                        Vector3 v = new Vector3(Mathf.Sin(angle) * speed * 0.3f, 1.5f, Mathf.Cos(angle) * speed);
                        sim.Launch(new Vector3(0f, TableGeometry.TopY + 0.5f, 0.2f), v, Vector3.zero);
                        launches++;
                        bool bounced = false;
                        for (int i = 0; i < 200 && sim.Active; i++)
                        {
                            events.Clear();
                            sim.Substep(steps[s], events);
                            bool insideRect = Mathf.Abs(sim.Position.x) <= wall.HalfWidth && sim.Position.y <= wall.TopY;
                            if (insideRect) Assert.LessOrEqual(sim.Position.z, face + 1e-4f, "ball crossed the wall at " + speed + " m/s");
                            for (int e = 0; e < events.Count; e++) if (events[e].Type == BallContactType.Wall) bounced = true;
                            if (bounced && sim.Velocity.z < 0f) break;
                        }
                        Assert.IsTrue(bounced, "a ball aimed at the wall must bounce (" + speed + " m/s)");
                    }
                }
            }
            Assert.Greater(launches, 100);
            Assert.AreEqual(0, wall.ClampActivations, "TryGetImpactTime caught every impact: the clamp never acted");
        }

        [Test]
        public void BallOverOrBesideTheWallIsNotBounced()
        {
            WallCollision wall = NewWall();
            float r = _cfg.physics.ballRadius;
            float t;
            Vector3 over = new Vector3(0f, wall.TopY + 0.3f, wall.FaceZ(r) - 0.2f);
            Assert.IsFalse(wall.TryGetImpactTime(over, new Vector3(0f, 0f, 5f), Vector3.zero, r, 1f, out t));
            Vector3 beside = new Vector3(wall.HalfWidth + 0.3f, 1f, wall.FaceZ(r) - 0.2f);
            Assert.IsFalse(wall.TryGetImpactTime(beside, new Vector3(0f, 0f, 5f), Vector3.zero, r, 1f, out t));
            Vector3 front = new Vector3(0f, 1f, wall.FaceZ(r) - 0.2f);
            Assert.IsTrue(wall.TryGetImpactTime(front, new Vector3(0f, 0f, 5f), Vector3.zero, r, 1f, out t));
            Assert.AreEqual(0.04f, t, 1e-5f);
        }

        [Test]
        public void ClampActsAndIsRecordedOnlyWhenForced()
        {
            WallCollision wall = NewWall();
            float r = _cfg.physics.ballRadius;
            float face = wall.FaceZ(r);
            // Normal: in front before and after -> nothing to do.
            Vector3 pos = new Vector3(0f, 1f, face - 0.05f);
            Vector3 vel = new Vector3(0f, 0f, 3f);
            Assert.IsFalse(wall.ClampInFront(new Vector3(0f, 1f, face - 0.1f), ref pos, ref vel, r));
            // Normal: went over the wall (already behind before) -> not a tunnel, nothing to do.
            pos = new Vector3(0f, 1f, face + 0.3f);
            Assert.IsFalse(wall.ClampInFront(new Vector3(0f, 1f, face + 0.2f), ref pos, ref vel, r));
            Assert.AreEqual(0, wall.ClampActivations);
            // Forced: teleported from in front to behind inside the wall -> clamped and recorded.
            pos = new Vector3(0f, 1f, face + 0.05f);
            vel = new Vector3(0f, 0f, 3f);
            Assert.IsTrue(wall.ClampInFront(new Vector3(0f, 1f, face - 0.05f), ref pos, ref vel, r));
            Assert.AreEqual(1, wall.ClampActivations);
            Assert.Less(pos.z, face);
            Assert.Less(vel.z, 0f);
            Assert.AreEqual(face + 0.05f, wall.LastClampPosition.z, 1e-6f);
        }

        [Test]
        public void WallIsDeterministic()
        {
            BallSimulation a = new BallSimulation(_cfg.physics);
            BallSimulation b = new BallSimulation(_cfg.physics);
            a.Wall = NewWall();
            b.Wall = NewWall();
            Vector3 p = new Vector3(0.2f, TableGeometry.TopY + 0.3f, -1.8f);
            Vector3 v = new Vector3(0.4f, 1.8f, 5.5f);
            Vector3 spin = new Vector3(0.6f, -1.2f, 0f);
            a.Launch(p, v, spin);
            b.Launch(p, v, spin);
            for (int i = 0; i < 900; i++)
            {
                a.Substep(_cfg.physics.substep, null);
                b.Substep(_cfg.physics.substep, null);
            }
            Assert.AreEqual(a.Position.x, b.Position.x);
            Assert.AreEqual(a.Position.y, b.Position.y);
            Assert.AreEqual(a.Position.z, b.Position.z);
            Assert.AreEqual(a.Velocity.z, b.Velocity.z);
        }

        [Test]
        public void SimulationWithoutWallIsUnchanged()
        {
            // Two identical runs, one with Wall == null: the null wall must not alter anything
            // before the ball would reach the wall plane (the rest of the game never sets it).
            BallSimulation plain = new BallSimulation(_cfg.physics);
            BallSimulation walled = new BallSimulation(_cfg.physics);
            walled.Wall = null;
            Vector3 p = new Vector3(0f, TableGeometry.TopY + 0.3f, -1.8f), v = new Vector3(0.3f, 1.6f, 4.5f);
            plain.Launch(p, v, Vector3.zero);
            walled.Launch(p, v, Vector3.zero);
            for (int i = 0; i < 600; i++)
            {
                plain.Substep(_cfg.physics.substep, null);
                walled.Substep(_cfg.physics.substep, null);
            }
            Assert.AreEqual(plain.Position.z, walled.Position.z);
            Assert.AreEqual(plain.Velocity.y, walled.Velocity.y);
        }

        // ------------------------------------------------------------------ training-partner return

        /// <summary>Simulates from a wall impact to the first contact. Returns the first contact
        /// and the ball's height above the net tape when it crosses z = 0.</summary>
        BallContactEvent ReturnFrom(WallCollision wall, Vector3 impactPos, Vector3 impactVel, out float netClearance)
        {
            float r = _cfg.physics.ballRadius;
            Vector3 pos = new Vector3(impactPos.x, impactPos.y, wall.FaceZ(r));
            Vector3 vel = impactVel;
            Vector3 spin = Vector3.zero;
            wall.Resolve(ref pos, ref vel, ref spin, r);
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Wall = wall;
            sim.Launch(pos, vel, Vector3.zero);
            List<BallContactEvent> events = new List<BallContactEvent>(8);
            netClearance = float.NegativeInfinity;
            for (int i = 0; i < 2000 && sim.Active; i++)
            {
                Vector3 before = sim.Position;
                events.Clear();
                sim.Substep(_cfg.physics.substep, events);
                if (before.z > 0f && sim.Position.z <= 0f)
                    netClearance = Mathf.Lerp(before.y, sim.Position.y, before.z / (before.z - sim.Position.z)) - (TableGeometry.NetTopY + r);
                for (int e = 0; e < events.Count; e++)
                    if (events[e].Type != BallContactType.NetCord) return events[e];
            }
            return new BallContactEvent { Type = BallContactType.Floor, Side = -1 };
        }

        [Test]
        public void ReturnBouncesAtConfiguredDepthAndClearsNet()
        {
            WallCollision wall = NewWall();
            int cases = 0;
            for (float x = -0.5f; x <= 0.51f; x += 0.25f)
                for (float y = 0.85f; y <= 1.61f; y += 0.25f)
                    for (float vz = 2f; vz <= 12f; vz += 2.5f)
                        for (float vx = -1f; vx <= 1.01f; vx += 1f)
                        {
                            float clearance;
                            BallContactEvent first = ReturnFrom(wall, new Vector3(x, y, 0f), new Vector3(vx, -0.5f, vz), out clearance);
                            string info = "impact x=" + x + " y=" + y + " vx=" + vx + " vz=" + vz + " -> " + first.Type + " at " + first.Point;
                            Assert.AreEqual(BallContactType.Table, first.Type, info);
                            Assert.AreEqual(0, first.Side, info);
                            Assert.AreEqual(_cfg.practiceWall.returnBounceDepth, first.Point.z, 0.02f, info);
                            Assert.LessOrEqual(Mathf.Abs(first.Point.x), TableGeometry.HalfWidth - WallCollision.SideMargin + 0.02f, info);
                            Assert.GreaterOrEqual(clearance, _cfg.practiceWall.returnNetClearance - 0.005f, info + " clearance=" + clearance);
                            cases++;
                        }
            Assert.GreaterOrEqual(cases, 300);
            Assert.AreEqual(0, wall.ClampActivations);
        }

        [Test]
        public void ZeroVariationIsDeterministic()
        {
            WallCollision a = NewWall(), b = NewWall();
            a.ReturnVariation = 0f;
            b.ReturnVariation = 0f;
            b.Seed = 98765; // with no variation the seed must not matter either
            float r = _cfg.physics.ballRadius;
            for (int i = 0; i < 100; i++)
            {
                Vector3 p1 = new Vector3(-0.5f + i * 0.01f, 0.9f + i * 0.005f, a.FaceZ(r)), p2 = p1;
                Vector3 v1 = new Vector3(0.8f - i * 0.016f, -0.3f, 2f + i * 0.08f), v2 = v1;
                Vector3 s1 = Vector3.zero, s2 = Vector3.zero;
                a.Resolve(ref p1, ref v1, ref s1, r);
                b.Resolve(ref p2, ref v2, ref s2, r);
                Assert.AreEqual(v1.x, v2.x);
                Assert.AreEqual(v1.y, v2.y);
                Assert.AreEqual(v1.z, v2.z);
            }
            // Same seed with variation: also identical between two runs.
            WallCollision c = NewWall(), d = NewWall();
            c.ReturnVariation = d.ReturnVariation = 0.08f;
            Vector3 pc = new Vector3(0.1f, 1.1f, c.FaceZ(r)), pd = pc, vc = new Vector3(0.3f, 0f, 5f), vd = vc, sc = Vector3.zero, sd = Vector3.zero;
            c.Resolve(ref pc, ref vc, ref sc, r);
            d.Resolve(ref pd, ref vd, ref sd, r);
            Assert.AreEqual(vc.x, vd.x);
            Assert.AreEqual(vc.y, vd.y);
            Assert.AreEqual(vc.z, vd.z);
        }

        [Test]
        public void VariationNeverExceedsItsBound()
        {
            WallCollision exact = NewWall(), varied = NewWall();
            exact.ReturnVariation = 0f;
            varied.ReturnVariation = 0.08f;
            float bound = WallCollision.VariationBound * varied.ReturnVariation;
            float r = _cfg.physics.ballRadius;
            int moved = 0;
            for (int i = 0; i < 500; i++)
            {
                Vector3 pos = new Vector3(-0.3f + (i % 7) * 0.1f, 0.9f + (i % 5) * 0.12f, exact.FaceZ(r));
                Vector3 vel = new Vector3(-0.6f + (i % 11) * 0.12f, -0.4f, 2.5f + (i % 13) * 0.6f);
                float vz = Mathf.Max(0.5f, Mathf.Min(vel.z * exact.Restitution, exact.MaxReturnSpeed));
                Vector3 t0 = exact.ReturnTarget(pos, vel, vel.x * exact.Friction, vz, r);
                Vector3 t1 = varied.ReturnTarget(pos, vel, vel.x * varied.Friction, vz, r);
                Assert.LessOrEqual(Mathf.Abs(t1.x - t0.x), bound + 1e-5f, "x deviation bounded");
                Assert.LessOrEqual(Mathf.Abs(t1.z - t0.z), bound + 1e-5f, "z deviation bounded");
                Assert.Less(t1.z, 0f, "always on the player's half");
                if (Mathf.Abs(t1.x - t0.x) > 1e-4f) moved++;
            }
            Assert.Greater(moved, 400, "the variation actually varies");
        }

        [Test]
        public void ServeWallAndStillRacketGiveValidReturns()
        {
            Brooklyneir.Rackets.RacketStatModel stats = new Brooklyneir.Rackets.RacketStatModel(new Brooklyneir.Rackets.RacketStats(5, 5, 5, 5, 5), _cfg.racket);
            ShotModel model = new ShotModel(_cfg, new Brooklyneir.Core.GameRandom(1));
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Wall = NewWall();
            PracticeWallReferee referee = new PracticeWallReferee();
            PracticeWallStats stats2 = new PracticeWallStats();
            List<BallContactEvent> events = new List<BallContactEvent>(8);
            Vector3 racket = new Vector3(0f, 0f, -_cfg.racket.defaultZ); // still, in the centre
            ShotContext serve = new ShotContext();
            serve.Hitter = 0;
            serve.Stats = stats;
            serve.SwingMultiplier = 1f;
            serve.NoiseMultiplier = 0.0001f;
            serve.IsServe = true;
            serve.ContactPoint = new Vector3(0f, TableGeometry.TopY + 0.26f, racket.z + 0.12f);
            ShotResult sr = model.Compute(serve);
            sim.Launch(serve.ContactPoint, sr.Velocity, sr.SpinAccel);
            referee.OnServe();
            for (int i = 0; i < 24000 && sim.Active && stats2.TotalReturns < 5; i++)
            {
                Vector3 before = sim.Position;
                events.Clear();
                sim.Substep(_cfg.physics.substep, events);
                for (int e = 0; e < events.Count; e++)
                {
                    PracticeOutcome o = referee.OnContact(events[e].Type, events[e].Side);
                    if (o.Return) stats2.RegisterReturn();
                    Assert.IsFalse(o.Lost, "lost: " + o.Reason + " after " + stats2.TotalReturns + " returns");
                }
                if (referee.CanHit && (before.z - racket.z) * (sim.Position.z - racket.z) <= 0f)
                {
                    float f = (before.z - racket.z) / (before.z - sim.Position.z);
                    Vector3 c = Vector3.Lerp(before, sim.Position, f);
                    float h = c.y - TableGeometry.TopY;
                    Assert.LessOrEqual(Mathf.Abs(c.x - racket.x), _cfg.racket.baseReach, "the return comes to the centre");
                    Assert.IsTrue(h >= _cfg.racket.minHittableHeight && h <= _cfg.racket.maxHittableHeight, "hittable height: " + h);
                    ShotContext hit = serve;
                    hit.IsServe = false;
                    hit.ContactPoint = c;
                    hit.RacketVelocity = Vector3.zero;
                    ShotResult hr = model.Compute(hit);
                    referee.OnRacketHit();
                    sim.Launch(c, hr.Velocity, hr.SpinAccel);
                }
            }
            Assert.GreaterOrEqual(stats2.TotalReturns, 1, "at least one valid return with a still racket");
        }

        // ------------------------------------------------------------------ still racket

        [Test]
        public void StillRacketShotLandsOnTheFarHalf()
        {
            // Regression: a stroke with the racket not moving at all is a valid, controlled shot.
            Brooklyneir.Rackets.RacketStatModel stats = new Brooklyneir.Rackets.RacketStatModel(new Brooklyneir.Rackets.RacketStats(5, 5, 5, 5, 5), _cfg.racket);
            ShotModel model = new ShotModel(_cfg, new Brooklyneir.Core.GameRandom(1));
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Wall = NewWall();
            List<BallContactEvent> events = new List<BallContactEvent>(8);
            float[] depths = { _cfg.racket.zoneNearZ, _cfg.racket.defaultZ, _cfg.racket.zoneFarZ };
            float[] heights = { _cfg.racket.minHittableHeight, 0.2f, 0.6f, 1.2f };
            foreach (float d in depths)
            {
                foreach (float h in heights)
                {
                    ShotContext c = new ShotContext();
                    c.Hitter = 0;
                    c.Stats = stats;
                    c.SwingMultiplier = 1f;
                    c.NoiseMultiplier = 0.0001f;
                    c.ContactPoint = new Vector3(0f, TableGeometry.TopY + h, -d);
                    c.RacketVelocity = Vector3.zero;
                    ShotResult r = model.Compute(c);
                    sim.Launch(c.ContactPoint, r.Velocity, r.SpinAccel);
                    BallContactType first = BallContactType.Floor;
                    int side = -1;
                    Vector3 point = Vector3.zero;
                    bool found = false;
                    for (int i = 0; i < 2000 && sim.Active && !found; i++)
                    {
                        events.Clear();
                        sim.Substep(_cfg.physics.substep, events);
                        for (int e = 0; e < events.Count && !found; e++)
                        {
                            if (events[e].Type == BallContactType.NetCord) continue;
                            first = events[e].Type;
                            side = events[e].Side;
                            point = events[e].Point;
                            found = true;
                        }
                    }
                    string info = "contact z=" + (-d) + " h=" + h + " type=" + r.Type + " target=" + r.IntendedTarget +
                                  " v=" + r.Velocity + " first=" + first + " at " + point;
                    Assert.AreEqual(BallContactType.Table, first, info);
                    Assert.AreEqual(1, side, info);
                }
            }
        }

        [Test]
        public void StillRacketReachesTheLowBallComingBackFromTheWall()
        {
            // Regression: the ball comes back from the wall low (~6 cm above the table at the
            // racket); with the old 12 cm minimum it passed through a still racket unhit.
            Brooklyneir.Rackets.RacketStatModel stats = new Brooklyneir.Rackets.RacketStatModel(new Brooklyneir.Rackets.RacketStats(5, 5, 5, 5, 5), _cfg.racket);
            ShotModel model = new ShotModel(_cfg, new Brooklyneir.Core.GameRandom(1));
            BallSimulation sim = new BallSimulation(_cfg.physics);
            sim.Wall = NewWall();
            PracticeWallReferee referee = new PracticeWallReferee();
            List<BallContactEvent> events = new List<BallContactEvent>(8);
            float racketZ = -_cfg.racket.defaultZ;
            ShotContext serve = new ShotContext();
            serve.Hitter = 0;
            serve.Stats = stats;
            serve.SwingMultiplier = 1f;
            serve.NoiseMultiplier = 0.0001f;
            serve.IsServe = true;
            serve.ContactPoint = new Vector3(0f, TableGeometry.TopY + 0.26f, racketZ + 0.12f);
            ShotResult sr = model.Compute(serve);
            sim.Launch(serve.ContactPoint, sr.Velocity, sr.SpinAccel);
            referee.OnServe();
            bool hit = false;
            float contactHeight = -1f;
            for (int i = 0; i < 3000 && sim.Active && !hit; i++)
            {
                Vector3 before = sim.Position;
                events.Clear();
                sim.Substep(_cfg.physics.substep, events);
                for (int e = 0; e < events.Count; e++) Assert.IsFalse(referee.OnContact(events[e].Type, events[e].Side).Lost, "lost before reaching the racket");
                if (referee.CanHit && (before.z - racketZ) * (sim.Position.z - racketZ) <= 0f)
                {
                    contactHeight = Mathf.Lerp(before.y, sim.Position.y, (before.z - racketZ) / (before.z - sim.Position.z)) - TableGeometry.TopY;
                    hit = contactHeight >= _cfg.racket.minHittableHeight && contactHeight <= _cfg.racket.maxHittableHeight;
                    Assert.IsTrue(hit, "ball reached the still racket at h=" + contactHeight + " (window " + _cfg.racket.minHittableHeight + ".." + _cfg.racket.maxHittableHeight + ")");
                }
            }
            Assert.IsTrue(hit, "the returned ball reached the racket plane after one bounce");
        }

        // ------------------------------------------------------------------ rules

        [Test]
        public void ReturnsBuildTheStreakAndALossResetsIt()
        {
            PracticeWallReferee r = new PracticeWallReferee();
            PracticeWallStats s = new PracticeWallStats();
            r.OnServe();
            Feed(r, s, BallContactType.Table, 1);
            Assert.IsFalse(Feed(r, s, BallContactType.Wall, 1).Return, "the serve is not a return");
            for (int i = 0; i < 3; i++)
            {
                Feed(r, s, BallContactType.Table, 0);
                Assert.IsTrue(r.OnRacketHit());
                Feed(r, s, BallContactType.Table, 1);
                Assert.IsTrue(Feed(r, s, BallContactType.Wall, 1).Return);
            }
            Assert.AreEqual(3, s.CurrentStreak);
            Assert.AreEqual(3, s.TotalReturns);
            PracticeOutcome lost = Feed(r, s, BallContactType.Floor, 0);
            Assert.IsTrue(lost.Lost);
            Assert.AreEqual(0, s.CurrentStreak, "streak resets on a lost ball");
            Assert.AreEqual(3, s.BestStreak, "best streak is kept");
            Assert.AreEqual(3, s.TotalReturns);
        }

        [Test]
        public void BallReachingPlayersHalfWithoutBounceIsLost()
        {
            // General rule: a ball that comes back must bounce once on the player's half first.
            PracticeWallReferee r = new PracticeWallReferee();
            r.OnServe();
            r.OnContact(BallContactType.Wall, 1);
            Assert.IsFalse(r.CanHit, "no stroke before the bounce");
            Assert.IsFalse(r.OnRacketHit());
            PracticeOutcome o = r.OnBallPosition(0f, 1f, -TableGeometry.HalfLength - 0.01f, 4f, 5f);
            Assert.IsTrue(o.Lost);
            Assert.AreEqual(PracticeLoss.NoBounce, o.Reason);

            r.OnServe();
            r.OnContact(BallContactType.Wall, 1);
            Assert.AreEqual(PracticeLoss.NoBounce, r.OnContact(BallContactType.Floor, 0).Reason);

            // A stroke that lands on the player's own half also reaches it without the required bounce cycle.
            r.OnServe();
            Assert.AreEqual(PracticeLoss.OwnSide, r.OnContact(BallContactType.Table, 0).Reason);
        }

        [Test]
        public void DoubleBounceNetAndOutAreLost()
        {
            PracticeWallReferee r = new PracticeWallReferee();
            r.OnServe();
            r.OnContact(BallContactType.Wall, 1);
            r.OnContact(BallContactType.Table, 0);
            Assert.IsTrue(r.CanHit);
            Assert.AreEqual(PracticeLoss.DoubleBounce, r.OnContact(BallContactType.Table, 0).Reason);

            r.OnServe();
            Assert.AreEqual(PracticeLoss.Net, r.OnContact(BallContactType.Net, 0).Reason);
            r.OnServe();
            Assert.AreEqual(PracticeLoss.Out, r.OnContact(BallContactType.Floor, 1).Reason);
            r.OnServe();
            Assert.IsFalse(r.OnContact(BallContactType.NetCord, 0).Lost, "the net cord keeps the ball alive");
        }

        [Test]
        public void SessionNeverEndsByItself()
        {
            PracticeWallReferee r = new PracticeWallReferee();
            PracticeWallStats s = new PracticeWallStats();
            for (int cycle = 0; cycle < 10000; cycle++)
            {
                r.OnServe();
                Assert.AreEqual(PracticeState.Outgoing, r.State);
                Feed(r, s, BallContactType.Wall, 1);
                Feed(r, s, BallContactType.Table, 0);
                r.OnRacketHit();
                Feed(r, s, BallContactType.Wall, 1);
                Feed(r, s, BallContactType.Floor, 0); // lost: no bounce
                s.Tick(0.5f);
                Assert.AreEqual(PracticeState.Lost, r.State, "a loss only waits for the next serve");
            }
            Assert.AreEqual(10000, s.TotalReturns);
            Assert.AreEqual(1, s.BestStreak);
            Assert.AreEqual(5000f, s.SessionSeconds, 0.5f);
        }

        static PracticeOutcome Feed(PracticeWallReferee r, PracticeWallStats s, BallContactType type, int side)
        {
            PracticeOutcome o = r.OnContact(type, side);
            if (o.Return) s.RegisterReturn();
            if (o.Lost) s.RegisterLoss();
            return o;
        }
    }
}
