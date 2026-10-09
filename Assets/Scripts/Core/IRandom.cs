namespace AutoBattler.Core
{
    public interface IRandom
    {
        /// <summary>Returns an integer in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }

    /// <summary>Deterministic xorshift64* generator so runs are reproducible across platforms.</summary>
    public sealed class SeededRandom : IRandom
    {
        private ulong _state;

        public SeededRandom(int seed)
        {
            // SplitMix64 scramble so nearby seeds produce unrelated sequences; state must be non-zero.
            ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            _state = (z ^ (z >> 31)) | 1UL;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new System.ArgumentOutOfRangeException(nameof(maxExclusive));
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            ulong value = _state * 0x2545F4914F6CDD1DUL;
            return (int)((value >> 33) % (ulong)maxExclusive);
        }
    }
}
