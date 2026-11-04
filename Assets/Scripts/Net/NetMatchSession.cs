using Brooklyneir.Config;
using Brooklyneir.Gameplay;
using Brooklyneir.PowerUps;
using UnityEngine;

namespace Brooklyneir.Net
{
    public enum NetRole { Host, Guest }

    public enum SessionState
    {
        Playing,
        Paused,
        /// <summary>Nothing heard for a while: both phones frozen, waiting for the other.</summary>
        WaitingReconnect,
        /// <summary>Heard again; the rally in play is void and both restart at a common tick.</summary>
        Resyncing,
        /// <summary>No reconnection in time: the match does not count.</summary>
        Void,
        Finished,
        OpponentLeft,
        Left
    }

    /// <summary>Sends a packet to the other phone (Unity Transport in the game, a fake link in tests).</summary>
    public interface INetLink
    {
        void Send(byte[] data, int length, bool reliable);
    }

    /// <summary>What the game does when something arrives. Ball states are already applied to Session.Ball.</summary>
    public interface INetMatchListener
    {
        void OnRemoteServe(BallLaunchMsg m);
        /// <summary>Host: accepted = the arbiter took the guest's hit. Guest: always true.</summary>
        void OnRemoteHitBegin(HitBeginMsg m, bool accepted);
        void OnRemoteHitLaunch(BallLaunchMsg m);
        void OnPowerRequest(PowerRequestMsg m);          // host only
        void OnPowerState(PowerStateMsg m);              // guest only
        void OnShieldSave(ShieldSaveMsg m);              // guest only
        void OnOrbState(OrbStateMsg m);                  // guest only
        void OnPointResult(PointResultMsg m);            // guest only (ScoreMirror already updated)
        void OnHostPointConfirmed(int winner, PointReason reason); // host only
        void OnPaused(PauseMsg m);
        void OnResumed(uint tick);
        void OnReconnected();                            // host: call HostVoidRally
        void OnRallyVoided(int server, uint resumeTick);
        void OnStateChanged(SessionState state);
    }

    /// <summary>
    /// The LAN logic of one phone (host or guest), independent of Unity and of the transport. The
    /// game feeds it local events and incoming packets; it keeps the shared clock, applies the
    /// other phone's ball states (fast-forwarded), runs the host's arbiter, mirrors the score on
    /// the guest, sends racket positions, pings, pauses, resumes and detects silences. The same
    /// class is used by the real game (step 7) and by the two-phone simulation in the tests.
    ///
    /// Rule for ball states: a state based on a launch older than this phone's latest own launch
    /// (NetBallState.BasedOn) is ignored, so a message computed before the other phone heard about
    /// a hit can never erase it. The pause snapshot is the exception: it is the host's decision.
    /// </summary>
    public sealed class NetMatchSession
    {
        public readonly NetRole Role;
        public readonly NetConfig Config;
        public readonly ClockSync Clock;
        public readonly BallSync Ball;
        public readonly HostArbiter Arbiter;      // host only
        public readonly ScoreMirror Mirror;       // guest only
        public readonly RemotePaddleBuffer RemotePaddle = new RemotePaddleBuffer();

        /// <summary>Slow-motion scales set by the game every frame (from PowerUpSystem.BallTimeScale).</summary>
        public float ScaleTowardP0 = 1f, ScaleTowardP1 = 1f;

        readonly INetLink _link;
        INetMatchListener _listener;
        readonly byte[] _out = new byte[NetProtocol.MaxPacket];
        readonly NetWriter _w;
        readonly NetReader _r = new NetReader();

        double _now, _lastReceived, _lastPing, _lastPaddle, _silenceStart;
        bool _started;
        uint _paddleSeq;
        Vector3 _paddlePos, _paddleTarget;
        uint _lastLocalLaunch;
        bool _hasLocalLaunch;
        bool _dropNextGuestLaunch;
        bool _pointReported;
        uint _pausedTick, _resumeTick;
        bool _resumeScheduled;
        NetBallState _pausedBall;

        public SessionState State { get; private set; }
        public uint Now { get; private set; }
        /// <summary>First tick of the match (MatchSetup.StartTick): nobody serves before it.</summary>
        public uint StartTick { get; set; }
        /// <summary>True once the guest's clock is synced and the start tick has come (the host only waits for the tick).</summary>
        public bool Started { get { return (Role == NetRole.Host || Clock.IsSynced) && Now >= StartTick; } }
        public int LocalPlayer { get { return Role == NetRole.Host ? 0 : 1; } }
        public int RemotePlayer { get { return 1 - LocalPlayer; } }
        /// <summary>While true the ball must not move (paused, waiting, or before the resume tick).</summary>
        public bool BallFrozen { get { return State != SessionState.Playing; } }
        /// <summary>A restart (after a pause or a void rally) is scheduled for ResumeTick.</summary>
        public bool ResumeScheduled { get { return _resumeScheduled; } }
        public uint ResumeTick { get { return _resumeTick; } }

        public NetMatchSession(NetRole role, NetConfig config, PhysicsConfig physics, INetLink link, int firstServer)
        {
            Role = role;
            Config = config;
            _link = link;
            _w = new NetWriter(_out);
            Ball = new BallSync(physics);
            Ball.SmoothingSeconds = config.ballCorrectionSmoothingSeconds;
            Clock = role == NetRole.Host ? ClockSync.ForHost(Ball.TickRate) : new ClockSync(Ball.TickRate, config.clockSamples);
            RemotePaddle.MaxGap = 0.25f;
            if (role == NetRole.Host) Arbiter = new HostArbiter(1, 0);
            else Mirror = new ScoreMirror(firstServer);
            State = SessionState.Playing;
        }

        public void SetListener(INetMatchListener listener) { _listener = listener; }

        void SetState(SessionState s)
        {
            if (State == s) return;
            State = s;
            if (_listener != null) _listener.OnStateChanged(s);
        }

        bool Over { get { return State == SessionState.Void || State == SessionState.Finished || State == SessionState.OpponentLeft || State == SessionState.Left; } }

        void Send(bool reliable) { if (!_w.Overflow) _link.Send(_out, _w.Length, reliable); }

        // =========================================================== per frame

        /// <summary>Call once per frame with this phone's clock (seconds).</summary>
        public void Update(double localTime)
        {
            _now = localTime;
            if (!_started) { _started = true; _lastReceived = localTime; _lastPing = double.NegativeInfinity; _lastPaddle = double.NegativeInfinity; }
            Clock.Update(localTime);
            Now = Clock.Tick(localTime);
            if (Arbiter != null)
                Arbiter.GraceTicks = (int)(Config.PointGraceMs((float)(Clock.RoundTrip * 1000.0)) * Ball.TickRate / 1000);
            if (Over) return;

            if (Role == NetRole.Guest && localTime - _lastPing >= (Clock.IsSynced ? Config.pingIntervalSeconds : 0.25))
            {
                _lastPing = localTime;
                NetProtocol.Write(_w, new PingMsg { T0 = localTime });
                Send(false);
            }
            if (localTime - _lastPaddle >= 1.0 / Config.paddleSendRate)
            {
                _lastPaddle = localTime;
                NetProtocol.Write(_w, new PaddleMsg { Seq = ++_paddleSeq, Tick = Now, PosX = _paddlePos.x, PosZ = _paddlePos.z, TargetX = _paddleTarget.x, TargetZ = _paddleTarget.z });
                Send(false);
            }

            // Silence: freeze both phones; too long: the match does not count.
            if (State == SessionState.Playing || State == SessionState.Paused || State == SessionState.Resyncing)
            {
                if (localTime - _lastReceived > Config.silenceTimeoutSeconds)
                {
                    _silenceStart = _lastReceived;
                    _resumeScheduled = false;
                    SetState(SessionState.WaitingReconnect);
                }
            }
            if (State == SessionState.WaitingReconnect && localTime - _silenceStart > Config.reconnectWindowSeconds)
            {
                SetState(SessionState.Void);
                return;
            }

            // Scheduled restart (after a pause or a void rally), at the same tick on both phones.
            if (_resumeScheduled && Now >= _resumeTick && (State == SessionState.Paused || State == SessionState.Resyncing))
            {
                _resumeScheduled = false;
                bool fromPause = State == SessionState.Paused;
                if (fromPause)
                {
                    Ball.Apply(_pausedBall, _resumeTick, _resumeTick, ScaleTowardP0, ScaleTowardP1, null);
                    if (Arbiter != null && _resumeTick > _pausedTick) Arbiter.ShiftPending(_resumeTick - _pausedTick);
                }
                else Ball.SetTick(_resumeTick);
                SetState(SessionState.Playing);
                if (_listener != null) _listener.OnResumed(_resumeTick);
            }

            if (Arbiter != null && State == SessionState.Playing)
            {
                Arbiter.Update(Now);
                if (Arbiter.PointConfirmed && !_pointReported)
                {
                    _pointReported = true;
                    if (_listener != null) _listener.OnHostPointConfirmed(Arbiter.Winner, Arbiter.Reason);
                }
            }
        }

        // =========================================================== local events

        public void LocalPaddle(Vector3 position, Vector3 target) { _paddlePos = position; _paddleTarget = target; }

        void MarkLocalLaunch(uint tick) { _lastLocalLaunch = tick; _hasLocalLaunch = true; }
        bool IsStale(NetBallState s) { return _hasLocalLaunch && s.BasedOn < _lastLocalLaunch; }

        /// <summary>A rally starts on the host (its own serve or the guest's): arbiter ready, point not reported.</summary>
        public void HostStartRally(int server)
        {
            if (Arbiter == null) return;
            Arbiter.StartRally(server);
            _pointReported = false;
        }

        public void LocalServe(BallLaunchMsg m)
        {
            m.Player = (byte)LocalPlayer;
            MarkLocalLaunch(m.Tick);
            HostStartRally(LocalPlayer);
            NetProtocol.Write(_w, MsgType.Serve, m);
            Send(true);
        }

        public void LocalHitBegin(Vector3 contact, uint tick)
        {
            NetProtocol.Write(_w, new HitBeginMsg { Tick = tick, Player = (byte)LocalPlayer, Contact = contact });
            Send(true);
        }

        public void LocalHitLaunch(BallLaunchMsg m)
        {
            m.Player = (byte)LocalPlayer;
            MarkLocalLaunch(m.Tick);
            NetProtocol.Write(_w, MsgType.HitLaunch, m);
            Send(true);
        }

        public void GuestRequestPower(PowerAction action)
        {
            if (Role != NetRole.Guest || Over) return;
            NetProtocol.Write(_w, new PowerRequestMsg { Player = (byte)LocalPlayer, Action = action });
            Send(true);
        }

        /// <summary>Host: the whole power state (with the ball when its speed changes, e.g. slow-motion on/off).</summary>
        public PowerStateMsg HostSendPowers(PowerUpSystem powers, bool withBall)
        {
            PowerStateMsg m = new PowerStateMsg { Tick = Ball.Tick, P0 = NetPowerPlayer.From(powers.Players[0]), P1 = NetPowerPlayer.From(powers.Players[1]), HasBall = withBall && Ball.Sim.Active };
            if (m.HasBall) m.Ball = Ball.Capture();
            if (Role != NetRole.Host || Over) return m;
            NetProtocol.Write(_w, m);
            Send(true);
            return m;
        }

        /// <summary>Host: call after launching the shield's defensive shot with Ball.LocalLaunch.</summary>
        public void HostShieldSave(int player)
        {
            if (Role != NetRole.Host) return;
            MarkLocalLaunch(Ball.LastLaunchTick);
            NetProtocol.Write(_w, new ShieldSaveMsg { Tick = Ball.Tick, Player = (byte)player, Ball = Ball.Capture() });
            Send(true);
        }

        public void HostSendOrb(OrbStateMsg m)
        {
            if (Role != NetRole.Host || Over) return;
            NetProtocol.Write(_w, m);
            Send(true);
        }

        /// <summary>Host: the point just awarded. Send any power changes before it: the last point closes the session.</summary>
        public void HostSendPoint(PointResultMsg m)
        {
            if (Role != NetRole.Host || Over) return;
            NetProtocol.Write(_w, m);
            Send(true);
            if (m.MatchWon) SetState(SessionState.Finished);
        }

        public void RequestPause(PauseReason reason)
        {
            if (Over) return;
            if (Role == NetRole.Host) DoPause(LocalPlayer, reason);
            else
            {
                NetProtocol.Write(_w, new PauseMsg { Player = (byte)LocalPlayer, Reason = reason, Tick = Now });
                Send(true);
            }
        }

        public void RequestResume()
        {
            if (Over) return;
            if (Role == NetRole.Host) DoResume();
            else
            {
                NetProtocol.Write(_w, new ResumeAtMsg { Tick = Now });
                Send(true);
            }
        }

        void DoPause(int player, PauseReason reason)
        {
            if (State != SessionState.Playing) return;
            _pausedTick = Ball.Tick;
            _pausedBall = Ball.Capture();
            _resumeScheduled = false;
            PauseMsg m = new PauseMsg { Player = (byte)player, Reason = reason, Tick = _pausedTick, Ball = _pausedBall };
            NetProtocol.Write(_w, m);
            Send(true);
            SetState(SessionState.Paused);
            if (_listener != null) _listener.OnPaused(m);
        }

        void DoResume()
        {
            if (State != SessionState.Paused || _resumeScheduled) return;
            _resumeTick = Now + (uint)Mathf.RoundToInt(Config.resumeCountdownSeconds * Ball.TickRate);
            _resumeScheduled = true;
            NetProtocol.Write(_w, new ResumeAtMsg { Tick = _resumeTick, Ball = _pausedBall });
            Send(true);
        }

        /// <summary>Host, after a reconnection: the rally in play does not count; both restart at a common tick.</summary>
        public void HostVoidRally(int server)
        {
            if (Role != NetRole.Host || Over) return;
            Arbiter.Abort();
            _pointReported = true; // nothing to report until the next rally starts
            _resumeTick = Now + (uint)Mathf.RoundToInt(Config.resumeCountdownSeconds * Ball.TickRate);
            _resumeScheduled = true;
            NetProtocol.Write(_w, new RallyVoidMsg { ResumeTick = _resumeTick, Server = (byte)server });
            Send(true);
            SetState(SessionState.Resyncing);
            if (_listener != null) _listener.OnRallyVoided(server, _resumeTick);
        }

        public void Leave()
        {
            if (Over) return;
            NetProtocol.Write(_w, new ByeMsg { Reason = ByeReason.Quit });
            Send(true);
            SetState(SessionState.Left);
        }

        // =========================================================== incoming

        /// <summary>Feed every packet received from the other phone.</summary>
        public void Receive(byte[] data, int length, double localTime)
        {
            if (Over) return;
            _lastReceived = localTime;
            if (State == SessionState.WaitingReconnect)
            {
                if (Role == NetRole.Host)
                {
                    SetState(SessionState.Resyncing);
                    if (_listener != null) _listener.OnReconnected();
                }
                else SetState(SessionState.Resyncing); // waits for the host's RallyVoid
            }
            _r.Reset(data, length);
            MsgType type = NetProtocol.PeekType(data, length);
            switch (type)
            {
                case MsgType.Ping:
                {
                    PingMsg m;
                    if (Role != NetRole.Host || !NetProtocol.TryRead(_r, out m)) return;
                    NetProtocol.Write(_w, new PongMsg { T0 = m.T0, T1 = localTime });
                    Send(false);
                    return;
                }
                case MsgType.Pong:
                {
                    PongMsg m;
                    if (Role == NetRole.Guest && NetProtocol.TryRead(_r, out m)) Clock.AddSample(m.T0, m.T1, localTime);
                    return;
                }
                case MsgType.Paddle:
                {
                    PaddleMsg m;
                    if (NetProtocol.TryRead(_r, out m)) RemotePaddle.Push(m.Seq, m.Tick / (double)Ball.TickRate, new Vector3(m.PosX, 0f, m.PosZ));
                    return;
                }
                case MsgType.Serve:
                case MsgType.HitLaunch:
                {
                    BallLaunchMsg m;
                    if (!NetProtocol.TryRead(_r, type, out m) || m.Player != RemotePlayer) return;
                    if (type == MsgType.HitLaunch && _dropNextGuestLaunch) { _dropNextGuestLaunch = false; return; }
                    if (State != SessionState.Playing && Role == NetRole.Host) return; // a launch during a pause does not count
                    if (type == MsgType.Serve) { _hasLocalLaunch = false; HostStartRally(RemotePlayer); }
                    m.Ball.BasedOn = m.Tick;
                    if (IsStale(m.Ball)) return;
                    Ball.Apply(m.Ball, m.Tick, Now, ScaleTowardP0, ScaleTowardP1, null);
                    if (_listener != null)
                    {
                        if (type == MsgType.Serve) _listener.OnRemoteServe(m);
                        else _listener.OnRemoteHitLaunch(m);
                    }
                    return;
                }
                case MsgType.HitBegin:
                {
                    HitBeginMsg m;
                    if (!NetProtocol.TryRead(_r, out m) || m.Player != RemotePlayer) return;
                    bool accepted = true;
                    if (Arbiter != null)
                    {
                        accepted = Arbiter.GuestHit(m.Tick) == GuestHitDecision.Accepted;
                        _dropNextGuestLaunch = !accepted;
                    }
                    if (accepted && !(_hasLocalLaunch && m.Tick < _lastLocalLaunch)) Ball.Sim.Hold(m.Contact);
                    if (_listener != null) _listener.OnRemoteHitBegin(m, accepted);
                    return;
                }
                case MsgType.PowerRequest:
                {
                    PowerRequestMsg m;
                    if (Role == NetRole.Host && NetProtocol.TryRead(_r, out m) && m.Player == RemotePlayer && _listener != null) _listener.OnPowerRequest(m);
                    return;
                }
                case MsgType.PowerState:
                {
                    PowerStateMsg m;
                    if (Role != NetRole.Guest || !NetProtocol.TryRead(_r, out m)) return;
                    if (m.HasBall && !IsStale(m.Ball)) Ball.Apply(m.Ball, m.Tick, Now, ScaleTowardP0, ScaleTowardP1, null);
                    if (_listener != null) _listener.OnPowerState(m);
                    return;
                }
                case MsgType.ShieldSave:
                {
                    ShieldSaveMsg m;
                    if (Role != NetRole.Guest || !NetProtocol.TryRead(_r, out m)) return;
                    if (!IsStale(m.Ball)) Ball.Apply(m.Ball, m.Tick, Now, ScaleTowardP0, ScaleTowardP1, null);
                    if (_listener != null) _listener.OnShieldSave(m);
                    return;
                }
                case MsgType.OrbState:
                {
                    OrbStateMsg m;
                    if (Role == NetRole.Guest && NetProtocol.TryRead(_r, out m) && _listener != null) _listener.OnOrbState(m);
                    return;
                }
                case MsgType.PointResult:
                {
                    PointResultMsg m;
                    if (Role != NetRole.Guest || !NetProtocol.TryRead(_r, out m)) return;
                    Mirror.Apply(m);
                    if (_listener != null) _listener.OnPointResult(m);
                    if (m.MatchWon) SetState(SessionState.Finished);
                    return;
                }
                case MsgType.Pause:
                {
                    PauseMsg m;
                    if (!NetProtocol.TryRead(_r, out m)) return;
                    if (Role == NetRole.Host) { DoPause(m.Player, m.Reason); return; } // the guest asks
                    if (State == SessionState.Paused) return;
                    _pausedTick = m.Tick;
                    _pausedBall = m.Ball;
                    _resumeScheduled = false;
                    Ball.Apply(m.Ball, m.Tick, m.Tick, ScaleTowardP0, ScaleTowardP1, null);
                    SetState(SessionState.Paused);
                    if (_listener != null) _listener.OnPaused(m);
                    return;
                }
                case MsgType.ResumeAt:
                {
                    ResumeAtMsg m;
                    if (!NetProtocol.TryRead(_r, out m)) return;
                    if (Role == NetRole.Host) { DoResume(); return; } // the guest asks
                    if (State != SessionState.Paused) return;
                    _resumeTick = m.Tick;
                    _pausedBall = m.Ball;
                    _resumeScheduled = true;
                    return;
                }
                case MsgType.RallyVoid:
                {
                    RallyVoidMsg m;
                    if (Role != NetRole.Guest || !NetProtocol.TryRead(_r, out m)) return;
                    _resumeTick = m.ResumeTick;
                    _resumeScheduled = true;
                    _hasLocalLaunch = false;
                    SetState(SessionState.Resyncing);
                    if (_listener != null) _listener.OnRallyVoided(m.Server, m.ResumeTick);
                    return;
                }
                case MsgType.Bye:
                {
                    ByeMsg m;
                    if (NetProtocol.TryRead(_r, out m)) SetState(SessionState.OpponentLeft);
                    return;
                }
            }
        }
    }
}
