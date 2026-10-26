using System.Collections.Generic;
using Brooklyneir.Config;
using UnityEngine;

namespace Brooklyneir.Physics
{
    public enum BallContactType { Table, Net, NetCord, Floor, Wall }

    public struct BallContactEvent
    {
        public BallContactType Type;
        public Vector3 Point;
        public Vector3 VelocityBefore;
        /// <summary>For table contacts: which half (0 = player 1, 1 = player 2).</summary>
        public int Side;
    }

    /// <summary>
    /// Deterministic ball simulation with analytic contact times.
    ///
    /// Why custom instead of Rigidbody: a table-tennis ball at 13 m/s with a 2.5 cm radius is exactly
    /// the case where PhysX needs continuous detection and still produces inconsistent bounces on a
    /// thin net. Here each substep integrates constant acceleration exactly and solves the time of
    /// contact with the table plane, the net plane and the floor analytically, so the ball can never
    /// tunnel, results are reproducible, and the very same code is used to predict trajectories
    /// (AI, landing marker) and to solve shots. The rendered ball is placed at this position every
    /// frame, so visuals never drift from the physics.
    ///
    /// Acceleration model: gravity plus a constant "spin acceleration" (x: side spin curve, y: topspin
    /// dip or backspin float). z acceleration is always zero, which keeps z(t) linear and lets the
    /// shot solver hit a target exactly.
    /// </summary>
    public class BallSimulation
    {
        public Vector3 Position;
        public Vector3 Velocity;
        /// <summary>Extra constant acceleration from spin (z component is ignored).</summary>
        public Vector3 SpinAccel;
        /// <summary>When false the ball is held (serve) and Step does nothing.</summary>
        public bool Active;
        /// <summary>Optional practice wall (null in every mode except the practice wall).</summary>
        public WallCollision Wall;

        readonly PhysicsConfig _cfg;

        public BallSimulation(PhysicsConfig cfg)
        {
            _cfg = cfg;
        }

        public PhysicsConfig Config { get { return _cfg; } }
        public float Radius { get { return _cfg.ballRadius; } }

        public Vector3 Acceleration
        {
            get { return new Vector3(SpinAccel.x, -_cfg.gravity + SpinAccel.y, 0f); }
        }

        public void Launch(Vector3 position, Vector3 velocity, Vector3 spinAccel)
        {
            Position = position;
            Velocity = velocity;
            SpinAccel = new Vector3(spinAccel.x, spinAccel.y, 0f);
            Active = true;
        }

        public void Hold(Vector3 position)
        {
            Position = position;
            Velocity = Vector3.zero;
            SpinAccel = Vector3.zero;
            Active = false;
        }

        public void CopyFrom(BallSimulation other)
        {
            Position = other.Position;
            Velocity = other.Velocity;
            SpinAccel = other.SpinAccel;
            Active = other.Active;
            Wall = other.Wall;
        }

        /// <summary>
        /// Advances the simulation by <paramref name="dt"/> seconds using fixed internal substeps.
        /// Contacts are appended to <paramref name="events"/> (may be null). Returns contacts added.
        /// </summary>
        public int Step(float dt, List<BallContactEvent> events)
        {
            if (!Active || dt <= 0f) return 0;
            int added = 0;
            float h = _cfg.substep;
            float remaining = dt;
            int guard = 0;
            while (remaining > 1e-7f && guard++ < 4000)
            {
                float step = remaining < h ? remaining : h;
                added += Substep(step, events);
                remaining -= step;
            }
            return added;
        }

        /// <summary>One substep, resolving up to a few contacts inside it.</summary>
        public int Substep(float h, List<BallContactEvent> events)
        {
            if (!Active) return 0;
            int added = 0;
            float timeLeft = h;
            for (int iteration = 0; iteration < 4 && timeLeft > 1e-7f; iteration++)
            {
                Vector3 a = Acceleration;
                float r = _cfg.ballRadius;
                Vector3 before = Position;

                // Resting / rolling on the floor: no more bounces, just roll to a stop.
                if (Position.y <= r + 1e-4f && Mathf.Abs(Velocity.y) < 0.2f)
                {
                    Position.y = r;
                    Velocity.y = 0f;
                    float damp = Mathf.Clamp01(1f - 1.5f * timeLeft);
                    Velocity.x *= damp;
                    Velocity.z *= damp;
                    Position.x += Velocity.x * timeLeft;
                    Position.z += Velocity.z * timeLeft;
                    if (Wall != null && Wall.ClampInFront(before, ref Position, ref Velocity, r)) added += AddWallEvent(events);
                    timeLeft = 0f;
                    break;
                }

                float tTable = float.MaxValue, tNet = float.MaxValue, tFloor = float.MaxValue;

                // Table top plane (only from above, inside the table rectangle).
                float tablePlane = TableGeometry.TopY + r;
                if (Position.y >= tablePlane - 1e-5f)
                {
                    float t;
                    if (SolveDescendingCrossing(Position.y, Velocity.y, a.y, tablePlane, timeLeft, out t))
                    {
                        Vector3 p = PositionAt(t, a);
                        if (Mathf.Abs(p.x) <= TableGeometry.HalfWidth + r * 0.5f &&
                            Mathf.Abs(p.z) <= TableGeometry.HalfLength + r * 0.5f)
                            tTable = t;
                    }
                }

                // Net plane (z = 0). The contact happens when the ball surface reaches the plane.
                if (Mathf.Abs(Velocity.z) > 1e-5f)
                {
                    float s = Mathf.Sign(Velocity.z);
                    if (Position.z * s <= -r)
                    {
                        float t = (-s * r - Position.z) / Velocity.z;
                        if (t >= 0f && t <= timeLeft)
                        {
                            Vector3 p = PositionAt(t, a);
                            if (Mathf.Abs(p.x) <= TableGeometry.NetHalfSpan && p.y > TableGeometry.TopY - r &&
                                p.y < TableGeometry.NetTopY + r)
                                tNet = t;
                        }
                    }
                }

                // Floor.
                if (Position.y >= r - 1e-5f)
                {
                    float t;
                    if (SolveDescendingCrossing(Position.y, Velocity.y, a.y, r, timeLeft, out t)) tFloor = t;
                }

                // Practice wall (analytic: z(t) is linear).
                float tWall = float.MaxValue;
                if (Wall != null)
                {
                    float t;
                    if (Wall.TryGetImpactTime(Position, Velocity, a, r, timeLeft, out t)) tWall = t;
                }

                float tFirst = Mathf.Min(Mathf.Min(tTable, tWall), Mathf.Min(tNet, tFloor));
                if (tFirst == float.MaxValue)
                {
                    Advance(timeLeft, a);
                    if (Wall != null && Wall.ClampInFront(before, ref Position, ref Velocity, r)) added += AddWallEvent(events);
                    timeLeft = 0f;
                    break;
                }

                Advance(tFirst, a);
                timeLeft -= tFirst;
                BallContactEvent e = new BallContactEvent();
                e.Point = Position;
                e.VelocityBefore = Velocity;

                if (tFirst == tWall && tWall != float.MaxValue)
                {
                    e.Type = BallContactType.Wall;
                    e.Side = 1;
                    Wall.Resolve(ref Position, ref Velocity, ref SpinAccel, r);
                }
                else if (tFirst == tTable)
                {
                    e.Type = BallContactType.Table;
                    e.Side = TableGeometry.SideOf(Position.z);
                    Position.y = tablePlane;
                    ResolveTableBounce();
                }
                else if (tFirst == tNet)
                {
                    float s = Mathf.Sign(Velocity.z);
                    if (Position.y >= TableGeometry.NetTopY - r * 0.3f)
                    {
                        // Clips the top tape: keeps going over with less pace and a small pop.
                        e.Type = BallContactType.NetCord;
                        Velocity.z *= 0.6f;
                        Velocity.x *= 0.8f;
                        Velocity.y = Mathf.Max(Velocity.y, 0f) * 0.3f + 0.7f;
                        Position.z = s * 0.0005f; // just past the plane: cannot retrigger
                    }
                    else
                    {
                        e.Type = BallContactType.Net;
                        Position.z = -s * (r + 0.001f);
                        Velocity.z = -Velocity.z * _cfg.netRestitution;
                        Velocity.x *= 0.6f;
                        Velocity.y *= 0.4f;
                        SpinAccel = Vector3.zero;
                    }
                    e.Side = TableGeometry.SideOf(Position.z);
                }
                else
                {
                    e.Type = BallContactType.Floor;
                    e.Side = TableGeometry.SideOf(Position.z);
                    Position.y = r;
                    Velocity.y = -Velocity.y * _cfg.floorRestitution;
                    Velocity.x *= _cfg.floorFriction;
                    Velocity.z *= _cfg.floorFriction;
                    SpinAccel = Vector3.zero;
                    if (Mathf.Abs(Velocity.y) < 0.15f) Velocity.y = 0f;
                }
                if (Wall != null && e.Type != BallContactType.Wall && Wall.ClampInFront(before, ref Position, ref Velocity, r))
                {
                    if (events != null) events.Add(e);
                    added++;
                    added += AddWallEvent(events);
                    continue;
                }
                if (events != null) events.Add(e);
                added++;
            }
            return added;
        }

        int AddWallEvent(List<BallContactEvent> events)
        {
            if (events == null) return 1;
            BallContactEvent w = new BallContactEvent();
            w.Type = BallContactType.Wall;
            w.Point = Position;
            w.VelocityBefore = Velocity;
            w.Side = 1;
            events.Add(w);
            return 1;
        }

        void ResolveTableBounce()
        {
            float topspin = -SpinAccel.y; // dip accel => topspin, positive means forward kick
            float dir = Mathf.Abs(Velocity.z) > 1e-4f ? Mathf.Sign(Velocity.z) : 0f;
            Velocity.y = -Velocity.y * _cfg.tableRestitution;
            Velocity.x *= _cfg.tableFriction;
            Velocity.z = Velocity.z * _cfg.tableFriction + dir * topspin * _cfg.topspinKick;
            // Keep the ball moving in the same direction even with heavy backspin.
            if (dir != 0f && Velocity.z * dir < 0.4f) Velocity.z = dir * 0.4f;
            SpinAccel *= _cfg.spinDecayOnBounce;
        }

        void Advance(float t, Vector3 a)
        {
            Position = PositionAt(t, a);
            Velocity = Velocity + a * t;
        }

        Vector3 PositionAt(float t, Vector3 a)
        {
            return Position + Velocity * t + a * (0.5f * t * t);
        }

        /// <summary>
        /// Smallest t in [0, maxT] where y0 + v t + 0.5 a t^2 == target while moving downward.
        /// </summary>
        public static bool SolveDescendingCrossing(float y0, float v, float a, float target, float maxT, out float t)
        {
            t = 0f;
            float c = y0 - target;
            if (c < -1e-5f) return false; // already below
            float qa = 0.5f * a, qb = v, qc = c;
            if (Mathf.Abs(qa) < 1e-7f)
            {
                if (qb >= 0f) return false;
                t = -qc / qb;
                return t >= 0f && t <= maxT;
            }
            float disc = qb * qb - 4f * qa * qc;
            if (disc < 0f) return false;
            float sq = Mathf.Sqrt(disc);
            float t1 = (-qb - sq) / (2f * qa);
            float t2 = (-qb + sq) / (2f * qa);
            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
            if (t1 >= 0f && t1 <= maxT && v + a * t1 <= 0f) { t = t1; return true; }
            if (t2 >= 0f && t2 <= maxT && v + a * t2 <= 0f) { t = t2; return true; }
            return false;
        }
    }
}
