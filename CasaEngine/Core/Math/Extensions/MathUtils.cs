namespace CasaEngine.Core.Math.Extensions;

public static class MathUtils
{
    public const float Epsilon = 1e-6f; // Value a 8x higher than 1.19209290E-07F

    public static bool FloatInRange(float value, float min, float max)
    {
        return value >= min && value <= max;
    }

    public static bool NearZero(float value, float epsilon = Epsilon)
        => MathF.Abs(value) <= epsilon;

    public static bool NearEqual(float a, float b, float epsilon = Epsilon)
        => MathF.Abs(a - b) <= epsilon;

    public static bool IsOne(float a)
    {
        return NearZero(a - 1.0f);
    }

    public static bool WithinEpsilon(float a, float b, float epsilon)
    {
        float num = a - b;
        return ((-epsilon <= num) && (num <= epsilon));
    }
}