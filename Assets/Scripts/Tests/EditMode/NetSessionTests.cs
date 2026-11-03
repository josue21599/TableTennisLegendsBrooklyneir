using System.Collections.Generic;
using Brooklyneir.AI;
using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Net;
using Brooklyneir.Physics;
using Brooklyneir.PowerUps;
using Brooklyneir.Rackets;
using NUnit.Framework;
using UnityEngine;

namespace Brooklyneir.Tests
{
    /// <summary>
    /// One simulated phone for the two-phone tests: an AI moves the local racket, the real shot
    /// model computes its hits, and everything it knows about the other phone comes through
    /// NetMatchSession (the class the game uses) over a LoopbackLink with latency and loss. The
    /// host keeps the authoritative MatchScore and PowerUpSystem; the guest only mirrors them.
    /// </summary>
    sealed class SimPhone : INetMatchListener
    {
        enum Phase { ServeWait, Rally, PointOver }

        public readonly NetMatchSession S;
        public readonly bool IsHost;
        public readonly int Me;
        readonly BalanceConfig _cfg;
        readonly ShotModel _model;
        readonly AIBrain _brain;
        readonly RacketStatModel _stats;
        readonly BallSimulation _scratch;
        readonly List<BallContactEvent> _buf = new List<BallContactEvent>();
        readonly List<BallContactEvent> _events = new List<BallContactEvent>();
        public readonly PowerUpSystem Powers;
        public readonly MatchScore Score;              // host only
        public readonly RallyReferee LocalRef = new RallyReferee(); // guest only (to know when it may hit)
        readonly double _clockOffset;
        readonly double _frameDt;
        double _nextFrame;
        Vector3 _racket;
        Phase _phase = Phase.ServeWait;
        int _server;
        uint _serveAt;
        bool _requested;
        bool _lastSlow0, _lastSlow1;
        readonly uint _pauseTicks;
        public bool GivePowers;
        readonly PowerUpType[] _grant = { PowerUpType.Slow, PowerUpType.Shield, PowerUpType.Fire, PowerUpType.Curve, PowerUpType.SuperSmash, PowerUpType.Shield, PowerUpType.Slow, PowerUpType.Freeze };
        int _grantIndex;

        // What happened, for the assertions.
        public int Hits, Serves, ShieldSaves, SlowActivations, PointsSeen, PredictionDisagreements, RalliesVoided;
        public readonly List<SessionState> States = new List<SessionState>();
        public readonly List<PointResultMsg> Points = new List<PointResultMsg>();
        public readonly List<PowerStateMsg> PowerStates = new List<PowerStateMsg>();
        public uint PausedTick, ResumedTick, VoidResumeTick;
        public int VoidServer = -1;
        public NetBallState PausedBall, ResumedBall;
        public bool Paused, Resumed;

        public SimPhone(bool host, LoopbackLink link, BalanceConfig cfg, NetConfig net, int seed, double clockOffset, double fps, int firstServer, uint startTick)
        {
            IsHost = host;
            Me = host ? 0 : 1;
            _cfg = cfg;
            GameRandom rng = new GameRandom(seed);
            _model = new ShotModel(cfg, rng);
            _brain = new AIBrain(Me, cfg.ai.normal, cfg.shots, rng);
            _stats = new RacketStatModel(new RacketStats(5, 7, 5, 5, 6), cfg.racket);
            _scratch = new BallSimulation(cfg.physics);
            Powers = new PowerUpSystem(cfg.powerUps, rng);
            S = new NetMatchSession(host ? NetRole.Host : NetRole.Guest, net, cfg.physics, host ? link.A : link.B, firstServer);
            S.SetListener(this);
            S.StartTick = startTick;
            _serveAt = startTick;
            if (host) Score = new MatchScore(5, 1, true, firstServer);
            _server = firstServer;
            _clockOffset = clockOffset;
            _frameDt = 1.0 / fps;
            _racket = Home;
            _pauseTicks = (uint)(cfg.match.pointPauseSeconds * S.Ball.TickRate);
        }

        Vector3 Home { get { return new Vector3(0f, 0.95f, TableGeometry.ForwardSign(Me) * -_cfg.racket.defaultZ); } }
        public double LocalTime(double global) { return global + _clockOffset; }
        RallyReferee Rules { get { return IsHost ? S.Arbiter.Referee : LocalRef; } }

        public void Tick(double global)
        {
            if (global + 1e-9 < _nextFrame) return;
            float dt = (float)_frameDt;
            _nextFrame += _frameDt;
            double local = LocalTime(global);
            S.ScaleTowardP0 = Powers.BallTimeScale(0);
            S.ScaleTowardP1 = Powers.BallTimeScale(1);
            S.Update(local);
            if (S.State != SessionState.Playing || !S.Started) return;

            if (IsHost)
            {
                Powers.Update(dt);
                bool s0 = Powers.Players[0].SlowActive, s1 = Powers.Players[1].SlowActive;
                if (s0 != _lastSlow0 || s1 != _lastSlow1)
                {
                    if ((s0 && !_lastSlow0) || (s1 && !_lastSlow1)) SlowActivations++;
                    _lastSlow0 = s0; _lastSlow1 = s1;
                    S.ScaleTowardP0 = Powers.BallTimeScale(0);
                    S.ScaleTowardP1 = Powers.BallTimeScale(1);
                    S.HostSendPowers(Powers, true);
                }
            }

            Vector3 target = _brain.DesiredPosition(dt, _racket, Home);
            _racket = Vector3.MoveTowards(_racket, target, _brain.Tuning.maxSpeed * dt);
            S.LocalPaddle(_racket, target);

            if (_phase == Phase.ServeWait || _phase == Phase.PointOver)
            {
                if (_server == Me && S.Now >= _serveAt) DoServe();
            }

            AdvanceBall();
            UsePowers();
        }

        void AdvanceBall()
        {
            BallSync b = S.Ball;
            while (b.Tick < S.Now)
            {
                Vector3 before = b.Sim.Position;
                _events.Clear();
                b.AdvanceTo(b.Tick + 1, S.ScaleTowardP0, S.ScaleTowardP1, _events);
                if (!b.Sim.Active) continue;
                uint tick = b.Tick;
                bool relaunched = false;
                for (int i = 0; i < _events.Count && !relaunched; i++) relaunched = OnBallEvent(_events[i], tick);
                if (relaunched) continue;
                if (TryLocalHit(before, b.Sim.Position, tick)) continue;
                if (IsHost && CheckShieldWall(before, b.Sim.Position, tick)) continue;
                Vector3 bp = b.Sim.Position;
                bool overTable = TableGeometry.IsOverTable(bp.x, bp.z);
                if ((bp.y < TableGeometry.TopY - 0.05f && !overTable) || Mathf.Abs(bp.x) > 4f || Mathf.Abs(bp.z) > 5f)
                {
                    bool wide = Mathf.Abs(bp.x) > TableGeometry.HalfWidth;
                    if (IsHost) S.Arbiter.OnBallDead(wide, tick); else LocalRef.OnBallDead(wide);
                }
            }
        }

        bool OnBallEvent(BallContactEvent e, uint tick)
        {
            if (IsHost)
            {
                RallyReferee r = S.Arbiter.Referee;
                if (e.Type == BallContactType.Table && r.NextBounceLoses(e.Side) && Powers.Players[e.Side].ShieldActive && r.CanHit(e.Side) && !S.Arbiter.PointPending)
                {
                    ShieldSave(e.Side, e.Point, tick);
                    return true;
                }
                if (e.Type == BallContactType.Table) S.Arbiter.OnTableBounce(e.Side, tick);
                else if (e.Type == BallContactType.Net || e.Type == BallContactType.NetCord) S.Arbiter.OnNetContact(tick);
                else if (e.Type == BallContactType.Floor) S.Arbiter.OnBallDead(Mathf.Abs(e.Point.x) > TableGeometry.HalfWidth, tick);
            }
            else
            {
                if (e.Type == BallContactType.Table) LocalRef.OnTableBounce(e.Side);
                else if (e.Type == BallContactType.Net || e.Type == BallContactType.NetCord) LocalRef.OnNetContact();
                else if (e.Type == BallContactType.Floor) LocalRef.OnBallDead(Mathf.Abs(e.Point.x) > TableGeometry.HalfWidth);
            }
            return false;
        }

        /// <summary>Same as MatchController.CheckShieldWall: the shield behind the receiver catches a ball it missed.</summary>
        bool CheckShieldWall(Vector3 before, Vector3 after, uint tick)
        {
            RallyReferee r = S.Arbiter.Referee;
            int rec = r.Receiver;
            if (r.RallyOver || S.Arbiter.PointPending || !Powers.Players[rec].ShieldActive || !r.CanHit(rec)) return false;
            float z = TableGeometry.ForwardSign(rec) * -(TableGeometry.HalfLength + 0.95f);
            if ((before.z - z) * (after.z - z) > 0f || Mathf.Abs(after.z - before.z) < 1e-6f) return false;
            float f = (z - before.z) / (after.z - before.z);
            Vector3 c = Vector3.LerpUnclamped(before, after, f);
            if (Mathf.Abs(c.x) > 1.3f || c.y < TableGeometry.TopY - 0.15f) return false;
            ShieldSave(rec, c, tick);
            return true;
        }

        void ShieldSave(int p, Vector3 point, uint tick)
        {
            Vector3 from = new Vector3(Mathf.Clamp(point.x, -1.2f, 1.2f), Mathf.Max(point.y, TableGeometry.TopY + 0.18f), point.z);
            ShotContext ctx = Context(p, from, 0f);
            ctx.RacketVelocity = new Vector3(0f, 0f, TableGeometry.ForwardSign(p) * (_cfg.shots.defensiveSwipeThreshold - 0.3f));
            ctx.SpecialArmed = ctx.SuperSmashArmed = ctx.CurveArmed = ctx.FireActive = false;
            ShotResult res = _model.Compute(ctx);
            S.Ball.LocalLaunch(from, res.Velocity, res.SpinAccel, tick, S.ScaleTowardP0, S.ScaleTowardP1);
            S.Arbiter.ShieldHit(p);
            Powers.OnShot(p, false, false, false);
            Powers.ConsumeShield(p);
            S.HostShieldSave(p);
            S.HostSendPowers(Powers, false);
            ShieldSaves++;
            if (p == Me) _brain.ClearPlan(); else Plan();
        }

        ShotContext Context(int p, Vector3 contact, float offset)
        {
            PlayerPowerState s = Powers.Players[p];
            ShotContext c = new ShotContext();
            c.Hitter = p; c.ContactPoint = contact; c.ContactOffset = offset; c.Stats = _stats;
            c.SwingMultiplier = Powers.SwingMultiplier(p);
            c.NoiseMultiplier = _brain.Tuning.shotNoiseMultiplier;
            c.SpecialArmed = s.SpecialArmed; c.SuperSmashArmed = s.SuperSmashArmed; c.CurveArmed = s.CurveArmed; c.FireActive = s.FireActive && !s.FireBurning;
            return c;
        }

        bool TryLocalHit(Vector3 before, Vector3 after, uint tick)
        {
            if (!Rules.CanHit(Me) || (IsHost && S.Arbiter.PointPending)) return false;
            float pz = _racket.z;
            if ((before.z - pz) * (after.z - pz) > 0f || Mathf.Abs(after.z - before.z) < 1e-6f) return false;
            float f = (pz - before.z) / (after.z - before.z);
            Vector3 c = Vector3.LerpUnclamped(before, after, f);
            float dx = c.x - _racket.x, h = c.y - TableGeometry.TopY;
            if (Mathf.Abs(dx) > _stats.Reach || h < _cfg.racket.minHittableHeight || h > _cfg.racket.maxHittableHeight) return false;
            ShotContext ctx = Context(Me, c, dx / _stats.Reach);
            ctx.RacketVelocity = _brain.ChooseSwing(h);
            ShotResult res = _model.Compute(ctx);
            S.LocalHitBegin(c, tick);
            NetBallState st = S.Ball.LocalLaunch(c, res.Velocity, res.SpinAccel, tick, S.ScaleTowardP0, S.ScaleTowardP1);
            if (IsHost)
            {
                S.Arbiter.HostHit(Me);
                Powers.OnShot(Me, res.ConsumedCurve, res.ConsumedSuperSmash, res.ConsumedSpecial);
                if (res.ConsumedCurve || res.ConsumedSuperSmash || res.ConsumedSpecial) S.HostSendPowers(Powers, false);
            }
            else LocalRef.RegisterHit(Me);
            S.LocalHitLaunch(new BallLaunchMsg { Tick = tick, Ball = st, Shot = res.Type, Strength = 0.5f, ConsumedCurve = res.ConsumedCurve, ConsumedSuperSmash = res.ConsumedSuperSmash, ConsumedSpecial = res.ConsumedSpecial });
            _brain.ClearPlan();
            Hits++;
            return true;
        }

        void DoServe()
        {
            float fwd = TableGeometry.ForwardSign(Me);
            Vector3 pos = new Vector3(_racket.x, TableGeometry.TopY + 0.22f, _racket.z + fwd * 0.12f);
            ShotContext ctx = Context(Me, pos, 0f);
            ctx.IsServe = true;
            ctx.SpecialArmed = ctx.SuperSmashArmed = ctx.CurveArmed = ctx.FireActive = false;
            ShotResult res = _model.Compute(ctx);
            NetBallState st = S.Ball.LocalLaunch(pos, res.Velocity, res.SpinAccel, S.Now, S.ScaleTowardP0, S.ScaleTowardP1);
            if (!IsHost) LocalRef.StartRally(Me);
            S.LocalServe(new BallLaunchMsg { Tick = S.Now, Ball = st, Shot = ShotType.Serve, Strength = 0.3f });
            _phase = Phase.Rally;
            _brain.ClearPlan();
            Serves++;
            if (IsHost) Grant();
        }

        /// <summary>Host only: hands out powers in turn, so slow-motion and the shield are used during the match.</summary>
        void Grant()
        {
            if (!GivePowers) return;
            int p = _grantIndex % 2;
            if (Powers.CanCollect(p)) { Powers.Collect(p, _grant[(_grantIndex / 2) % _grant.Length]); S.HostSendPowers(Powers, false); }
            _grantIndex++;
        }

        void UsePowers()
        {
            if (!GivePowers || _phase != Phase.Rally || Rules.RallyOver) return;
            PlayerPowerState mine = Powers.Players[Me];
            if (mine.Held == PowerUpType.None || mine.Cooldown > 0f) { if (mine.Held == PowerUpType.None) _requested = false; return; }
            // The shield protects my side: raise it when the ball comes to me. The rest: when it goes away.
            bool comingToMe = Rules.Receiver == Me;
            if ((mine.Held == PowerUpType.Shield) != comingToMe) return;
            if (IsHost)
            {
                if (Powers.TryActivate(Me) == PowerActivationResult.Activated) S.HostSendPowers(Powers, false);
            }
            else if (!_requested)
            {
                S.GuestRequestPower(PowerAction.ActivateHeld);
                _requested = true;
            }
        }

        void Plan()
        {
            float plane = TableGeometry.ForwardSign(Me) * -_cfg.racket.defaultZ;
            Vector3 pt; float tt;
            if (ShotSolver.PredictPlaneCrossing(S.Ball.Sim, _scratch, _buf, plane, Me, 3f, out pt, out tt))
                _brain.PlanReturn(pt, _stats.Reach, _cfg.racket.zoneHalfWidth);
        }

        // ------------------------------------------------------------ listener

        public void OnRemoteServe(BallLaunchMsg m)
        {
            if (!IsHost) LocalRef.StartRally(m.Player);
            _phase = Phase.Rally;
            Plan();
            if (IsHost) Grant();
        }

        public void OnRemoteHitBegin(HitBeginMsg m, bool accepted) { }

        public void OnRemoteHitLaunch(BallLaunchMsg m)
        {
            if (IsHost)
            {
                Powers.OnShot(m.Player, m.ConsumedCurve, m.ConsumedSuperSmash, m.ConsumedSpecial);
                if (m.ConsumedCurve || m.ConsumedSuperSmash || m.ConsumedSpecial) S.HostSendPowers(Powers, false);
            }
            else LocalRef.RegisterHit(m.Player);
            Plan();
        }

        public void OnPowerRequest(PowerRequestMsg m)
        {
            PowerActivationResult r = m.Action == PowerAction.ActivateHeld ? Powers.TryActivate(m.Player) : PowerActivationResult.Blocked;
            if (r == PowerActivationResult.Activated) S.HostSendPowers(Powers, false);
            else S.HostSendPowers(Powers, false); // the guest learns it was not granted
        }

        public void OnPowerState(PowerStateMsg m)
        {
            m.P0.ApplyTo(Powers.Players[0]);
            m.P1.ApplyTo(Powers.Players[1]);
            PowerStates.Add(m);
            if (Powers.Players[Me].Held == PowerUpType.None) _requested = false;
        }

        public void OnShieldSave(ShieldSaveMsg m)
        {
            LocalRef.RegisterHit(m.Player);
            ShieldSaves++;
            Plan();
        }

        public void OnOrbState(OrbStateMsg m) { }

        public void OnPointResult(PointResultMsg m)
        {
            int predicted = LocalRef.RallyOver ? LocalRef.Winner : -1;
            if (predicted >= 0 && predicted != m.Winner) PredictionDisagreements++;
            LocalRef.Abort();
            Points.Add(m);
            PointsSeen++;
            _phase = Phase.PointOver;
            _server = m.NextServer;
            _serveAt = S.Now + _pauseTicks;
            _brain.ClearPlan();
        }

        public void OnHostPointConfirmed(int winner, PointReason reason)
        {
            ScoreEvent ev = Score.AwardPoint(winner);
            Powers.OnPoint(winner);
            Powers.OnRallyEnded();
            PointResultMsg m = ScoreMirror.Build(Score, ev, reason, 0f);
            Points.Add(m);
            PointsSeen++;
            S.HostSendPowers(Powers, false); // first the powers: the last point closes the session
            S.HostSendPoint(m);
            _phase = Phase.PointOver;
            _server = Score.Server;
            _serveAt = S.Now + _pauseTicks;
            _brain.ClearPlan();
        }

        public void OnPaused(PauseMsg m) { Paused = true; PausedTick = m.Tick; PausedBall = S.Ball.Capture(); }
        public void OnResumed(uint tick) { Resumed = true; ResumedTick = tick; ResumedBall = S.Ball.Capture(); }
        public void OnReconnected() { S.HostVoidRally(Score.Server); }

        public void OnRallyVoided(int server, uint resumeTick)
        {
            RalliesVoided++;
            VoidServer = server;
            VoidResumeTick = resumeTick;
            _server = server;
            _phase = Phase.ServeWait;
            _serveAt = resumeTick + 30;
            LocalRef.Abort();
            S.Ball.Sim.Hold(new Vector3(0f, -10f, 0f));
            _brain.ClearPlan();
        }

        public void OnStateChanged(SessionState state) { States.Add(state); }
    }

    [TestFixture]
    public class NetSessionTests
    {
        BalanceConfig _cfg;
        NetConfig _net;

        [SetUp]
        public void Setup()
        {
            _cfg = new BalanceConfig();
            _cfg.Validate();
            _net = new NetConfig();
            _net.Validate();
        }

        /// <summary>Runs both phones on a common timeline (1/480 s), delivering packets as they arrive.</summary>
        static void Run(LoopbackLink link, SimPhone host, SimPhone guest, double from, double until, System.Func<double, bool> stop = null, System.Action<double> each = null)
        {
            for (double t = from; t < until; t += 1.0 / 480.0)
            {
                link.Now = t;
                if (each != null) each(t);
                link.Deliver(0, host.S, host.LocalTime(t));
                link.Deliver(1, guest.S, guest.LocalTime(t));
                host.Tick(t);
                guest.Tick(t);
                if (stop != null && stop(t)) return;
            }
        }

        void Phones(int seed, bool powers, out LoopbackLink link, out SimPhone host, out SimPhone guest)
        {
            link = new LoopbackLink(seed) { LatencySeconds = 0.030, JitterSeconds = 0.015, UnreliableLoss = 0.05 };
            // The host's clock reads 1000 s at the start, the guest's −37,25 s; the match starts 2 s
            // later on the host's clock (in the game this tick travels in MatchSetup).
            uint start = (uint)((1000.0 + 2.0) * 240.0);
            host = new SimPhone(true, link, _cfg, _net, seed * 7 + 1, 1000.0, 60.0, seed % 2, start) { GivePowers = powers };
            guest = new SimPhone(false, link, _cfg, _net, seed * 7 + 2, -37.25, 90.0, seed % 2, start) { GivePowers = powers };
        }

        static bool BothFinished(SimPhone h, SimPhone g) { return h.S.State == SessionState.Finished && g.S.State == SessionState.Finished; }

        static void SameScore(SimPhone host, SimPhone guest)
        {
            Assert.AreEqual(host.Points.Count, guest.Points.Count, "same number of points");
            for (int i = 0; i < host.Points.Count; i++)
            {
                PointResultMsg a = host.Points[i], b = guest.Points[i];
                Assert.IsTrue(a.Winner == b.Winner && a.Points0 == b.Points0 && a.Points1 == b.Points1 && a.Sets0 == b.Sets0 && a.Sets1 == b.Sets1 && a.NextServer == b.NextServer,
                    "point " + (i + 1) + " is the same on both phones");
            }
            Assert.IsTrue(host.Score.Points[0] == guest.S.Mirror.Points[0] && host.Score.Points[1] == guest.S.Mirror.Points[1]);
            Assert.IsTrue(host.Score.Sets[0] == guest.S.Mirror.Sets[0] && host.Score.Sets[1] == guest.S.Mirror.Sets[1]);
            Assert.AreEqual(host.Score.MatchWinner, guest.S.Mirror.MatchWinner, "same winner");
        }

        [Test]
        public void AFullMatchWithLatencyJitterAndLossEndsWithTheSameScoreOnBothPhones()
        {
            LoopbackLink link; SimPhone host, guest;
            Phones(21, false, out link, out host, out guest);
            Run(link, host, guest, 0, 900, t => BothFinished(host, guest));
            Assert.IsTrue(BothFinished(host, guest), "the match ends on both phones");
            Assert.IsTrue(guest.S.Clock.IsSynced && guest.S.Clock.RoundTrip < 0.12, "the guest's clock is synced");
            SameScore(host, guest);
            Assert.GreaterOrEqual(host.Points.Count, 5);
            Assert.Greater(host.Hits + guest.Hits, 2 * host.Points.Count, "real rallies were played");
            Assert.Greater(guest.Hits, 0); Assert.Greater(host.Hits, 0);
            Assert.Greater(link.Dropped, 0, "packets were really lost on the way");
            Assert.AreEqual(0, guest.PredictionDisagreements, "the guest's own reading of every point agreed with the host's (it only displays the host's)");
        }

        [Test]
        public void WithPowersBothPhonesEndWithTheSameScoreAndTheSamePowers()
        {
            int slows = 0, shields = 0;
            for (int seed = 1; seed <= 4; seed++)
            {
                LoopbackLink link; SimPhone host, guest;
                Phones(seed, true, out link, out host, out guest);
                Run(link, host, guest, 0, 1500, t => BothFinished(host, guest));
                Assert.IsTrue(BothFinished(host, guest), "the match ends on both phones (seed " + seed + ")");
                SameScore(host, guest);
                Assert.AreEqual(host.ShieldSaves, guest.ShieldSaves, "every shield save reached the guest");
                Assert.Greater(guest.PowerStates.Count, 10);
                for (int p = 0; p < 2; p++)
                {
                    NetPowerPlayer a = NetPowerPlayer.From(host.Powers.Players[p]), b = NetPowerPlayer.From(guest.Powers.Players[p]);
                    Assert.IsTrue(a.Held == b.Held && a.Cooldown == b.Cooldown && a.SlowTimer == b.SlowTimer && a.ShieldTimer == b.ShieldTimer && a.FrozenTimer == b.FrozenTimer &&
                                  a.FireActive == b.FireActive && a.FireBurning == b.FireBurning && a.CurveArmedTimer == b.CurveArmedTimer && a.SuperSmashArmedTimer == b.SuperSmashArmedTimer &&
                                  a.SpecialEnergy == b.SpecialEnergy && a.SpecialArmed == b.SpecialArmed, "player " + (p + 1) + " has exactly the same powers on both phones (seed " + seed + ")");
                }
                Assert.AreEqual(0, guest.PredictionDisagreements, "no point read differently (seed " + seed + ")");
                slows += host.SlowActivations;
                shields += host.ShieldSaves;
            }
            Assert.Greater(slows, 0, "slow-motion was used (it changes the ball's speed on both phones)");
            Assert.Greater(shields, 0, "the shield saved balls (it changes the trajectory)");
        }

        [Test]
        public void AShortCutPausesBothVoidsThePointAndTheMatchGoesOn()
        {
            LoopbackLink link; SimPhone host, guest;
            Phones(9, false, out link, out host, out guest);
            double cutAt = -1;
            Run(link, host, guest, 0, 900, t => host.Points.Count >= 2 && host.Hits + guest.Hits > 6, null);
            double t0 = link.Now;
            cutAt = t0 + 0.4;
            Run(link, host, guest, t0, t0 + 120, t => BothFinished(host, guest), t => link.Blackout = t >= cutAt && t < cutAt + 2.0);
            Assert.Contains(SessionState.WaitingReconnect, host.States, "the host noticed the cut");
            Assert.Contains(SessionState.WaitingReconnect, guest.States, "the guest noticed the cut");
            Assert.AreEqual(1, host.RalliesVoided); Assert.AreEqual(1, guest.RalliesVoided);
            Assert.AreEqual(host.VoidServer, guest.VoidServer, "same server after the cut");
            Assert.AreEqual(host.VoidResumeTick, guest.VoidResumeTick, "both restart at the same tick");
            Assert.IsTrue(BothFinished(host, guest), "and the match ends normally");
            SameScore(host, guest);
        }

        [Test]
        public void ACutLongerThanTheReconnectWindowVoidsTheMatchOnBothPhones()
        {
            LoopbackLink link; SimPhone host, guest;
            Phones(13, false, out link, out host, out guest);
            Run(link, host, guest, 0, 900, t => host.Points.Count >= 1);
            double t0 = link.Now;
            double window = _net.reconnectWindowSeconds;
            Run(link, host, guest, t0, t0 + window + 10, null, t => link.Blackout = t < t0 + window + 3);
            Assert.AreEqual(SessionState.Void, host.S.State, "host: match void");
            Assert.AreEqual(SessionState.Void, guest.S.State, "guest: match void");
            int pointsBefore = host.Points.Count;
            Run(link, host, guest, link.Now, link.Now + 20);
            Assert.AreEqual(SessionState.Void, host.S.State, "messages after the void are ignored");
            Assert.AreEqual(pointsBefore, host.Points.Count, "nothing else is played");
        }

        [Test]
        public void AfterAPauseResumeAtRestartsBothPhonesAtTheSameTickWithTheSameBall()
        {
            LoopbackLink link; SimPhone host, guest;
            Phones(17, false, out link, out host, out guest);
            // Pause in the middle of a rally, asked by the guest.
            Run(link, host, guest, 0, 900, t => host.Points.Count >= 1 && guest.S.Ball.Sim.Active && guest.Hits + host.Hits > 3);
            double t0 = link.Now;
            guest.S.RequestPause(PauseReason.Button);
            Run(link, host, guest, t0, t0 + 2, t => host.Paused && guest.Paused);
            Assert.IsTrue(host.Paused && guest.Paused, "both phones paused");
            Assert.AreEqual(host.PausedTick, guest.PausedTick, "at the same tick");
            Run(link, host, guest, link.Now, link.Now + 5); // stays paused, nothing moves
            Assert.AreEqual(SessionState.Paused, host.S.State); Assert.AreEqual(SessionState.Paused, guest.S.State);
            guest.S.RequestResume();
            Run(link, host, guest, link.Now, link.Now + 10, t => host.Resumed && guest.Resumed);
            Assert.IsTrue(host.Resumed && guest.Resumed, "both resumed");
            Assert.AreEqual(host.ResumedTick, guest.ResumedTick, "at the same tick");
            NetBallState a = host.ResumedBall, b = guest.ResumedBall;
            Assert.IsTrue(a.Position.x == b.Position.x && a.Position.y == b.Position.y && a.Position.z == b.Position.z &&
                          a.Velocity.x == b.Velocity.x && a.Velocity.y == b.Velocity.y && a.Velocity.z == b.Velocity.z &&
                          a.Spin.x == b.Spin.x && a.Spin.y == b.Spin.y && a.Active == b.Active, "with the same ball, bit for bit");
            Assert.Greater(host.ResumedTick, host.PausedTick + 240, "after the countdown");
            Run(link, host, guest, link.Now, link.Now + 900, t => BothFinished(host, guest));
            Assert.IsTrue(BothFinished(host, guest));
            SameScore(host, guest);
        }
    }
}
