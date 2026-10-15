namespace MonkeyPost.Core
{
    /// <summary>Small seeded RNG (xorshift64*) so matches and bots are reproducible in tests.</summary>
    public sealed class DeterministicRandom
    {
        ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = Mix(seed);
            if (_state == 0) _state = 0x9E3779B97F4A7C15UL;
        }

        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 2685821657736338717UL;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / (1 << 24));

        /// <summary>Uniform in [0, maxExclusive).</summary>
        public int Next(int maxExclusive) => (int)(NextULong() % (ulong)maxExclusive);

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public bool Chance(float probability) => NextFloat() < probability;

        /// <summary>SplitMix64 finaliser, used to derive well-spread seeds.</summary>
        public static ulong Mix(ulong value)
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
