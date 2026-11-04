using System;
#if BROOKLYNEIR_TRANSPORT
using Unity.Collections;
using Unity.Networking.Transport;
#endif

namespace Brooklyneir.Net
{
    /// <summary>What the transport reports, once per Poll, in arrival order.</summary>
    public interface ILanTransportListener
    {
        void OnLanConnected(int peer);
        void OnLanDisconnected(int peer);
        void OnLanData(int peer, byte[] data, int length);
    }

    /// <summary>
    /// The two phones' connection over the local network, with Unity Transport (UDP): a reliable
    /// and ordered channel for events (hits, points, powers, pauses) and an unreliable one for
    /// what is sent many times per second (racket positions, pings). The host listens and keeps
    /// up to MaxPeers connections (the guest, plus a guest coming back while the old connection
    /// has not timed out yet); the guest connects by IP address.
    ///
    /// Testing aid (net.json): simulatedLatencyMs delays every packet this phone sends, and
    /// simulatedLossPercent drops that share of the unreliable ones (the reliable channel would
    /// resend them anyway). Both are 0 in the game. No allocation per frame: packets are copied
    /// into preallocated buffers.
    ///
    /// Without the Unity Transport package (define BROOKLYNEIR_TRANSPORT, set by the asmdef
    /// versionDefines) the class still compiles and Available is false.
    /// </summary>
    public sealed class LanTransport : IDisposable
    {
        public const int MaxPeers = 2;
        const int DelayedCapacity = 256;

        /// <summary>One-way delay added to every packet this phone sends (ms).</summary>
        public int SimulatedLatencyMs;
        /// <summary>Share of unreliable packets this phone drops on purpose (0..100).</summary>
        public float SimulatedLossPercent;

        public bool IsHost { get; private set; }
        public bool Running { get; private set; }

        readonly byte[] _in = new byte[NetProtocol.MaxPacket];
        readonly Random _lossRng = new Random(12345);

        // Delayed packets (simulated latency): a ring of fixed buffers.
        readonly byte[][] _delayedData = new byte[DelayedCapacity][];
        readonly double[] _delayedDue = new double[DelayedCapacity];
        readonly int[] _delayedPeer = new int[DelayedCapacity];
        readonly int[] _delayedLength = new int[DelayedCapacity];
        readonly bool[] _delayedReliable = new bool[DelayedCapacity];
        int _delayedHead, _delayedCount;
        double _now;

        public LanTransport()
        {
            for (int i = 0; i < DelayedCapacity; i++) _delayedData[i] = new byte[NetProtocol.MaxPacket];
        }

        public static bool Available
        {
#if BROOKLYNEIR_TRANSPORT
            get { return true; }
#else
            get { return false; }
#endif
        }

#if BROOKLYNEIR_TRANSPORT
        NetworkDriver _driver;
        NetworkPipeline _reliable;
        readonly NetworkConnection[] _peers = new NetworkConnection[MaxPeers];
        readonly bool[] _connected = new bool[MaxPeers];
        NativeArray<byte> _native;

        NetworkDriver CreateDriver()
        {
            NetworkSettings settings = new NetworkSettings();
            // Connect: 10 tries, 0.5 s apart. A connection with no traffic for 4 s is closed (the
            // match notices a silence much earlier: net.json silenceTimeoutSeconds). The reliable channel
            // keeps its default window (32 packets in flight): events are a few per second.
            settings.WithNetworkConfigParameters(connectTimeoutMS: 500, maxConnectAttempts: 10, disconnectTimeoutMS: 4000, heartbeatTimeoutMS: 250);
            NetworkDriver d = NetworkDriver.Create(settings);
            _reliable = d.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            if (!_native.IsCreated) _native = new NativeArray<byte>(NetProtocol.MaxPacket, Allocator.Persistent);
            return d;
        }
#endif

        /// <summary>Host: listen on this port. False with a reason if it cannot.</summary>
        public bool StartHost(int port, out string error)
        {
            Stop();
            error = null;
#if BROOKLYNEIR_TRANSPORT
            _driver = CreateDriver();
            if (_driver.Bind(NetworkEndpoint.AnyIpv4.WithPort((ushort)port)) != 0)
            {
                error = "No se pudo abrir el puerto " + port + ".";
                Stop();
                return false;
            }
            if (_driver.Listen() != 0)
            {
                error = "No se pudo esperar conexiones en el puerto " + port + ".";
                Stop();
                return false;
            }
            IsHost = true;
            Running = true;
            return true;
#else
            error = NotInstalled;
            return false;
#endif
        }

        /// <summary>Guest: connect to the host's address (e.g. "192.168.1.23"). The result comes later through Poll.</summary>
        public bool StartClient(string address, int port, out string error)
        {
            Stop();
            error = null;
#if BROOKLYNEIR_TRANSPORT
            NetworkEndpoint ep;
            if (!NetworkEndpoint.TryParse(address, (ushort)port, out ep, NetworkFamily.Ipv4))
            {
                error = "La dirección «" + address + "» no es válida.";
                return false;
            }
            _driver = CreateDriver();
            _peers[0] = _driver.Connect(ep);
            _connected[0] = false;
            IsHost = false;
            Running = true;
            return true;
#else
            error = NotInstalled;
            return false;
#endif
        }

        public const string NotInstalled = "Falta el paquete Unity Transport (Package Manager).";

        /// <summary>Call once per frame: sends due delayed packets, accepts guests and reports events.</summary>
        public void Poll(double now, ILanTransportListener listener)
        {
            _now = now;
#if BROOKLYNEIR_TRANSPORT
            if (!Running) return;
            FlushDelayed(now);
            _driver.ScheduleUpdate().Complete();

            if (IsHost)
            {
                NetworkConnection c;
                while ((c = _driver.Accept()) != default(NetworkConnection))
                {
                    int slot = FreeSlot();
                    if (slot < 0) { _driver.Disconnect(c); continue; }
                    _peers[slot] = c;
                    _connected[slot] = true;
                    listener.OnLanConnected(slot);
                }
            }

            for (int p = 0; p < MaxPeers; p++)
            {
                if (!_peers[p].IsCreated) continue;
                DataStreamReader reader;
                NetworkEvent.Type type;
                while (_peers[p].IsCreated && (type = _driver.PopEventForConnection(_peers[p], out reader)) != NetworkEvent.Type.Empty)
                {
                    switch (type)
                    {
                        case NetworkEvent.Type.Connect:
                            _connected[p] = true;
                            listener.OnLanConnected(p);
                            break;
                        case NetworkEvent.Type.Data:
                        {
                            int len = reader.Length;
                            if (len <= 0 || len > NetProtocol.MaxPacket) break;
                            NativeArray<byte> slice = _native.GetSubArray(0, len);
                            reader.ReadBytes(slice);
                            NativeArray<byte>.Copy(slice, 0, _in, 0, len);
                            listener.OnLanData(p, _in, len);
                            break;
                        }
                        case NetworkEvent.Type.Disconnect:
                            _peers[p] = default(NetworkConnection);
                            _connected[p] = false;
                            listener.OnLanDisconnected(p);
                            break;
                    }
                }
            }
#endif
        }

#if BROOKLYNEIR_TRANSPORT
        int FreeSlot()
        {
            for (int i = 0; i < MaxPeers; i++) if (!_peers[i].IsCreated) return i;
            return -1;
        }
#endif

        public bool IsConnected(int peer)
        {
#if BROOKLYNEIR_TRANSPORT
            return Running && peer >= 0 && peer < MaxPeers && _connected[peer];
#else
            return false;
#endif
        }

        public void Send(int peer, byte[] data, int length, bool reliable)
        {
            if (!Running || length <= 0 || length > NetProtocol.MaxPacket) return;
            if (!reliable && SimulatedLossPercent > 0f && _lossRng.NextDouble() * 100.0 < SimulatedLossPercent) return;
            if (SimulatedLatencyMs > 0)
            {
                if (_delayedCount >= DelayedCapacity) return; // a full queue behaves like a lost packet
                int i = (_delayedHead + _delayedCount) % DelayedCapacity;
                Buffer.BlockCopy(data, 0, _delayedData[i], 0, length);
                _delayedLength[i] = length;
                _delayedPeer[i] = peer;
                _delayedReliable[i] = reliable;
                _delayedDue[i] = _now + SimulatedLatencyMs / 1000.0;
                _delayedCount++;
                return;
            }
            SendNow(peer, data, length, reliable);
        }

        void FlushDelayed(double now)
        {
            while (_delayedCount > 0 && _delayedDue[_delayedHead] <= now)
            {
                int i = _delayedHead;
                SendNow(_delayedPeer[i], _delayedData[i], _delayedLength[i], _delayedReliable[i]);
                _delayedHead = (_delayedHead + 1) % DelayedCapacity;
                _delayedCount--;
            }
        }

        void SendNow(int peer, byte[] data, int length, bool reliable)
        {
#if BROOKLYNEIR_TRANSPORT
            if (!Running || peer < 0 || peer >= MaxPeers || !_connected[peer]) return;
            DataStreamWriter w;
            if (_driver.BeginSend(reliable ? _reliable : NetworkPipeline.Null, _peers[peer], out w) != 0) return;
            NativeArray<byte> slice = _native.GetSubArray(0, length);
            NativeArray<byte>.Copy(data, 0, slice, 0, length);
            if (!w.WriteBytes(slice)) { _driver.AbortSend(w); return; }
            _driver.EndSend(w);
#endif
        }

        /// <summary>Closes one connection (e.g. a rejected guest, after its Reject has gone out).</summary>
        public void Disconnect(int peer)
        {
#if BROOKLYNEIR_TRANSPORT
            if (!Running || peer < 0 || peer >= MaxPeers || !_peers[peer].IsCreated) return;
            _driver.Disconnect(_peers[peer]);
            _peers[peer] = default(NetworkConnection);
            _connected[peer] = false;
#endif
        }

        /// <summary>Sends what is queued right now (before closing, so a last Bye goes out).</summary>
        public void Flush()
        {
#if BROOKLYNEIR_TRANSPORT
            if (!Running) return;
            FlushDelayed(double.MaxValue);
            _driver.ScheduleUpdate().Complete();
#endif
        }

        /// <summary>Sends this frame's packets now instead of at the next Poll (call late in the frame).</summary>
        public void FlushSends()
        {
#if BROOKLYNEIR_TRANSPORT
            if (!Running) return;
            FlushDelayed(_now);
            _driver.ScheduleFlushSend().Complete();
#endif
        }

        public void Stop()
        {
            _delayedCount = 0;
            _delayedHead = 0;
#if BROOKLYNEIR_TRANSPORT
            if (_driver.IsCreated)
            {
                for (int i = 0; i < MaxPeers; i++)
                {
                    if (_peers[i].IsCreated) _driver.Disconnect(_peers[i]);
                    _peers[i] = default(NetworkConnection);
                    _connected[i] = false;
                }
                _driver.ScheduleUpdate().Complete();
                _driver.Dispose();
            }
            _driver = default(NetworkDriver);
#endif
            Running = false;
        }

        public void Dispose()
        {
            Stop();
#if BROOKLYNEIR_TRANSPORT
            if (_native.IsCreated) _native.Dispose();
#endif
        }
    }

    /// <summary>The match's link to the other phone over a LanTransport connection.</summary>
    public sealed class LanPeerLink : INetLink
    {
        readonly LanTransport _transport;
        public int Peer;

        public LanPeerLink(LanTransport transport, int peer) { _transport = transport; Peer = peer; }

        public void Send(byte[] data, int length, bool reliable) { _transport.Send(Peer, data, length, reliable); }
    }
}
