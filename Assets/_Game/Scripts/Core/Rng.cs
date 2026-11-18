namespace VibeRun.Core
{
    /// <summary>Small deterministic RNG (xorshift32) so a seed gives the same run on every device and platform.</summary>
    public sealed class Rng
    {
        uint _s;

        public Rng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }

        public uint NextUInt()
        {
            uint x = _s;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            _s = x;
            return x;
        }

        /// <summary>[0, max)</summary>
        public int Range(int max) => max <= 0 ? 0 : (int)(NextUInt() % (uint)max);

        public int Range(int min, int max) => min + Range(max - min);

        public float Value() => (NextUInt() & 0xFFFFFF) / (float)0x1000000;

        /// <summary>Stable FNV-1a hash for strings (seed codes, dates).</summary>
        public static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            return h == 0 ? 1u : h;
        }
    }
}
