using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using UnityEngine;

namespace Brooklyneir.Net
{
    /// <summary>
    /// The LAN connection on this phone, from "Crear partida" / "Unirse" to the end of the match:
    /// opens the transport, runs the handshake (protocol, app version and balance.json hash,
    /// session code) with SessionStateMachine, measures the round trip in the lobby, starts the
    /// match (the host sends MatchSetup with the start tick; both create their NetMatchSession),
    /// feeds every packet to the session and updates it once per frame, and brings the guest
    /// back with its session code if the connection drops. Runs before every other script
    /// (DefaultExecutionOrder), so the match always sees this frame's packets; LateUpdate sends
    /// this frame's packets at once. Lives on the game root; screens and the match only read it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LanLobby : MonoBehaviour, ILanTransportListener
    {
        public NetConfig Config { get; private set; }
        public NetIdentity Identity { get; private set; }
        public NetRole Role { get; private set; }
        public SessionStateMachine Machine { get; private set; }
        public LanTransport Transport { get; private set; }
        /// <summary>Peer of the other phone in the transport (host: the accepted guest; guest: 0), or -1.</summary>
        public int ActivePeer { get; private set; }
        public string LocalName { get; private set; }
        public string RivalName { get; private set; }
        public string RivalRacket { get; private set; }
        public string Error { get; private set; }
        /// <summary>Round trip of the last pong (ms), or -1 before the first.</summary>
        public float RoundTripMs { get; private set; }
        public string LocalAddress { get; private set; }

        /// <summary>The match on this phone (from MatchSetup until the next Host/Join), or null.</summary>
        public NetMatchSession Match { get; private set; }
        /// <summary>Seconds from the host's MatchSetup to the first serve (countdown on both phones).</summary>
        public const float StartDelaySeconds = 3.5f;

        /// <summary>Something the screens show changed (state, rival, error).</summary>
        public event Action Changed;

        readonly byte[] _out = new byte[NetProtocol.MaxPacket];
        NetWriter _w;
        readonly NetReader _r = new NetReader();
        double _lastPing;
        LanPeerLink _link;
        string _address;
        bool _retryConnect;
        // A rejected guest is disconnected a moment later, so its Reject reaches it first.
        readonly double[] _kickAt = new double[LanTransport.MaxPeers];

        public bool Active { get { return Machine != null && Machine.State != LinkState.Idle && Machine.State != LinkState.Ended; } }

        public void Init(string balanceJson)
        {
            Config = LoadConfig();
            _w = new NetWriter(_out);
            Identity = new NetIdentity { Protocol = NetProtocol.Version, AppVersion = Application.version, BalanceHash = NetProtocol.Hash(balanceJson ?? "") };
            Transport = new LanTransport();
            Transport.SimulatedLatencyMs = Config.simulatedLatencyMs;
            Transport.SimulatedLossPercent = Config.simulatedLossPercent;
            ActivePeer = -1;
            RoundTripMs = -1f;
        }

        public static NetConfig LoadConfig()
        {
            NetConfig cfg = new NetConfig();
            TextAsset json = Resources.Load<TextAsset>(NetConfig.ResourcePath);
            if (json != null && !string.IsNullOrEmpty(json.text))
            {
                try { JsonUtility.FromJsonOverwrite(json.text, cfg); }
                catch (Exception e)
                {
                    Debug.LogError("[TTLB] net.json is invalid, using defaults: " + e.Message);
                    cfg = new NetConfig();
                }
            }
            cfg.Validate();
            return cfg;
        }

        static double Now { get { return Time.realtimeSinceStartupAsDouble; } }

        void Notify() { if (Changed != null) Changed(); }

        void Reset(NetRole role)
        {
            Transport.Stop();
            Role = role;
            Machine = new SessionStateMachine(role, Identity, Config);
            ActivePeer = -1;
            RivalName = null;
            RivalRacket = null;
            Error = null;
            RoundTripMs = -1f;
            _lastPing = double.NegativeInfinity;
            for (int i = 0; i < _kickAt.Length; i++) _kickAt[i] = 0.0;
            Match = null;
            _link = null;
            _retryConnect = false;
            LocalName = role == NetRole.Host ? "J1" : "J2"; // the profile has no name yet
            LocalAddress = LocalIPv4();
        }

        // ============================================================ start / stop

        public bool Host()
        {
            Reset(NetRole.Host);
            string err;
            if (!Transport.StartHost(Config.gamePort, out err)) { Error = err; Machine.LocalLeave(); Notify(); return false; }
            Machine.StartHosting((uint)UnityEngine.Random.Range(1, int.MaxValue));
            Notify();
            return true;
        }

        public bool Join(string address)
        {
            Reset(NetRole.Guest);
            _address = address.Trim();
            string err;
            if (!Transport.StartClient(_address, Config.gamePort, out err)) { Error = err; Machine.LocalLeave(); Notify(); return false; }
            Machine.StartConnecting();
            Notify();
            return true;
        }

        /// <summary>Leaves (says Bye so the other phone knows at once) and closes the connection.</summary>
        public void Close()
        {
            if (Machine == null) return;
            if (ActivePeer >= 0 && Transport.IsConnected(ActivePeer))
            {
                NetProtocol.Write(_w, new ByeMsg { Reason = ByeReason.Quit });
                Transport.Send(ActivePeer, _out, _w.Length, true);
                Transport.Flush();
            }
            if (Machine.State != LinkState.Ended) Machine.LocalLeave();
            Transport.Stop();
            ActivePeer = -1;
            Notify();
        }

        /// <summary>Leaving a match in play: Bye to the other phone (it wins), then close.</summary>
        public void LeaveMatch()
        {
            if (Match != null) Match.Leave();
            if (Machine != null) Machine.LocalLeave();
            Close();
        }

        // ============================================================ match start

        /// <summary>
        /// Host, in the lobby: starts a quick or custom match. The rackets are each player's own,
        /// the table the host's. Powers stay off until step 7b-3.
        /// </summary>
        public bool HostStartMatch(MatchConfig source)
        {
            if (Role != NetRole.Host || Machine == null || Machine.State != LinkState.Lobby || ActivePeer < 0) return false;
            MatchConfig cfg = source.Clone();
            cfg.player1IsAI = false;
            cfg.player2IsAI = false;
            cfg.powerUpsEnabled = false;
            cfg.powerFrequency = PowerFrequency.Off;
            if (Game.Save != null)
            {
                cfg.racketP1 = Game.Save.Data.racketP1;
                cfg.tableId = Game.Save.Data.selectedTable;
            }
            if (!string.IsNullOrEmpty(RivalRacket)) cfg.racketP2 = RivalRacket;
            cfg.Validate();
            int first = UnityEngine.Random.value < 0.5f ? 0 : 1;
            CreateSession(NetRole.Host, first);
            Match.Update(Now);
            uint start = Match.Now + (uint)Mathf.RoundToInt(StartDelaySeconds * Match.Ball.TickRate);
            Match.StartTick = start;
            NetProtocol.Write(_w, MatchSetupMsg.From(cfg, (byte)first, start, LocalName, RivalName ?? "J2"));
            Transport.Send(ActivePeer, _out, _w.Length, true);
            Machine.MatchStarted();
            Notify();
            Game.Flow.StartLanMatch(cfg, Match, first, RivalName ?? "J2");
            return true;
        }

        void CreateSession(NetRole role, int firstServer)
        {
            _link = new LanPeerLink(Transport, ActivePeer);
            Match = new NetMatchSession(role, Config, Game.Balance.physics, _link, firstServer);
        }

        void GuestOnMatchSetup()
        {
            MatchSetupMsg m;
            if (Match != null || Machine.State != LinkState.Lobby || !NetProtocol.TryRead(_r, out m)) return;
            MatchConfig cfg = m.ToConfig();
            cfg.Validate();
            CreateSession(NetRole.Guest, m.FirstServer);
            Match.StartTick = m.StartTick;
            if (!string.IsNullOrEmpty(m.NameP1)) RivalName = m.NameP1;
            Machine.MatchStarted();
            Notify();
            Game.Flow.StartLanMatch(cfg, Match, m.FirstServer, RivalName ?? "J1");
        }

        void OnDestroy() { if (Transport != null) Transport.Dispose(); }

        void OnApplicationQuit() { Close(); }

        // ============================================================ frame

        void Update()
        {
            if (Machine == null) return;
            double now = Now;
            if (Transport.Running) Transport.Poll(now, this);
            for (int i = 0; i < _kickAt.Length; i++)
            {
                if (_kickAt[i] > 0.0 && now >= _kickAt[i]) { _kickAt[i] = 0.0; Transport.Disconnect(i); }
            }
            if (_retryConnect)
            {
                // Guest: the connection dropped during the match; try again with the session code.
                _retryConnect = false;
                string err;
                if (Machine.State == LinkState.Reconnecting) Transport.StartClient(_address, Config.gamePort, out err);
            }
            LinkState before = Machine.State;
            Machine.Update(now);
            if (Match != null)
            {
                Match.Update(now);
                if (Match.State == SessionState.Void) Machine.MatchVoided();
                else if (Match.State == SessionState.Finished) Machine.MatchFinished();
            }
            if (Machine.State == LinkState.Lobby && ActivePeer >= 0 && now - _lastPing >= 1.0)
            {
                _lastPing = now;
                Send(new PingMsg { T0 = now });
            }
            if (Machine.State != before) Notify();
        }

        /// <summary>Sends what is queued now (the app is about to go to the background).</summary>
        public void FlushNow()
        {
            if (Transport != null && Transport.Running) Transport.FlushSends();
        }

        void LateUpdate()
        {
            if (Transport != null && Transport.Running) Transport.FlushSends();
        }

        void Send(PingMsg m) { NetProtocol.Write(_w, m); Transport.Send(ActivePeer, _out, _w.Length, false); }

        // ============================================================ transport events

        public void OnLanConnected(int peer)
        {
            if (Role == NetRole.Guest)
            {
                ActivePeer = peer;
                Machine.GuestOnConnected();
                NetProtocol.Write(_w, new HelloMsg
                {
                    Protocol = Identity.Protocol,
                    AppVersion = Identity.AppVersion,
                    BalanceHash = Identity.BalanceHash,
                    Name = LocalName,
                    RacketId = Game.Save != null ? Game.Save.Data.racketP1 : "",
                    SessionCode = Machine.HelloCode
                });
                Transport.Send(peer, _out, _w.Length, true);
                if (_link != null) _link.Peer = peer;
                Notify();
            }
            // Host: wait for its Hello.
        }

        public void OnLanDisconnected(int peer)
        {
            if (Role == NetRole.Guest)
            {
                ActivePeer = -1;
                switch (Machine.State)
                {
                    case LinkState.Connecting:
                    case LinkState.Handshaking:
                        Machine.ConnectFailed();
                        Error = "No se encontró la partida en esa dirección. Comprueba la IP y que los dos estéis en la misma wifi.";
                        break;
                    case LinkState.InMatch:
                        Machine.TransportDisconnected(Now); // -> Reconnecting
                        _retryConnect = true;
                        break;
                    case LinkState.Reconnecting:
                        _retryConnect = true; // that try failed: another one, until the window closes
                        break;
                    default:
                        Machine.TransportDisconnected(Now);
                        break;
                }
                Notify();
                return;
            }
            if (peer != ActivePeer) return; // a rejected or stale connection
            ActivePeer = -1;
            Machine.TransportDisconnected(Now);
            Notify();
        }

        public void OnLanData(int peer, byte[] data, int length)
        {
            _r.Reset(data, length);
            MsgType type = NetProtocol.PeekType(data, length);
            if (Match != null && peer == ActivePeer && type != MsgType.Hello && type != MsgType.Welcome && type != MsgType.Reject && type != MsgType.MatchSetup)
            {
                // In a match every packet belongs to the session (pings, rackets, hits, points...).
                Match.Receive(data, length, Now);
                if (type == MsgType.Bye) Machine.OnBye();
                return;
            }
            switch (type)
            {
                case MsgType.MatchSetup: if (Role == NetRole.Guest && peer == ActivePeer) GuestOnMatchSetup(); return;
                case MsgType.Hello: if (Role == NetRole.Host) HostOnHello(peer); return;
                case MsgType.Welcome:
                {
                    WelcomeMsg m;
                    if (Role != NetRole.Guest || !NetProtocol.TryRead(_r, out m)) return;
                    Machine.GuestOnWelcome(m);
                    if (RivalName == null) RivalName = "J1";
                    Notify();
                    return;
                }
                case MsgType.Reject:
                {
                    RejectMsg m;
                    if (Role != NetRole.Guest || !NetProtocol.TryRead(_r, out m)) return;
                    Machine.GuestOnReject(m);
                    Error = string.IsNullOrEmpty(m.Text) ? RejectText(m.Reason) : m.Text;
                    Transport.Stop();
                    ActivePeer = -1;
                    Notify();
                    return;
                }
                case MsgType.Ping:
                {
                    PingMsg m;
                    if (!NetProtocol.TryRead(_r, out m) || peer != ActivePeer) return;
                    NetProtocol.Write(_w, new PongMsg { T0 = m.T0, T1 = Now });
                    Transport.Send(peer, _out, _w.Length, false);
                    return;
                }
                case MsgType.Pong:
                {
                    PongMsg m;
                    if (!NetProtocol.TryRead(_r, out m) || peer != ActivePeer) return;
                    float rtt = (float)((Now - m.T0) * 1000.0);
                    bool changed = RoundTripMs < 0f || Mathf.Abs(rtt - RoundTripMs) >= 1f;
                    RoundTripMs = rtt;
                    if (changed) Notify();
                    return;
                }
                case MsgType.Bye:
                {
                    if (peer != ActivePeer) return;
                    Machine.OnBye();
                    Transport.Stop();
                    ActivePeer = -1;
                    Notify();
                    return;
                }
            }
        }

        void HostOnHello(int peer)
        {
            HelloMsg m;
            if (!NetProtocol.TryRead(_r, out m)) return;
            // A different protocol version leaves the rest unread: compare only that.
            if (m.Protocol != Identity.Protocol) { Reject(peer, RejectReason.ProtocolMismatch); return; }
            RejectReason why = Machine.HostOnHello(m);
            if (why != RejectReason.None) { Reject(peer, why); return; }
            if (ActivePeer >= 0 && ActivePeer != peer) Transport.Disconnect(ActivePeer); // the old connection of a returning guest
            ActivePeer = peer;
            if (_link != null) _link.Peer = peer; // a returning guest: the match goes on over the new connection
            RivalName = string.IsNullOrEmpty(m.Name) ? "J2" : m.Name;
            RivalRacket = m.RacketId;
            NetProtocol.Write(_w, new WelcomeMsg { SessionCode = Machine.SessionCode, HostTick = 0, PlayerIndex = 1 });
            Transport.Send(peer, _out, _w.Length, true);
            Notify();
        }

        void Reject(int peer, RejectReason why)
        {
            NetProtocol.Write(_w, new RejectMsg { Reason = why, Text = RejectText(why) });
            Transport.Send(peer, _out, _w.Length, true);
            _kickAt[peer] = Now + 0.5;
        }

        public static string RejectText(RejectReason r)
        {
            switch (r)
            {
                case RejectReason.ProtocolMismatch:
                case RejectReason.AppVersionMismatch: return "Versiones distintas del juego: instala la misma en los dos móviles.";
                case RejectReason.BalanceMismatch: return "Los ajustes del juego no coinciden: instala la misma versión en los dos móviles.";
                case RejectReason.Full: return "Esa partida ya tiene dos jugadores.";
                case RejectReason.BadSession: return "Esa partida ya no existe.";
                default: return "El anfitrión ha rechazado la conexión.";
            }
        }

        // ============================================================ address

        /// <summary>
        /// This phone's IPv4 address on the local network (what the other player types), or "" if
        /// none. Private addresses (10.x, 172.16-31.x, 192.168.x) of the Wi-Fi interfaces come first
        /// (also the shared hotspot, where the default route may be mobile data); then the address
        /// the system would use to reach the local network (a UDP "connect" sends nothing).
        /// Step 8 adds Android's own interface list, more reliable on recent versions.
        /// </summary>
        public static string LocalIPv4()
        {
            string wifi = null, other = null;
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    string name = (ni.Name ?? "").ToLowerInvariant();
                    bool wifiName = name.StartsWith("wlan") || name.StartsWith("ap") || name.StartsWith("swlan") || name.StartsWith("wl") || name.StartsWith("wi") || name.StartsWith("en") || name.StartsWith("eth");
                    foreach (UnicastIPAddressInformation a in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (a.Address.AddressFamily != AddressFamily.InterNetwork || !IsPrivate(a.Address)) continue;
                        if (wifiName && wifi == null) wifi = a.Address.ToString();
                        else if (other == null) other = a.Address.ToString();
                    }
                }
            }
            catch (Exception) { }
            if (wifi != null) return wifi;
            try
            {
                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("192.168.255.255", 9);
                    IPEndPoint ep = s.LocalEndPoint as IPEndPoint;
                    if (ep != null && IsPrivate(ep.Address)) return ep.Address.ToString();
                }
            }
            catch (Exception) { }
            return other ?? "";
        }

        static bool IsPrivate(IPAddress a)
        {
            byte[] b = a.GetAddressBytes();
            if (b.Length != 4) return false;
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }
    }
}
