using System.Collections.Generic;
using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Physics;
using Brooklyneir.PowerUps;
using UnityEngine;

namespace Brooklyneir.AI
{
    /// <summary>
    /// Connects an <see cref="AIBrain"/> to a racket in a real match: predicts where the incoming
    /// ball will cross its racket plane (using the same simulation as the game), moves the racket
    /// there through the brain's reaction delay and speed limit, serves after a short pause, and
    /// uses held powers and the special shot when the brain decides to.
    /// </summary>
    public class AIPlayer
    {
        public readonly int Player;
        public readonly AIBrain Brain;
        readonly RacketController _racket;
        readonly BallSimulation _scratch;
        readonly List<BallContactEvent> _buffer = new List<BallContactEvent>(16);
        readonly RacketTuning _rt;
        readonly float _serveDelay;
        float _serveTimer = -1f;
        float _contactEta = -1f;

        public AIPlayer(int player, AIDifficultyTuning tuning, BalanceConfig cfg, RacketController racket, IRandomSource rng)
        {
            Player = player;
            Brain = new AIBrain(player, tuning, cfg.shots, rng);
            _racket = racket;
            _scratch = new BallSimulation(cfg.physics);
            _rt = cfg.racket;
            _serveDelay = cfg.match.serveAutoTimeoutAI;
        }

        public float NoiseMultiplier { get { return Brain.Tuning.shotNoiseMultiplier; } }

        public void ResetForPoint()
        {
            Brain.ClearPlan();
            _contactEta = -1f;
            _racket.ClearSyntheticSwing();
        }

        public void BeginServeWait()
        {
            _serveTimer = _serveDelay * Random.Range(0.8f, 1.3f);
        }

        /// <summary>True once, when it is time to serve.</summary>
        public bool TickServe(float dt)
        {
            if (_serveTimer < 0f) return false;
            _serveTimer -= dt;
            if (_serveTimer <= 0f)
            {
                _serveTimer = -1f;
                // Aim the serve a little: shift the racket before serving.
                _racket.SetTarget(new Vector3(Random.Range(-0.45f, 0.45f), 0f, _racket.Home.z));
                return true;
            }
            return false;
        }

        /// <summary>The opponent just hit: plan the return.</summary>
        public void OnOpponentShot(BallSimulation ball)
        {
            float plane = _racket.Home.z;
            Vector3 point;
            float time;
            if (ShotSolver.PredictPlaneCrossing(ball, _scratch, _buffer, plane, Player, 3f, out point, out time))
            {
                Brain.PlanReturn(point, _racket.Reach, _rt.zoneHalfWidth);
                _contactEta = time;
            }
            else
            {
                Brain.ClearPlan();
                _contactEta = -1f;
            }
        }

        public void Tick(float dt, bool ballComingToMe)
        {
            if (_contactEta >= 0f) _contactEta -= dt;
            Vector3 target = Brain.DesiredPosition(dt, _racket.Position, _racket.Home);
            _racket.SetTarget(target);
        }

        /// <summary>Seconds until the planned contact (negative if none).</summary>
        public float ContactEta { get { return _contactEta; } }

        public Vector3 DecideSwing(float ballHeightAboveTable)
        {
            Vector3 swing = Brain.ChooseSwing(ballHeightAboveTable);
            _racket.SetSyntheticSwing(swing);
            return swing;
        }

        public void ConsiderPowers(PowerUpSystem powers, float dt, bool ballComingToMe)
        {
            PlayerPowerState mine = powers.Players[Player];
            PlayerPowerState other = powers.Players[1 - Player];
            bool aboutToHit = ballComingToMe && _contactEta >= 0f && _contactEta < 0.45f;
            if (mine.Held != PowerUpType.None && mine.Cooldown <= 0f &&
                Brain.WantsToActivate(mine.Held, dt, ballComingToMe, aboutToHit, other.ShieldActive))
                powers.TryActivate(Player);
            if (mine.SpecialReady && !mine.SpecialArmed && Brain.WantsSpecial(aboutToHit))
                powers.TryArmSpecial(Player);
        }
    }
}
