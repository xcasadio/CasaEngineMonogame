using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Authoring;

/// <summary>
/// Inclusive two-dimensional range used by particle authoring data.
/// </summary>
public readonly struct Vector2Range : IEquatable<Vector2Range>
{
    public Vector2Range(Vector2 min, Vector2 max)
    {
        ThrowIfInvalid(min, nameof(min));
        ThrowIfInvalid(max, nameof(max));

        Min = new Vector2(MathF.Min(min.X, max.X), MathF.Min(min.Y, max.Y));
        Max = new Vector2(MathF.Max(min.X, max.X), MathF.Max(min.Y, max.Y));
    }

    public Vector2 Min { get; }

    public Vector2 Max { get; }

    public bool IsConstant => Min == Max;

    public Vector2 Sample(ref ParticleRandom random)
        => new(random.NextFloat(Min.X, Max.X), random.NextFloat(Min.Y, Max.Y));

    public Vector2 Clamp(Vector2 value)
        => new(Clamp(value.X, Min.X, Max.X), Clamp(value.Y, Min.Y, Max.Y));

    public static Vector2Range Constant(Vector2 value) => new(value, value);

    public bool Equals(Vector2Range other)
        => Min.Equals(other.Min) && Max.Equals(other.Max);

    public override bool Equals(object obj)
        => obj is Vector2Range other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Min, Max);

    public static bool operator ==(Vector2Range left, Vector2Range right)
        => left.Equals(right);

    public static bool operator !=(Vector2Range left, Vector2Range right)
        => !left.Equals(right);

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static void ThrowIfInvalid(Vector2 value, string parameterName)
    {
        if (float.IsNaN(value.X) || float.IsInfinity(value.X)
            || float.IsNaN(value.Y) || float.IsInfinity(value.Y))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Particle vector ranges require finite values.");
        }
    }
}