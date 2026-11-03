// Headless measurement for smashMinHeight (NOT shipped). Player 0 is a fixed "player" stand-in
// (normal AI brain, human shot noise); player 1 is the CPU at the difficulty under test. Same
// shot rules for both. Reports the height of the CPU's returns where they cross the player's
// racket plane, and for several smashMinHeight values: CPU smashes per point, hits per point
// and the share of points the CPU wins. Same physics/shot/referee code as the game.
using System;
using System.Collections.Generic;
using System.Globalization;
using Brooklyneir.AI;
using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Physics;
using Brooklyneir.Rackets;
using UnityEngine;

public static class SmashSweep
{
    struct Result { public int Rallies, Timeouts, Hits, CpuSmashes, PlayerSmashes, CpuWins; public List<float> Heights; public int Crossings, CrossingsInWindow; }

    public static void Main(string[] args)
    {
        int rallies = args.Length > 0 ? int.Parse(args[0]) : 2000;
        string[] diffs = { "easy", "normal", "hard" };
        float[] heights = { 0.38f, 0.30f, 0.28f, 0.26f };
        if (args.Length > 1) { var l = new List<float>(heights); l.Add(float.Parse(args[1], CultureInfo.InvariantCulture)); heights = l.ToArray(); }

        Console.WriteLine("== Altura (m sobre la mesa) de las devoluciones de la CPU al cruzar el plano de la pala del jugador (smashMinHeight 0,38)");
        foreach (string d in diffs)
        {
            Result r = Run(d, 0.38f, rallies, 1234, true);
            r.Heights.Sort();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-6} n={1}  p50={2:F3}  p75={3:F3}  p90={4:F3}  min={5:F3} max={6:F3}  dentro de la zona golpeable: {7:P0}",
                d, r.Heights.Count, P(r.Heights, 0.5f), P(r.Heights, 0.75f), P(r.Heights, 0.9f), r.Heights[0], r.Heights[r.Heights.Count - 1],
                r.CrossingsInWindow / (float)Math.Max(1, r.Crossings)));
            string frac = "       bolas por encima de:";
            float[] cuts = { 0.26f, 0.28f, 0.30f, 0.32f, 0.34f, 0.35f, 0.36f, 0.38f };
            foreach (float c in cuts)
            {
                int above = 0;
                foreach (float v in r.Heights) if (v >= c) above++;
                frac += string.Format(CultureInfo.InvariantCulture, "  {0:F2}→{1:P0}", c, above / (float)r.Heights.Count);
            }
            Console.WriteLine(frac);
        }
        Console.WriteLine();
        Console.WriteLine("== Por valor de smashMinHeight (" + rallies + " puntos por casilla)");
        foreach (float h in heights)
            foreach (string d in diffs)
            {
                Result r = Run(d, h, rallies, 1234, false);
                int played = r.Rallies - r.Timeouts;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "h={0:F2} {1,-6} remates CPU/punto={2:F3}  remates jugador/punto={3:F3}  golpes/punto={4:F2}  CPU gana {5:P1}  (sin terminar: {6})",
                    h, d, r.CpuSmashes / (float)played, r.PlayerSmashes / (float)played, r.Hits / (float)played, r.CpuWins / (float)played, r.Timeouts));
            }
    }

    static float P(List<float> sorted, float q)
    {
        float idx = q * (sorted.Count - 1);
        int i = (int)Math.Floor(idx);
        int j = Math.Min(sorted.Count - 1, i + 1);
        return sorted[i] + (sorted[j] - sorted[i]) * (idx - i);
    }

    static Result Run(string diff, float smashMinHeight, int rallies, int seed, bool recordHeights)
    {
        BalanceConfig cfg = new BalanceConfig();
        cfg.Validate();
        cfg.shots.smashMinHeight = smashMinHeight;
        GameRandom rng = new GameRandom(seed);
        AIDifficultyTuning cpuT = diff == "easy" ? cfg.ai.easy : diff == "normal" ? cfg.ai.normal : cfg.ai.hard;
        AIBrain[] brains = { new AIBrain(0, cfg.ai.normal, cfg.shots, rng), new AIBrain(1, cpuT, cfg.shots, rng) };
        float[] noise = { cfg.shots.humanNoiseMultiplier, cpuT.shotNoiseMultiplier };
        RacketStatModel stats = new RacketStatModel(new RacketStats(5, 7, 5, 5, 6), cfg.racket);
        ShotModel model = new ShotModel(cfg, rng);
        BallSimulation sim = new BallSimulation(cfg.physics);
        BallSimulation scratch = new BallSimulation(cfg.physics);
        List<BallContactEvent> events = new List<BallContactEvent>();
        List<BallContactEvent> buf = new List<BallContactEvent>();
        RallyReferee referee = new RallyReferee();
        Result res = new Result { Heights = new List<float>() };
        float dt = 1f / 60f;
        for (int r = 0; r < rallies; r++)
        {
            res.Rallies++;
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
            float time = 0;
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
                        if (e.Type == BallContactType.Table) referee.OnTableBounce(e.Side);
                        else if (e.Type == BallContactType.Net || e.Type == BallContactType.NetCord) referee.OnNetContact();
                        else if (e.Type == BallContactType.Floor) referee.OnBallDead(Mathf.Abs(e.Point.x) > TableGeometry.HalfWidth);
                    }
                    if (referee.RallyOver) break;
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
                            ctx.RacketVelocity = brains[rec].ChooseSwing(hgt); ctx.Stats = stats; ctx.SwingMultiplier = 1; ctx.NoiseMultiplier = noise[rec];
                            ShotResult shot = model.Compute(ctx);
                            referee.RegisterHit(rec);
                            res.Hits++;
                            if (shot.Type == ShotType.Smash) { if (rec == 1) res.CpuSmashes++; else res.PlayerSmashes++; }
                            sim.Launch(c, shot.Velocity, shot.SpinAccel);
                            brains[rec].ClearPlan();
                            PlanFor(1 - rec, brains, sim, scratch, buf, cfg, stats);
                            if (recordHeights && rec == 1) RecordCrossing(sim, scratch, buf, cfg, ref res);
                        }
                    }
                    Vector3 bp = sim.Position;
                    if (!referee.RallyOver)
                    {
                        bool overTable = TableGeometry.IsOverTable(bp.x, bp.z);
                        if ((bp.y < TableGeometry.TopY - 0.05f && !overTable) || Mathf.Abs(bp.x) > 4 || Mathf.Abs(bp.z) > 5)
                            referee.OnBallDead(Mathf.Abs(bp.x) > TableGeometry.HalfWidth);
                    }
                }
            }
            if (!referee.RallyOver) { res.Timeouts++; continue; }
            if (referee.Winner == 1) res.CpuWins++;
        }
        return res;
    }

    /// <summary>Height where a CPU return, after bouncing on the player's half, crosses the player's default racket plane.</summary>
    static void RecordCrossing(BallSimulation sim, BallSimulation scratch, List<BallContactEvent> buf, BalanceConfig cfg, ref Result res)
    {
        Vector3 pt; float tt;
        float plane = -cfg.racket.defaultZ;
        if (!ShotSolver.PredictPlaneCrossing(sim, scratch, buf, plane, 0, 3f, out pt, out tt)) return;
        float hgt = pt.y - TableGeometry.TopY;
        res.Crossings++;
        if (hgt >= cfg.racket.minHittableHeight && hgt <= cfg.racket.maxHittableHeight) res.CrossingsInWindow++;
        res.Heights.Add(hgt);
    }

    static void PlanFor(int p, AIBrain[] brains, BallSimulation sim, BallSimulation scratch, List<BallContactEvent> buf, BalanceConfig cfg, RacketStatModel stats)
    {
        float plane = TableGeometry.ForwardSign(p) * -cfg.racket.defaultZ;
        Vector3 pt; float tt;
        if (ShotSolver.PredictPlaneCrossing(sim, scratch, buf, plane, p, 3f, out pt, out tt))
            brains[p].PlanReturn(pt, stats.Reach, cfg.racket.zoneHalfWidth);
    }
}
