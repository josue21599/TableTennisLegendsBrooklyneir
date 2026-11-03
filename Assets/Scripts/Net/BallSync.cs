using System.Collections.Generic;
using Brooklyneir.Config;
using Brooklyneir.Physics;
using UnityEngine;

namespace Brooklyneir.Net
{
    /// <summary>
    /// The ball in a LAN match, advanced in fixed ticks (one physics substep each, 1/240 s by
    /// default) instead of by frame time. BallSimulation.Step(dt) splits each frame into substeps
    /// plus a shorter remainder, so a 60 Hz and a 120 Hz phone would round differently even on the
    /// same CPU; counting whole ticks makes both phones run exactly the same substeps for the same
    /// instant. Pure, allocation-free, unit-tested. BallSimulation's public API is unchanged.
    ///
    /// Apply() replaces the ball with an authoritative state (serve, hit, shield save, pause...)
    /// taken at an earlier tick and fast-forwards it to the current tick, so the ball is where the
    /// other phone has it right now; any accumulated difference disappears at every hit. The
    /// physics jumps; only the drawing is smoothed (VisualOffset decays over a short time).
    /// </summary>
    public sealed class BallSync
    {
        public readonly BallSimulation Sim;
        readonly float _h;
        Vector3 _prev;
        Vector3 _visualOffset;

        /// <summary>Tick of the launch the current trajectory comes from (see NetBallState.BasedOn).</summary>
        public uint LastLaunchTick { get; private set; }

        /// <summary>Tick the simulation state corresponds to.</summary>
        public uint Tick { get; private set; }
        public int TickRate { get; private set; }
        /// <summary>Longest fast-forward in one call (ticks); a bigger gap jumps instead of hanging.</summary>
        public int MaxTicksPerAdvance = 240 * 5;
        /// <summary>Time constant of the visual smoothing after a correction (s). 0 = none.</summary>
        public float SmoothingSeconds = 0.1f;

        public BallSync(PhysicsConfig cfg)
        {
            Sim = new BallSimulation(cfg);
            _h = cfg.substep;
            TickRate = Mathf.RoundToInt(1f / cfg.substep);
        }

        /// <summary>Time scale of one tick: slow-motion applies to the ball heading to the slowed player.</summary>
        public static float ScaleFor(BallSimulation sim, float scaleTowardP0, float scaleTowardP1)
        {
            int heading = sim.Velocity.z > 0f ? 1 : 0;
            return heading == 1 ? scaleTowardP1 : scaleTowardP0;
        }

        /// <summary>Starts counting at a tick without changing the ball (match start, resume).</summary>
        public void SetTick(uint tick)
        {
            Tick = tick;
            _prev = Sim.Position;
        }

        /// <summary>Advances whole ticks up to targetTick. Contacts are appended to events (may be null). Returns ticks run.</summary>
        public int AdvanceTo(uint targetTick, float scaleTowardP0, float scaleTowardP1, List<BallContactEvent> events)
        {
            if (targetTick <= Tick) return 0;
            long gap = (long)targetTick - Tick;
            int run = 0;
            if (gap > MaxTicksPerAdvance)
            {
                // Far behind (clock glitch): do not hang; keep only the last part of the gap.
                Tick = targetTick - (uint)MaxTicksPerAdvance;
            }
            while (Tick < targetTick)
            {
                _prev = Sim.Position;
                if (Sim.Active) Sim.Substep(_h * ScaleFor(Sim, scaleTowardP0, scaleTowardP1), events);
                Tick++;
                run++;
            }
            return run;
        }

        /// <summary>
        /// Replaces the ball with a state taken at stateTick and fast-forwards it to nowTick. The
        /// drawn ball keeps its old place for a moment and slides to the new one (visual only).
        /// A state from the "future" (stateTick > nowTick) waits until the clock reaches it.
        /// </summary>
        public int Apply(NetBallState state, uint stateTick, uint nowTick, float scaleTowardP0, float scaleTowardP1, List<BallContactEvent> events)
        {
            Vector3 drawnBefore = Sim.Position + _visualOffset;
            LastLaunchTick = state.BasedOn;
            if (state.Active) Sim.Launch(state.Position, state.Velocity, state.Spin);
            else Sim.Hold(state.Position);
            Tick = stateTick;
            _prev = Sim.Position;
            int run = AdvanceTo(nowTick, scaleTowardP0, scaleTowardP1, events);
            _visualOffset = SmoothingSeconds > 0f ? drawnBefore - Sim.Position : Vector3.zero;
            if (_visualOffset.sqrMagnitude > 1f) _visualOffset = Vector3.zero; // a jump of > 1 m is not smoothed
            return run;
        }

        /// <summary>Current state, to send to the other phone.</summary>
        public NetBallState Capture()
        {
            NetBallState s = new NetBallState();
            s.Position = Sim.Position;
            s.Velocity = Sim.Velocity;
            s.Spin = Sim.SpinAccel;
            s.Active = Sim.Active;
            s.BasedOn = LastLaunchTick;
            return s;
        }

        /// <summary>This phone launches the ball (serve, hit, shield save) at tick: returns the state to send.</summary>
        public NetBallState LocalLaunch(Vector3 position, Vector3 velocity, Vector3 spin, uint tick, float scaleTowardP0, float scaleTowardP1)
        {
            NetBallState s = new NetBallState { Position = position, Velocity = velocity, Spin = new Vector3(spin.x, spin.y, 0f), Active = true, BasedOn = tick };
            Apply(s, tick, tick, scaleTowardP0, scaleTowardP1, null);
            return s;
        }

        /// <summary>Visual offset left by the last correction (draw position = physics + this).</summary>
        public Vector3 VisualOffset { get { return _visualOffset; } }

        /// <summary>Decays the visual offset; call once per frame.</summary>
        public void UpdateVisual(float dt)
        {
            if (SmoothingSeconds <= 0f || dt <= 0f) { _visualOffset = Vector3.zero; return; }
            _visualOffset *= Mathf.Exp(-dt / SmoothingSeconds);
            if (_visualOffset.sqrMagnitude < 1e-10f) _visualOffset = Vector3.zero;
        }

        /// <summary>Where to draw the ball: between the last two ticks (fraction 0..1) plus the visual offset.</summary>
        public Vector3 DrawPosition(float fraction)
        {
            return Vector3.Lerp(_prev, Sim.Position, Mathf.Clamp01(fraction)) + _visualOffset;
        }
    }
}
