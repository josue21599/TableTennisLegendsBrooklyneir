using System;
using System.Collections.Generic;
using Brooklyneir.Net;

namespace Brooklyneir.Tests
{
    /// <summary>
    /// Fake network between two simulated phones, for tests: configurable latency, jitter and loss
    /// on the unreliable channel; the reliable channel is ordered and never loses packets while
    /// the link is up (like Unity Transport's reliable pipeline). A blackout drops everything,
    /// both ways. Deterministic with its seed. Lives with the tests: it is not part of the game.
    /// </summary>
    public sealed class LoopbackLink
    {
        struct Packet { public double Arrival; public byte[] Data; public int Length; public long Order; }

        sealed class End : INetLink
        {
            public LoopbackLink Owner; public int From;
            public void Send(byte[] data, int length, bool reliable) { Owner.Enqueue(From, data, length, reliable); }
        }

        readonly List<Packet>[] _queues = { new List<Packet>(), new List<Packet>() };
        readonly double[] _lastReliable = new double[2];
        readonly Random _rnd;
        long _order;
        public double Now;
        public double LatencySeconds = 0.030;
        public double JitterSeconds = 0.015;
        public double UnreliableLoss = 0.05;
        public bool Blackout;
        public int Sent, Dropped;

        public readonly INetLink A, B;

        public LoopbackLink(int seed)
        {
            _rnd = new Random(seed);
            A = new End { Owner = this, From = 0 };
            B = new End { Owner = this, From = 1 };
        }

        void Enqueue(int from, byte[] data, int length, bool reliable)
        {
            Sent++;
            if (Blackout || (!reliable && _rnd.NextDouble() < UnreliableLoss)) { Dropped++; return; }
            double arrival = Now + LatencySeconds + (_rnd.NextDouble() * 2 - 1) * JitterSeconds;
            if (arrival < Now) arrival = Now;
            if (reliable)
            {
                if (arrival < _lastReliable[from]) arrival = _lastReliable[from]; // ordered
                _lastReliable[from] = arrival;
            }
            byte[] copy = new byte[length];
            Array.Copy(data, copy, length);
            _queues[1 - from].Add(new Packet { Arrival = arrival, Data = copy, Length = length, Order = _order++ });
        }

        /// <summary>Delivers every packet for side `to` (0 = A, 1 = B) that has arrived by Now.</summary>
        public void Deliver(int to, NetMatchSession session, double localTime)
        {
            List<Packet> q = _queues[to];
            q.Sort((x, y) => x.Arrival != y.Arrival ? x.Arrival.CompareTo(y.Arrival) : x.Order.CompareTo(y.Order));
            int n = 0;
            while (n < q.Count && q[n].Arrival <= Now)
            {
                if (!Blackout) session.Receive(q[n].Data, q[n].Length, localTime);
                else Dropped++;
                n++;
            }
            q.RemoveRange(0, n);
        }
    }
}
