using System.Collections.Generic;
using Brooklyneir.AI;
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
    public class TouchZoneRouterTests
    {
        [Test]
        public void TwoSimultaneousFingersControlBothPlayers()
        {
            TouchZoneRouter r = new TouchZoneRouter();
            Assert.AreEqual(0, r.Began(3, 100f, 1080f, false));
            Assert.AreEqual(1, r.Began(7, 900f, 1080f, false));
            Assert.AreEqual(0, r.OwnerOf(3));
            Assert.AreEqual(1, r.OwnerOf(7));
            Assert.AreEqual(0, r.Ended(3));
            Assert.AreEqual(1, r.OwnerOf(7), "releasing one finger does not affect the other");
        }

        [Test]
        public void ExtraFingerInSameZoneIsIgnored()
        {
            TouchZoneRouter r = new TouchZoneRouter();
            r.Began(1, 100f, 1080f, false);
            Assert.AreEqual(-1, r.Began(2, 200f, 1080f, false));
            Assert.AreEqual(1, r.FingerOf(0));
        }

        [Test]
        public void TouchesOnUIAreNeverAssigned()
        {
            TouchZoneRouter r = new TouchZoneRouter();
            Assert.AreEqual(-1, r.Began(1, 100f, 1080f, true));
            Assert.AreEqual(TouchZoneRouter.NoFinger, r.FingerOf(0));
        }

        [Test]
        public void SingleHumanUsesWholeScreen()
        {
            TouchZoneRouter r = new TouchZoneRouter();
            r.SetHumanControlled(1, false);
            Assert.AreEqual(0, r.Began(4, 1000f, 1080f, false), "right half also controls player 1 vs CPU");
        }

        [Test]
        public void FingerKeepsOwnerAcrossMiddleLine()
        {
            TouchZoneRouter r = new TouchZoneRouter();
            r.Began(9, 500f, 1080f, false);
            // Moving does not re-route; only Began assigns.
            Assert.AreEqual(0, r.OwnerOf(9));
            Assert.AreEqual(1, r.Began(10, 1000f, 1080f, false));
        }
    }

    [TestFixture]
    public class AIBrainTests
    {
        [Test]
        public void ReactionDelayHoldsPositionThenMoves()
        {
            BalanceConfig cfg = new BalanceConfig();
            cfg.Validate();
            AIBrain b = new AIBrain(1, cfg.ai.normal, cfg.shots, new GameRandom(1));
            Vector3 current = new Vector3(0f, 0.95f, 1.75f);
            b.PlanReturn(new Vector3(0.5f, 1.0f, 1.75f), 0.2f, 1.15f);
            Vector3 first = b.DesiredPosition(0.01f, current, current);
            Assert.AreEqual(current.x, first.x, 0.0001f, "still reacting");
            Vector3 later = current;
            for (int i = 0; i < 60; i++) later = b.DesiredPosition(1f / 60f, current, current);
            Assert.AreNotEqual(current.x, later.x);
            Assert.LessOrEqual(Mathf.Abs(later.x), 1.15f);
        }

        [Test]
        public void HarderAIMissesLessOften()
        {
            BalanceConfig cfg = new BalanceConfig();
            cfg.Validate();
            Assert.Less(MissRate(cfg.ai.hard, cfg), MissRate(cfg.ai.easy, cfg));
        }

        static float MissRate(AIDifficultyTuning t, BalanceConfig cfg)
        {
            AIBrain b = new AIBrain(1, t, cfg.shots, new GameRandom(5));
            int misses = 0;
            for (int i = 0; i < 2000; i++)
            {
                b.PlanReturn(new Vector3(0f, 1f, 1.75f), 0.2f, 1.15f);
                if (b.PlannedMiss) misses++;
            }
            return misses / 2000f;
        }

        [Test]
        public void SwingDirectionMatchesPlayer()
        {
            BalanceConfig cfg = new BalanceConfig();
            cfg.Validate();
            AIBrain p2 = new AIBrain(1, cfg.ai.hard, cfg.shots, new GameRandom(2));
            int forwardish = 0;
            for (int i = 0; i < 500; i++) if (p2.ChooseSwing(0.4f).z < 0f) forwardish++;
            Assert.Greater(forwardish, 250, "player 2 swings towards -z most of the time");
        }
    }

    /// <summary>
    /// Integration: two CPUs play full rallies with the real physics, shot model, referee and
    /// brains (no rendering). Guards against regressions that would make rallies impossible or
    /// endless.
    /// </summary>
    [TestFixture]
    public class HeadlessRallyTests
    {
        [Test]
        public void CpuRalliesAreLongEnoughAndAlwaysEnd()
        {
            BalanceConfig cfg = new BalanceConfig();
            cfg.Validate();
            GameRandom rng = new GameRandom(2024);
            RacketStatModel stats = new RacketStatModel(new RacketStats(5, 7, 5, 5, 6), cfg.racket);
            ShotModel model = new ShotModel(cfg, rng);
            AIBrain[] brains = { new AIBrain(0, cfg.ai.normal, cfg.shots, rng), new AIBrain(1, cfg.ai.normal, cfg.shots, rng) };
            BallSimulation sim = new BallSimulation(cfg.physics);
            BallSimulation scratch = new BallSimulation(cfg.physics);
            List<BallContactEvent> events = new List<BallContactEvent>();
            List<BallContactEvent> buf = new List<BallContactEvent>();
            RallyReferee referee = new RallyReferee();
            int totalHits = 0;
            const int rallies = 60;
            const float dt = 1f / 60f;
            for (int r = 0; r < rallies; r++)
            {
                int server = r % 2;
                Vector3[] racket = { new Vector3(0, 0.95f, -cfg.racket.defaultZ), new Vector3(0, 0.95f, cfg.racket.defaultZ) };
                brains[0].ClearPlan();
                brains[1].ClearPlan();
                referee.StartRally(server);
                ShotContext sc = new ShotContext();
                sc.Hitter = server;
                sc.ContactPoint = new Vector3(0f, TableGeometry.TopY + 0.22f, racket[server].z);
                sc.IsServe = true;
                sc.Stats = stats;
                sc.SwingMultiplier = 1f;
                sc.NoiseMultiplier = 1f;
                ShotResult serve = model.Compute(sc);
                sim.Launch(sc.ContactPoint, serve.Velocity, serve.SpinAccel);
                Plan(1 - server, brains, sim, scratch, buf, cfg, stats);
                float time = 0f;
                while (!referee.RallyOver)
                {
                    time += dt;
                    Assert.Less(time, 120f, "a rally must not last forever");
                    for (int p = 0; p < 2; p++)
                    {
                        Vector3 home = new Vector3(0, 0.95f, TableGeometry.ForwardSign(p) * -cfg.racket.defaultZ);
                        Vector3 target = brains[p].DesiredPosition(dt, racket[p], home);
                        racket[p] = Vector3.MoveTowards(racket[p], target, brains[p].Tuning.maxSpeed * dt);
                    }
                    for (int s = 0; s < 4 && !referee.RallyOver; s++)
                    {
                        Vector3 before = sim.Position;
                        events.Clear();
                        sim.Substep(dt / 4f, events);
                        for (int i = 0; i < events.Count; i++)
                        {
                            BallContactEvent e = events[i];
                            if (e.Type == BallContactType.Table) referee.OnTableBounce(e.Side);
                            else if (e.Type == BallContactType.Floor) referee.OnBallDead(false);
                            else referee.OnNetContact();
                        }
                        if (referee.RallyOver) break;
                        int rec = referee.Receiver;
                        float pz = racket[rec].z;
                        Vector3 after = sim.Position;
                        if (referee.CanHit(rec) && (before.z - pz) * (after.z - pz) <= 0f && Mathf.Abs(after.z - before.z) > 1e-6f)
                        {
                            Vector3 c = Vector3.LerpUnclamped(before, after, (pz - before.z) / (after.z - before.z));
                            float dx = c.x - racket[rec].x;
                            float h = c.y - TableGeometry.TopY;
                            if (Mathf.Abs(dx) <= stats.Reach && h >= cfg.racket.minHittableHeight && h <= cfg.racket.maxHittableHeight)
                            {
                                ShotContext ctx = new ShotContext();
                                ctx.Hitter = rec;
                                ctx.ContactPoint = c;
                                ctx.ContactOffset = dx / stats.Reach;
                                ctx.RacketVelocity = brains[rec].ChooseSwing(h);
                                ctx.Stats = stats;
                                ctx.SwingMultiplier = 1f;
                                ctx.NoiseMultiplier = 1f;
                                ShotResult res = model.Compute(ctx);
                                referee.RegisterHit(rec);
                                totalHits++;
                                sim.Launch(c, res.Velocity, res.SpinAccel);
                                brains[rec].ClearPlan();
                                Plan(1 - rec, brains, sim, scratch, buf, cfg, stats);
                            }
                        }
                        Vector3 bp = sim.Position;
                        if (!referee.RallyOver && ((bp.y < TableGeometry.TopY - 0.05f && !TableGeometry.IsOverTable(bp.x, bp.z)) || Mathf.Abs(bp.x) > 4f || Mathf.Abs(bp.z) > 5f))
                            referee.OnBallDead(Mathf.Abs(bp.x) > TableGeometry.HalfWidth);
                    }
                }
                Assert.IsTrue(referee.Winner == 0 || referee.Winner == 1);
            }
            Assert.Greater(totalHits / (float)rallies, 2f, "normal CPUs should exchange several shots");
        }

        static void Plan(int p, AIBrain[] brains, BallSimulation sim, BallSimulation scratch, List<BallContactEvent> buf, BalanceConfig cfg, RacketStatModel stats)
        {
            float plane = TableGeometry.ForwardSign(p) * -cfg.racket.defaultZ;
            Vector3 pt;
            float t;
            if (ShotSolver.PredictPlaneCrossing(sim, scratch, buf, plane, p, 3f, out pt, out t))
                brains[p].PlanReturn(pt, stats.Reach, cfg.racket.zoneHalfWidth);
        }
    }
}
