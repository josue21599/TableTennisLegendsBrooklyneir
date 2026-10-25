using System;
using Brooklyneir.Config;
using Brooklyneir.Core;

namespace Brooklyneir.PowerUps
{
    public enum PowerUpType { None = -1, Fire = 0, Slow = 1, Curve = 2, SuperSmash = 3, Shield = 4, Freeze = 5, Random = 6 }

    public enum PowerActivationResult { Activated, NothingHeld, OnCooldown, Blocked }

    /// <summary>Per-player power state. Every timed effect has a hard duration cap.</summary>
    public class PlayerPowerState
    {
        public PowerUpType Held = PowerUpType.None;
        public float Cooldown;
        /// <summary>Fire is armed (next own shot) or burning (the ball in play is on fire).</summary>
        public bool FireActive;
        public bool FireBurning;
        public float FireTimer;
        public float SlowTimer;
        public float CurveArmedTimer;
        public float SuperSmashArmedTimer;
        public float ShieldTimer;
        public float FrozenTimer;
        public float SpecialEnergy;
        public bool SpecialArmed;

        public bool SlowActive { get { return SlowTimer > 0f; } }
        public bool CurveArmed { get { return CurveArmedTimer > 0f; } }
        public bool SuperSmashArmed { get { return SuperSmashArmedTimer > 0f; } }
        public bool ShieldActive { get { return ShieldTimer > 0f; } }
        public bool Frozen { get { return FrozenTimer > 0f; } }
        public bool SpecialReady { get { return SpecialEnergy >= 100f; } }

        public void Clear()
        {
            Held = PowerUpType.None;
            Cooldown = 0f;
            FireActive = false;
            FireBurning = false;
            FireTimer = SlowTimer = CurveArmedTimer = SuperSmashArmedTimer = ShieldTimer = FrozenTimer = 0f;
            SpecialEnergy = 0f;
            SpecialArmed = false;
        }
    }

    /// <summary>
    /// Pure power-up rules for both players: collection, activation, durations, cooldowns,
    /// anti-stacking (one held power, global cooldown, no re-activation of a running effect) and
    /// cleanup. The match controller asks it for multipliers; the views listen to its events.
    /// </summary>
    public class PowerUpSystem
    {
        readonly PowerUpTuning _t;
        readonly IRandomSource _rng;
        public readonly PlayerPowerState[] Players = { new PlayerPowerState(), new PlayerPowerState() };

        public event Action<int, PowerUpType> Collected;          // player, resolved type
        public event Action<int, PowerUpType> Activated;          // player, type
        public event Action<int, PowerUpType> Ended;              // player owning the effect, type
        public event Action<int> FreezeBlocked;                   // player whose shield blocked it
        public event Action<int> SpecialReady;

        public static readonly PowerUpType[] Concrete =
            { PowerUpType.Fire, PowerUpType.Slow, PowerUpType.Curve, PowerUpType.SuperSmash, PowerUpType.Shield, PowerUpType.Freeze };

        public PowerUpSystem(PowerUpTuning tuning, IRandomSource rng)
        {
            _t = tuning;
            _rng = rng;
        }

        public PowerUpTuning Tuning { get { return _t; } }

        public void ResetAll()
        {
            Players[0].Clear();
            Players[1].Clear();
        }

        /// <summary>Clears effects that must not survive the end of a rally.</summary>
        public void OnRallyEnded()
        {
            for (int p = 0; p < 2; p++)
            {
                PlayerPowerState s = Players[p];
                if (s.FireActive) EndFire(p);
                if (s.SlowTimer > 0f) { s.SlowTimer = 0f; Raise(Ended, p, PowerUpType.Slow); }
                if (s.FrozenTimer > 0f) { s.FrozenTimer = 0f; Raise(Ended, 1 - p, PowerUpType.Freeze); }
            }
        }

        public void Update(float dt)
        {
            for (int p = 0; p < 2; p++)
            {
                PlayerPowerState s = Players[p];
                if (s.Cooldown > 0f) s.Cooldown = Math.Max(0f, s.Cooldown - dt);
                if (s.FireActive)
                {
                    s.FireTimer -= dt;
                    if (s.FireTimer <= 0f) EndFire(p);
                }
                Tick(ref s.SlowTimer, dt, p, PowerUpType.Slow);
                Tick(ref s.CurveArmedTimer, dt, p, PowerUpType.Curve);
                Tick(ref s.SuperSmashArmedTimer, dt, p, PowerUpType.SuperSmash);
                Tick(ref s.ShieldTimer, dt, p, PowerUpType.Shield);
                if (s.FrozenTimer > 0f)
                {
                    s.FrozenTimer -= dt;
                    if (s.FrozenTimer <= 0f) { s.FrozenTimer = 0f; Raise(Ended, 1 - p, PowerUpType.Freeze); }
                }
            }
        }

        void Tick(ref float timer, float dt, int player, PowerUpType type)
        {
            if (timer <= 0f) return;
            timer -= dt;
            if (timer <= 0f)
            {
                timer = 0f;
                Raise(Ended, player, type);
            }
        }

        public bool CanCollect(int player)
        {
            return player >= 0 && player < 2 && Players[player].Held == PowerUpType.None;
        }

        /// <summary>Gives an orb to a player. Random orbs resolve to a concrete, implemented power.</summary>
        public PowerUpType Collect(int player, PowerUpType orbType)
        {
            if (!CanCollect(player)) return PowerUpType.None;
            PowerUpType resolved = orbType == PowerUpType.Random ? RollConcrete() : orbType;
            if (resolved == PowerUpType.None || resolved == PowerUpType.Random) resolved = PowerUpType.Fire;
            Players[player].Held = resolved;
            Raise(Collected, player, resolved);
            return resolved;
        }

        public PowerUpType RollConcrete()
        {
            float[] w = { _t.weightFire, _t.weightSlow, _t.weightCurve, _t.weightSuperSmash, _t.weightShield, _t.weightFreeze };
            float total = 0f;
            for (int i = 0; i < w.Length; i++) total += Math.Max(0f, w[i]);
            if (total <= 0f) return PowerUpType.Fire;
            float roll = _rng.Value() * total;
            for (int i = 0; i < w.Length; i++)
            {
                roll -= Math.Max(0f, w[i]);
                if (roll < 0f) return Concrete[i];
            }
            return Concrete[Concrete.Length - 1];
        }

        /// <summary>Picks the type of the next orb on the table using the configured weights.</summary>
        public PowerUpType RollOrbType()
        {
            float[] w = { _t.weightFire, _t.weightSlow, _t.weightCurve, _t.weightSuperSmash, _t.weightShield, _t.weightFreeze, _t.weightRandom };
            float total = 0f;
            for (int i = 0; i < w.Length; i++) total += Math.Max(0f, w[i]);
            if (total <= 0f) return PowerUpType.Random;
            float roll = _rng.Value() * total;
            for (int i = 0; i < w.Length; i++)
            {
                roll -= Math.Max(0f, w[i]);
                if (roll < 0f) return (PowerUpType)i;
            }
            return PowerUpType.Random;
        }

        public PowerActivationResult TryActivate(int player)
        {
            PlayerPowerState s = Players[player];
            PlayerPowerState o = Players[1 - player];
            if (s.Held == PowerUpType.None) return PowerActivationResult.NothingHeld;
            if (s.Cooldown > 0f) return PowerActivationResult.OnCooldown;
            PowerUpType type = s.Held;
            // Anti-stacking: a running effect of the same kind cannot be refreshed.
            if ((type == PowerUpType.Fire && s.FireActive) || (type == PowerUpType.Slow && s.SlowActive) ||
                (type == PowerUpType.Curve && s.CurveArmed) || (type == PowerUpType.SuperSmash && s.SuperSmashArmed) ||
                (type == PowerUpType.Shield && s.ShieldActive) || (type == PowerUpType.Freeze && o.Frozen))
                return PowerActivationResult.Blocked;

            s.Held = PowerUpType.None;
            s.Cooldown = _t.globalCooldown;
            switch (type)
            {
                case PowerUpType.Fire:
                    s.FireActive = true;
                    s.FireTimer = _t.fireDuration;
                    break;
                case PowerUpType.Slow:
                    s.SlowTimer = _t.slowDuration;
                    break;
                case PowerUpType.Curve:
                    s.CurveArmedTimer = _t.curveDuration;
                    break;
                case PowerUpType.SuperSmash:
                    s.SuperSmashArmedTimer = _t.superSmashDuration;
                    break;
                case PowerUpType.Shield:
                    s.ShieldTimer = _t.shieldDuration;
                    break;
                case PowerUpType.Freeze:
                    if (o.ShieldActive)
                    {
                        // Counter-play: an active shield absorbs the freeze (the shield stays up).
                        Raise(Activated, player, type);
                        if (FreezeBlocked != null) FreezeBlocked(1 - player);
                        return PowerActivationResult.Activated;
                    }
                    o.FrozenTimer = _t.freezeDuration;
                    break;
            }
            Raise(Activated, player, type);
            return PowerActivationResult.Activated;
        }

        /// <summary>Called right after <paramref name="hitter"/> hits the ball.</summary>
        public void OnShot(int hitter, bool consumedCurve, bool consumedSuperSmash, bool consumedSpecial)
        {
            PlayerPowerState s = Players[hitter];
            if (consumedCurve && s.CurveArmedTimer > 0f) { s.CurveArmedTimer = 0f; Raise(Ended, hitter, PowerUpType.Curve); }
            if (consumedSuperSmash && s.SuperSmashArmedTimer > 0f) { s.SuperSmashArmedTimer = 0f; Raise(Ended, hitter, PowerUpType.SuperSmash); }
            if (consumedSpecial) { s.SpecialArmed = false; s.SpecialEnergy = 0f; }
            // Returning an opponent's fire ball puts the fire out.
            if (Players[1 - hitter].FireBurning) EndFire(1 - hitter);
            // The hitter's armed fire is now on the ball.
            if (s.FireActive && !s.FireBurning) s.FireBurning = true;
            AddEnergy(hitter, _t.specialEnergyPerHit);
        }

        /// <summary>Uses up the shield (it saved a point).</summary>
        public void ConsumeShield(int player)
        {
            if (Players[player].ShieldTimer > 0f)
            {
                Players[player].ShieldTimer = 0f;
                Raise(Ended, player, PowerUpType.Shield);
            }
        }

        public void OnPoint(int winner)
        {
            AddEnergy(winner, _t.specialEnergyPerPointWon);
            AddEnergy(1 - winner, _t.specialEnergyPerPointLost);
        }

        public void AddEnergy(int player, float amount)
        {
            PlayerPowerState s = Players[player];
            bool wasReady = s.SpecialReady;
            s.SpecialEnergy = Math.Min(100f, Math.Max(0f, s.SpecialEnergy + amount));
            if (!wasReady && s.SpecialReady && SpecialReady != null) SpecialReady(player);
        }

        public bool TryArmSpecial(int player)
        {
            PlayerPowerState s = Players[player];
            if (!s.SpecialReady || s.SpecialArmed) return false;
            s.SpecialArmed = true;
            return true;
        }

        // ---- Queries used by the match controller ----

        /// <summary>Ball time scale: Slow makes the ball travel slower while it flies towards its owner.</summary>
        public float BallTimeScale(int ballHeadingToPlayer)
        {
            if (ballHeadingToPlayer < 0 || ballHeadingToPlayer > 1) return 1f;
            return Players[ballHeadingToPlayer].SlowActive ? _t.slowTimeScale : 1f;
        }

        public float FollowSpeedMultiplier(int player)
        {
            return Players[player].Frozen ? _t.freezeSpeedMultiplier : 1f;
        }

        public float SwingMultiplier(int player)
        {
            return Players[player].Frozen ? _t.freezeSwingMultiplier : 1f;
        }

        public bool AnyEffectActive()
        {
            for (int p = 0; p < 2; p++)
            {
                PlayerPowerState s = Players[p];
                if (s.FireActive || s.SlowActive || s.CurveArmed || s.SuperSmashArmed || s.ShieldActive || s.Frozen) return true;
            }
            return false;
        }

        void EndFire(int player)
        {
            PlayerPowerState s = Players[player];
            if (!s.FireActive) return;
            s.FireActive = false;
            s.FireBurning = false;
            s.FireTimer = 0f;
            Raise(Ended, player, PowerUpType.Fire);
        }

        void Raise(Action<int, PowerUpType> ev, int player, PowerUpType type)
        {
            if (ev != null) ev(player, type);
        }

        public static string DisplayName(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Fire: return "PELOTA DE FUEGO";
                case PowerUpType.Slow: return "RALENTIZACIÓN";
                case PowerUpType.Curve: return "EFECTO CURVO";
                case PowerUpType.SuperSmash: return "SÚPER REMATE";
                case PowerUpType.Shield: return "ESCUDO";
                case PowerUpType.Freeze: return "CONGELACIÓN";
                case PowerUpType.Random: return "PODER ALEATORIO";
                default: return "";
            }
        }

        public static string HowTo(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Fire: return "Tu próximo golpe sale ardiendo y más rápido. Se apaga cuando el rival lo devuelve.";
                case PowerUpType.Slow: return "La pelota va más lenta mientras viaja hacia ti.";
                case PowerUpType.Curve: return "Tu próximo golpe hace una curva cerrada hacia el objetivo.";
                case PowerUpType.SuperSmash: return "Tu próximo golpe es un remate imparable... casi.";
                case PowerUpType.Shield: return "Un muro te salva una vez de perder el punto. También bloquea la congelación.";
                case PowerUpType.Freeze: return "El rival se mueve y golpea más lento durante unos segundos.";
                case PowerUpType.Random: return "Te da uno de los otros seis poderes al recogerlo.";
                default: return "";
            }
        }
    }
}
