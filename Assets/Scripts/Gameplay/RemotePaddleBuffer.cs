using UnityEngine;

namespace Brooklyneir.Gameplay
{
    /// <summary>
    /// Racket positions received from the other phone, each with a sequence number and a time
    /// (seconds on the shared match clock). The racket is drawn a little in the past (render time
    /// = now - delay) and interpolated between the two samples around that time, so it moves
    /// smoothly even when packets are lost. Repeated or out-of-order packets are dropped,
    /// extrapolation past the newest sample is limited, and a long gap is never slid across:
    /// the racket waits and then jumps cleanly. Fixed-size ring, no allocations. Pure, unit-tested.
    /// </summary>
    public class RemotePaddleBuffer
    {
        struct Sample { public uint Seq; public double Time; public Vector3 Pos; }

        readonly Sample[] _ring;
        int _start, _count;
        bool _hasSeq;
        uint _lastSeq;

        /// <summary>Longest extrapolation past the newest sample (s).</summary>
        public float MaxExtrapolation = 0.1f;
        /// <summary>Two samples further apart than this (s) are not interpolated between.</summary>
        public float MaxGap = 0.25f;

        public RemotePaddleBuffer(int capacity = 32)
        {
            _ring = new Sample[capacity < 4 ? 4 : capacity];
        }

        public int Count { get { return _count; } }
        public int Dropped { get; private set; }

        public void Clear()
        {
            _start = 0;
            _count = 0;
            _hasSeq = false;
            Dropped = 0;
        }

        /// <summary>Adds a sample. Returns false (and counts it) if it is repeated, older than the newest one, or out of order.</summary>
        public bool Push(uint seq, double time, Vector3 position)
        {
            if (_hasSeq && (int)(seq - _lastSeq) <= 0) { Dropped++; return false; }
            if (_count > 0 && time <= Get(_count - 1).Time) { Dropped++; return false; }
            _hasSeq = true;
            _lastSeq = seq;
            if (_count == _ring.Length)
            {
                _start = (_start + 1) % _ring.Length;
                _count--;
            }
            _ring[(_start + _count) % _ring.Length] = new Sample { Seq = seq, Time = time, Pos = position };
            _count++;
            return true;
        }

        Sample Get(int i) { return _ring[(_start + i) % _ring.Length]; }

        /// <summary>Position to draw at <paramref name="renderTime"/>. False if nothing has arrived yet.</summary>
        public bool TrySample(double renderTime, out Vector3 position)
        {
            position = Vector3.zero;
            if (_count == 0) return false;
            Sample first = Get(0);
            if (renderTime <= first.Time) { position = first.Pos; return true; }
            for (int i = 0; i < _count - 1; i++)
            {
                Sample a = Get(i), b = Get(i + 1);
                if (renderTime > b.Time) continue;
                double span = b.Time - a.Time;
                if (span > MaxGap) { position = renderTime >= b.Time ? b.Pos : a.Pos; return true; } // wait across a gap, then jump
                float k = (float)((renderTime - a.Time) / span);
                position = Vector3.Lerp(a.Pos, b.Pos, k);
                return true;
            }
            Sample last = Get(_count - 1);
            position = last.Pos;
            if (_count >= 2)
            {
                Sample prev = Get(_count - 2);
                double span = last.Time - prev.Time;
                if (span > 1e-6 && span <= MaxGap)
                {
                    Vector3 v = (last.Pos - prev.Pos) / (float)span;
                    float ahead = Mathf.Min((float)(renderTime - last.Time), MaxExtrapolation);
                    position = last.Pos + v * ahead;
                }
            }
            return true;
        }
    }
}
