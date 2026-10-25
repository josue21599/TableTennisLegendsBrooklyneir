using System;

namespace Brooklyneir.Core
{
    public interface IRandomSource
    {
        /// <summary>Uniform in [0, 1).</summary>
        float Value();
        float Range(float min, float max);
        /// <summary>Integer in [min, maxExclusive).</summary>
        int Range(int min, int maxExclusive);
        /// <summary>Standard normal sample (mean 0, sigma 1), clamped to +-3 sigma.</summary>
        float Gaussian();
    }

    /// <summary>
    /// Small deterministic xorshift generator. Seedable (tournament brackets are reproducible from
    /// the saved seed) and independent of UnityEngine.Random so gameplay logic can be unit-tested.
    /// </summary>
    public class GameRandom : IRandomSource
    {
        uint _state;
        bool _hasSpare;
        float _spare;

        public GameRandom(int seed)
        {
            Reseed(seed);
        }

        public void Reseed(int seed)
        {
            _state = (uint)seed ^ 0x9E3779B9u;
            if (_state == 0) _state = 0x6D2B79F5u;
            _hasSpare = false;
            for (int i = 0; i < 4; i++) NextUInt();
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public float Value()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Value();
        }

        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        public float Gaussian()
        {
            if (_hasSpare)
            {
                _hasSpare = false;
                return _spare;
            }
            float u, v, s;
            do
            {
                u = Value() * 2f - 1f;
                v = Value() * 2f - 1f;
                s = u * u + v * v;
            } while (s >= 1f || s <= 1e-7f);
            float m = (float)Math.Sqrt(-2.0 * Math.Log(s) / s);
            _spare = Clamp3(v * m);
            _hasSpare = true;
            return Clamp3(u * m);
        }

        static float Clamp3(float g)
        {
            return g < -3f ? -3f : (g > 3f ? 3f : g);
        }
    }
}
