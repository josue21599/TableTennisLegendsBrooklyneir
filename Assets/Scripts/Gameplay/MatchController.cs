using System;
using System.Collections.Generic;
using Brooklyneir.AI;
using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.CameraSystem;
using Brooklyneir.Config;
using Brooklyneir.Controls;
using Brooklyneir.Core;
using Brooklyneir.Physics;
using Brooklyneir.PowerUps;
using Brooklyneir.Rackets;
using UnityEngine;

namespace Brooklyneir.Gameplay
{
    public enum MatchPhase { Inactive, Intro, ServeWait, Rally, PointOver, MatchOver, TrainingIntro, TrainingFeed }

    /// <summary>
    /// Runs a match (or a training drill, or the menu demo). It owns the ball simulation, the
    /// referee, the score, the power-ups and both rackets, advances them in a fixed order every
    /// frame and tells the audio, effects, camera and HUD what happened. Rules live in the pure
    /// classes (RallyReferee, MatchScore, PowerUpSystem, ShotModel); this class only orchestrates.
    /// </summary>
    public class MatchController : MonoBehaviour
    {
        public static readonly Color[] PlayerColors = { new Color(1f, 0.48f, 0.1f), new Color(0.13f, 0.76f, 1f) };
        static readonly Color GloveColor = new Color(0.93f, 0.92f, 0.9f);

        // ---- events for UI / flow
        public event Action<int, string, Color> Message;      // player (-1 = both), text, colour
        public event Action<string> Countdown;               // "3", "2", "1", "¡YA!", "" = hide
        public event Action<MatchResult> MatchFinished;
        public event Action<TrainingResult> TrainingFinished;
        public event Action<int, PointReason> PointScored;

        public MatchPhase Phase { get; private set; }
        public MatchConfig Config { get; private set; }
        public MatchScore Score { get; private set; }
        public RallyReferee Referee { get; private set; }
        public PowerUpSystem Powers { get; private set; }
        public RacketController[] Rackets { get; private set; }
        public bool Paused { get; private set; }
        public bool[] Human { get; private set; }
        public int RallyHits { get; private set; }
        public float MatchClock { get; private set; }
        public bool IsDemo { get { return Config != null && Config.mode == GameMode.Demo; } }
        public bool IsTraining { get { return Config != null && Config.mode == GameMode.Training; } }
        public DrillDefinition Drill { get { return _drill; } }
        public int TrainingFed { get { return _fed; } }
        public int TrainingSuccesses { get { return _successes; } }
        public string TrainingHint { get; private set; }
        public int ServeOwner { get { return _server; } }

        BalanceConfig _cfg;
        BallSimulation _sim;
        BallSimulation _scratch;
        ShotModel _shots;
        GameRandom _rng;
        readonly List<BallContactEvent> _events = new List<BallContactEvent>(8);
        readonly List<BallContactEvent> _predictBuffer = new List<BallContactEvent>(16);
        BallView _ballView;
        LandingMarker _marker;
        LandingMarker _targetRing;
        ShieldView[] _shields;
        PowerOrbView _orb;
        AIPlayer[] _ai = new AIPlayer[2];
        TouchInputController _input;
        CameraDirector _camera;
        VfxSystem _vfx;
        float _phaseTimer;
        float _orbTimer;
        int _server;
        int _countdownStep;
        bool _ballInPlay;
        bool _timeUpAnnounced;
        float _sfxScale = 1f;
        MatchResult _result;
        int[] _pointsWon = new int[2];
        int _matchWinner = -1;

        // training
        DrillDefinition _drill;
        int _fed, _successes;
        bool _awaitingLanding;
        ShotResult _lastTrainingShot;
        int _controlSide = 1;
        Vector3 _precisionTarget;

        public void Init(BalanceConfig cfg, TouchInputController input, CameraDirector cam, VfxSystem vfx)
        {
            _cfg = cfg;
            _input = input;
            _camera = cam;
            _vfx = vfx;
            _rng = new GameRandom(Environment.TickCount);
            _sim = new BallSimulation(cfg.physics);
            _scratch = new BallSimulation(cfg.physics);
            _shots = new ShotModel(cfg, _rng);
            Referee = new RallyReferee();
            Powers = new PowerUpSystem(cfg.powerUps, _rng);
            Powers.Activated += OnPowerActivated;
            Powers.Ended += OnPowerEnded;
            Powers.Collected += OnPowerCollected;
            Powers.FreezeBlocked += OnFreezeBlocked;
            Powers.SpecialReady += p => Say(p, "¡ESPECIAL LISTO!", new Color(1f, 0.85f, 0.3f));
            _input.ServeRequested += OnServeRequested;

            Rackets = new RacketController[2];
            for (int p = 0; p < 2; p++)
            {
                GameObject g = new GameObject("RacketP" + (p + 1));
                g.transform.SetParent(transform, false);
                Rackets[p] = g.AddComponent<RacketController>();
                g.SetActive(false);
            }
            GameObject ball = new GameObject("BallView");
            ball.transform.SetParent(transform, false);
            _ballView = ball.AddComponent<BallView>();
            _ballView.Init(cfg.physics.ballRadius, cam.Cam);
            _ballView.SetVisible(false);

            GameObject marker = new GameObject("LandingMarker");
            marker.transform.SetParent(transform, false);
            _marker = marker.AddComponent<LandingMarker>();
            _marker.Init();
            GameObject target = new GameObject("TrainingTarget");
            target.transform.SetParent(transform, false);
            _targetRing = target.AddComponent<LandingMarker>();
            _targetRing.Init();

            _shields = new ShieldView[2];
            for (int p = 0; p < 2; p++)
            {
                GameObject s = new GameObject("ShieldP" + (p + 1));
                s.transform.SetParent(transform, false);
                _shields[p] = s.AddComponent<ShieldView>();
                _shields[p].Init(p, PlayerColors[p]);
            }
            GameObject orb = new GameObject("PowerOrb");
            orb.transform.SetParent(transform, false);
            _orb = orb.AddComponent<PowerOrbView>();
            _orb.Init(cam.Cam);
            Human = new bool[2];
            Phase = MatchPhase.Inactive;
        }

        // =================================================================== lifecycle

        public void StartMatch(MatchConfig config)
        {
            Abort();
            config.Validate();
            Config = config;
            _sfxScale = IsDemo ? 0.25f : 1f;
            Human[0] = !config.player1IsAI && config.mode != GameMode.Demo;
            Human[1] = !config.player2IsAI && config.mode != GameMode.Demo && config.mode != GameMode.Training;

            SetupRacket(0, config.racketP1);
            SetupRacket(1, config.racketP2);
            Rackets[1].SetVisible(!IsTraining);

            _ai[0] = Human[0] ? null : new AIPlayer(0, DifficultyTuning(IsDemo ? AIDifficulty.Hard : config.aiDifficulty), _cfg, Rackets[0], _rng);
            _ai[1] = (Human[1] || IsTraining) ? null : new AIPlayer(1, DifficultyTuning(IsDemo ? AIDifficulty.Hard : config.aiDifficulty), _cfg, Rackets[1], _rng);

            var settings = Game.Save != null ? Game.Save.Data.settings : null;
            Rackets[0].Assist = Human[0] && settings != null && settings.assistP1;
            Rackets[1].Assist = Human[1] && settings != null && settings.assistP2;

            _input.Bind(Rackets[0], Rackets[1], Human[0], Human[1]);
            _input.SetEnabled(Human[0] || Human[1]);

            int firstServer = _rng.Value() < 0.5f ? 0 : 1;
            if (IsTraining) firstServer = 1;
            int points = IsDemo ? 999 : config.pointsToWin;
            Score = new MatchScore(points, config.SetsToWin, config.winByTwo, firstServer);
            Powers.ResetAll();
            _result = new MatchResult { Config = config };
            _pointsWon = new int[2];
            _matchWinner = -1;
            MatchClock = 0f;
            _timeUpAnnounced = false;
            _orbTimer = OrbInterval() * 0.6f;
            _ballView.SetVisible(true);
            _ballView.SetLook(BallLook.Normal);
            Referee.Abort();

            if (IsTraining)
            {
                Phase = MatchPhase.TrainingIntro;
                return;
            }
            if (IsDemo)
            {
                PrepareServe(firstServer);
                return;
            }
            Phase = MatchPhase.Intro;
            _phaseTimer = 0f;
            _countdownStep = 0;
            _camera.SetMode(CameraMode.Match);
            PlaceHeldBall(firstServer);
            _server = firstServer;
        }

        GameObject _machine;

        void EnsureMachine()
        {
            if (_machine != null) { _machine.SetActive(true); return; }
            _machine = new GameObject("BallMachine");
            _machine.transform.SetParent(transform, false);
            _machine.transform.position = new Vector3(0f, 0f, TableGeometry.HalfLength + 0.55f);
            Material body = MaterialLibrary.Surface(SurfaceKind.Plastic, new Color(0.95f, 0.55f, 0.15f));
            Material metal = MaterialLibrary.Surface(SurfaceKind.Metal, new Color(0.3f, 0.32f, 0.36f));
            Material hopper = MaterialLibrary.LitTransparent(new Color(0.8f, 0.9f, 1f, 0.45f));
            AddMachinePart("Stand", MeshFactory.Cylinder(0.04f, 0.8f, 12), metal, new Vector3(0f, 0.4f, 0f), Vector3.zero);
            AddMachinePart("Base", MeshFactory.Cylinder(0.25f, 0.04f, 20), metal, new Vector3(0f, 0.02f, 0f), Vector3.zero);
            AddMachinePart("Body", MeshFactory.RoundedBox(new Vector3(0.36f, 0.22f, 0.3f), 0.05f, 3), body, new Vector3(0f, 0.9f, 0f), Vector3.zero);
            AddMachinePart("Barrel", MeshFactory.Cylinder(0.045f, 0.3f, 16), metal, new Vector3(0f, 0.98f, -0.22f), new Vector3(70f, 0f, 0f));
            AddMachinePart("Hopper", MeshFactory.Frustum(0.12f, 0.2f, 0.22f, 18), hopper, new Vector3(0f, 1.12f, 0.03f), Vector3.zero);
        }

        void AddMachinePart(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(_machine.transform, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public void StartTraining(DrillDefinition drill, string racketId)
        {
            MatchConfig c = new MatchConfig();
            c.mode = GameMode.Training;
            c.player2IsAI = true;
            c.powerUpsEnabled = false;
            c.racketP1 = racketId;
            c.racketP2 = racketId;
            c.tableId = Game.Arena != null && Game.Arena.Current != null ? Game.Arena.Current.Id : "classic";
            _drill = drill;
            _fed = 0;
            _successes = 0;
            _awaitingLanding = false;
            StartMatch(c);
            EnsureMachine();
            _sim.Hold(new Vector3(0f, TableGeometry.TopY + 0.32f, TableGeometry.HalfLength + 0.15f));
            _camera.SetMode(CameraMode.Match);
            TrainingHint = drill.Instructions;
        }

        /// <summary>Called by the training intro overlay when the player taps "Empezar".</summary>
        public void BeginTrainingDrill()
        {
            if (Phase != MatchPhase.TrainingIntro) return;
            Phase = MatchPhase.TrainingFeed;
            _phaseTimer = 0.8f;
            UpdateTrainingTarget();
        }

        void SetupRacket(int p, string racketId)
        {
            RacketDefinition def = RacketCatalog.Get(racketId);
            Rackets[p].gameObject.SetActive(true);
            Rackets[p].Setup(p, def, _cfg.racket, PlayerColors[p], GloveColor);
        }

        AIDifficultyTuning DifficultyTuning(AIDifficulty d)
        {
            switch (d)
            {
                case AIDifficulty.Easy: return _cfg.ai.easy;
                case AIDifficulty.Hard: return _cfg.ai.hard;
                default: return _cfg.ai.normal;
            }
        }

        /// <summary>Stops everything and hides the match objects. Safe to call at any time.</summary>
        public void Abort()
        {
            Phase = MatchPhase.Inactive;
            Paused = false;
            Time.timeScale = 1f;
            Referee.Abort();
            Powers.ResetAll();
            _sim.Hold(new Vector3(0f, -10f, 0f));
            _ballInPlay = false;
            if (_ballView != null) { _ballView.SetVisible(false); _ballView.ClearTrail(); }
            if (_marker != null) _marker.Hide();
            if (_targetRing != null) _targetRing.Hide();
            if (_orb != null) _orb.Despawn();
            if (_shields != null) for (int i = 0; i < 2; i++) _shields[i].SetActive(false);
            if (_vfx != null) { _vfx.SetFireTrail(null, false); _vfx.ClearAll(); }
            if (_input != null) _input.SetEnabled(false);
            if (Rackets != null) for (int i = 0; i < 2; i++) { Rackets[i].SetFrozen(false); Rackets[i].gameObject.SetActive(false); }
            if (Countdown != null) Countdown("");
            if (_machine != null) _machine.SetActive(false);
            _ai[0] = _ai[1] = null;
        }

        public void SetPaused(bool paused)
        {
            if (Phase == MatchPhase.Inactive || IsDemo) return;
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            _input.SetEnabled(!paused && (Human[0] || Human[1]));
            if (Game.Audio != null) Game.Audio.SetDucked(paused);
        }

        /// <summary>Restarts the same configuration (rematch / restart from pause).</summary>
        public void Restart()
        {
            if (Config == null) return;
            if (IsTraining && _drill != null) StartTraining(_drill, Config.racketP1);
            else StartMatch(Config.Clone());
        }

        // =================================================================== frame

        void Update()
        {
            if (Phase == MatchPhase.Inactive || Paused) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            switch (Phase)
            {
                case MatchPhase.Intro: UpdateIntro(dt); break;
                case MatchPhase.PointOver:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                    {
                        if (Score.MatchOver) FinishMatch();
                        else PrepareServe(Score.Server);
                    }
                    break;
                case MatchPhase.TrainingFeed:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) FeedTrainingBall();
                    break;
            }

            bool live = Phase == MatchPhase.Rally || Phase == MatchPhase.ServeWait;
            if (live && !IsTraining && !IsDemo) UpdateClock(dt);
            if (live) Powers.Update(dt);

            UpdateAgents(dt);
            for (int p = 0; p < 2; p++)
            {
                if (!Rackets[p].gameObject.activeSelf) continue;
                bool incoming = _ballInPlay && !Referee.RallyOver && Referee.Receiver == p;
                Rackets[p].Tick(dt, Powers.FollowSpeedMultiplier(p), _sim.Position, incoming);
                Rackets[p].SetFrozen(Powers.Players[p].Frozen);
                _shields[p].SetActive(Powers.Players[p].ShieldActive);
            }

            if (Phase == MatchPhase.ServeWait) PlaceHeldBall(_server);
            else if (_sim.Active) SimulateBall(dt);

            UpdateOrbs(dt);
            _ballView.SetLook(CurrentLook());
            _ballView.Sync(_sim.Position, dt);
            _camera.TrackBall(_sim.Position, _ballInPlay);
        }

        void UpdateIntro(float dt)
        {
            _phaseTimer += dt;
            int step = Mathf.FloorToInt(_phaseTimer / 0.6f);
            if (step != _countdownStep || _phaseTimer < dt * 1.5f)
            {
                _countdownStep = step;
                string text = step == 0 ? "3" : step == 1 ? "2" : step == 2 ? "1" : step == 3 ? "¡YA!" : "";
                if (Countdown != null) Countdown(text);
                if (step <= 2) PlaySfx(Brooklyneir.Audio.Sfx.Countdown, 1f);
                else if (step == 3) PlaySfx(Brooklyneir.Audio.Sfx.CountdownGo, 1f);
            }
            if (_phaseTimer >= 2.6f)
            {
                if (Countdown != null) Countdown("");
                PrepareServe(_server);
            }
        }

        void UpdateClock(float dt)
        {
            MatchClock += dt;
            if (Config.timeLimitMinutes > 0f && !_timeUpAnnounced && MatchClock >= Config.timeLimitMinutes * 60f)
            {
                _timeUpAnnounced = true;
                Score.SignalTimeUp();
                bool tied = Score.Points[0] == Score.Points[1];
                Say(-1, tied ? "¡TIEMPO! PUNTO DE ORO" : "¡TIEMPO! ÚLTIMO PUNTO", new Color(1f, 0.85f, 0.3f));
            }
        }

        void UpdateAgents(float dt)
        {
            for (int p = 0; p < 2; p++)
            {
                AIPlayer ai = _ai[p];
                if (ai == null) continue;
                bool coming = _ballInPlay && !Referee.RallyOver && Referee.Receiver == p;
                ai.Tick(dt, coming);
                if (Config.powerUpsEnabled && Phase == MatchPhase.Rally) ai.ConsiderPowers(Powers, dt, coming);
                if (Phase == MatchPhase.ServeWait && _server == p && ai.TickServe(dt)) Serve(p);
            }
        }

        // =================================================================== serve

        void PrepareServe(int server)
        {
            _server = server;
            Phase = MatchPhase.ServeWait;
            _ballInPlay = false;
            Referee.Abort();
            _sim.Hold(HeldBallPosition(server));
            _ballView.ClearTrail();
            _marker.Hide();
            for (int p = 0; p < 2; p++) if (_ai[p] != null) _ai[p].ResetForPoint();
            if (_ai[server] != null) _ai[server].BeginServeWait();
            else if (!IsDemo) Say(server, "TOCA PARA SACAR", Color.white);
            RallyHits = 0;
        }

        Vector3 HeldBallPosition(int server)
        {
            RacketController r = Rackets[server];
            float fwd = TableGeometry.ForwardSign(server);
            float bob = Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 0.12f;
            return new Vector3(r.Position.x, TableGeometry.TopY + 0.2f + bob, r.Position.z + fwd * 0.12f);
        }

        void PlaceHeldBall(int server)
        {
            _sim.Hold(HeldBallPosition(server));
        }

        void OnServeRequested(int player)
        {
            if (Paused) return;
            if (Phase == MatchPhase.ServeWait && player == _server && Human[player]) Serve(player);
        }

        void Serve(int server)
        {
            if (Phase != MatchPhase.ServeWait) return;
            ShotContext ctx = BaseContext(server, HeldBallPosition(server), 0f);
            ctx.IsServe = true;
            ShotResult res = _shots.Compute(ctx);
            Referee.StartRally(server);
            Launch(server, ctx.ContactPoint, res);
            Phase = MatchPhase.Rally;
            Rackets[server].PlaySwing(0.3f);
            PlaySfx(Brooklyneir.Audio.Sfx.Serve, 0.9f);
            if (Message != null) Message(server, "", Color.white);
        }

        // =================================================================== ball

        void SimulateBall(float dt)
        {
            int heading = _sim.Velocity.z > 0f ? 1 : 0;
            float simDt = dt * (_ballInPlay ? Powers.BallTimeScale(heading) : 1f);
            float h = _cfg.physics.substep;
            int steps = Mathf.Clamp(Mathf.CeilToInt(simDt / h), 1, 64);
            float step = simDt / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 before = _sim.Position;
                _events.Clear();
                _sim.Substep(step, _events);
                for (int e = 0; e < _events.Count; e++) HandleContact(_events[e]);
                if (!_sim.Active) break;
                if (_ballInPlay && !Referee.RallyOver)
                {
                    if (CheckRacketHit(before, _sim.Position)) continue;
                    CheckShieldWall(before, _sim.Position);
                    CheckOrbPickup(_sim.Position);
                    CheckDeadBall(_sim.Position);
                }
            }
        }

        void HandleContact(BallContactEvent e)
        {
            float speed = e.VelocityBefore.magnitude;
            switch (e.Type)
            {
                case BallContactType.Table:
                    PlaySfx(Brooklyneir.Audio.Sfx.Bounce, Mathf.Clamp01(0.4f + speed * 0.1f), Mathf.Lerp(0.9f, 1.15f, Mathf.Clamp01(speed / 8f)));
                    _vfx.Bounce(e.Point, Color.white);
                    _marker.Hide();
                    if (!_ballInPlay || Referee.RallyOver) return;
                    if (IsTraining && Referee.LastHitter == 0 && e.Side == 1) { TrainingLanded(e.Point); return; }
                    if (Referee.NextBounceLoses(e.Side) && Powers.Players[e.Side].ShieldActive)
                    {
                        ShieldSave(e.Side, e.Point);
                        return;
                    }
                    Referee.OnTableBounce(e.Side);
                    if (Referee.RallyOver) EndRally();
                    break;
                case BallContactType.Net:
                case BallContactType.NetCord:
                    PlaySfx(Brooklyneir.Audio.Sfx.Net, Mathf.Clamp01(0.5f + speed * 0.08f));
                    if (!_ballInPlay || Referee.RallyOver) return;
                    Referee.OnNetContact();
                    if (e.Type == BallContactType.NetCord) Say(-1, "¡RED... Y PASA!", new Color(1f, 0.9f, 0.5f));
                    break;
                case BallContactType.Floor:
                    if (speed > 1f) PlaySfx(Brooklyneir.Audio.Sfx.Bounce, 0.35f, 0.7f);
                    if (!_ballInPlay || Referee.RallyOver) return;
                    Referee.OnBallDead(Mathf.Abs(e.Point.x) > TableGeometry.HalfWidth + 0.02f);
                    if (Referee.RallyOver) EndRally();
                    break;
            }
        }

        bool CheckRacketHit(Vector3 before, Vector3 after)
        {
            int r = Referee.Receiver;
            if (!Referee.CanHit(r) || !Rackets[r].gameObject.activeSelf) return false;
            float pz = Rackets[r].Position.z;
            if ((before.z - pz) * (after.z - pz) > 0f || Mathf.Abs(after.z - before.z) < 1e-6f) return false;
            float f = (pz - before.z) / (after.z - before.z);
            Vector3 c = Vector3.LerpUnclamped(before, after, f);
            float dx = c.x - Rackets[r].Position.x;
            float height = c.y - TableGeometry.TopY;
            float reach = Rackets[r].Reach;
            if (Mathf.Abs(dx) > reach || height < _cfg.racket.minHittableHeight || height > _cfg.racket.maxHittableHeight) return false;
            ExecuteHit(r, c, Mathf.Clamp(dx / reach, -1f, 1f));
            return true;
        }

        ShotContext BaseContext(int p, Vector3 contact, float offset)
        {
            PlayerPowerState s = Powers.Players[p];
            ShotContext ctx = new ShotContext();
            ctx.Hitter = p;
            ctx.ContactPoint = contact;
            ctx.ContactOffset = offset;
            ctx.RacketVelocity = Rackets[p].SwingVelocity;
            ctx.Stats = Rackets[p].Stats;
            ctx.Assist = Rackets[p].Assist;
            ctx.SpecialArmed = s.SpecialArmed;
            ctx.SuperSmashArmed = s.SuperSmashArmed;
            ctx.CurveArmed = s.CurveArmed;
            ctx.FireActive = s.FireActive && !s.FireBurning;
            ctx.SwingMultiplier = Powers.SwingMultiplier(p);
            ctx.NoiseMultiplier = _ai[p] != null ? _ai[p].NoiseMultiplier : 1f;
            return ctx;
        }

        void ExecuteHit(int p, Vector3 contact, float offset)
        {
            if (_ai[p] != null) _ai[p].DecideSwing(contact.y - TableGeometry.TopY);
            ShotContext ctx = BaseContext(p, contact, offset);
            ShotResult res = _shots.Compute(ctx);
            Referee.RegisterHit(p);
            Powers.OnShot(p, res.ConsumedCurve, res.ConsumedSuperSmash, res.ConsumedSpecial);
            Launch(p, contact, res);
            RallyHits++;
            _result.Hits[p]++;
            if (res.Type == ShotType.Smash || res.Type == ShotType.SuperSmash) _result.Smashes[p]++;

            float strength;
            Color fx;
            switch (res.Type)
            {
                case ShotType.Smash: strength = 0.9f; fx = new Color(1f, 0.85f, 0.4f); Say(p, "¡REMATE!", fx); break;
                case ShotType.SuperSmash: strength = 1f; fx = new Color(1f, 0.3f, 0.3f); Say(p, "¡SÚPER REMATE!", fx); break;
                case ShotType.Special: strength = 1f; fx = new Color(1f, 0.85f, 0.3f); Say(p, "¡BROOKLYN BOMB!", fx); break;
                case ShotType.Fast: strength = 0.6f; fx = Color.white; break;
                case ShotType.Spin: strength = 0.5f; fx = new Color(0.8f, 0.6f, 1f); Say(p, "¡EFECTO!", fx); break;
                case ShotType.Defensive: strength = 0.25f; fx = new Color(0.7f, 0.9f, 1f); break;
                default: strength = 0.4f; fx = Color.white; break;
            }
            if (res.ConsumedCurve) Say(p, "¡CURVA!", PowerVisuals.ColorOf(PowerUpType.Curve));
            Rackets[p].PlaySwing(strength);
            _vfx.Hit(contact, fx, strength);
            if (res.Type == ShotType.SuperSmash || res.Type == ShotType.Special || res.Type == ShotType.Smash) _vfx.Smash(contact, fx);
            PlaySfx(strength > 0.85f ? Brooklyneir.Audio.Sfx.HitSmash : (strength < 0.3f ? Brooklyneir.Audio.Sfx.HitSoft : Brooklyneir.Audio.Sfx.HitClean), 0.6f + strength * 0.4f, _rng.Range(0.95f, 1.06f));
            if (strength > 0.85f) PlaySfx(Brooklyneir.Audio.Sfx.Whoosh, 0.6f);
            if (Human[p] && Game.Haptics != null) Game.Haptics.Pulse(strength);
            _camera.Shake(res.CameraShake);
            if (res.Type == ShotType.SuperSmash || res.Type == ShotType.Special) _camera.Punch(1f);
            if (IsTraining && p == 0)
            {
                _lastTrainingShot = res;
                _awaitingLanding = true;
            }
        }

        void Launch(int hitter, Vector3 from, ShotResult res)
        {
            _sim.Launch(from, res.Velocity, res.SpinAccel);
            _ballInPlay = true;
            _ballView.SetSpin(res.Velocity, res.SpinAccel);
            Vector3 landing;
            float t;
            bool showMarker = Game.Save == null || Game.Save.Data.settings.showLandingMarker;
            if (showMarker && ShotSolver.PredictFirstBounce(_sim, _scratch, _predictBuffer, 2.5f, out landing, out t))
                _marker.Show(landing, PlayerColors[hitter]);
            else
                _marker.Hide();
            int other = 1 - hitter;
            if (_ai[other] != null) _ai[other].OnOpponentShot(_sim);
        }

        void CheckShieldWall(Vector3 before, Vector3 after)
        {
            int r = Referee.Receiver;
            if (!Powers.Players[r].ShieldActive || !Referee.CanHit(r)) return;
            float z = _shields[r].PlaneZ;
            if ((before.z - z) * (after.z - z) > 0f || Mathf.Abs(after.z - before.z) < 1e-6f) return;
            float f = (z - before.z) / (after.z - before.z);
            Vector3 c = Vector3.LerpUnclamped(before, after, f);
            if (Mathf.Abs(c.x) > 1.3f || c.y < TableGeometry.TopY - 0.15f) return;
            ShieldSave(r, c);
        }

        void ShieldSave(int p, Vector3 point)
        {
            if (!Referee.CanHit(p)) return;
            Vector3 from = new Vector3(Mathf.Clamp(point.x, -1.2f, 1.2f), Mathf.Max(point.y, TableGeometry.TopY + 0.18f), point.z);
            ShotContext ctx = BaseContext(p, from, 0f);
            ctx.RacketVelocity = new Vector3(0f, 0f, TableGeometry.ForwardSign(p) * (_cfg.shots.defensiveSwipeThreshold - 0.3f));
            ctx.SpecialArmed = ctx.SuperSmashArmed = ctx.CurveArmed = ctx.FireActive = false;
            ShotResult res = _shots.Compute(ctx);
            Referee.RegisterHit(p);
            Powers.OnShot(p, false, false, false);
            Powers.ConsumeShield(p);
            Launch(p, from, res);
            _shields[p].Flash();
            _vfx.Pickup(from, PowerVisuals.ColorOf(PowerUpType.Shield));
            PlaySfx(Brooklyneir.Audio.Sfx.ShieldSave, 1f);
            Say(p, "¡SALVADO POR EL ESCUDO!", PowerVisuals.ColorOf(PowerUpType.Shield));
            RallyHits++;
        }

        void CheckDeadBall(Vector3 bp)
        {
            bool overTable = TableGeometry.IsOverTable(bp.x, bp.z);
            MatchTuning m = _cfg.match;
            if ((bp.y < TableGeometry.TopY - 0.05f && !overTable) || Mathf.Abs(bp.x) > m.outOfBoundsX || Mathf.Abs(bp.z) > m.outOfBoundsZ)
            {
                if (IsTraining && _awaitingLanding)
                {
                    TrainingResolve(false);
                    return;
                }
                Referee.OnBallDead(Mathf.Abs(bp.x) > TableGeometry.HalfWidth + 0.02f);
                if (Referee.RallyOver) EndRally();
            }
        }

        // =================================================================== points

        void EndRally()
        {
            _marker.Hide();
            int winner = Referee.Winner;
            Powers.OnRallyEnded();
            if (RallyHits > _result.LongestRally) _result.LongestRally = RallyHits;

            if (IsTraining)
            {
                // The machine "won": the player missed or the return failed.
                TrainingResolve(false);
                return;
            }
            if (winner < 0)
            {
                PrepareServe(Score.Server);
                return;
            }
            int before0 = Score.Points[0], before1 = Score.Points[1];
            ScoreEvent ev = Score.AwardPoint(winner);
            if (ev.SetWon)
            {
                int[] setScore = { before0, before1 };
                setScore[winner]++;
                _result.SetScores.Add(setScore);
            }
            Powers.OnPoint(winner);
            _pointsWon[winner]++;
            Phase = MatchPhase.PointOver;
            _phaseTimer = _cfg.match.pointPauseSeconds;

            if (IsDemo)
            {
                _phaseTimer = 1.2f;
                return;
            }
            string reason = RallyReferee.Describe(Referee.Reason);
            Say(winner, "¡PUNTO!", PlayerColors[winner]);
            Say(1 - winner, reason, new Color(1f, 1f, 1f, 0.85f));
            PlaySfx(Brooklyneir.Audio.Sfx.Point, 0.9f);
            if (Game.Arena != null) Game.Arena.Cheer(RallyHits >= 6 ? 1f : 0.5f);
            if (RallyHits >= 6) PlaySfx(Brooklyneir.Audio.Sfx.Cheer, 0.7f);
            if (ev.SetWon && !ev.MatchWon)
            {
                Say(-1, "SET PARA JUGADOR " + (winner + 1), PlayerColors[winner]);
                _phaseTimer += 1f;
            }
            else if (!ev.MatchWon && Score.IsMatchPoint(0) ^ Score.IsMatchPoint(1))
            {
                Say(-1, "PUNTO DE PARTIDO", new Color(1f, 0.85f, 0.3f));
            }
            if (ev.MatchWon) _matchWinner = winner;
            if (PointScored != null) PointScored(winner, Referee.Reason);
        }

        void FinishMatch()
        {
            Phase = MatchPhase.MatchOver;
            _ballInPlay = false;
            _input.SetEnabled(false);
            _result.Winner = _matchWinner >= 0 ? _matchWinner : Score.MatchWinner;
            _result.Sets[0] = Score.Sets[0];
            _result.Sets[1] = Score.Sets[1];
            _result.Points[0] = _pointsWon[0];
            _result.Points[1] = _pointsWon[1];
            _result.DurationSeconds = MatchClock;
            Powers.ResetAll();
            _vfx.SetFireTrail(null, false);
            if (_orb != null) _orb.Despawn();
            _vfx.Confetti(new Vector3(0f, 2.2f, 0f));
            if (Game.Arena != null) Game.Arena.Cheer(1f);
            PlaySfx(Brooklyneir.Audio.Sfx.Win, 1f);
            if (MatchFinished != null) MatchFinished(_result);
        }

        // =================================================================== power-ups

        float OrbInterval()
        {
            if (Config == null) return 9999f;
            switch (Config.powerFrequency)
            {
                case PowerFrequency.Low: return _cfg.powerUps.spawnIntervalLow;
                case PowerFrequency.High: return _cfg.powerUps.spawnIntervalHigh;
                case PowerFrequency.Normal: return _cfg.powerUps.spawnIntervalNormal;
                default: return 9999f;
            }
        }

        void UpdateOrbs(float dt)
        {
            if (Config == null || !Config.powerUpsEnabled || IsTraining) return;
            if (_orb.Alive)
            {
                if (_orb.Expired) _orb.Despawn();
                return;
            }
            if (Phase != MatchPhase.Rally) return;
            _orbTimer -= dt;
            if (_orbTimer > 0f) return;
            _orbTimer = OrbInterval() * _rng.Range(0.8f, 1.2f);
            float side = _rng.Value() < 0.5f ? -1f : 1f;
            Vector3 pos = new Vector3(_rng.Range(-0.5f, 0.5f), TableGeometry.TopY + _rng.Range(0.18f, 0.34f), side * _rng.Range(0.35f, 0.95f));
            _orb.Spawn(Powers.RollOrbType(), pos, _cfg.powerUps.orbLifetime);
        }

        void CheckOrbPickup(Vector3 ball)
        {
            if (!_orb.Alive) return;
            if ((ball - _orb.Centre).sqrMagnitude > _cfg.powerUps.orbPickupRadius * _cfg.powerUps.orbPickupRadius) return;
            int owner = Referee.LastHitter;
            if (!Powers.CanCollect(owner)) return;
            Vector3 at = _orb.Centre;
            PowerUpType orbType = _orb.Type;
            _orb.Despawn();
            PowerUpType got = Powers.Collect(owner, orbType);
            _vfx.Pickup(at, PowerVisuals.ColorOf(got));
        }

        /// <summary>Called by the HUD power button.</summary>
        public void RequestActivatePower(int player)
        {
            if (Paused || (Phase != MatchPhase.Rally && Phase != MatchPhase.ServeWait)) return;
            PowerActivationResult r = Powers.TryActivate(player);
            if (r == PowerActivationResult.OnCooldown) { Say(player, "RECARGANDO...", new Color(1f, 1f, 1f, 0.8f)); PlaySfx(Brooklyneir.Audio.Sfx.UiError, 0.6f); }
            else if (r == PowerActivationResult.Blocked) { Say(player, "YA ESTÁ ACTIVO", new Color(1f, 1f, 1f, 0.8f)); PlaySfx(Brooklyneir.Audio.Sfx.UiError, 0.6f); }
        }

        /// <summary>Called by the HUD special button.</summary>
        public void RequestSpecial(int player)
        {
            if (Paused || (Phase != MatchPhase.Rally && Phase != MatchPhase.ServeWait)) return;
            if (Powers.TryArmSpecial(player))
            {
                Say(player, "¡GOLPE ESPECIAL PREPARADO!", new Color(1f, 0.85f, 0.3f));
                PlaySfx(Brooklyneir.Audio.Sfx.Special, 0.8f);
            }
        }

        void OnPowerCollected(int player, PowerUpType type)
        {
            if (IsDemo) return;
            Say(player, PowerUpSystem.DisplayName(type), PowerVisuals.ColorOf(type));
            PlaySfx(Brooklyneir.Audio.Sfx.Pickup, 0.9f);
        }

        void OnPowerActivated(int player, PowerUpType type)
        {
            _result.PowersUsed[player]++;
            Color c = PowerVisuals.ColorOf(type);
            switch (type)
            {
                case PowerUpType.Fire: PlaySfx(Brooklyneir.Audio.Sfx.PowerFire, 1f); break;
                case PowerUpType.Slow: PlaySfx(Brooklyneir.Audio.Sfx.PowerSlow, 1f); break;
                case PowerUpType.Curve: PlaySfx(Brooklyneir.Audio.Sfx.PowerCurve, 1f); break;
                case PowerUpType.SuperSmash: PlaySfx(Brooklyneir.Audio.Sfx.PowerSmash, 1f); break;
                case PowerUpType.Shield: PlaySfx(Brooklyneir.Audio.Sfx.PowerShield, 1f); break;
                case PowerUpType.Freeze:
                    PlaySfx(Brooklyneir.Audio.Sfx.PowerFreeze, 1f);
                    if (!Powers.Players[1 - player].ShieldActive)
                    {
                        _vfx.Freeze(Rackets[1 - player].transform.position);
                        Say(1 - player, "¡CONGELADO!", c);
                    }
                    break;
            }
            if (IsDemo) return;
            Say(player, PowerUpSystem.DisplayName(type) + "!", c);
            _camera.Shake(0.15f);
        }

        void OnPowerEnded(int player, PowerUpType type)
        {
            if (type == PowerUpType.Fire) _vfx.SetFireTrail(null, false);
        }

        void OnFreezeBlocked(int player)
        {
            Say(player, "¡EL ESCUDO BLOQUEA EL HIELO!", PowerVisuals.ColorOf(PowerUpType.Shield));
            _shields[player].Flash();
        }

        BallLook CurrentLook()
        {
            if (!_ballInPlay) return BallLook.Normal;
            int hitter = Referee.LastHitter;
            PlayerPowerState s = Powers.Players[hitter];
            if (s.FireBurning)
            {
                _vfx.SetFireTrail(_ballView.BallTransform, true);
                return BallLook.Fire;
            }
            _vfx.SetFireTrail(null, false);
            float side = Mathf.Abs(_sim.SpinAccel.x);
            if (side >= _cfg.shots.curveSideAccel * 0.8f) return BallLook.Curve;
            if (_sim.Velocity.magnitude > _cfg.shots.superSmash.horizontalSpeed * 0.95f) return BallLook.SuperSmash;
            int heading = _sim.Velocity.z > 0f ? 1 : 0;
            if (Powers.Players[heading].SlowActive) return BallLook.Slow;
            return BallLook.Normal;
        }

        // =================================================================== training

        void FeedTrainingBall()
        {
            if (_drill == null) return;
            if (_fed >= _drill.Balls)
            {
                FinishTraining();
                return;
            }
            _fed++;
            UpdateTrainingTarget();
            Vector3 from = new Vector3(_rng.Range(-0.35f, 0.35f), TableGeometry.TopY + 0.32f, TableGeometry.HalfLength + 0.15f);
            Vector3 target = new Vector3(_rng.Range(-0.45f, 0.45f), TableGeometry.TopY + _cfg.physics.ballRadius, -_rng.Range(0.6f, 1.0f));
            ShotTypeTuning t = _drill.HighFeed ? _cfg.shots.defensive : _cfg.shots.normal;
            float speed = _drill.HighFeed ? 2.8f : 3.6f;
            Vector3 accel = new Vector3(0f, -_cfg.physics.gravity + t.verticalAccel, 0f);
            Vector3 flat = target - from;
            flat.y = 0f;
            ShotSolution sol = ShotSolver.SolveFlattened(from, target, flat.magnitude / speed, accel, _cfg.physics, _drill.HighFeed ? 0.9f : 0.25f, 0.8f);
            Referee.StartRally(1);
            _sim.Launch(from, sol.Velocity, new Vector3(0f, t.verticalAccel, 0f));
            _ballInPlay = true;
            _awaitingLanding = false;
            _ballView.SetSpin(sol.Velocity, Vector3.zero);
            Phase = MatchPhase.Rally;
            PlaySfx(Brooklyneir.Audio.Sfx.Serve, 0.8f);
        }

        void UpdateTrainingTarget()
        {
            if (_drill == null) { _targetRing.Hide(); return; }
            if (_drill.Kind == DrillKind.Precision)
            {
                _precisionTarget = new Vector3(_rng.Range(-0.5f, 0.5f), TableGeometry.TopY, _rng.Range(0.55f, 1.1f));
                _targetRing.Show(_precisionTarget, new Color(1f, 0.8f, 0.2f));
                _targetRing.transform.localScale = Vector3.one * 0.5f;
            }
            else if (_drill.Kind == DrillKind.Control)
            {
                _controlSide = -_controlSide;
                _targetRing.Show(new Vector3(_controlSide * 0.4f, TableGeometry.TopY, 0.8f), new Color(0.3f, 1f, 0.6f));
            }
            else _targetRing.Hide();
        }

        void TrainingLanded(Vector3 point)
        {
            if (!_awaitingLanding) return;
            bool ok;
            switch (_drill.Kind)
            {
                case DrillKind.Precision: ok = (new Vector2(point.x - _precisionTarget.x, point.z - _precisionTarget.z)).magnitude <= 0.26f; break;
                case DrillKind.Control: ok = Mathf.Sign(point.x) == _controlSide && Mathf.Abs(point.x) > 0.05f; break;
                case DrillKind.Spin: ok = Mathf.Abs(_lastTrainingShot.SideSpin) >= _cfg.shots.spinLabelThreshold * 0.8f; break;
                case DrillKind.Smash: ok = _lastTrainingShot.Type == ShotType.Smash || _lastTrainingShot.Type == ShotType.SuperSmash; break;
                default: ok = true; break;
            }
            TrainingResolve(ok);
        }

        void TrainingResolve(bool success)
        {
            if (Phase != MatchPhase.Rally) return;
            _awaitingLanding = false;
            Referee.Abort();
            _ballInPlay = false;
            if (success)
            {
                _successes++;
                Say(0, "¡BIEN!", new Color(0.4f, 1f, 0.5f));
                PlaySfx(Brooklyneir.Audio.Sfx.Coin, 0.7f);
            }
            else
            {
                Say(0, "FALLO", new Color(1f, 0.5f, 0.5f));
            }
            Phase = MatchPhase.TrainingFeed;
            _phaseTimer = 1.1f;
        }

        void FinishTraining()
        {
            Phase = MatchPhase.MatchOver;
            _targetRing.Hide();
            _input.SetEnabled(false);
            TrainingResult r = new TrainingResult
            {
                Drill = _drill,
                Successes = _successes,
                Balls = _drill.Balls,
                Stars = TrainingDrills.StarsFor(_drill, _successes)
            };
            PlaySfx(r.Stars >= 2 ? Brooklyneir.Audio.Sfx.Win : Brooklyneir.Audio.Sfx.Point, 0.9f);
            if (TrainingFinished != null) TrainingFinished(r);
        }

        // =================================================================== helpers

        void Say(int player, string text, Color color)
        {
            if (IsDemo) return;
            if (Message != null) Message(player, text, color);
        }

        void PlaySfx(Brooklyneir.Audio.Sfx s, float volume, float pitch = 1f)
        {
            if (Game.Audio != null) Game.Audio.Play(s, volume * _sfxScale, pitch);
        }

        void OnApplicationPause(bool paused)
        {
            // Android: leaving the app (call, home button) pauses the match; nothing is lost.
            if (paused && !Paused && Phase != MatchPhase.Inactive && !IsDemo && Phase != MatchPhase.MatchOver)
            {
                SetPaused(true);
                if (Game.Flow != null) Game.Flow.OnMatchPausedExternally();
            }
        }
    }
}
