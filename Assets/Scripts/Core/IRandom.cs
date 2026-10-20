namespace AutoBattler.Core
{
    public interface IRandom
    {
        // [0, maxExclusive)
        int Next(int maxExclusive);
    }

    // xorshift64* so a seed replays the same everywhere (System.Random isn't guaranteed to)
    public sealed class SeededRandom : IRandom
    {
        private ulong _state;

        public SeededRandom(int seed)
        {
            // splitmix so seeds 1,2,3 don't give similar sequences. state can't be 0
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
