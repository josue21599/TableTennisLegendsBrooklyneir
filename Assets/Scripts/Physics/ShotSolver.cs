using System.Collections.Generic;
using Brooklyneir.Config;
using UnityEngine;

namespace Brooklyneir.Physics
{
    public struct ShotSolution
    {
        public Vector3 Velocity;
        public float FlightTime;
        public bool ClearsNet;
        public float NetClearance;
    }

    /// <summary>
    /// Inverse ballistics: given where the ball is, where it should land and how long the flight
    /// should take, returns the launch velocity. Uses the same constant-acceleration model as
    /// <see cref="BallSimulation"/>, so the simulated ball lands on the requested point (before any
    /// deliberate noise is added by the shot model).
    /// </summary>
    public static class ShotSolver
    {
        public static Vector3 SolveVelocity(Vector3 start, Vector3 target, float flightTime, Vector3 accel)
        {
            float t = Mathf.Max(0.05f, flightTime);
            return (target - start - accel * (0.5f * t * t)) / t;
        }

        /// <summary>Height of the ball centre when it crosses z = 0, or +inf if it never does.</summary>
        public static float HeightAtNet(Vector3 start, Vector3 velocity, Vector3 accel, float flightTime)
        {
            if (Mathf.Abs(velocity.z) < 1e-5f) return float.PositiveInfinity;
            float tn = -start.z / velocity.z;
            if (tn <= 0f || tn >= flightTime) return float.PositiveInfinity;
            return start.y + velocity.y * tn + 0.5f * accel.y * tn * tn;
        }

        /// <summary>
        /// Finds a velocity that reaches <paramref name="target"/> while clearing the net and staying
        /// under the speed cap. If the requested time is too short it is stretched (slower, higher arc).
        /// </summary>
        public static ShotSolution Solve(Vector3 start, Vector3 target, float desiredTime, Vector3 accel, PhysicsConfig cfg)
        {
            ShotSolution sol = new ShotSolution();
            float t = Mathf.Clamp(desiredTime, cfg.minFlightTime, cfg.maxFlightTime);
            float required = TableGeometry.NetTopY + cfg.ballRadius + cfg.netClearMargin;
            for (int i = 0; i < 24; i++)
            {
                Vector3 v = SolveVelocity(start, target, t, accel);
                float yNet = HeightAtNet(start, v, accel, t);
                bool fast = v.magnitude > cfg.maxBallSpeed;
                bool clears = yNet >= required;
                sol.Velocity = v;
                sol.FlightTime = t;
                sol.ClearsNet = clears;
                sol.NetClearance = float.IsInfinity(yNet) ? 1f : yNet - (TableGeometry.NetTopY + cfg.ballRadius);
                if (!fast && clears) return sol;
                if (t >= cfg.maxFlightTime) return sol;
                t = Mathf.Min(cfg.maxFlightTime, t * 1.07f);
            }
            return sol;
        }

        /// <summary>
        /// Like <see cref="Solve"/> but flattens loopy shots: if the ball would clear the net by more
        /// than <paramref name="maxClearance"/>, the flight is shortened (bisection on time, the net
        /// clearance grows monotonically with flight time) down to at most
        /// <paramref name="minTimeFactor"/> of the requested time.
        /// </summary>
        public static ShotSolution SolveFlattened(Vector3 start, Vector3 target, float desiredTime, Vector3 accel,
            PhysicsConfig cfg, float maxClearance, float minTimeFactor)
        {
            ShotSolution sol = Solve(start, target, desiredTime, accel, cfg);
            if (!sol.ClearsNet || sol.NetClearance <= maxClearance) return sol;
            float lo = Mathf.Max(cfg.minFlightTime, sol.FlightTime * minTimeFactor);
            float hi = sol.FlightTime;
            if (lo >= hi) return sol;
            ShotSolution best = sol;
            ShotSolution atLo = Evaluate(start, target, lo, accel, cfg);
            if (atLo.NetClearance >= maxClearance)
            {
                return atLo.ClearsNet && atLo.Velocity.magnitude <= cfg.maxBallSpeed ? atLo : sol;
            }
            for (int i = 0; i < 12; i++)
            {
                float mid = 0.5f * (lo + hi);
                ShotSolution m = Evaluate(start, target, mid, accel, cfg);
                if (m.NetClearance > maxClearance) { hi = mid; best = m; }
                else lo = mid;
            }
            if (!best.ClearsNet || best.Velocity.magnitude > cfg.maxBallSpeed) return sol;
            return best;
        }

        static ShotSolution Evaluate(Vector3 start, Vector3 target, float t, Vector3 accel, PhysicsConfig cfg)
        {
            ShotSolution s = new ShotSolution();
            s.Velocity = SolveVelocity(start, target, t, accel);
            s.FlightTime = t;
            float yNet = HeightAtNet(start, s.Velocity, accel, t);
            s.NetClearance = float.IsInfinity(yNet) ? 1f : yNet - (TableGeometry.NetTopY + cfg.ballRadius);
            s.ClearsNet = yNet >= TableGeometry.NetTopY + cfg.ballRadius + cfg.netClearMargin;
            return s;
        }

        /// <summary>
        /// Simulates forward (on a scratch simulation) until the first table contact.
        /// </summary>
        public static bool PredictFirstBounce(BallSimulation source, BallSimulation scratch, List<BallContactEvent> buffer,
            float maxTime, out Vector3 point, out float time)
        {
            point = Vector3.zero;
            time = 0f;
            scratch.CopyFrom(source);
            if (!scratch.Active) return false;
            float h = scratch.Config.substep * 2f;
            float elapsed = 0f;
            while (elapsed < maxTime)
            {
                buffer.Clear();
                scratch.Substep(h, buffer);
                elapsed += h;
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (buffer[i].Type == BallContactType.Table)
                    {
                        point = buffer[i].Point;
                        time = elapsed;
                        return true;
                    }
                    if (buffer[i].Type == BallContactType.Floor) return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Simulates forward until the ball crosses the plane z = <paramref name="planeZ"/> after having
        /// bounced on <paramref name="requiredBounceSide"/> (use -1 for no requirement).
        /// Used by the AI to decide where to put its racket.
        /// </summary>
        public static bool PredictPlaneCrossing(BallSimulation source, BallSimulation scratch, List<BallContactEvent> buffer,
            float planeZ, int requiredBounceSide, float maxTime, out Vector3 point, out float time)
        {
            point = Vector3.zero;
            time = 0f;
            scratch.CopyFrom(source);
            if (!scratch.Active) return false;
            bool bounced = requiredBounceSide < 0;
            float h = scratch.Config.substep * 2f;
            float elapsed = 0f;
            while (elapsed < maxTime)
            {
                Vector3 before = scratch.Position;
                buffer.Clear();
                scratch.Substep(h, buffer);
                elapsed += h;
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (buffer[i].Type == BallContactType.Table && buffer[i].Side == requiredBounceSide) bounced = true;
                    if (buffer[i].Type == BallContactType.Floor) return false;
                }
                Vector3 after = scratch.Position;
                if (bounced && (before.z - planeZ) * (after.z - planeZ) <= 0f && Mathf.Abs(after.z - before.z) > 1e-6f)
                {
                    float f = (planeZ - before.z) / (after.z - before.z);
                    point = Vector3.LerpUnclamped(before, after, f);
                    time = elapsed - h * (1f - f);
                    return true;
                }
            }
            return false;
        }
    }
}
