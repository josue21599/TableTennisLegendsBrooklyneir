using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Physics;
using Brooklyneir.PowerUps;
using UnityEngine;

namespace Brooklyneir.AI
{
    /// <summary>
    /// Decision-making for a CPU opponent (pure logic, unit-tested). It never teleports: it only
    /// produces a target position that the racket then reaches at a limited speed, after a
    /// reaction delay, with a positional error, and occasionally a deliberate misread whose
    /// probability depends on the difficulty.
    /// </summary>
    public class AIBrain
    {
        readonly AIDifficultyTuning _t;
        readonly ShotTuning _shots;
        readonly IRandomSource _rng;
        readonly int _player;

        public float ReactionRemaining { get; private set; }
        public bool HasPlan { get; private set; }
        public Vector3 PlannedTarget { get; private set; }
        public bool PlannedMiss { get; private set; }
        float _powerCheckTimer;

        public AIBrain(int player, AIDifficultyTuning tuning, ShotTuning shots, IRandomSource rng)
        {
            _player = player;
            _t = tuning;
            _shots = shots;
            _rng = rng;
        }

        public AIDifficultyTuning Tuning { get { return _t; } }

        public void ClearPlan()
        {
            HasPlan = false;
            PlannedMiss = false;
            ReactionRemaining = 0f;
        }

        /// <summary>
        /// Called once when the opponent hits the ball towards this AI.
        /// <paramref name="predictedContact"/> is where the ball will cross the AI's racket plane.
        /// </summary>
        public void PlanReturn(Vector3 predictedContact, float reach, float zoneHalfWidth)
        {
            ReactionRemaining = _t.reactionTime * _rng.Range(0.8f, 1.25f);
            float x = predictedContact.x + _rng.Gaussian() * _t.positionError;
            PlannedMiss = _rng.Value() < _t.missChance;
            if (PlannedMiss)
            {
                float side = _rng.Value() < 0.5f ? -1f : 1f;
                x += side * (reach * 1.3f + _rng.Range(0.06f, 0.18f));
            }
            else
            {
                // Hit slightly off-centre on purpose to steer the ball (stays inside the reach).
                float steer = _rng.Range(-0.55f, 0.55f) * reach;
                x -= steer;
            }
            x = Mathf.Clamp(x, -zoneHalfWidth, zoneHalfWidth);
            PlannedTarget = new Vector3(x, predictedContact.y, predictedContact.z);
            HasPlan = true;
        }

        /// <summary>Returns where the racket should go this frame.</summary>
        public Vector3 DesiredPosition(float dt, Vector3 current, Vector3 home)
        {
            if (!HasPlan) return new Vector3(Mathf.Lerp(current.x, home.x, 0.5f), home.y, home.z);
            if (ReactionRemaining > 0f)
            {
                ReactionRemaining -= dt;
                return current;
            }
            return new Vector3(PlannedTarget.x, home.y, home.z);
        }

        /// <summary>Synthetic swing velocity at contact (same input a human would produce).</summary>
        public Vector3 ChooseSwing(float ballHeightAboveTable)
        {
            float fwd = TableGeometry.ForwardSign(_player);
            float roll = _rng.Value();
            float forward;
            if (ballHeightAboveTable >= _shots.smashMinHeight + 0.05f && roll < _t.aggression * 0.8f)
                forward = _shots.smashSwipeThreshold + _rng.Range(0.2f, 1.2f);
            else if (roll < _t.aggression * 1.4f)
                forward = _shots.fastSwipeThreshold + _rng.Range(0.1f, 0.9f);
            else if (roll > 0.93f)
                forward = _shots.defensiveSwipeThreshold - _rng.Range(0.1f, 0.6f);
            else
                forward = _rng.Range(-0.3f, _shots.fastSwipeThreshold * 0.8f);

            // Same stroke model as a human: pick where to send the ball sideways, then the stroke
            // that produces it (found through ShotModel.LateralShift, so the AI and the shot model
            // can never disagree). Keep smashes straight and normal strokes below the spin threshold.
            float maxLateral = forward >= _shots.smashSwipeThreshold
                ? forward * Mathf.Tan(_shots.smashMaxAngle * Mathf.Deg2Rad) * 0.9f
                : _shots.spinSwipeThreshold * 0.95f;
            float desiredShift = _rng.Range(-0.85f, 0.85f) * _shots.maxShiftX;
            float lateral = LateralForShift(desiredShift, maxLateral);
            if (forward < _shots.fastSwipeThreshold && forward > _shots.defensiveSwipeThreshold && _rng.Value() < _t.spinUsage)
            {
                lateral = (_rng.Value() < 0.5f ? -1f : 1f) * (_shots.spinSwipeThreshold + _rng.Range(0.4f, 1.6f));
                forward = _rng.Range(-0.2f, 0.6f);
            }
            return new Vector3(lateral, 0f, forward * fwd);
        }

        /// <summary>Smallest sideways stroke speed whose ShotModel.LateralShift reaches the desired shift.</summary>
        float LateralForShift(float desiredShift, float maxLateral)
        {
            float sign = desiredShift < 0f ? -1f : 1f;
            float want = Mathf.Abs(desiredShift);
            const float step = 0.05f;
            for (float v = 0f; v <= maxLateral; v += step)
            {
                if (Mathf.Abs(ShotModel.LateralShift(sign * v, _shots)) >= want) return sign * v;
            }
            return sign * maxLateral;
        }

        /// <summary>
        /// Decides whether to fire the held power now. Checked a few times per second, with
        /// situational preferences so the AI plays its powers sensibly and within the rules.
        /// </summary>
        public bool WantsToActivate(PowerUpType held, float dt, bool ballComingToMe, bool aboutToHit, bool opponentHasShield)
        {
            if (held == PowerUpType.None) return false;
            _powerCheckTimer -= dt;
            if (_powerCheckTimer > 0f) return false;
            _powerCheckTimer = 0.25f;
            float chance = _t.powerUseChancePerSecond * 0.25f;
            switch (held)
            {
                case PowerUpType.Fire:
                case PowerUpType.Curve:
                case PowerUpType.SuperSmash:
                    if (!aboutToHit) return false;
                    chance *= 3f;
                    break;
                case PowerUpType.Slow:
                case PowerUpType.Shield:
                    if (!ballComingToMe) return false;
                    chance *= 2f;
                    break;
                case PowerUpType.Freeze:
                    if (opponentHasShield) return false;
                    if (ballComingToMe) return false;
                    chance *= 2f;
                    break;
            }
            return _rng.Value() < chance;
        }

        public bool WantsSpecial(bool aboutToHit)
        {
            return aboutToHit && _rng.Value() < 0.35f + _t.aggression * 0.5f;
        }
    }
}
