using Brooklyneir.AI;
using Brooklyneir.Physics;
using Brooklyneir.PowerUps;
using UnityEngine;

namespace Brooklyneir.Gameplay
{
    /// <summary>
    /// What moves one racket during a match. The match asks its driver instead of checking
    /// "is it human / is there an AI": touch (a finger on this phone, handled by the touch input),
    /// CPU (wraps the existing AIPlayer unchanged) or network (positions from the other phone).
    /// </summary>
    public abstract class PaddleDriver
    {
        public abstract PlayerControl Kind { get; }
        /// <summary>Hits of this racket are detected on this phone (false for the network: they arrive as messages).</summary>
        public virtual bool DetectsHitsLocally { get { return true; } }
        /// <summary>Hold the ball on the blade for the hit-stop and read the strongest stroke (fingers only).</summary>
        public virtual bool UsesHitDwell { get { return false; } }
        public virtual float NoiseMultiplier(float humanDefault) { return humanDefault; }

        public virtual void ResetForPoint() { }
        public virtual void BeginServeWait() { }
        public virtual void Tick(float dt, bool ballComingToMe) { }
        public virtual void ConsiderPowers(PowerUpSystem powers, float dt, bool ballComingToMe) { }
        /// <summary>True when the driver serves now (only the CPU serves by itself).</summary>
        public virtual bool TickServe(float dt) { return false; }
        public virtual void PrepareSwing(float ballHeightAboveTable) { }
        public virtual void OnOpponentShot(BallSimulation ball) { }
    }

    /// <summary>Racket nobody moves (the side of the ball machine in training).</summary>
    public sealed class NoPaddleDriver : PaddleDriver
    {
        public override PlayerControl Kind { get { return PlayerControl.None; } }
    }

    /// <summary>A finger on this phone. Movement and the serve tap come from TouchInputController.</summary>
    public sealed class TouchPaddleDriver : PaddleDriver
    {
        readonly RacketController _racket;

        public TouchPaddleDriver(RacketController racket) { _racket = racket; }

        public override PlayerControl Kind { get { return PlayerControl.Local; } }
        public override bool UsesHitDwell { get { return true; } }

        /// <summary>A finger's stroke is always the measured one, never a CPU-decided swing.</summary>
        public override void ResetForPoint() { if (_racket != null) _racket.ClearSyntheticSwing(); }
    }

    /// <summary>The CPU: the same AIPlayer calls the match made before, in the same order.</summary>
    public sealed class CpuPaddleDriver : PaddleDriver
    {
        public readonly AIPlayer Ai;

        public CpuPaddleDriver(AIPlayer ai) { Ai = ai; }

        public override PlayerControl Kind { get { return PlayerControl.Cpu; } }
        public override float NoiseMultiplier(float humanDefault) { return Ai.NoiseMultiplier; }
        public override void ResetForPoint() { Ai.ResetForPoint(); }
        public override void BeginServeWait() { Ai.BeginServeWait(); }
        public override void Tick(float dt, bool ballComingToMe) { Ai.Tick(dt, ballComingToMe); }
        public override void ConsiderPowers(PowerUpSystem powers, float dt, bool ballComingToMe) { Ai.ConsiderPowers(powers, dt, ballComingToMe); }
        public override bool TickServe(float dt) { return Ai.TickServe(dt); }
        public override void PrepareSwing(float ballHeightAboveTable) { Ai.DecideSwing(ballHeightAboveTable); }
        public override void OnOpponentShot(BallSimulation ball) { Ai.OnOpponentShot(ball); }
    }

    /// <summary>
    /// The player on the other phone. The racket follows positions from a RemotePaddleBuffer,
    /// drawn a little in the past. Its hits are never detected here: they arrive as messages
    /// (phase 2). In phase 1 nothing feeds the buffer yet.
    /// </summary>
    public sealed class RemotePaddleDriver : PaddleDriver
    {
        readonly RacketController _racket;
        public readonly RemotePaddleBuffer Buffer = new RemotePaddleBuffer();
        /// <summary>How far in the past the remote racket is drawn (s).</summary>
        public float RenderDelay = 0.1f;
        /// <summary>Shared match clock (s). Phase 1: advanced by Tick; phase 2: set from the synced clock.</summary>
        public double Clock;

        public RemotePaddleDriver(RacketController racket) { _racket = racket; }

        public override PlayerControl Kind { get { return PlayerControl.Remote; } }
        public override bool DetectsHitsLocally { get { return false; } }

        public override void Tick(float dt, bool ballComingToMe)
        {
            Clock += dt;
            Vector3 p;
            if (Buffer.TrySample(Clock - RenderDelay, out p)) _racket.SetTarget(p);
        }
    }
}
