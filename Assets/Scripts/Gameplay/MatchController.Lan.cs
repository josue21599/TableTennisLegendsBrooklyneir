using System;
using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.Net;
using Brooklyneir.Physics;
using UnityEngine;

namespace Brooklyneir.Gameplay
{
    /// <summary>
    /// LAN match (the bridge between MatchController and NetMatchSession). Each phone runs the
    /// same match code with these differences:
    /// - the ball advances in whole ticks of the shared clock (BallSync), so both phones run the
    ///   same physics substeps; states from the other phone are applied fast-forwarded;
    /// - only the local racket's hits are detected here; the other phone's arrive as messages;
    /// - the host's arbiter (HostArbiter) is the referee and decides every point; the guest keeps
    ///   a local referee only to know when it may hit, and replays the host's points on its
    ///   MatchScore (same rules, same first server, so the same score);
    /// - pauses, silences and void rallies freeze the ball on both phones (session state);
    /// - nothing starts before the start tick (MatchSetup) and the guest's clock is synced.
    /// Powers are off in LAN until step 7b-3.
    /// </summary>
    public partial class MatchController : INetMatchListener
    {
        NetMatchSession _net;
        BallSimulation _localSim;
        RallyReferee _localReferee;
        RemotePaddleDriver _remoteDriver;
        uint _lanTick;
        float _goTimer;
        int _lanCountdown = -1;
        bool _lanTimeUpShown;

        /// <summary>Raised on the main thread when the LAN session changes state (pause, silence, end).</summary>
        public event Action<SessionState> LanStateChanged;

        public NetMatchSession Net { get { return _net; } }
        public bool IsLan { get { return _net != null; } }
        public bool IsLanHost { get { return _net != null && _net.Role == NetRole.Host; } }
        public bool IsLanGuest { get { return _net != null && _net.Role == NetRole.Guest; } }
        /// <summary>The player on this phone in a LAN match (host 0, guest 1), or -1.</summary>
        public int LanLocalPlayer { get { return _net != null ? _net.LocalPlayer : -1; } }

        /// <summary>Starts a LAN match on this phone. config comes from the host's MatchSetup.</summary>
        public void StartLanMatch(MatchConfig config, NetMatchSession session, int firstServer, string rivalName)
        {
            config.powerUpsEnabled = false; // step 7b-3
            config.powerFrequency = PowerFrequency.Off;
            StartMatchCore(config, session.RemotePlayer, firstServer);
            _net = session;
            _sim = session.Ball.Sim;
            if (session.Role == NetRole.Host) Referee = session.Arbiter.Referee;
            Referee.Abort();
            session.SetListener(this);
            RivalName = rivalName;
            int me = session.LocalPlayer;
            var settings = Game.Save != null ? Game.Save.Data.settings : null;
            Rackets[me].Assist = settings != null && settings.assistP1; // this phone's own setting
            Rackets[1 - me].Assist = false;
            _remoteDriver = _drivers[session.RemotePlayer] as RemotePaddleDriver;
            if (_remoteDriver != null)
            {
                _remoteDriver.Buffer = session.RemotePaddle;
                _remoteDriver.ExternalClock = true;
                _remoteDriver.RenderDelay = session.Config.remoteRenderDelaySeconds;
            }
            _sim.Hold(HeldBallPosition(firstServer));
            session.Ball.SetTick(session.Now);
            _lanCountdown = -1;
            _goTimer = 0f;
            _lanTimeUpShown = false;
        }

        void LanDetach()
        {
            if (_net == null) return;
            _net.SetListener(null);
            _net = null;
            _remoteDriver = null;
            _sim = _localSim;
            Referee = _localReferee;
        }

        // =================================================================== frame

        void LanUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (dt <= 0f) return;
            _net.ScaleTowardP0 = Powers.BallTimeScale(0);
            _net.ScaleTowardP1 = Powers.BallTimeScale(1);
            int me = _net.LocalPlayer;
            _net.LocalPaddle(Rackets[me].Position, Rackets[me].Target);
            if (_remoteDriver != null) _remoteDriver.Clock = _net.Now / (double)_net.Ball.TickRate;
            if (_goTimer > 0f)
            {
                _goTimer -= dt;
                if (_goTimer <= 0f && Countdown != null) Countdown("");
            }

            if (_net.State != SessionState.Playing)
            {
                LanFrozen(dt);
                return;
            }
            if (_lanCountdown >= 100) { _lanCountdown = -1; if (Countdown != null) Countdown(""); } // a resume countdown just ended

            switch (Phase)
            {
                case MatchPhase.Intro: LanUpdateIntro(); break;
                case MatchPhase.PointOver:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        if (Score.MatchOver) FinishMatch();
                        else PrepareServe(Score.Server);
                    }
                    break;
            }
            if (Phase == MatchPhase.MatchOver || Phase == MatchPhase.Inactive) return;

            bool live = Phase == MatchPhase.Rally || Phase == MatchPhase.ServeWait;
            if (live) UpdateClock(dt);
            if (live) Powers.Update(dt);

            UpdateAgents(dt);
            for (int p = 0; p < 2; p++)
            {
                bool incoming = _ballInPlay && !Referee.RallyOver && Referee.Receiver == p;
                UpdateRacketPrediction(p, incoming);
                Rackets[p].Tick(dt, Powers.FollowSpeedMultiplier(p), _sim.Position, incoming);
                Rackets[p].SetFrozen(Powers.Players[p].Frozen);
                _shields[p].SetActive(Powers.Players[p].ShieldActive);
            }

            UpdatePendingHit(dt);
            if (Phase == MatchPhase.ServeWait || Phase == MatchPhase.Intro)
            {
                PlaceHeldBall(_server);
                _net.Ball.SetTick(_net.Now);
            }
            else LanAdvanceBall();
            LanDrawBall(dt);
        }

        void LanDrawBall(float dt)
        {
            _net.Ball.UpdateVisual(dt);
            Vector3 drawn = _sim.Position + _net.Ball.VisualOffset;
            _ballView.SetLook(CurrentLook());
            _ballView.Sync(drawn, dt);
            _camera.TrackBall(drawn, _ballInPlay);
            if (Game.Arena != null) Game.Arena.SetCrowdFocus(drawn, _ballInPlay || Phase == MatchPhase.ServeWait);
        }

        /// <summary>Paused, waiting for the other phone or about to resume: nothing moves; a countdown before resuming.</summary>
        void LanFrozen(float dt)
        {
            for (int p = 0; p < 2; p++) Rackets[p].Tick(dt, 0f, _sim.Position, false);
            if (_net.ResumeScheduled)
            {
                long left = (long)_net.ResumeTick - _net.Now;
                int secs = Mathf.Max(1, Mathf.CeilToInt(left / (float)_net.Ball.TickRate));
                if (left > 0 && _lanCountdown != 100 + secs)
                {
                    _lanCountdown = 100 + secs;
                    if (Countdown != null) Countdown(secs.ToString());
                    PlaySfx(Brooklyneir.Audio.Sfx.Countdown, 1f);
                }
            }
            LanDrawBall(dt);
        }

        /// <summary>Countdown to the start tick (both phones show the same numbers), then the first serve.</summary>
        void LanUpdateIntro()
        {
            if (_net.Started)
            {
                if (Countdown != null) Countdown("¡YA!");
                PlaySfx(Brooklyneir.Audio.Sfx.CountdownGo, 1f);
                _goTimer = 0.6f;
                _lanCountdown = -1;
                PrepareServe(_server);
                return;
            }
            if (!_net.Clock.IsSynced) return;
            long left = (long)_net.StartTick - _net.Now;
            int secs = Mathf.CeilToInt(left / (float)_net.Ball.TickRate);
            if (secs >= 1 && secs <= 3 && secs != _lanCountdown)
            {
                _lanCountdown = secs;
                if (Countdown != null) Countdown(secs.ToString());
                PlaySfx(Brooklyneir.Audio.Sfx.Countdown, 1f);
            }
        }

        /// <summary>
        /// Advances the ball one tick at a time up to the shared clock. Contacts go to the arbiter
        /// (host) or to the local referee (guest); only this phone's racket is tested for a hit,
        /// interpolated across the frame like in SimulateBall.
        /// </summary>
        void LanAdvanceBall()
        {
            BallSync b = _net.Ball;
            uint target = _net.Now;
            if (target <= b.Tick) return;
            if (target - b.Tick > (uint)b.MaxTicksPerAdvance) b.SetTick(target - (uint)b.MaxTicksPerAdvance);
            uint start = b.Tick;
            float n = target - start;
            while (b.Tick < target)
            {
                Vector3 before = b.Sim.Position;
                _stepFrom = (b.Tick - start) / n;
                _stepTo = (b.Tick + 1 - start) / n;
                _events.Clear();
                b.AdvanceTo(b.Tick + 1, _net.ScaleTowardP0, _net.ScaleTowardP1, _events);
                _lanTick = b.Tick;
                for (int e = 0; e < _events.Count; e++) LanHandleContact(_events[e]);
                if (!b.Sim.Active) continue;
                if (_ballInPlay && !Referee.RallyOver)
                {
                    if (CheckRacketHit(before, b.Sim.Position)) continue;
                    LanCheckDeadBall(b.Sim.Position);
                }
            }
        }

        void LanHandleContact(BallContactEvent e)
        {
            float speed = e.VelocityBefore.magnitude;
            switch (e.Type)
            {
                case BallContactType.Table:
                    PlaySfx(Brooklyneir.Audio.Sfx.Bounce, Mathf.Clamp01(0.4f + speed * 0.1f), Mathf.Lerp(0.9f, 1.15f, Mathf.Clamp01(speed / 8f)));
                    _vfx.Bounce(e.Point, Color.white);
                    _marker.Hide();
                    if (!_ballInPlay) return;
                    if (IsLanHost) _net.Arbiter.OnTableBounce(e.Side, _lanTick);
                    else Referee.OnTableBounce(e.Side);
                    break;
                case BallContactType.Net:
                case BallContactType.NetCord:
                    PlaySfx(Brooklyneir.Audio.Sfx.Net, Mathf.Clamp01(0.5f + speed * 0.08f));
                    if (!_ballInPlay || Referee.RallyOver) return;
                    if (IsLanHost) _net.Arbiter.OnNetContact(_lanTick);
                    else Referee.OnNetContact();
                    if (e.Type == BallContactType.NetCord) Say(-1, "¡RED... Y PASA!", new Color(1f, 0.9f, 0.5f));
                    break;
                case BallContactType.Floor:
                    if (speed > 1f) PlaySfx(Brooklyneir.Audio.Sfx.Bounce, 0.35f, 0.7f);
                    if (!_ballInPlay) return;
                    bool wide = Mathf.Abs(e.Point.x) > TableGeometry.HalfWidth + 0.02f;
                    if (IsLanHost) _net.Arbiter.OnBallDead(wide, _lanTick);
                    else Referee.OnBallDead(wide);
                    break;
            }
        }

        void LanCheckDeadBall(Vector3 bp)
        {
            bool overTable = TableGeometry.IsOverTable(bp.x, bp.z);
            MatchTuning m = _cfg.match;
            if ((bp.y < TableGeometry.TopY - 0.05f && !overTable) || Mathf.Abs(bp.x) > m.outOfBoundsX || Mathf.Abs(bp.z) > m.outOfBoundsZ)
            {
                bool wide = Mathf.Abs(bp.x) > TableGeometry.HalfWidth + 0.02f;
                if (IsLanHost) _net.Arbiter.OnBallDead(wide, _lanTick);
                else Referee.OnBallDead(wide);
            }
        }

        /// <summary>After a local serve or hit was launched with BallSync.LocalLaunch: tell the other phone.</summary>
        void LanSendLaunch(bool serve, ShotResult res)
        {
            BallLaunchMsg m = new BallLaunchMsg
            {
                Tick = _net.Ball.LastLaunchTick,
                Ball = _net.Ball.Capture(),
                Shot = serve ? ShotType.Serve : res.Type,
                Strength = serve ? 0.3f : 0.5f,
                ConsumedCurve = res.ConsumedCurve,
                ConsumedSuperSmash = res.ConsumedSuperSmash,
                ConsumedSpecial = res.ConsumedSpecial
            };
            if (serve) _net.LocalServe(m);
            else _net.LocalHitLaunch(m);
        }

        /// <summary>The other player left: whoever stays wins. The match ends with the current score.</summary>
        public void FinishLanByAbandonment()
        {
            if (!IsLan || Phase == MatchPhase.MatchOver || Phase == MatchPhase.Inactive) return;
            _matchWinner = _net.LocalPlayer;
            FinishMatch();
        }

        // =================================================================== INetMatchListener

        public void OnRemoteServe(BallLaunchMsg m)
        {
            int p = m.Player;
            _server = p;
            _hitPending = false;
            if (!IsLanHost) Referee.StartRally(p); // the host's arbiter already started it
            Phase = MatchPhase.Rally;
            _ballInPlay = true;
            RallyHits = 0;
            _ballView.ClearTrail();
            _ballView.SetSpin(m.Ball.Velocity, m.Ball.Spin);
            ShowLandingAndWarn(p);
            Rackets[p].PlaySwing(0.3f, ShotType.Serve);
            PlaySfx(Brooklyneir.Audio.Sfx.Serve, 0.9f);
            if (Message != null) Message(p, "", Color.white);
        }

        public void OnRemoteHitBegin(HitBeginMsg m, bool accepted)
        {
            if (accepted) _marker.Hide(); // the session holds the ball at the contact point
        }

        public void OnRemoteHitLaunch(BallLaunchMsg m)
        {
            int p = m.Player;
            // The host's arbiter took it at HitBegin. The guest's referee follows the host's hit even
            // if, a moment earlier, it had seen the rally end (the host's ball is the reference).
            if (!IsLanHost)
            {
                if (Referee.RallyOver) Referee.StartRally(p);
                else Referee.RegisterHit(p);
            }
            _ballInPlay = true;
            _ballView.SetSpin(m.Ball.Velocity, m.Ball.Spin);
            ShowLandingAndWarn(p);
            float shake = m.Shot == ShotType.SuperSmash || m.Shot == ShotType.Special ? 0.35f : (m.Shot == ShotType.Smash ? 0.2f : 0.05f);
            PresentHit(p, m.Ball.Position, m.Shot, m.ConsumedCurve, shake);
        }

        public void OnPowerRequest(PowerRequestMsg m) { }
        public void OnPowerState(PowerStateMsg m) { }
        public void OnShieldSave(ShieldSaveMsg m) { }
        public void OnOrbState(OrbStateMsg m) { }

        /// <summary>Guest: the host awarded a point. The same MatchScore rules give the same score here.</summary>
        public void OnPointResult(PointResultMsg m)
        {
            if (Phase == MatchPhase.MatchOver || Phase == MatchPhase.Inactive) return;
            if (m.TimeUp && !Score.TimeUp)
            {
                Score.SignalTimeUp();
                if (!_lanTimeUpShown) { _lanTimeUpShown = true; Say(-1, "¡TIEMPO!", new Color(1f, 0.85f, 0.3f)); }
            }
            Referee.Abort();
            EndRallyWith(m.Winner, m.Reason);
            MatchClock = m.Clock;
            if (Score.Points[0] != m.Points0 || Score.Points[1] != m.Points1 || Score.Sets[0] != m.Sets0 || Score.Sets[1] != m.Sets1 || Score.Server != m.NextServer)
                Debug.LogWarning("[TTLB] LAN: el marcador del invitado no coincide con el del anfitrión (" + Score.Points[0] + "-" + Score.Points[1] + " frente a " + m.Points0 + "-" + m.Points1 + ").");
        }

        public void OnHostPointConfirmed(int winner, PointReason reason)
        {
            if (Phase != MatchPhase.Rally) return;
            EndRally();
        }

        public void OnPaused(PauseMsg m)
        {
            Paused = true;
            _input.SetEnabled(false);
            if (Game.Audio != null) Game.Audio.SetDucked(true);
        }

        public void OnResumed(uint tick)
        {
            Paused = false;
            _input.SetEnabled(Human[0] || Human[1]);
            if (Game.Audio != null) Game.Audio.SetDucked(false);
            if (Countdown != null) Countdown("");
            _lanCountdown = -1;
            if (LanStateChanged != null) LanStateChanged(SessionState.Playing);
        }

        public void OnReconnected()
        {
            // Host: the rally in play does not count; both restart at a common tick.
            _net.HostVoidRally(Score.Server);
        }

        public void OnRallyVoided(int server, uint resumeTick)
        {
            _hitPending = false;
            if (Phase == MatchPhase.MatchOver) return;
            if (Phase != MatchPhase.Intro) PrepareServe(server);
            Say(-1, "CONEXIÓN RECUPERADA: SE REPITE EL PUNTO", new Color(1f, 0.9f, 0.5f));
        }

        public void OnStateChanged(SessionState state)
        {
            if (state == SessionState.WaitingReconnect) Say(-1, "CONEXIÓN PERDIDA... ESPERANDO AL OTRO MÓVIL", new Color(1f, 0.6f, 0.4f));
            if (state == SessionState.Paused) { Paused = true; _input.SetEnabled(false); }
            if (LanStateChanged != null) LanStateChanged(state);
        }
    }
}
