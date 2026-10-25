// Headless AI-vs-AI rally simulator used to tune the physics/shot constants. NOT shipped.
using System;
using System.Collections.Generic;
using Brooklyneir.AI;
using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Physics;
using Brooklyneir.Rackets;
using UnityEngine;

public static class Tune
{
    public static void Main(string[] args)
    {
        string diff = args.Length > 0 ? args[0] : "hard";
        int rallies = args.Length > 1 ? int.Parse(args[1]) : 400;
        BalanceConfig cfg = new BalanceConfig();
        cfg.Validate();
        GameRandom rng = new GameRandom(1234);
        AIDifficultyTuning t = diff == "easy" ? cfg.ai.easy : diff == "normal" ? cfg.ai.normal : cfg.ai.hard;
        AIBrain[] brains = { new AIBrain(0, t, cfg.shots, rng), new AIBrain(1, t, cfg.shots, rng) };
        RacketStatModel stats = new RacketStatModel(new RacketStats(5, 7, 5, 5, 6), cfg.racket);
        ShotModel model = new ShotModel(cfg, rng);
        BallSimulation sim = new BallSimulation(cfg.physics);
        BallSimulation scratch = new BallSimulation(cfg.physics);
        List<BallContactEvent> events = new List<BallContactEvent>();
        List<BallContactEvent> buf = new List<BallContactEvent>();
        RallyReferee referee = new RallyReferee();
        Dictionary<string, int> reasons = new Dictionary<string, int>();
        Dictionary<string, int> types = new Dictionary<string, int>();
        int totalHits = 0; float maxApex = 0; float apexSum = 0; int apexN = 0; float ftSum = 0; int ftN = 0;
        int timeouts = 0; float netSum = 0; int netN = 0; float netMin = 9;
        float dt = 1f / 60f;
        for (int r = 0; r < rallies; r++)
        {
            int server = r % 2;
            Vector3[] racket = { new Vector3(0, 0.95f, -cfg.racket.defaultZ), new Vector3(0, 0.95f, cfg.racket.defaultZ) };
            brains[0].ClearPlan(); brains[1].ClearPlan();
            referee.StartRally(server);
            racket[server].x = rng.Range(-0.4f, 0.4f);
            ShotContext sc = new ShotContext();
            sc.Hitter = server; sc.ContactPoint = new Vector3(racket[server].x, TableGeometry.TopY + 0.22f, racket[server].z);
            sc.IsServe = true; sc.Stats = stats; sc.SwingMultiplier = 1; sc.NoiseMultiplier = 1;
            ShotResult sr = model.Compute(sc);
            sim.Launch(sc.ContactPoint, sr.Velocity, sr.SpinAccel);
            PlanFor(1 - server, brains, sim, scratch, buf, cfg, stats);
            float apex = 0; float time = 0; float shotStart = 0; bool lastHitWasFromRacket = true;
            while (!referee.RallyOver && time < 60f)
            {
                time += dt;
                for (int p = 0; p < 2; p++)
                {
                    Vector3 home = new Vector3(0, 0.95f, TableGeometry.ForwardSign(p) * -cfg.racket.defaultZ);
                    Vector3 target = brains[p].DesiredPosition(dt, racket[p], home);
                    racket[p] = Vector3.MoveTowards(racket[p], target, brains[p].Tuning.maxSpeed * dt);
                }
                int sub = 4; float h = dt / sub;
                for (int s = 0; s < sub && !referee.RallyOver; s++)
                {
                    Vector3 before = sim.Position;
                    events.Clear();
                    sim.Substep(h, events);
                    foreach (var e in events)
                    {
                        if (e.Type == BallContactType.Table) { referee.OnTableBounce(e.Side); if (ftN < 100000 && lastHitWasFromRacket) { ftSum += time - shotStart; ftN++; lastHitWasFromRacket = false; } }
                        else if (e.Type == BallContactType.Net || e.Type == BallContactType.NetCord) referee.OnNetContact();
                        else if (e.Type == BallContactType.Floor) referee.OnBallDead(Mathf.Abs(e.Point.x) > TableGeometry.HalfWidth);
                    }
                    if (referee.RallyOver) break;
                    apex = Mathf.Max(apex, sim.Position.y);
                    int rec = referee.Receiver;
                    float pz = racket[rec].z;
                    Vector3 after = sim.Position;
                    if (referee.CanHit(rec) && (before.z - pz) * (after.z - pz) <= 0 && Mathf.Abs(after.z - before.z) > 1e-6f)
                    {
                        float f = (pz - before.z) / (after.z - before.z);
                        Vector3 c = Vector3.LerpUnclamped(before, after, f);
                        float dx = c.x - racket[rec].x;
                        float hgt = c.y - TableGeometry.TopY;
                        if (Mathf.Abs(dx) <= stats.Reach && hgt >= cfg.racket.minHittableHeight && hgt <= cfg.racket.maxHittableHeight)
                        {
                            ShotContext ctx = new ShotContext();
                            ctx.Hitter = rec; ctx.ContactPoint = c; ctx.ContactOffset = dx / stats.Reach;
                            ctx.RacketVelocity = brains[rec].ChooseSwing(hgt); ctx.Stats = stats; ctx.SwingMultiplier = 1; ctx.NoiseMultiplier = brains[rec].Tuning.shotNoiseMultiplier;
                            ShotResult res = model.Compute(ctx);
                            referee.RegisterHit(rec);
                            totalHits++;
                            string k = res.Type.ToString(); types[k] = types.ContainsKey(k) ? types[k] + 1 : 1;
                            if (apex > 0) { maxApex = Mathf.Max(maxApex, apex); apexSum += apex; apexN++; }
                            apex = 0;
                            float yNet = ShotSolver.HeightAtNet(c, res.Velocity, new Vector3(res.SpinAccel.x, -cfg.physics.gravity + res.SpinAccel.y, 0), 5f);
                            if (!float.IsInfinity(yNet)) { netSum += yNet - TableGeometry.NetTopY; netN++; netMin = Mathf.Min(netMin, yNet - TableGeometry.NetTopY); }
                            sim.Launch(c, res.Velocity, res.SpinAccel);
                            shotStart = time; lastHitWasFromRacket = true;
                            brains[rec].ClearPlan();
                            PlanFor(1 - rec, brains, sim, scratch, buf, cfg, stats);
                        }
                    }
                    // dead-ball checks
                    Vector3 bp = sim.Position;
                    if (!referee.RallyOver)
                    {
                        bool overTable = TableGeometry.IsOverTable(bp.x, bp.z);
                        if ((bp.y < TableGeometry.TopY - 0.05f && !overTable) || Mathf.Abs(bp.x) > 4 || Mathf.Abs(bp.z) > 5)
                            referee.OnBallDead(Mathf.Abs(bp.x) > TableGeometry.HalfWidth);
                    }
                }
            }
            if (!referee.RallyOver) { timeouts++; continue; }
            string rk = referee.Reason.ToString();
            reasons[rk] = reasons.ContainsKey(rk) ? reasons[rk] + 1 : 1;
        }
        Console.WriteLine("Difficulty: " + diff + "  rallies: " + rallies + "  timeouts: " + timeouts);
        Console.WriteLine("Avg hits per rally (excl. serve): " + (totalHits / (float)rallies).ToString("F2"));
        Console.WriteLine("Apex avg: " + (apexSum / Math.Max(1, apexN)).ToString("F2") + "  max: " + maxApex.ToString("F2") + "  (table top 0.76)");
        Console.WriteLine("Net clearance avg: " + (netSum / Math.Max(1, netN)).ToString("F3") + "  min: " + netMin.ToString("F3"));
        Console.WriteLine("Avg shot flight time to first bounce: " + (ftSum / Math.Max(1, ftN)).ToString("F2"));
        foreach (var kv in reasons) Console.WriteLine("  reason " + kv.Key + ": " + kv.Value);
        foreach (var kv in types) Console.WriteLine("  shot " + kv.Key + ": " + kv.Value);
    }

    static void PlanFor(int p, AIBrain[] brains, BallSimulation sim, BallSimulation scratch, List<BallContactEvent> buf, BalanceConfig cfg, RacketStatModel stats)
    {
        float plane = TableGeometry.ForwardSign(p) * -cfg.racket.defaultZ;
        Vector3 pt; float tt;
        if (ShotSolver.PredictPlaneCrossing(sim, scratch, buf, plane, p, 3f, out pt, out tt))
            brains[p].PlanReturn(pt, stats.Reach, cfg.racket.zoneHalfWidth);
    }
}
