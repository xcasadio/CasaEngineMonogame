namespace CasaEngine.Framework.Particles.Authoring;

/// <summary>
/// Small deterministic pseudo-random generator for particle sampling.
/// </summary>
public struct ParticleRandom
{
    private const uint FallbackSeed = 0x6D2B79F5u;
    private uint _state;

    public ParticleRandom(uint seed)
    {
        _state = seed == 0u ? FallbackSeed : seed;
    }

    public uint State => _state;

    public uint NextUInt()
    {
        uint state = _state;
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        _state = state == 0u ? FallbackSeed : state;
        return _state;
    }

    public float NextFloat01()
        => (NextUInt() & 0x00FFFFFFu) * (1.0f / 16777216.0f);

    public float NextFloat(float min, float max)
    {
        if (float.IsNaN(min) || float.IsInfinity(min))
        {
            throw new ArgumentOutOfRangeException(nameof(min), min, "Particle random ranges require finite values.");
        }

        if (float.IsNaN(max) || float.IsInfinity(max))
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Particle random ranges require finite values.");
        }

        if (min == max)
        {
            return min;
        }

        float normalizedMin = MathF.Min(min, max);
        float normalizedMax = MathF.Max(min, max);
        return normalizedMin + (normalizedMax - normalizedMin) * NextFloat01();
    }
}