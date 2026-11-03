using System;
using System.Runtime.InteropServices;
using Brooklyneir.Gameplay;
using Brooklyneir.PowerUps;
using UnityEngine;

namespace Brooklyneir.Net
{
    /// <summary>Message types of the LAN protocol (first byte of every packet).</summary>
    public enum MsgType : byte
    {
        None = 0, Hello = 1, Welcome = 2, Reject = 3, MatchSetup = 4, Ping = 5, Pong = 6, Paddle = 7,
        Serve = 8, HitBegin = 9, HitLaunch = 10, PowerRequest = 11, PowerState = 12, ShieldSave = 13,
        PointResult = 14, Pause = 15, ResumeAt = 16, Bye = 17, OrbState = 18, RallyVoid = 19
    }

    public enum RejectReason : byte { None = 0, ProtocolMismatch = 1, AppVersionMismatch = 2, BalanceMismatch = 3, Full = 4, BadSession = 5 }
    public enum PowerAction : byte { None = 0, ActivateHeld = 1, ArmSpecial = 2 }
    public enum PauseReason : byte { Button = 0, Background = 1, ConnectionLost = 2 }
    public enum ByeReason : byte { Quit = 0, Error = 1 }

    // ------------------------------------------------------------------ writer / reader

    [StructLayout(LayoutKind.Explicit)]
    struct Bits32 { [FieldOffset(0)] public float F; [FieldOffset(0)] public uint U; }

    [StructLayout(LayoutKind.Explicit)]
    struct Bits64 { [FieldOffset(0)] public double D; [FieldOffset(0)] public ulong U; }

    /// <summary>
    /// Writes little-endian values into a caller-owned byte array (reused for every packet, so
    /// writing never allocates). Overflow sets <see cref="Overflow"/> and stops writing; it never throws.
    /// </summary>
    public sealed class NetWriter
    {
        byte[] _buf;
        public int Length { get; private set; }
        public bool Overflow { get; private set; }
        public byte[] Buffer { get { return _buf; } }

        public NetWriter(byte[] buffer) { _buf = buffer; }

        public void Reset() { Length = 0; Overflow = false; }

        bool Room(int n)
        {
            if (Overflow || Length + n > _buf.Length) { Overflow = true; return false; }
            return true;
        }

        public void U8(byte v) { if (Room(1)) _buf[Length++] = v; }
        public void Bool(bool v) { U8(v ? (byte)1 : (byte)0); }
        public void S8(sbyte v) { U8(unchecked((byte)v)); }

        public void U16(ushort v)
        {
            if (!Room(2)) return;
            _buf[Length++] = (byte)v;
            _buf[Length++] = (byte)(v >> 8);
        }

        public void U32(uint v)
        {
            if (!Room(4)) return;
            _buf[Length++] = (byte)v;
            _buf[Length++] = (byte)(v >> 8);
            _buf[Length++] = (byte)(v >> 16);
            _buf[Length++] = (byte)(v >> 24);
        }

        public void U64(ulong v)
        {
            U32((uint)v);
            U32((uint)(v >> 32));
        }

        public void F32(float v) { Bits32 b = new Bits32(); b.F = v; U32(b.U); }
        public void F64(double v) { Bits64 b = new Bits64(); b.D = v; U64(b.U); }
        public void Vec3(Vector3 v) { F32(v.x); F32(v.y); F32(v.z); }

        /// <summary>UTF-8 text with a 1-byte length, cut to at most maxBytes without splitting a character.</summary>
        public void Str(string s, int maxBytes)
        {
            if (s == null) s = "";
            int chars = 0, bytes = 0;
            while (chars < s.Length)
            {
                char c = s[chars];
                int n, step = 1;
                if (c < 0x80) n = 1;
                else if (c < 0x800) n = 2;
                else if (char.IsHighSurrogate(c) && chars + 1 < s.Length && char.IsLowSurrogate(s[chars + 1])) { n = 4; step = 2; }
                else n = 3;
                if (bytes + n > maxBytes || bytes + n > 255) break;
                bytes += n;
                chars += step;
            }
            U8((byte)bytes);
            if (!Room(bytes)) return;
            int written = System.Text.Encoding.UTF8.GetBytes(s, 0, chars, _buf, Length);
            Length += written;
        }
    }

    /// <summary>
    /// Reads what NetWriter wrote. Any read past the end (truncated or garbage packet) sets
    /// <see cref="Failed"/> and returns zeros from then on; it never throws.
    /// </summary>
    public sealed class NetReader
    {
        byte[] _buf;
        int _len;
        public int Position { get; private set; }
        public bool Failed { get; private set; }

        public NetReader() { }
        public NetReader(byte[] buffer, int length) { Reset(buffer, length); }

        public void Reset(byte[] buffer, int length)
        {
            _buf = buffer;
            _len = buffer == null ? 0 : Math.Min(Math.Max(0, length), buffer.Length);
            Position = 0;
            Failed = false;
        }

        public int Remaining { get { return _len - Position; } }

        bool Has(int n)
        {
            if (Failed || Position + n > _len) { Failed = true; return false; }
            return true;
        }

        public byte U8() { return Has(1) ? _buf[Position++] : (byte)0; }
        public bool Bool() { return U8() != 0; }
        public sbyte S8() { return unchecked((sbyte)U8()); }

        public ushort U16()
        {
            if (!Has(2)) return 0;
            ushort v = (ushort)(_buf[Position] | (_buf[Position + 1] << 8));
            Position += 2;
            return v;
        }

        public uint U32()
        {
            if (!Has(4)) return 0;
            uint v = (uint)(_buf[Position] | (_buf[Position + 1] << 8) | (_buf[Position + 2] << 16) | (_buf[Position + 3] << 24));
            Position += 4;
            return v;
        }

        public ulong U64() { ulong lo = U32(); ulong hi = U32(); return lo | (hi << 32); }
        public float F32() { Bits32 b = new Bits32(); b.U = U32(); return b.F; }
        public double F64() { Bits64 b = new Bits64(); b.U = U64(); return b.D; }
        public Vector3 Vec3() { float x = F32(), y = F32(), z = F32(); return new Vector3(x, y, z); }

        /// <summary>Reads a string (allocates; only used by lobby messages, never per frame).</summary>
        public string Str(int maxBytes)
        {
            int n = U8();
            if (n > maxBytes) { Failed = true; return ""; }
            if (!Has(n)) return "";
            string s;
            try { s = System.Text.Encoding.UTF8.GetString(_buf, Position, n); }
            catch (Exception) { Failed = true; return ""; }
            Position += n;
            return s;
        }
    }

    // ------------------------------------------------------------------ payloads

    /// <summary>
    /// Full ball state sent with every serve, hit, shield save, pause and resume. BasedOn is the
    /// tick of the launch (serve, hit or shield save) this trajectory comes from: a phone ignores
    /// a state based on a launch older than its own latest one, so a message computed before the
    /// other phone heard about a hit can never erase that hit.
    /// </summary>
    public struct NetBallState
    {
        public Vector3 Position, Velocity, Spin;
        public bool Active;
        public uint BasedOn;

        public void Write(NetWriter w) { w.Vec3(Position); w.Vec3(Velocity); w.Vec3(Spin); w.Bool(Active); w.U32(BasedOn); }
        public static NetBallState Read(NetReader r)
        {
            NetBallState b = new NetBallState();
            b.Position = r.Vec3(); b.Velocity = r.Vec3(); b.Spin = r.Vec3(); b.Active = r.Bool(); b.BasedOn = r.U32();
            return b;
        }
    }

    /// <summary>Everything in a PlayerPowerState, so the guest shows exactly the host's powers.</summary>
    public struct NetPowerPlayer
    {
        public sbyte Held;
        public bool FireActive, FireBurning, SpecialArmed;
        public float Cooldown, FireTimer, SlowTimer, CurveArmedTimer, SuperSmashArmedTimer, ShieldTimer, FrozenTimer, SpecialEnergy;

        public static NetPowerPlayer From(PlayerPowerState s)
        {
            NetPowerPlayer p = new NetPowerPlayer();
            p.Held = (sbyte)s.Held;
            p.FireActive = s.FireActive; p.FireBurning = s.FireBurning; p.SpecialArmed = s.SpecialArmed;
            p.Cooldown = s.Cooldown; p.FireTimer = s.FireTimer; p.SlowTimer = s.SlowTimer; p.CurveArmedTimer = s.CurveArmedTimer;
            p.SuperSmashArmedTimer = s.SuperSmashArmedTimer; p.ShieldTimer = s.ShieldTimer; p.FrozenTimer = s.FrozenTimer; p.SpecialEnergy = s.SpecialEnergy;
            return p;
        }

        public void ApplyTo(PlayerPowerState s)
        {
            s.Held = (PowerUpType)Held;
            s.FireActive = FireActive; s.FireBurning = FireBurning; s.SpecialArmed = SpecialArmed;
            s.Cooldown = Cooldown; s.FireTimer = FireTimer; s.SlowTimer = SlowTimer; s.CurveArmedTimer = CurveArmedTimer;
            s.SuperSmashArmedTimer = SuperSmashArmedTimer; s.ShieldTimer = ShieldTimer; s.FrozenTimer = FrozenTimer; s.SpecialEnergy = SpecialEnergy;
        }

        public void Write(NetWriter w)
        {
            w.S8(Held);
            w.U8((byte)((FireActive ? 1 : 0) | (FireBurning ? 2 : 0) | (SpecialArmed ? 4 : 0)));
            w.F32(Cooldown); w.F32(FireTimer); w.F32(SlowTimer); w.F32(CurveArmedTimer);
            w.F32(SuperSmashArmedTimer); w.F32(ShieldTimer); w.F32(FrozenTimer); w.F32(SpecialEnergy);
        }

        public static NetPowerPlayer Read(NetReader r)
        {
            NetPowerPlayer p = new NetPowerPlayer();
            p.Held = r.S8();
            byte f = r.U8();
            p.FireActive = (f & 1) != 0; p.FireBurning = (f & 2) != 0; p.SpecialArmed = (f & 4) != 0;
            p.Cooldown = r.F32(); p.FireTimer = r.F32(); p.SlowTimer = r.F32(); p.CurveArmedTimer = r.F32();
            p.SuperSmashArmedTimer = r.F32(); p.ShieldTimer = r.F32(); p.FrozenTimer = r.F32(); p.SpecialEnergy = r.F32();
            return p;
        }
    }

    // ------------------------------------------------------------------ messages

    public struct HelloMsg
    {
        public ushort Protocol; public string AppVersion; public uint BalanceHash; public string Name; public string RacketId; public uint SessionCode;
    }

    public struct WelcomeMsg { public uint SessionCode; public uint HostTick; public byte PlayerIndex; }
    public struct RejectMsg { public RejectReason Reason; public string Text; }

    public struct MatchSetupMsg
    {
        public GameMode Mode; public byte PointsToWin, BestOfSets; public bool WinByTwo; public float TimeLimitMinutes;
        public bool PowerUps; public PowerFrequency Frequency; public string TableId, RacketP1, RacketP2, NameP1, NameP2;
        public byte FirstServer; public uint StartTick;

        public static MatchSetupMsg From(MatchConfig c, byte firstServer, uint startTick, string nameP1, string nameP2)
        {
            MatchSetupMsg m = new MatchSetupMsg();
            m.Mode = c.mode; m.PointsToWin = (byte)Mathf.Clamp(c.pointsToWin, 0, 255); m.BestOfSets = (byte)Mathf.Clamp(c.bestOfSets, 0, 255);
            m.WinByTwo = c.winByTwo; m.TimeLimitMinutes = c.timeLimitMinutes; m.PowerUps = c.powerUpsEnabled; m.Frequency = c.powerFrequency;
            m.TableId = c.tableId; m.RacketP1 = c.racketP1; m.RacketP2 = c.racketP2; m.NameP1 = nameP1; m.NameP2 = nameP2;
            m.FirstServer = firstServer; m.StartTick = startTick;
            return m;
        }

        public MatchConfig ToConfig()
        {
            MatchConfig c = new MatchConfig();
            c.mode = Mode; c.pointsToWin = PointsToWin; c.bestOfSets = BestOfSets; c.winByTwo = WinByTwo; c.timeLimitMinutes = TimeLimitMinutes;
            c.powerUpsEnabled = PowerUps; c.powerFrequency = Frequency; c.tableId = TableId; c.racketP1 = RacketP1; c.racketP2 = RacketP2;
            c.player1IsAI = false; c.player2IsAI = false;
            return c;
        }
    }

    public struct PingMsg { public double T0; }
    public struct PongMsg { public double T0, T1; }
    public struct PaddleMsg { public uint Seq, Tick; public float PosX, PosZ, TargetX, TargetZ; }

    /// <summary>Serve and HitLaunch: the ball state right after the racket, with its tick, and the armed powers it used up.</summary>
    public struct BallLaunchMsg
    {
        public uint Tick; public byte Player; public NetBallState Ball; public ShotType Shot; public float Strength;
        public bool ConsumedCurve, ConsumedSuperSmash, ConsumedSpecial;
    }
    public struct HitBeginMsg { public uint Tick; public byte Player; public Vector3 Contact; }
    public struct PowerRequestMsg { public byte Player; public PowerAction Action; }
    public struct PowerStateMsg { public uint Tick; public NetPowerPlayer P0, P1; public bool HasBall; public NetBallState Ball; }
    public struct ShieldSaveMsg { public uint Tick; public byte Player; public NetBallState Ball; }
    public struct OrbStateMsg { public uint Tick; public bool Active; public Vector3 Position; public sbyte Type; }

    public struct PointResultMsg
    {
        public byte Winner; public PointReason Reason; public ushort Points0, Points1; public byte Sets0, Sets1, NextServer;
        public bool MatchPoint0, MatchPoint1, SetWon, MatchWon, TimeUp; public float Clock;
    }

    public struct PauseMsg { public byte Player; public PauseReason Reason; public uint Tick; public NetBallState Ball; }
    public struct ResumeAtMsg { public uint Tick; public NetBallState Ball; }
    public struct ByeMsg { public ByeReason Reason; }
    /// <summary>After a reconnection the rally in play does not count: both resume at ResumeTick with this server.</summary>
    public struct RallyVoidMsg { public uint ResumeTick; public byte Server; }

    /// <summary>
    /// The LAN protocol: every message as a binary packet whose first byte is its MsgType. Write*
    /// fills a reused NetWriter (no allocation); TryRead* validates the type and the length and
    /// never throws on a truncated or garbage packet. Pure, unit-tested.
    /// </summary>
    public static class NetProtocol
    {
        public const ushort Version = 1;
        public const int MaxPacket = 512;
        public const int MaxName = 16, MaxAppVersion = 16, MaxId = 24, MaxText = 48;

        /// <summary>FNV-1a hash of a text (used for balance.json, so both phones play the same physics).</summary>
        public static uint Hash(string s)
        {
            uint h = 2166136261u;
            if (s == null) return h;
            unchecked
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '\r') continue; // same hash with Windows or Unix line endings
                    h ^= c;
                    h *= 16777619u;
                }
            }
            return h;
        }

        public static MsgType PeekType(byte[] buf, int len)
        {
            if (buf == null || len < 1) return MsgType.None;
            byte t = buf[0];
            return t >= 1 && t <= (byte)MsgType.RallyVoid ? (MsgType)t : MsgType.None;
        }

        static bool Begin(NetReader r, MsgType expected)
        {
            return (MsgType)r.U8() == expected && !r.Failed;
        }

        static bool End(NetReader r) { return !r.Failed && r.Remaining == 0; }

        // ---- Hello
        public static void Write(NetWriter w, HelloMsg m)
        {
            w.Reset(); w.U8((byte)MsgType.Hello);
            w.U16(m.Protocol); w.Str(m.AppVersion, MaxAppVersion); w.U32(m.BalanceHash); w.Str(m.Name, MaxName); w.Str(m.RacketId, MaxId); w.U32(m.SessionCode);
        }

        /// <summary>Reads only the protocol version first, so a different version can be rejected cleanly.</summary>
        public static bool TryRead(NetReader r, out HelloMsg m)
        {
            m = new HelloMsg();
            if (!Begin(r, MsgType.Hello)) return false;
            m.Protocol = r.U16();
            if (r.Failed) return false;
            if (m.Protocol != Version) return true; // caller rejects with ProtocolMismatch
            m.AppVersion = r.Str(MaxAppVersion); m.BalanceHash = r.U32(); m.Name = r.Str(MaxName); m.RacketId = r.Str(MaxId); m.SessionCode = r.U32();
            return End(r);
        }

        // ---- Welcome / Reject
        public static void Write(NetWriter w, WelcomeMsg m) { w.Reset(); w.U8((byte)MsgType.Welcome); w.U32(m.SessionCode); w.U32(m.HostTick); w.U8(m.PlayerIndex); }
        public static bool TryRead(NetReader r, out WelcomeMsg m)
        {
            m = new WelcomeMsg();
            if (!Begin(r, MsgType.Welcome)) return false;
            m.SessionCode = r.U32(); m.HostTick = r.U32(); m.PlayerIndex = r.U8();
            return End(r) && m.PlayerIndex <= 1;
        }

        public static void Write(NetWriter w, RejectMsg m) { w.Reset(); w.U8((byte)MsgType.Reject); w.U8((byte)m.Reason); w.Str(m.Text, MaxText); }
        public static bool TryRead(NetReader r, out RejectMsg m)
        {
            m = new RejectMsg();
            if (!Begin(r, MsgType.Reject)) return false;
            m.Reason = (RejectReason)r.U8(); m.Text = r.Str(MaxText);
            return End(r);
        }

        // ---- MatchSetup
        public static void Write(NetWriter w, MatchSetupMsg m)
        {
            w.Reset(); w.U8((byte)MsgType.MatchSetup);
            w.U8((byte)m.Mode); w.U8(m.PointsToWin); w.U8(m.BestOfSets); w.Bool(m.WinByTwo); w.F32(m.TimeLimitMinutes);
            w.Bool(m.PowerUps); w.U8((byte)m.Frequency); w.Str(m.TableId, MaxId); w.Str(m.RacketP1, MaxId); w.Str(m.RacketP2, MaxId);
            w.Str(m.NameP1, MaxName); w.Str(m.NameP2, MaxName); w.U8(m.FirstServer); w.U32(m.StartTick);
        }

        public static bool TryRead(NetReader r, out MatchSetupMsg m)
        {
            m = new MatchSetupMsg();
            if (!Begin(r, MsgType.MatchSetup)) return false;
            m.Mode = (GameMode)r.U8(); m.PointsToWin = r.U8(); m.BestOfSets = r.U8(); m.WinByTwo = r.Bool(); m.TimeLimitMinutes = r.F32();
            m.PowerUps = r.Bool(); m.Frequency = (PowerFrequency)r.U8(); m.TableId = r.Str(MaxId); m.RacketP1 = r.Str(MaxId); m.RacketP2 = r.Str(MaxId);
            m.NameP1 = r.Str(MaxName); m.NameP2 = r.Str(MaxName); m.FirstServer = r.U8(); m.StartTick = r.U32();
            return End(r) && m.FirstServer <= 1 && ControlSetup.AllowsRemote(m.Mode);
        }

        // ---- Ping / Pong
        public static void Write(NetWriter w, PingMsg m) { w.Reset(); w.U8((byte)MsgType.Ping); w.F64(m.T0); }
        public static bool TryRead(NetReader r, out PingMsg m) { m = new PingMsg(); if (!Begin(r, MsgType.Ping)) return false; m.T0 = r.F64(); return End(r); }
        public static void Write(NetWriter w, PongMsg m) { w.Reset(); w.U8((byte)MsgType.Pong); w.F64(m.T0); w.F64(m.T1); }
        public static bool TryRead(NetReader r, out PongMsg m) { m = new PongMsg(); if (!Begin(r, MsgType.Pong)) return false; m.T0 = r.F64(); m.T1 = r.F64(); return End(r); }

        // ---- Paddle
        public static void Write(NetWriter w, PaddleMsg m)
        {
            w.Reset(); w.U8((byte)MsgType.Paddle);
            w.U32(m.Seq); w.U32(m.Tick); w.F32(m.PosX); w.F32(m.PosZ); w.F32(m.TargetX); w.F32(m.TargetZ);
        }

        public static bool TryRead(NetReader r, out PaddleMsg m)
        {
            m = new PaddleMsg();
            if (!Begin(r, MsgType.Paddle)) return false;
            m.Seq = r.U32(); m.Tick = r.U32(); m.PosX = r.F32(); m.PosZ = r.F32(); m.TargetX = r.F32(); m.TargetZ = r.F32();
            return End(r);
        }

        // ---- Serve / HitLaunch
        public static void Write(NetWriter w, MsgType type, BallLaunchMsg m)
        {
            w.Reset(); w.U8((byte)type);
            w.U32(m.Tick); w.U8(m.Player); m.Ball.Write(w); w.U8((byte)m.Shot); w.F32(m.Strength);
            w.U8((byte)((m.ConsumedCurve ? 1 : 0) | (m.ConsumedSuperSmash ? 2 : 0) | (m.ConsumedSpecial ? 4 : 0)));
        }

        public static bool TryRead(NetReader r, MsgType type, out BallLaunchMsg m)
        {
            m = new BallLaunchMsg();
            if ((type != MsgType.Serve && type != MsgType.HitLaunch) || !Begin(r, type)) return false;
            m.Tick = r.U32(); m.Player = r.U8(); m.Ball = NetBallState.Read(r); m.Shot = (ShotType)r.U8(); m.Strength = r.F32();
            byte f = r.U8();
            m.ConsumedCurve = (f & 1) != 0; m.ConsumedSuperSmash = (f & 2) != 0; m.ConsumedSpecial = (f & 4) != 0;
            return End(r) && m.Player <= 1;
        }

        // ---- HitBegin
        public static void Write(NetWriter w, HitBeginMsg m) { w.Reset(); w.U8((byte)MsgType.HitBegin); w.U32(m.Tick); w.U8(m.Player); w.Vec3(m.Contact); }
        public static bool TryRead(NetReader r, out HitBeginMsg m)
        {
            m = new HitBeginMsg();
            if (!Begin(r, MsgType.HitBegin)) return false;
            m.Tick = r.U32(); m.Player = r.U8(); m.Contact = r.Vec3();
            return End(r) && m.Player <= 1;
        }

        // ---- Powers and orbs
        public static void Write(NetWriter w, PowerRequestMsg m) { w.Reset(); w.U8((byte)MsgType.PowerRequest); w.U8(m.Player); w.U8((byte)m.Action); }
        public static bool TryRead(NetReader r, out PowerRequestMsg m)
        {
            m = new PowerRequestMsg();
            if (!Begin(r, MsgType.PowerRequest)) return false;
            m.Player = r.U8(); m.Action = (PowerAction)r.U8();
            return End(r) && m.Player <= 1 && (m.Action == PowerAction.ActivateHeld || m.Action == PowerAction.ArmSpecial);
        }

        public static void Write(NetWriter w, PowerStateMsg m)
        {
            w.Reset(); w.U8((byte)MsgType.PowerState);
            w.U32(m.Tick); m.P0.Write(w); m.P1.Write(w); w.Bool(m.HasBall);
            if (m.HasBall) m.Ball.Write(w);
        }

        public static bool TryRead(NetReader r, out PowerStateMsg m)
        {
            m = new PowerStateMsg();
            if (!Begin(r, MsgType.PowerState)) return false;
            m.Tick = r.U32(); m.P0 = NetPowerPlayer.Read(r); m.P1 = NetPowerPlayer.Read(r); m.HasBall = r.Bool();
            if (m.HasBall) m.Ball = NetBallState.Read(r);
            return End(r);
        }

        public static void Write(NetWriter w, ShieldSaveMsg m) { w.Reset(); w.U8((byte)MsgType.ShieldSave); w.U32(m.Tick); w.U8(m.Player); m.Ball.Write(w); }
        public static bool TryRead(NetReader r, out ShieldSaveMsg m)
        {
            m = new ShieldSaveMsg();
            if (!Begin(r, MsgType.ShieldSave)) return false;
            m.Tick = r.U32(); m.Player = r.U8(); m.Ball = NetBallState.Read(r);
            return End(r) && m.Player <= 1;
        }

        public static void Write(NetWriter w, OrbStateMsg m) { w.Reset(); w.U8((byte)MsgType.OrbState); w.U32(m.Tick); w.Bool(m.Active); w.Vec3(m.Position); w.S8(m.Type); }
        public static bool TryRead(NetReader r, out OrbStateMsg m)
        {
            m = new OrbStateMsg();
            if (!Begin(r, MsgType.OrbState)) return false;
            m.Tick = r.U32(); m.Active = r.Bool(); m.Position = r.Vec3(); m.Type = r.S8();
            return End(r);
        }

        // ---- PointResult
        public static void Write(NetWriter w, PointResultMsg m)
        {
            w.Reset(); w.U8((byte)MsgType.PointResult);
            w.U8(m.Winner); w.U8((byte)m.Reason); w.U16(m.Points0); w.U16(m.Points1); w.U8(m.Sets0); w.U8(m.Sets1); w.U8(m.NextServer);
            w.U8((byte)((m.MatchPoint0 ? 1 : 0) | (m.MatchPoint1 ? 2 : 0) | (m.SetWon ? 4 : 0) | (m.MatchWon ? 8 : 0) | (m.TimeUp ? 16 : 0)));
            w.F32(m.Clock);
        }

        public static bool TryRead(NetReader r, out PointResultMsg m)
        {
            m = new PointResultMsg();
            if (!Begin(r, MsgType.PointResult)) return false;
            m.Winner = r.U8(); m.Reason = (PointReason)r.U8(); m.Points0 = r.U16(); m.Points1 = r.U16(); m.Sets0 = r.U8(); m.Sets1 = r.U8(); m.NextServer = r.U8();
            byte f = r.U8();
            m.MatchPoint0 = (f & 1) != 0; m.MatchPoint1 = (f & 2) != 0; m.SetWon = (f & 4) != 0; m.MatchWon = (f & 8) != 0; m.TimeUp = (f & 16) != 0;
            m.Clock = r.F32();
            return End(r) && m.Winner <= 1 && m.NextServer <= 1;
        }

        // ---- Pause / ResumeAt / Bye
        public static void Write(NetWriter w, PauseMsg m) { w.Reset(); w.U8((byte)MsgType.Pause); w.U8(m.Player); w.U8((byte)m.Reason); w.U32(m.Tick); m.Ball.Write(w); }
        public static bool TryRead(NetReader r, out PauseMsg m)
        {
            m = new PauseMsg();
            if (!Begin(r, MsgType.Pause)) return false;
            m.Player = r.U8(); m.Reason = (PauseReason)r.U8(); m.Tick = r.U32(); m.Ball = NetBallState.Read(r);
            return End(r) && m.Player <= 1;
        }

        public static void Write(NetWriter w, ResumeAtMsg m) { w.Reset(); w.U8((byte)MsgType.ResumeAt); w.U32(m.Tick); m.Ball.Write(w); }
        public static bool TryRead(NetReader r, out ResumeAtMsg m)
        {
            m = new ResumeAtMsg();
            if (!Begin(r, MsgType.ResumeAt)) return false;
            m.Tick = r.U32(); m.Ball = NetBallState.Read(r);
            return End(r);
        }

        public static void Write(NetWriter w, RallyVoidMsg m) { w.Reset(); w.U8((byte)MsgType.RallyVoid); w.U32(m.ResumeTick); w.U8(m.Server); }
        public static bool TryRead(NetReader r, out RallyVoidMsg m)
        {
            m = new RallyVoidMsg();
            if (!Begin(r, MsgType.RallyVoid)) return false;
            m.ResumeTick = r.U32(); m.Server = r.U8();
            return End(r) && m.Server <= 1;
        }

        public static void Write(NetWriter w, ByeMsg m) { w.Reset(); w.U8((byte)MsgType.Bye); w.U8((byte)m.Reason); }
        public static bool TryRead(NetReader r, out ByeMsg m) { m = new ByeMsg(); if (!Begin(r, MsgType.Bye)) return false; m.Reason = (ByeReason)r.U8(); return End(r); }
    }
}
