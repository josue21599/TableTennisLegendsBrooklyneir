using Brooklyneir.Config;
using UnityEngine;

namespace Brooklyneir.Physics
{
    /// <summary>
    /// Vertical practice wall: the plane z = PlaneZ, facing the player on the negative-z side,
    /// from the floor up to TopY and from -HalfWidth to +HalfWidth in x. Pure C#, no allocations.
    ///
    /// Contact: the ball's z acceleration is always zero in this project (see BallSimulation), so
    /// z(t) is linear and the impact time is exact: t = (PlaneZ - r - z) / vz. There is no
    /// sampling, so the ball cannot tunnel through the wall at any speed. ClampInFront is a second
    /// safety net for the impossible case; it counts its activations so tests (and the game, in a
    /// warning) can prove it never fires in normal play.
    ///
    /// Bounce ("training partner" return): the z speed comes from the restitution (capped), the
    /// sideways speed from the friction, and the vertical speed is solved with
    /// ShotSolver.SolveFlattened so the ball comes straight back and bounces at ReturnBounceDepth
    /// on the player's half, clearing the net by at least ReturnNetClearance. An optional,
    /// bounded Gaussian variation moves that landing point; it is a pure function of the impact
    /// (position, velocity) and Seed, so predictions made on a copied simulation (landing
    /// marker, racket height) see exactly the same return as the real ball.
    /// </summary>
    public class WallCollision
    {
        public float PlaneZ;
        public float HalfWidth;
        public float TopY;
        /// <summary>Fraction of the z speed kept on impact (upper bound of the return speed).</summary>
        public float Restitution;
        /// <summary>Fraction of the sideways (x) speed kept on impact.</summary>
        public float Friction;
        /// <summary>Kept for compatibility; the return is spin-free.</summary>
        public float SpinDecay;
        /// <summary>Largest z speed the ball comes back with (m/s).</summary>
        public float MaxReturnSpeed = 100f;

        /// <summary>Physics used to solve the return. Null = plain bounce (no solved return).</summary>
        public PhysicsConfig Physics;
        /// <summary>z of the return bounce on the player's half (m, negative).</summary>
        public float ReturnBounceDepth = -0.75f;
        /// <summary>Minimum height above the net tape for the return (m).</summary>
        public float ReturnNetClearance = 0.30f;
        /// <summary>Standard deviation (m) of the landing point; 0 = exact. Bounded to 2 sigma.</summary>
        public float ReturnVariation;
        public int Seed;

        /// <summary>Returns never land closer than this to a side line (table geometry, not tuning).</summary>
        public const float SideMargin = 0.20f;
        public const float VariationBound = 2f; // in standard deviations

        /// <summary>Times the safety clamp had to act. Must stay 0 in normal play.</summary>
        public int ClampActivations { get; private set; }
        public Vector3 LastClampPosition { get; private set; }

        public WallCollision(float planeZ, float halfWidth, float topY, float restitution, float friction, float spinDecay)
        {
            PlaneZ = planeZ;
            HalfWidth = halfWidth;
            TopY = topY;
            Restitution = restitution;
            Friction = friction;
            SpinDecay = spinDecay;
        }

        /// <summary>z of the ball centre when its surface touches the wall.</summary>
        public float FaceZ(float radius) { return PlaneZ - radius; }

        bool InsideWallRect(Vector3 p, float radius)
        {
            return Mathf.Abs(p.x) <= HalfWidth + radius * 0.5f && p.y <= TopY + radius * 0.5f && p.y >= -radius;
        }

        /// <summary>
        /// Time in [0, maxT] at which the ball (moving towards the wall) touches it, if it hits
        /// the wall rectangle. Balls going over or beside the wall do not hit it.
        /// </summary>
        public bool TryGetImpactTime(Vector3 pos, Vector3 vel, Vector3 accel, float radius, float maxT, out float t)
        {
            t = 0f;
            if (vel.z <= 1e-5f) return false;
            float face = FaceZ(radius);
            if (pos.z > face + 1e-5f) return false; // behind the face: came over or beside the wall
            t = (face - pos.z) / vel.z;
            if (t < 0f) t = 0f;
            if (t > maxT) return false;
            Vector3 p = pos + vel * t + accel * (0.5f * t * t);
            return InsideWallRect(p, radius);
        }

        /// <summary>Bounce off the wall. Call with the ball at the contact point.</summary>
        public void Resolve(ref Vector3 pos, ref Vector3 vel, ref Vector3 spin, float radius)
        {
            Vector3 incoming = vel;
            pos.z = FaceZ(radius) - 0.0005f;
            float vz = Mathf.Max(0.5f, Mathf.Min(Mathf.Abs(incoming.z) * Restitution, MaxReturnSpeed));
            float vx = incoming.x * Friction;
            spin = Vector3.zero;
            if (Physics == null)
            {
                vel = new Vector3(vx, incoming.y, -vz);
                return;
            }

            Vector3 target = ReturnTarget(pos, incoming, vx, vz, radius);
            Vector3 g = new Vector3(0f, -Physics.gravity, 0f);
            float desiredTime = (pos.z - target.z) / vz;
            ShotSolution sol = ShotSolver.SolveFlattened(pos, target, desiredTime, g, Physics, ReturnNetClearance + 0.3f, 0.8f);
            Vector3 v = sol.Velocity;
            float t = sol.FlightTime;
            // Guarantee the configured margin over the net: a slower, higher return if needed.
            float required = TableGeometry.NetTopY + radius + ReturnNetClearance;
            for (int i = 0; i < 24 && ShotSolver.HeightAtNet(pos, v, g, t) < required; i++)
            {
                t *= 1.06f;
                v = ShotSolver.SolveVelocity(pos, target, t, g);
            }
            vel = v;
        }

        /// <summary>
        /// Landing point of the return: ReturnBounceDepth on the player's half, at the x where the
        /// friction-reduced sideways speed takes it, plus the optional bounded variation, always
        /// kept on the player's half and away from the side lines.
        /// </summary>
        public Vector3 ReturnTarget(Vector3 pos, Vector3 incoming, float vx, float vz, float radius)
        {
            float z = ReturnBounceDepth;
            float t0 = (pos.z - z) / Mathf.Max(0.5f, vz);
            float x = pos.x + vx * t0;
            if (ReturnVariation > 0f)
            {
                float gx, gz;
                Gaussian2(pos, incoming, out gx, out gz);
                float bound = VariationBound * ReturnVariation;
                x += Mathf.Clamp(gx * ReturnVariation, -bound, bound);
                z += Mathf.Clamp(gz * ReturnVariation, -bound, bound);
            }
            float side = TableGeometry.HalfWidth - SideMargin;
            x = Mathf.Clamp(x, -side, side);
            z = Mathf.Clamp(z, -TableGeometry.HalfLength + 0.15f, -0.2f);
            return new Vector3(x, TableGeometry.TopY + radius, z);
        }

        /// <summary>Two standard normal values, a pure function of the impact and Seed.</summary>
        void Gaussian2(Vector3 pos, Vector3 vel, out float a, out float b)
        {
            unchecked
            {
                uint h = (uint)Seed * 0x9E3779B1u;
                h = Mix(h ^ (uint)(int)(pos.x * 4096f));
                h = Mix(h ^ (uint)(int)(pos.y * 4096f));
                h = Mix(h ^ (uint)(int)(vel.x * 4096f));
                h = Mix(h ^ (uint)(int)(vel.y * 4096f));
                h = Mix(h ^ (uint)(int)(vel.z * 4096f));
                float u1 = ((h >> 8) + 0.5f) / 16777216f;
                h = Mix(h + 0x9E3779B9u);
                float u2 = (h >> 8) / 16777216f;
                float r = Mathf.Sqrt(-2f * Mathf.Log(u1));
                float ang = 2f * Mathf.PI * u2;
                a = r * Mathf.Cos(ang);
                b = r * Mathf.Sin(ang);
            }
        }

        static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>
        /// Safety net: if the ball went from in front of the face to behind it while inside the
        /// wall rectangle (which TryGetImpactTime makes impossible), put it back in front and
        /// bounce it. Returns true and counts the activation if it had to act.
        /// </summary>
        public bool ClampInFront(Vector3 before, ref Vector3 pos, ref Vector3 vel, float radius)
        {
            float face = FaceZ(radius);
            if (pos.z <= face + 1e-4f) return false;
            if (before.z > face + 1e-4f) return false; // was already behind (went over or beside)
            if (!InsideWallRect(pos, radius)) return false;
            LastClampPosition = pos;
            ClampActivations++;
            pos.z = face - 0.0005f;
            if (vel.z > 0f) vel.z = -vel.z * Restitution;
            return true;
        }
    }
}
