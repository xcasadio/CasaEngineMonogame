namespace CasaEngine.Framework.Particles.Authoring;

/// <summary>
/// Inclusive floating point range used by particle authoring data.
/// </summary>
public readonly struct FloatRange : IEquatable<FloatRange>
{
    public FloatRange(float min, float max)
    {
        ThrowIfInvalid(min, nameof(min));
        ThrowIfInvalid(max, nameof(max));

        if (min <= max)
        {
            Min = min;
            Max = max;
        }
        else
        {
            Min = max;
            Max = min;
        }
    }

    public float Min { get; }

    public float Max { get; }

    public bool IsConstant => Min == Max;

    public float Sample(ref ParticleRandom random)
        => random.NextFloat(Min, Max);

    public float Clamp(float value)
    {
        if (value < Min)
        {
            return Min;
        }

        return value > Max ? Max : value;
    }

    public static FloatRange Constant(float value) => new(value, value);

    public bool Equals(FloatRange other)
        => Min.Equals(other.Min) && Max.Equals(other.Max);

    public override bool Equals(object obj)
        => obj is FloatRange other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Min, Max);

    public static bool operator ==(FloatRange left, FloatRange right)
        => left.Equals(right);

    public static bool operator !=(FloatRange left, FloatRange right)
        => !left.Equals(right);

    private static void ThrowIfInvalid(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Particle ranges require finite values.");
        }
    }
}