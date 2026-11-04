using System;

namespace Brooklyneir.Net
{
    /// <summary>
    /// Shared match clock for LAN play. The host's clock is the reference; the guest estimates its
    /// offset to it with Ping/Pong (NTP style) and turns host time into ticks (1/240 s by default,
    /// the physics substep). Pure, allocation-free, unit-tested.
    ///
    /// One sample: the guest sends t0 (its clock), the host answers with t1 (host clock), the guest
    /// receives at t2. Round trip = t2 − t0; offset = t1 + round trip / 2 − t2. Wi-Fi delays are
    /// irregular, so of the last N samples only the one with the lowest round trip is trusted: its
    /// error is at most half of that round trip.
    ///
    /// The applied offset never jumps backwards: once synced, changes are slewed at most
    /// MaxSlew seconds per second, so the guest's tick always moves forward (between 0.9× and 1.1×
    /// real time with the default) and never repeats or skips back.
    /// </summary>
    public sealed class ClockSync
    {
        struct Sample { public double Rtt, Offset; }

        readonly Sample[] _samples;
        int _count, _next;
        readonly bool _isHost;
        double _estimate, _applied, _lastUpdate;
        bool _hasUpdate;
        uint _lastTick;

        public readonly int TickRate;
        /// <summary>Largest change of the applied offset per second of real time.</summary>
        public double MaxSlew = 0.1;

        public ClockSync(int tickRate, int samples = 16)
        {
            TickRate = tickRate < 1 ? 240 : tickRate;
            _samples = new Sample[samples < 1 ? 1 : samples];
        }

        /// <summary>The host's own clock: offset 0, always synced.</summary>
        public static ClockSync ForHost(int tickRate)
        {
            return new ClockSync(tickRate, 1, true);
        }

        ClockSync(int tickRate, int samples, bool host) : this(tickRate, samples)
        {
            _isHost = host;
        }

        public bool IsHost { get { return _isHost; } }
        public bool IsSynced { get { return _isHost || _count > 0; } }
        public int SampleCount { get { return _count; } }
        /// <summary>Best (lowest) round trip among the kept samples, seconds. 0 for the host.</summary>
        public double RoundTrip { get; private set; }
        /// <summary>Current best estimate of host clock − local clock (s).</summary>
        public double EstimatedOffset { get { return _estimate; } }
        /// <summary>Offset actually applied (slewed towards the estimate).</summary>
        public double AppliedOffset { get { return _applied; } }

        public void Reset()
        {
            _count = 0;
            _next = 0;
            _estimate = _applied = 0;
            _hasUpdate = false;
            _lastTick = 0;
            RoundTrip = 0;
        }

        /// <summary>Adds a Ping/Pong sample. Samples with a negative round trip (clock glitch) are ignored.</summary>
        public void AddSample(double t0, double t1, double t2)
        {
            if (_isHost) return;
            double rtt = t2 - t0;
            if (!(rtt >= 0) || double.IsInfinity(rtt)) return;
            bool first = _count == 0;
            _samples[_next] = new Sample { Rtt = rtt, Offset = t1 + rtt * 0.5 - t2 };
            _next = (_next + 1) % _samples.Length;
            if (_count < _samples.Length) _count++;
            int best = 0;
            for (int i = 1; i < _count; i++)
                if (_samples[i].Rtt < _samples[best].Rtt) best = i;
            _estimate = _samples[best].Offset;
            RoundTrip = _samples[best].Rtt;
            if (first)
            {
                // The first estimate is taken at once (nothing is shown yet), and the tick may go back
                // this once: before syncing, Tick counted the guest's own clock, which can be minutes
                // ahead of the host's (each phone counts from its own app start).
                _applied = _estimate;
                _lastTick = 0;
            }
        }

        /// <summary>Call once per frame with the local clock: slews the applied offset towards the estimate.</summary>
        public void Update(double localNow)
        {
            if (_isHost || _count == 0) { _lastUpdate = localNow; _hasUpdate = true; return; }
            double dt = _hasUpdate ? localNow - _lastUpdate : 0;
            _lastUpdate = localNow;
            _hasUpdate = true;
            if (dt <= 0) return;
            double step = MaxSlew * dt;
            double diff = _estimate - _applied;
            _applied += diff > step ? step : (diff < -step ? -step : diff);
        }

        /// <summary>Host clock now, seconds.</summary>
        public double HostTime(double localNow)
        {
            return _isHost ? localNow : localNow + _applied;
        }

        /// <summary>Host time converted to a tick, never going back from the previous call.</summary>
        public uint Tick(double localNow)
        {
            double t = HostTime(localNow);
            uint tick = t <= 0 ? 0u : (uint)Math.Floor(t * TickRate);
            if (tick < _lastTick) tick = _lastTick;
            _lastTick = tick;
            return tick;
        }

        /// <summary>Seconds of host time a number of ticks represents.</summary>
        public double TicksToSeconds(long ticks) { return ticks / (double)TickRate; }
    }
}
