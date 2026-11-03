using System;
using Brooklyneir.Gameplay;
using Brooklyneir.Net;
using Brooklyneir.PowerUps;
using NUnit.Framework;
using UnityEngine;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class NetProtocolTests
    {
        readonly byte[] _buf = new byte[NetProtocol.MaxPacket];
        NetWriter _w;
        readonly NetReader _r = new NetReader();

        [SetUp]
        public void Setup() { _w = new NetWriter(_buf); }

        NetReader Read() { _r.Reset(_buf, _w.Length); return _r; }

        static NetBallState Ball(float k)
        {
            NetBallState b = new NetBallState();
            b.Position = new Vector3(-0.7f * k, 0.93f, 1.37f); b.Velocity = new Vector3(1.5f, 2.25f, -9.75f * k); b.Spin = new Vector3(-3.5f, 1e-7f, 0f); b.Active = true;
            return b;
        }

        static void SameBall(NetBallState a, NetBallState b)
        {
            Assert.IsTrue(a.Position == b.Position && a.Velocity == b.Velocity && a.Spin == b.Spin && a.Active == b.Active, "ball state must be bit-exact");
        }

        /// <summary>One sample of every message, written into the writer; returns its type.</summary>
        MsgType WriteSample(int i)
        {
            switch (i)
            {
                case 0: NetProtocol.Write(_w, new HelloMsg { Protocol = NetProtocol.Version, AppVersion = "1.4.2", BalanceHash = 0xDEADBEEF, Name = "Josué", RacketId = "neon_pulse", SessionCode = uint.MaxValue }); return MsgType.Hello;
                case 1: NetProtocol.Write(_w, new WelcomeMsg { SessionCode = 123456789, HostTick = uint.MaxValue - 1, PlayerIndex = 1 }); return MsgType.Welcome;
                case 2: NetProtocol.Write(_w, new RejectMsg { Reason = RejectReason.BalanceMismatch, Text = "Versión distinta" }); return MsgType.Reject;
                case 3:
                {
                    MatchConfig c = new MatchConfig { mode = GameMode.Custom, pointsToWin = 11, bestOfSets = 3, winByTwo = false, timeLimitMinutes = 2.5f, powerUpsEnabled = true, powerFrequency = PowerFrequency.High, tableId = "rooftop", racketP1 = "iron_wall", racketP2 = "rooftop_viper" };
                    NetProtocol.Write(_w, MatchSetupMsg.From(c, 1, 98765, "Josué", "J2")); return MsgType.MatchSetup;
                }
                case 4: NetProtocol.Write(_w, new PingMsg { T0 = 12345.678901234 }); return MsgType.Ping;
                case 5: NetProtocol.Write(_w, new PongMsg { T0 = 1e-9, T1 = 9e12 }); return MsgType.Pong;
                case 6: NetProtocol.Write(_w, new PaddleMsg { Seq = uint.MaxValue, Tick = 0, PosX = -0.62f, PosZ = -1.95f, TargetX = float.MaxValue, TargetZ = float.MinValue }); return MsgType.Paddle;
                case 7: NetProtocol.Write(_w, MsgType.Serve, new BallLaunchMsg { Tick = 240, Player = 0, Ball = Ball(1f), Shot = ShotType.Serve, Strength = 0.3f }); return MsgType.Serve;
                case 8: NetProtocol.Write(_w, new HitBeginMsg { Tick = 777, Player = 1, Contact = new Vector3(0.1f, 0.95f, 1.9f) }); return MsgType.HitBegin;
                case 9: NetProtocol.Write(_w, MsgType.HitLaunch, new BallLaunchMsg { Tick = 778, Player = 1, Ball = Ball(-1f), Shot = ShotType.Smash, Strength = 0.9f }); return MsgType.HitLaunch;
                case 10: NetProtocol.Write(_w, new PowerRequestMsg { Player = 1, Action = PowerAction.ArmSpecial }); return MsgType.PowerRequest;
                case 11:
                {
                    PlayerPowerState s = new PlayerPowerState { Held = PowerUpType.Shield, Cooldown = 1.25f, FireActive = true, FireBurning = false, SlowTimer = 2.5f, ShieldTimer = 4f, FrozenTimer = 0.5f, SpecialEnergy = 100f, SpecialArmed = true };
                    NetProtocol.Write(_w, new PowerStateMsg { Tick = 1000, P0 = NetPowerPlayer.From(s), P1 = NetPowerPlayer.From(new PlayerPowerState()), HasBall = true, Ball = Ball(0.5f) }); return MsgType.PowerState;
                }
                case 12: NetProtocol.Write(_w, new ShieldSaveMsg { Tick = 1500, Player = 0, Ball = Ball(2f) }); return MsgType.ShieldSave;
                case 13: NetProtocol.Write(_w, new PointResultMsg { Winner = 1, Reason = PointReason.OutLong, Points0 = 300, Points1 = 298, Sets0 = 2, Sets1 = 1, NextServer = 0, MatchPoint1 = true, SetWon = true, TimeUp = true, Clock = 119.5f }); return MsgType.PointResult;
                case 14: NetProtocol.Write(_w, new PauseMsg { Player = 1, Reason = PauseReason.Background, Tick = 5000, Ball = Ball(1f) }); return MsgType.Pause;
                case 15: NetProtocol.Write(_w, new ResumeAtMsg { Tick = 5720, Ball = Ball(1f) }); return MsgType.ResumeAt;
                case 16: NetProtocol.Write(_w, new ByeMsg { Reason = ByeReason.Quit }); return MsgType.Bye;
                default: NetProtocol.Write(_w, new OrbStateMsg { Tick = 42, Active = true, Position = new Vector3(0.3f, 1.1f, -0.2f), Type = (sbyte)PowerUpType.Random }); return MsgType.OrbState;
            }
        }

        const int Samples = 18;

        /// <summary>Reads the current packet as the given type; returns false if it does not parse.</summary>
        bool ReadAny(MsgType t)
        {
            NetReader r = Read();
            switch (t)
            {
                case MsgType.Hello: { HelloMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.Welcome: { WelcomeMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.Reject: { RejectMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.MatchSetup: { MatchSetupMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.Ping: { PingMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.Pong: { PongMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.Paddle: { PaddleMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.Serve: case MsgType.HitLaunch: { BallLaunchMsg m; return NetProtocol.TryRead(r, t, out m); }
                case MsgType.HitBegin: { HitBeginMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.PowerRequest: { PowerRequestMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.PowerState: { PowerStateMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.ShieldSave: { ShieldSaveMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.PointResult: { PointResultMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.Pause: { PauseMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.ResumeAt: { ResumeAtMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.Bye: { ByeMsg m; return NetProtocol.TryRead(r, out m); }
                case MsgType.OrbState: { OrbStateMsg m; return NetProtocol.TryRead(r, out m); }
                default: return false;
            }
        }

        [Test]
        public void EveryMessageRoundTripsExactly()
        {
            for (int i = 0; i < Samples; i++)
            {
                MsgType t = WriteSample(i);
                Assert.IsFalse(_w.Overflow, t + " overflow");
                Assert.AreEqual(t, NetProtocol.PeekType(_buf, _w.Length), t + " type byte");
                Assert.IsTrue(ReadAny(t), t + " must read back");
            }
            // Field by field on the messages that carry gameplay state.
            HelloMsg h; WriteSample(0); Assert.IsTrue(NetProtocol.TryRead(Read(), out h));
            Assert.AreEqual("Josué", h.Name); Assert.AreEqual("1.4.2", h.AppVersion); Assert.AreEqual(0xDEADBEEFu, h.BalanceHash); Assert.AreEqual(uint.MaxValue, h.SessionCode);
            MatchSetupMsg ms; WriteSample(3); Assert.IsTrue(NetProtocol.TryRead(Read(), out ms));
            MatchConfig c = ms.ToConfig();
            Assert.IsTrue(c.mode == GameMode.Custom && c.pointsToWin == 11 && c.bestOfSets == 3 && !c.winByTwo && c.timeLimitMinutes == 2.5f && c.powerFrequency == PowerFrequency.High);
            Assert.IsTrue(c.tableId == "rooftop" && c.racketP1 == "iron_wall" && c.racketP2 == "rooftop_viper" && ms.FirstServer == 1 && ms.StartTick == 98765);
            PaddleMsg p; WriteSample(6); Assert.IsTrue(NetProtocol.TryRead(Read(), out p));
            Assert.IsTrue(p.Seq == uint.MaxValue && p.PosX == -0.62f && p.TargetX == float.MaxValue && p.TargetZ == float.MinValue);
            BallLaunchMsg hit; WriteSample(9); Assert.IsTrue(NetProtocol.TryRead(Read(), MsgType.HitLaunch, out hit));
            SameBall(Ball(-1f), hit.Ball); Assert.IsTrue(hit.Tick == 778 && hit.Player == 1 && hit.Shot == ShotType.Smash && hit.Strength == 0.9f);
            PowerStateMsg ps; WriteSample(11); Assert.IsTrue(NetProtocol.TryRead(Read(), out ps));
            PlayerPowerState back = new PlayerPowerState();
            ps.P0.ApplyTo(back);
            Assert.IsTrue(back.Held == PowerUpType.Shield && back.Cooldown == 1.25f && back.FireActive && !back.FireBurning && back.SlowTimer == 2.5f && back.ShieldTimer == 4f && back.FrozenTimer == 0.5f && back.SpecialEnergy == 100f && back.SpecialArmed);
            Assert.IsTrue(ps.HasBall); SameBall(Ball(0.5f), ps.Ball);
            PointResultMsg pr; WriteSample(13); Assert.IsTrue(NetProtocol.TryRead(Read(), out pr));
            Assert.IsTrue(pr.Winner == 1 && pr.Reason == PointReason.OutLong && pr.Points0 == 300 && pr.Points1 == 298 && pr.Sets0 == 2 && pr.Sets1 == 1);
            Assert.IsTrue(!pr.MatchPoint0 && pr.MatchPoint1 && pr.SetWon && !pr.MatchWon && pr.TimeUp && pr.Clock == 119.5f);
            PongMsg pong; WriteSample(5); Assert.IsTrue(NetProtocol.TryRead(Read(), out pong));
            Assert.IsTrue(pong.T0 == 1e-9 && pong.T1 == 9e12, "doubles are bit-exact");
        }

        [Test]
        public void TruncatedGarbageOrWrongPacketsAreRejectedWithoutExceptions()
        {
            for (int i = 0; i < Samples; i++)
            {
                MsgType t = WriteSample(i);
                int full = _w.Length;
                for (int cut = 0; cut < full; cut++)
                {
                    _r.Reset(_buf, cut);
                    Assert.IsFalse(ReadTruncated(t, cut), t + " truncated to " + cut + " bytes must be rejected");
                }
                // One extra byte at the end: rejected too (it is not that message).
                WriteSample(i);
                _buf[_w.Length] = 0x55;
                _r.Reset(_buf, _w.Length + 1);
                Assert.IsFalse(ReadTruncated(t, _w.Length + 1), t + " with a trailing byte must be rejected");
            }
            // Reading a packet as another type fails cleanly.
            WriteSample(6);
            HitBeginMsg wrong;
            Assert.IsFalse(NetProtocol.TryRead(Read(), out wrong));
            // Random garbage never throws.
            System.Random rnd = new System.Random(7);
            for (int n = 0; n < 3000; n++)
            {
                int len = rnd.Next(0, 80);
                for (int k = 0; k < len; k++) _buf[k] = (byte)rnd.Next(256);
                _w.Reset();
                for (int k = 0; k < len; k++) _w.U8(_buf[k]);
                MsgType t = NetProtocol.PeekType(_buf, len);
                Assert.DoesNotThrow(() => ReadAny(t == MsgType.None ? MsgType.Hello : t));
            }
            Assert.AreEqual(MsgType.None, NetProtocol.PeekType(_buf, 0));
            _buf[0] = 200;
            Assert.AreEqual(MsgType.None, NetProtocol.PeekType(_buf, 1));
            // Out-of-range fields are rejected: player 2, a LAN setup for a mode without LAN.
            _w.Reset(); _w.U8((byte)MsgType.HitBegin); _w.U32(1); _w.U8(2); _w.Vec3(Vector3.zero);
            Assert.IsFalse(ReadAny(MsgType.HitBegin), "player index 2 does not exist");
            NetProtocol.Write(_w, MatchSetupMsg.From(new MatchConfig { mode = GameMode.Tournament }, 0, 0, "a", "b"));
            Assert.IsFalse(ReadAny(MsgType.MatchSetup), "tournament cannot be played over LAN");
        }

        bool ReadTruncated(MsgType t, int len)
        {
            // ReadAny resets the reader to the writer length, so read here with the given length.
            _w.Reset();
            for (int k = 0; k < len; k++) _w.U8(_buf[k]);
            bool ok = ReadAny(t);
            return ok;
        }

        [Test]
        public void ADifferentProtocolVersionIsDetectedBeforeReadingTheRest()
        {
            _w.Reset(); _w.U8((byte)MsgType.Hello); _w.U16((ushort)(NetProtocol.Version + 1)); _w.U8(200); // rest in an unknown layout
            HelloMsg h;
            Assert.IsTrue(NetProtocol.TryRead(Read(), out h), "the version itself is readable");
            Assert.AreEqual(NetProtocol.Version + 1, h.Protocol, "so the host can answer Reject(ProtocolMismatch)");
            Assert.IsNull(h.Name, "nothing else is trusted from another version");
            RejectMsg rej;
            NetProtocol.Write(_w, new RejectMsg { Reason = RejectReason.ProtocolMismatch, Text = "Actualizad los dos móviles" });
            Assert.IsTrue(NetProtocol.TryRead(Read(), out rej));
            Assert.AreEqual(RejectReason.ProtocolMismatch, rej.Reason);
        }

        [Test]
        public void TextsAreCutToTheirMaximumWithoutBreakingCharacters()
        {
            string[] names = { "Abcdefghijklmnopqrstuvwxyz", "ÁÉÍÓÚáéíóúñÑüÜ¡¿", "J🏓🏓🏓🏓🏓", "", null };
            for (int i = 0; i < names.Length; i++)
            {
                NetProtocol.Write(_w, new HelloMsg { Protocol = NetProtocol.Version, AppVersion = "1", Name = names[i], RacketId = "x" });
                HelloMsg h;
                Assert.IsTrue(NetProtocol.TryRead(Read(), out h), "name " + i);
                Assert.LessOrEqual(System.Text.Encoding.UTF8.GetByteCount(h.Name), NetProtocol.MaxName);
                string src = names[i] ?? "";
                Assert.IsTrue(src.StartsWith(h.Name, StringComparison.Ordinal), "the cut keeps a prefix of whole characters: '" + h.Name + "'");
                Assert.IsFalse(h.Name.Contains("�"), "no broken character");
            }
            NetProtocol.Write(_w, new HelloMsg { Protocol = NetProtocol.Version, AppVersion = "1", Name = "ÁÉÍÓÚáéíóúñÑüÜ¡¿", RacketId = "x" });
            HelloMsg cut;
            NetProtocol.TryRead(Read(), out cut);
            Assert.AreEqual("ÁÉÍÓÚáéí", cut.Name, "8 two-byte letters = 16 bytes");
        }

        [Test]
        public void WritingPacketsDoesNotAllocate()
        {
            PaddleMsg p = new PaddleMsg { Seq = 1, Tick = 2, PosX = 0.1f, PosZ = -1.9f, TargetX = 0.2f, TargetZ = -1.8f };
            BallLaunchMsg hit = new BallLaunchMsg { Tick = 3, Player = 0, Ball = Ball(1f), Shot = ShotType.Fast, Strength = 0.6f };
            PowerStateMsg ps = new PowerStateMsg { Tick = 4, HasBall = true, Ball = Ball(1f) };
            NetReader r = new NetReader();
            for (int i = 0; i < 1000; i++) { NetProtocol.Write(_w, p); NetProtocol.Write(_w, MsgType.HitLaunch, hit); } // warm-up
            GC.Collect();
            int before = GC.CollectionCount(0);
            for (int i = 0; i < 1000000; i++)
            {
                p.Seq = (uint)i;
                NetProtocol.Write(_w, p);
                r.Reset(_buf, _w.Length);
                PaddleMsg back;
                NetProtocol.TryRead(r, out back);
                NetProtocol.Write(_w, MsgType.HitLaunch, hit);
                NetProtocol.Write(_w, ps);
            }
            Assert.AreEqual(before, GC.CollectionCount(0), "a million packets written and read without a single garbage collection");
            Assert.AreSame(_buf, _w.Buffer, "the same buffer is reused");
        }

        [Test]
        public void NetConfigRepairsBadValuesAndClampsTheGrace()
        {
            NetConfig c = new NetConfig { discoveryPort = 80, gamePort = 47777, paddleSendRate = 500, clockSamples = 1, pointGraceMinMs = 300, pointGraceMaxMs = 100, silenceTimeoutSeconds = float.NaN, reconnectWindowSeconds = -1f, simulatedLatencyMs = 5000, simulatedLossPercent = 90f };
            c.Validate();
            NetConfig d = new NetConfig();
            Assert.AreEqual(d.discoveryPort, c.discoveryPort);
            Assert.AreNotEqual(c.discoveryPort, c.gamePort, "the two ports are different");
            Assert.AreEqual(d.paddleSendRate, c.paddleSendRate);
            Assert.AreEqual(d.clockSamples, c.clockSamples);
            Assert.GreaterOrEqual(c.pointGraceMaxMs, c.pointGraceMinMs);
            Assert.AreEqual(d.silenceTimeoutSeconds, c.silenceTimeoutSeconds, 1e-6f);
            Assert.AreEqual(d.reconnectWindowSeconds, c.reconnectWindowSeconds, 1e-6f);
            Assert.AreEqual(0, c.simulatedLatencyMs);
            Assert.AreEqual(0f, c.simulatedLossPercent, 1e-6f);
            Assert.AreEqual(d.pointGraceExtraMs, d.PointGraceMs(0f), "zero round trip: only the extra");
            NetConfig noExtra = new NetConfig { pointGraceExtraMs = 0 };
            Assert.AreEqual(noExtra.pointGraceMinMs, noExtra.PointGraceMs(10f), "never below the minimum");
            Assert.AreEqual(30 / 2 + d.pointGraceExtraMs, d.PointGraceMs(30f), "half the round trip plus the extra");
            Assert.AreEqual(d.pointGraceMaxMs, d.PointGraceMs(2000f), "never above the maximum");
            // The balance hash ignores Windows/Unix line endings but not content.
            Assert.AreEqual(NetProtocol.Hash("{\n\"a\": 1\n}"), NetProtocol.Hash("{\r\n\"a\": 1\r\n}"));
            Assert.AreNotEqual(NetProtocol.Hash("{\"a\": 1}"), NetProtocol.Hash("{\"a\": 2}"));
        }
    }
}
