using Brooklyneir.Config;
using Brooklyneir.Core;
using Brooklyneir.PowerUps;
using NUnit.Framework;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class PowerUpTests
    {
        PowerUpTuning _t;
        PowerUpSystem _sys;

        [SetUp]
        public void Setup()
        {
            _t = new PowerUpTuning();
            _t.Validate();
            _sys = new PowerUpSystem(_t, new GameRandom(42));
        }

        void Run(float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) _sys.Update(0.05f);
        }

        [Test]
        public void OnlyOneHeldPowerAtATime()
        {
            Assert.AreEqual(PowerUpType.Slow, _sys.Collect(0, PowerUpType.Slow));
            Assert.AreEqual(PowerUpType.None, _sys.Collect(0, PowerUpType.Fire));
            Assert.AreEqual(PowerUpType.Slow, _sys.Players[0].Held);
        }

        [Test]
        public void RandomOrbResolvesToImplementedPower()
        {
            for (int i = 0; i < 200; i++)
            {
                PowerUpSystem s = new PowerUpSystem(_t, new GameRandom(i));
                PowerUpType got = s.Collect(1, PowerUpType.Random);
                Assert.AreNotEqual(PowerUpType.Random, got);
                Assert.AreNotEqual(PowerUpType.None, got);
                Assert.IsTrue(System.Array.IndexOf(PowerUpSystem.Concrete, got) >= 0);
            }
        }

        [Test]
        public void TimedEffectsEndAndRaiseEvent()
        {
            int ended = 0;
            _sys.Ended += (p, type) => { if (type == PowerUpType.Slow) ended++; };
            _sys.Collect(0, PowerUpType.Slow);
            Assert.AreEqual(PowerActivationResult.Activated, _sys.TryActivate(0));
            Assert.IsTrue(_sys.Players[0].SlowActive);
            Assert.AreEqual(_t.slowTimeScale, _sys.BallTimeScale(0), 0.0001f);
            Assert.AreEqual(1f, _sys.BallTimeScale(1), 0.0001f, "slow only affects the ball heading to its owner");
            Run(_t.slowDuration + 0.2f);
            Assert.IsFalse(_sys.Players[0].SlowActive);
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void CooldownPreventsChainingPowers()
        {
            _sys.Collect(0, PowerUpType.Shield);
            _sys.TryActivate(0);
            _sys.Collect(0, PowerUpType.Freeze);
            Assert.AreEqual(PowerActivationResult.OnCooldown, _sys.TryActivate(0));
            Run(_t.globalCooldown + 0.1f);
            Assert.AreEqual(PowerActivationResult.Activated, _sys.TryActivate(0));
        }

        [Test]
        public void FreezeSlowsOpponentTemporarily()
        {
            _sys.Collect(0, PowerUpType.Freeze);
            _sys.TryActivate(0);
            Assert.IsTrue(_sys.Players[1].Frozen);
            Assert.Less(_sys.FollowSpeedMultiplier(1), 1f);
            Assert.Greater(_sys.FollowSpeedMultiplier(1), 0f, "never a full lock");
            Assert.AreEqual(1f, _sys.FollowSpeedMultiplier(0), 0.0001f);
            Run(_t.freezeDuration + 0.1f);
            Assert.IsFalse(_sys.Players[1].Frozen);
        }

        [Test]
        public void ShieldBlocksFreeze()
        {
            bool blocked = false;
            _sys.FreezeBlocked += p => blocked = true;
            _sys.Collect(1, PowerUpType.Shield);
            _sys.TryActivate(1);
            _sys.Collect(0, PowerUpType.Freeze);
            _sys.TryActivate(0);
            Assert.IsTrue(blocked);
            Assert.IsFalse(_sys.Players[1].Frozen);
            Assert.IsTrue(_sys.Players[1].ShieldActive);
        }

        [Test]
        public void FireBurnsOnOwnersShotAndEndsWhenReturned()
        {
            _sys.Collect(0, PowerUpType.Fire);
            _sys.TryActivate(0);
            Assert.IsTrue(_sys.Players[0].FireActive);
            Assert.IsFalse(_sys.Players[0].FireBurning);
            _sys.OnShot(0, false, false, false);
            Assert.IsTrue(_sys.Players[0].FireBurning);
            _sys.OnShot(1, false, false, false);
            Assert.IsFalse(_sys.Players[0].FireActive, "returning the fire ball puts it out");
        }

        [Test]
        public void ArmedShotsAreConsumed()
        {
            _sys.Collect(0, PowerUpType.SuperSmash);
            _sys.TryActivate(0);
            Assert.IsTrue(_sys.Players[0].SuperSmashArmed);
            _sys.OnShot(0, false, true, false);
            Assert.IsFalse(_sys.Players[0].SuperSmashArmed);
        }

        [Test]
        public void SameEffectCannotStack()
        {
            _sys.Collect(0, PowerUpType.Shield);
            _sys.TryActivate(0);
            Run(_t.globalCooldown + 0.1f);
            if (_sys.Players[0].ShieldActive)
            {
                _sys.Collect(0, PowerUpType.Shield);
                Assert.AreEqual(PowerActivationResult.Blocked, _sys.TryActivate(0));
                Assert.AreEqual(PowerUpType.Shield, _sys.Players[0].Held, "a blocked power is kept");
            }
        }

        [Test]
        public void RallyEndAndResetClearEverything()
        {
            _sys.Collect(0, PowerUpType.Fire);
            _sys.TryActivate(0);
            _sys.Collect(1, PowerUpType.Freeze);
            _sys.TryActivate(1);
            _sys.OnRallyEnded();
            Assert.IsFalse(_sys.Players[0].FireActive);
            Assert.IsFalse(_sys.Players[0].Frozen);
            _sys.Collect(0, PowerUpType.Shield);
            _sys.ResetAll();
            Assert.IsFalse(_sys.AnyEffectActive());
            Assert.AreEqual(PowerUpType.None, _sys.Players[0].Held);
        }

        [Test]
        public void SpecialEnergyFillsAndArms()
        {
            int ready = 0;
            _sys.SpecialReady += p => ready++;
            for (int i = 0; i < 30; i++) _sys.OnShot(0, false, false, false);
            Assert.IsTrue(_sys.Players[0].SpecialReady);
            Assert.AreEqual(1, ready);
            Assert.IsTrue(_sys.TryArmSpecial(0));
            _sys.OnShot(0, false, false, true);
            Assert.IsFalse(_sys.Players[0].SpecialArmed);
            Assert.Less(_sys.Players[0].SpecialEnergy, 100f);
        }
    }
}
