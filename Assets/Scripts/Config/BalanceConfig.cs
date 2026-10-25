using System;

namespace Brooklyneir.Config
{
    /// <summary>
    /// Every gameplay tunable lives here. Defaults are compiled in; Resources/Config/balance.json
    /// overrides any subset of them (JsonUtility.FromJsonOverwrite), so balance can be changed
    /// without touching code. Pure data: no UnityEngine dependency.
    /// </summary>
    [Serializable]
    public class BalanceConfig
    {
        public PhysicsConfig physics = new PhysicsConfig();
        public ShotTuning shots = new ShotTuning();
        public RacketTuning racket = new RacketTuning();
        public PowerUpTuning powerUps = new PowerUpTuning();
        public AITuning ai = new AITuning();
        public EconomyTuning economy = new EconomyTuning();
        public MatchTuning match = new MatchTuning();

        /// <summary>Clamps values that could break the simulation if a JSON file is edited badly.</summary>
        public void Validate()
        {
            if (physics == null) physics = new PhysicsConfig();
            if (shots == null) shots = new ShotTuning();
            if (racket == null) racket = new RacketTuning();
            if (powerUps == null) powerUps = new PowerUpTuning();
            if (ai == null) ai = new AITuning();
            if (economy == null) economy = new EconomyTuning();
            if (match == null) match = new MatchTuning();
            physics.Validate();
            shots.Validate();
            racket.Validate();
            powerUps.Validate();
            ai.Validate();
            economy.Validate();
        }
    }

    [Serializable]
    public class PhysicsConfig
    {
        public float gravity = 9.0f;
        public float ballRadius = 0.032f; // slightly oversized for legibility on phones
        public float tableRestitution = 0.8f;
        public float tableFriction = 0.94f;
        public float floorRestitution = 0.55f;
        public float floorFriction = 0.8f;
        public float netRestitution = 0.15f;
        public float spinDecayOnBounce = 0.45f;
        /// <summary>Forward velocity added on bounce per unit of topspin (m/s per m/s^2 of dip).</summary>
        public float topspinKick = 0.09f;
        public float netClearMargin = 0.04f;
        public float maxBallSpeed = 13.5f;
        public float minFlightTime = 0.34f;
        public float maxFlightTime = 1.25f;
        public float substep = 1f / 240f;

        public void Validate()
        {
            gravity = Clamp(gravity, 3f, 30f);
            ballRadius = Clamp(ballRadius, 0.015f, 0.05f);
            tableRestitution = Clamp(tableRestitution, 0.3f, 0.98f);
            tableFriction = Clamp(tableFriction, 0.5f, 1f);
            floorRestitution = Clamp(floorRestitution, 0.1f, 0.9f);
            floorFriction = Clamp(floorFriction, 0.3f, 1f);
            netRestitution = Clamp(netRestitution, 0f, 0.8f);
            spinDecayOnBounce = Clamp(spinDecayOnBounce, 0f, 1f);
            maxBallSpeed = Clamp(maxBallSpeed, 4f, 25f);
            minFlightTime = Clamp(minFlightTime, 0.2f, 1f);
            maxFlightTime = Clamp(maxFlightTime, minFlightTime + 0.1f, 3f);
            substep = Clamp(substep, 1f / 600f, 1f / 60f);
        }

        internal static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
    }

    [Serializable]
    public class ShotTypeTuning
    {
        public float horizontalSpeed = 3.6f;
        public float targetDepth = 0.85f;     // metres past the net
        public float aimNoise = 0.05f;        // sigma of landing x/z error (m)
        public float verticalNoise = 0.10f;   // sigma of launch vy error (m/s) -> net / long errors
        public float verticalAccel = -1.5f;   // extra constant accel on y (topspin dip < 0, float > 0)
        public float maxAimX = 0.60f;         // how wide this shot may be aimed
        public float cameraShake = 0f;
        /// <summary>Highest allowed clearance over the net (m). Loopy shots are flattened down to it.</summary>
        public float maxNetClearance = 0.25f;
        /// <summary>Flattening may shorten the flight to this fraction of the speed-based time, never less.</summary>
        public float minTimeFactor = 0.75f;

        public ShotTypeTuning() { }
        public ShotTypeTuning(float speed, float depth, float noise, float vNoise, float vAccel, float aimX, float shake, float maxClearance)
        {
            horizontalSpeed = speed; targetDepth = depth; aimNoise = noise; verticalNoise = vNoise;
            verticalAccel = vAccel; maxAimX = aimX; cameraShake = shake; maxNetClearance = maxClearance;
        }

        public void Validate()
        {
            horizontalSpeed = PhysicsConfig.Clamp(horizontalSpeed, 1f, 15f);
            targetDepth = PhysicsConfig.Clamp(targetDepth, 0.2f, 1.3f);
            aimNoise = PhysicsConfig.Clamp(aimNoise, 0f, 0.4f);
            verticalNoise = PhysicsConfig.Clamp(verticalNoise, 0f, 1.5f);
            verticalAccel = PhysicsConfig.Clamp(verticalAccel, -8f, 4f);
            maxAimX = PhysicsConfig.Clamp(maxAimX, 0.1f, 0.74f);
            cameraShake = PhysicsConfig.Clamp(cameraShake, 0f, 1f);
            maxNetClearance = PhysicsConfig.Clamp(maxNetClearance, 0.06f, 2f);
            minTimeFactor = PhysicsConfig.Clamp(minTimeFactor, 0.4f, 1f);
        }
    }

    [Serializable]
    public class ShotTuning
    {
        public ShotTypeTuning normal = new ShotTypeTuning(4.2f, 0.85f, 0.05f, 0.08f, -1.2f, 0.58f, 0f, 0.24f);
        public ShotTypeTuning fast = new ShotTypeTuning(5.6f, 1.00f, 0.07f, 0.16f, -2.4f, 0.62f, 0.05f, 0.16f);
        public ShotTypeTuning smash = new ShotTypeTuning(7.2f, 1.05f, 0.09f, 0.26f, -3.6f, 0.66f, 0.35f, 0.12f);
        public ShotTypeTuning defensive = new ShotTypeTuning(3.1f, 1.10f, 0.05f, 0.06f, 0.4f, 0.50f, 0f, 0.6f);
        public ShotTypeTuning special = new ShotTypeTuning(6.6f, 1.05f, 0.03f, 0.05f, -2.4f, 0.66f, 0.5f, 0.15f);
        public ShotTypeTuning superSmash = new ShotTypeTuning(8.8f, 1.10f, 0.03f, 0.04f, -4.0f, 0.66f, 0.8f, 0.10f);
        public ShotTypeTuning serve = new ShotTypeTuning(3.8f, 0.75f, 0.04f, 0.0f, -0.8f, 0.55f, 0f, 0.22f);

        /// <summary>Racket speed toward the net (m/s) needed for each shot class.</summary>
        public float fastSwipeThreshold = 1.3f;
        public float smashSwipeThreshold = 2.8f;
        public float defensiveSwipeThreshold = -0.9f;
        /// <summary>Minimum ball height above the table for a smash to be allowed.</summary>
        public float smashMinHeight = 0.22f;
        /// <summary>Lateral racket speed (m/s) above which side spin is produced.</summary>
        public float spinSwipeThreshold = 1.1f;
        /// <summary>Lateral accel (m/s^2) produced per m/s of lateral swipe beyond the threshold.</summary>
        public float sideSpinPerSwipe = 1.1f;
        public float maxSideSpin = 3.2f;
        /// <summary>Spin magnitude at which a shot is labelled "Efecto".</summary>
        public float spinLabelThreshold = 1.4f;
        /// <summary>How much the contact point on the blade steers the ball (m of landing x per unit offset).</summary>
        public float contactSteer = 0.42f;
        /// <summary>How much the lateral swipe steers the ball (m of landing x per m/s).</summary>
        public float swipeSteer = 0.10f;
        public float fireSpeedMultiplier = 1.35f;
        public float curveSideAccel = 3.4f;
        public float assistNoiseMultiplier = 0.55f;

        public void Validate()
        {
            ShotTypeTuning[] all = { normal, fast, smash, defensive, special, superSmash, serve };
            for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].Validate();
            if (normal == null) normal = new ShotTypeTuning(4.2f, 0.85f, 0.05f, 0.08f, -1.2f, 0.58f, 0f, 0.24f);
            if (fast == null) fast = new ShotTypeTuning(5.6f, 1.00f, 0.07f, 0.16f, -2.4f, 0.62f, 0.05f, 0.16f);
            if (smash == null) smash = new ShotTypeTuning(7.2f, 1.05f, 0.09f, 0.26f, -3.6f, 0.66f, 0.35f, 0.12f);
            if (defensive == null) defensive = new ShotTypeTuning(3.1f, 1.10f, 0.05f, 0.06f, 0.4f, 0.50f, 0f, 0.6f);
            if (special == null) special = new ShotTypeTuning(6.6f, 1.05f, 0.03f, 0.05f, -2.4f, 0.66f, 0.5f, 0.15f);
            if (superSmash == null) superSmash = new ShotTypeTuning(8.8f, 1.10f, 0.03f, 0.04f, -4.0f, 0.66f, 0.8f, 0.10f);
            if (serve == null) serve = new ShotTypeTuning(3.8f, 0.75f, 0.04f, 0.0f, -0.8f, 0.55f, 0f, 0.22f);
            if (smashSwipeThreshold < fastSwipeThreshold + 0.2f) smashSwipeThreshold = fastSwipeThreshold + 0.2f;
            if (defensiveSwipeThreshold > -0.1f) defensiveSwipeThreshold = -0.1f;
            maxSideSpin = PhysicsConfig.Clamp(maxSideSpin, 0f, 8f);
            fireSpeedMultiplier = PhysicsConfig.Clamp(fireSpeedMultiplier, 1f, 2f);
            curveSideAccel = PhysicsConfig.Clamp(curveSideAccel, 0f, 8f);
            assistNoiseMultiplier = PhysicsConfig.Clamp(assistNoiseMultiplier, 0.1f, 1f);
        }
    }

    [Serializable]
    public class RacketTuning
    {
        /// <summary>Playable zone for player 1 (z &lt; 0). Player 2 mirrors it.</summary>
        public float zoneHalfWidth = 1.15f;
        public float zoneNearZ = 1.42f;   // closest distance from the net (just behind the end line at 1.37)
        public float zoneFarZ = 2.15f;
        public float defaultZ = 1.7f;
        public float baseReach = 0.17f;       // lateral half-width of the hit window (m)
        public float reachPerControl = 0.10f; // extra reach at control 10
        public float assistReach = 0.09f;
        public float baseFollowSpeed = 5.0f;  // m/s at response 0
        public float followSpeedPerResponse = 3.5f;
        public float followSharpness = 22f;   // exponential smoothing constant
        public float minHittableHeight = 0.12f; // above the table top
        public float maxHittableHeight = 1.25f;
        public float statPowerSpeedRange = 0.35f;   // +-17.5% speed between power 0..10
        public float statSpinRange = 0.8f;          // spin multiplier 0.6..1.4
        public float statPrecisionNoiseRange = 0.7f; // noise multiplier 1.35..0.65

        public void Validate()
        {
            zoneHalfWidth = PhysicsConfig.Clamp(zoneHalfWidth, 0.5f, 2f);
            zoneNearZ = PhysicsConfig.Clamp(zoneNearZ, 1.2f, 2f);
            zoneFarZ = PhysicsConfig.Clamp(zoneFarZ, zoneNearZ + 0.2f, 3.5f);
            defaultZ = PhysicsConfig.Clamp(defaultZ, zoneNearZ, zoneFarZ);
            baseReach = PhysicsConfig.Clamp(baseReach, 0.08f, 0.5f);
            baseFollowSpeed = PhysicsConfig.Clamp(baseFollowSpeed, 1f, 15f);
            followSharpness = PhysicsConfig.Clamp(followSharpness, 2f, 60f);
        }
    }

    [Serializable]
    public class PowerUpTuning
    {
        public float spawnIntervalLow = 18f;
        public float spawnIntervalNormal = 11f;
        public float spawnIntervalHigh = 6.5f;
        public float orbLifetime = 9f;
        public float orbPickupRadius = 0.13f;
        public float globalCooldown = 6f;          // per player after activating a power
        public float fireDuration = 6f;            // safety cap; normally ends when returned
        public float slowDuration = 5f;
        public float slowTimeScale = 0.55f;
        public float curveDuration = 8f;           // armed window
        public float superSmashDuration = 8f;      // armed window
        public float shieldDuration = 9f;
        public float freezeDuration = 2.6f;
        public float freezeSpeedMultiplier = 0.38f;
        public float freezeSwingMultiplier = 0.6f;
        public float specialEnergyPerHit = 7f;
        public float specialEnergyPerPointWon = 12f;
        public float specialEnergyPerPointLost = 16f; // comeback mechanic
        // Random-orb weights (fire, slow, curve, superSmash, shield, freeze, random)
        public float weightFire = 1f;
        public float weightSlow = 1f;
        public float weightCurve = 1f;
        public float weightSuperSmash = 0.8f;
        public float weightShield = 0.9f;
        public float weightFreeze = 0.8f;
        public float weightRandom = 1.2f;

        public void Validate()
        {
            spawnIntervalLow = PhysicsConfig.Clamp(spawnIntervalLow, 3f, 120f);
            spawnIntervalNormal = PhysicsConfig.Clamp(spawnIntervalNormal, 2f, 120f);
            spawnIntervalHigh = PhysicsConfig.Clamp(spawnIntervalHigh, 1.5f, 120f);
            orbLifetime = PhysicsConfig.Clamp(orbLifetime, 2f, 60f);
            orbPickupRadius = PhysicsConfig.Clamp(orbPickupRadius, 0.05f, 0.4f);
            globalCooldown = PhysicsConfig.Clamp(globalCooldown, 0f, 60f);
            fireDuration = PhysicsConfig.Clamp(fireDuration, 1f, 30f);
            slowDuration = PhysicsConfig.Clamp(slowDuration, 1f, 20f);
            slowTimeScale = PhysicsConfig.Clamp(slowTimeScale, 0.25f, 1f);
            curveDuration = PhysicsConfig.Clamp(curveDuration, 1f, 30f);
            superSmashDuration = PhysicsConfig.Clamp(superSmashDuration, 1f, 30f);
            shieldDuration = PhysicsConfig.Clamp(shieldDuration, 1f, 30f);
            freezeDuration = PhysicsConfig.Clamp(freezeDuration, 0.5f, 6f);
            freezeSpeedMultiplier = PhysicsConfig.Clamp(freezeSpeedMultiplier, 0.15f, 1f);
            freezeSwingMultiplier = PhysicsConfig.Clamp(freezeSwingMultiplier, 0.2f, 1f);
        }
    }

    [Serializable]
    public class AIDifficultyTuning
    {
        public float reactionTime = 0.2f;
        public float maxSpeed = 3f;
        public float positionError = 0.08f;   // sigma (m) of where it thinks the ball will be
        public float missChance = 0.08f;      // chance of a big misread per incoming ball
        public float aggression = 0.4f;       // likelihood of fast/smash shots
        public float spinUsage = 0.3f;
        public float powerUseChancePerSecond = 0.25f;
        public float shotNoiseMultiplier = 1.2f;

        public AIDifficultyTuning() { }
        public AIDifficultyTuning(float reaction, float speed, float posErr, float miss, float aggr, float spin, float power, float noise)
        {
            reactionTime = reaction; maxSpeed = speed; positionError = posErr; missChance = miss;
            aggression = aggr; spinUsage = spin; powerUseChancePerSecond = power; shotNoiseMultiplier = noise;
        }
    }

    [Serializable]
    public class AITuning
    {
        public AIDifficultyTuning easy = new AIDifficultyTuning(0.32f, 2.3f, 0.12f, 0.16f, 0.15f, 0.1f, 0.12f, 1.5f);
        public AIDifficultyTuning normal = new AIDifficultyTuning(0.2f, 3.2f, 0.07f, 0.08f, 0.4f, 0.3f, 0.25f, 1.15f);
        public AIDifficultyTuning hard = new AIDifficultyTuning(0.11f, 4.3f, 0.05f, 0.05f, 0.65f, 0.5f, 0.45f, 0.9f);

        public void Validate()
        {
            if (easy == null) easy = new AIDifficultyTuning(0.32f, 2.3f, 0.12f, 0.16f, 0.15f, 0.1f, 0.12f, 1.5f);
            if (normal == null) normal = new AIDifficultyTuning(0.2f, 3.2f, 0.07f, 0.08f, 0.4f, 0.3f, 0.25f, 1.15f);
            if (hard == null) hard = new AIDifficultyTuning(0.11f, 4.3f, 0.05f, 0.05f, 0.65f, 0.5f, 0.45f, 0.9f);
        }
    }

    [Serializable]
    public class EconomyTuning
    {
        public int startingCoins = 300;
        public int winVsEasy = 60;
        public int winVsNormal = 100;
        public int winVsHard = 160;
        public int lossConsolation = 15;
        public int localMatchPlayed = 40;
        public int tournamentRoundWin = 80;
        public int tournamentChampion = 600;
        public int trainingStar = 25;          // per star, first time only
        public int trainingRepeat = 10;
        public int maxCoins = 999999;

        public void Validate()
        {
            if (startingCoins < 0) startingCoins = 0;
            if (maxCoins < 1000) maxCoins = 1000;
        }
    }

    [Serializable]
    public class MatchTuning
    {
        public float pointPauseSeconds = 1.3f;
        public float serveAutoTimeoutAI = 1.1f;
        public float outOfBoundsX = 4f;
        public float outOfBoundsZ = 5f;
        public float deadBallHeight = 0.35f; // a missed ball below this height ends the rally
    }
}
