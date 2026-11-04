// Declarations of the Unity Transport 2.x / Collections API used by Net/LanTransport.cs, only to
// type-check it with Mono (compiled with -define:BROOKLYNEIR_TRANSPORT). Not functional. The real
// With*Parameters extensions take "ref this" (Mono's mcs does not support it, so here it is "this").
using System;

namespace Unity.Collections
{
    public enum Allocator { Invalid = 0, None = 1, Temp = 2, TempJob = 3, Persistent = 4 }

    public struct NativeArray<T> : IDisposable where T : struct
    {
        public NativeArray(int length, Allocator allocator) { }
        public int Length { get { return 0; } }
        public bool IsCreated { get { return false; } }
        public NativeArray<T> GetSubArray(int start, int length) { return this; }
        public void Dispose() { }
        public static void Copy(T[] src, int srcIndex, NativeArray<T> dst, int dstIndex, int length) { }
        public static void Copy(NativeArray<T> src, int srcIndex, T[] dst, int dstIndex, int length) { }
    }

    public struct DataStreamWriter
    {
        public bool HasFailedWrites { get { return false; } }
        public bool WriteBytes(NativeArray<byte> value) { return true; }
    }

    public struct DataStreamReader
    {
        public int Length { get { return 0; } }
        public void ReadBytes(NativeArray<byte> array) { }
        public byte ReadByte() { return 0; }
    }
}

namespace Unity.Jobs
{
    public struct JobHandle { public void Complete() { } }
}

namespace Unity.Networking.Transport
{
    using Unity.Collections;
    using Unity.Jobs;

    public enum NetworkFamily { Invalid = 0, Ipv4 = 2, Ipv6 = 23 }

    public struct NetworkEndpoint
    {
        public static NetworkEndpoint AnyIpv4 { get { return new NetworkEndpoint(); } }
        public NetworkEndpoint WithPort(ushort port) { return this; }
        public static bool TryParse(string address, ushort port, out NetworkEndpoint endpoint, NetworkFamily family = NetworkFamily.Ipv4) { endpoint = new NetworkEndpoint(); return true; }
        public ushort Port { get { return 0; } }
    }

    public struct NetworkConnection : IEquatable<NetworkConnection>
    {
        public enum State { Disconnected, Disconnecting, Connecting, Connected }
        public bool IsCreated { get { return false; } }
        public bool Equals(NetworkConnection o) { return true; }
        public static bool operator ==(NetworkConnection a, NetworkConnection b) { return true; }
        public static bool operator !=(NetworkConnection a, NetworkConnection b) { return false; }
        public override bool Equals(object o) { return true; }
        public override int GetHashCode() { return 0; }
    }

    public struct NetworkPipeline { public static NetworkPipeline Null { get { return new NetworkPipeline(); } } }

    public struct NetworkEvent { public enum Type : short { Empty = 0, Data, Connect, Disconnect } }

    public struct ReliableSequencedPipelineStage { }

    public struct NetworkSettings : IDisposable { public void Dispose() { } }

    public static class NetworkConfigParameterExtensions
    {
        public static NetworkSettings WithNetworkConfigParameters(this NetworkSettings settings, int connectTimeoutMS = 1000, int maxConnectAttempts = 60, int disconnectTimeoutMS = 30000, int heartbeatTimeoutMS = 500, int reconnectionTimeoutMS = 2000, int maxFrameTimeMS = 0, int fixedFrameTimeMS = 0, int receiveQueueCapacity = 512, int sendQueueCapacity = 512, int maxMessageSize = 1400) { return settings; }
    }

    public struct NetworkDriver : IDisposable
    {
        public static NetworkDriver Create(NetworkSettings settings) { return new NetworkDriver(); }
        public bool IsCreated { get { return false; } }
        public bool Listening { get { return false; } }
        public NetworkPipeline CreatePipeline(params Type[] stages) { return new NetworkPipeline(); }
        public int Bind(NetworkEndpoint endpoint) { return 0; }
        public int Listen() { return 0; }
        public NetworkConnection Accept() { return new NetworkConnection(); }
        public NetworkConnection Connect(NetworkEndpoint endpoint) { return new NetworkConnection(); }
        public int Disconnect(NetworkConnection connection) { return 0; }
        public JobHandle ScheduleUpdate() { return new JobHandle(); }
        public JobHandle ScheduleFlushSend() { return new JobHandle(); }
        public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader) { reader = new DataStreamReader(); return NetworkEvent.Type.Empty; }
        public int BeginSend(NetworkPipeline pipe, NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0) { writer = new DataStreamWriter(); return 0; }
        public int EndSend(DataStreamWriter writer) { return 0; }
        public void AbortSend(DataStreamWriter writer) { }
        public NetworkConnection.State GetConnectionState(NetworkConnection connection) { return NetworkConnection.State.Connected; }
        public void Dispose() { }
    }
}
