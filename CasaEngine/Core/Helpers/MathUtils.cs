namespace CasaEngine.Core.Helpers;

using MathXna = Microsoft.Xna.Framework.MathHelper;

public static class MathUtils
{
    public const float Epsilon = 1e-6f; // Value a 8x higher than 1.19209290E-07F

    public static bool FloatInRange(float value, float min, float max)
    {
        return value >= min && value <= max;
    }

    public static bool NearEqual(float a, float b)
    {
        if (IsZero(a - b))
            return true;

        return Math.Abs(a - b) < Epsilon;
    }

    //public static unsafe bool NearEqual(float a, float b)
    //{
    //    // Check if the numbers are really close -- needed
    //    // when comparing numbers near zero.
    //    if (IsZero(a - b))
    //        return true;
    //
    //    // Original from Bruce Dawson: http://randomascii.wordpress.com/2012/02/25/comparing-floating-point-numbers-2012-edition/
    //    int aInt = *(int*)&a;
    //    int bInt = *(int*)&b;
    //
    //    // Different signs means they do not match.
    //    if ((aInt < 0) != (bInt < 0))
    //        return false;
    //
    //    // Find the difference in ULPs.
    //    int ulp = Math.Abs(aInt - bInt);
    //
    //    // Choose of maxUlp = 4
    //    // according to http://code.google.com/p/googletest/source/browse/trunk/include/gtest/internal/gtest-internal.h
    //    const int maxUlp = 4;
    //    return (ulp <= maxUlp);
    //}

    public static bool IsZero(float a)
    {
        return Math.Abs(a) < Epsilon;
    }

    public static bool IsOne(float a)
    {
        return IsZero(a - 1.0f);
    }

    public static bool WithinEpsilon(float a, float b, float epsilon)
    {
        float num = a - b;
        return ((-epsilon <= num) && (num <= epsilon));
    }

    public static T[] Array<T>(T value, int length)
    {
        var result = new T[length];
        for (var i = 0; i < length; i++)
            result[i] = value;

        return result;
    }


    // CalculateCursorRay Calculates a world space ray starting at the camera's
    // "eye" and pointing in the direction of the cursor. Viewport.Unproject is used
    // to accomplish this. see the accompanying documentation for more explanation
    // of the math behind this function.


    /// <summary>
    /// Interpolates between two values using a cubic equation.
    /// </summary>
    /// <param name="value1">Source value.</param>
    /// <param name="value2">Source value.</param>
    /// <param name="amount">Weighting value.</param>
    /// <returns>Interpolated value.</returns>
    public static float CubicInterpolate(float value1, float value2, float amount)
    {
        amount = MathXna.Clamp(amount, 0f, 1f);
        return MathXna.SmoothStep(value1, value2, amount * amount * (3f - 2f * amount));
    }

    /// <summary>
    /// Returns the angle whose cosine is the specified value.
    /// </summary>
    /// <param name="value">A number representing a cosine.</param>
    /// <returns>The angle whose cosine is the specified value.</returns>
    public static float Acos(float value)
    {
        return (float)Math.Acos(value);
    }

    /// <summary>
    /// Returns the angle whose sine is the specified value.
    /// </summary>
    /// <param name="value">A number representing a sine.</param>
    /// <returns>The angle whose sine is the specified value.</returns>
    public static float Asin(float value)
    {
        return (float)Math.Asin(value);
    }

    /// <summary>
    /// Returns the angle whos tangent is the speicified number.
    /// </summary>
    /// <param name="value">A number representing a tangent.</param>
    /// <returns>The angle whos tangent is the speicified number.</returns>
    public static float Atan(float value)
    {
        return (float)Math.Atan(value);
    }

    /// <summary>
    /// Returns the angle whose tangent is the quotient of the two specified numbers.
    /// </summary>
    /// <param name="y">The y coordinate of a point.</param>
    /// <param name="x">The x coordinate of a point.</param>
    /// <returns>The angle whose tangent is the quotient of the two specified numbers.</returns>
    public static float Atan2(float y, float x)
    {
        return (float)Math.Atan2(y, x);
    }

    /// <summary>
    /// Returns the sine of the specified angle.
    /// </summary>
    /// <param name="value">An angle specified in radians.</param>
    /// <returns>The sine of the specified angle.</returns>
    public static float Sin(float value)
    {
        return (float)Math.Sin(value);
    }

    /// <summary>
    /// Returns the hyperbolic sine of the specified angle.
    /// </summary>
    /// <param name="value">An angle specified in radians.</param>
    /// <returns>The hyperbolic sine of the specified angle.</returns>
    public static float Sinh(float value)
    {
        return (float)Math.Sinh(value);
    }

    /// <summary>
    /// Returns the cosine of the specified angle.
    /// </summary>
    /// <param name="value">An angle specified in radians.</param>
    /// <returns>The cosine of the specified angle.</returns>
    public static float Cos(float value)
    {
        return (float)Math.Cos(value);
    }

    /// <summary>
    /// Returns the hyperbolic cosine of the specified angle.
    /// </summary>
    /// <param name="value">An angle specified in radians.</param>
    /// <returns>The hyperbolic cosine of the specified angle.</returns>
    public static float Cosh(float value)
    {
        return (float)Math.Cosh(value);
    }

    /// <summary>
    /// Returns the tangent of the specified angle.
    /// </summary>
    /// <param name="value">An angle specified in radians.</param>
    /// <returns>The tangent of the specified angle.</returns>
    public static float Tan(float value)
    {
        return (float)Math.Tan(value);
    }

    /// <summary>
    /// Returns the hyperbolic tangent of the specified angle.
    /// </summary>
    /// <param name="value">An angle specified in radians.</param>
    /// <returns>The hyperbolic tangent of the specified angle.</returns>
    public static float Tanh(float value)
    {
        return (float)Math.Tanh(value);
    }

    /// <summary>
    /// Returns the natural (base e) logarithm of the specified value.
    /// </summary>
    /// <param name="value">A number whose logarithm is to be found.</param>
    /// <returns>The natural (base e) logarithm of the specified value.</returns>
    public static float Log(float value)
    {
        return (float)Math.Log(value);
    }

    /// <summary>
    /// Returns the specified value raised to the specified power.
    /// </summary>
    /// <param name="value">Source value.</param>
    /// <param name="power">A single precision floating point number that specifies a power.</param>
    /// <returns>The specified value raised to the specified power.</returns>
    public static float Pow(float value, float power)
    {
        return (float)Math.Pow(value, power);
    }

    /// <summary>
    /// Returns the square root of the specified value.
    /// </summary>
    /// <param name="value">Source value.</param>
    /// <returns>The square root of the specified value.</returns>
    public static float Sqrt(float value)
    {
        return (float)Math.Sqrt(value);
    }
}