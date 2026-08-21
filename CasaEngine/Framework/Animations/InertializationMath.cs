namespace CasaEngine.Framework.Animations;

/// <summary>
/// Scalar-channel "offset decay" math for pose inertialization, after David Bollo,
/// "Inertialization: High-Performance Animation Transitions in Gears of War" (GDC 2018).
/// Operates on a single scalar channel: a starting offset <c>x0</c> from the target value,
/// with velocity <c>v0</c>, decaying smoothly to zero (with zero velocity and zero
/// acceleration) by the end of the transition.
/// </summary>
internal static class InertializationMath
{
    private const float MinDurationSeconds = 1e-4f;

    /// <summary>
    /// Computes the duration actually used for the decay curve of one scalar channel.
    /// If the offset is moving away from zero (or stationary), the full requested duration is
    /// used. If it is moving back towards zero fast enough to reach it (and overshoot) before
    /// the requested duration elapses, the duration is shortened so the curve settles at zero
    /// instead of overshooting and oscillating back &mdash; matching Bollo's
    /// <c>T = min(T, -5 * x0 / v0)</c> heuristic.
    /// </summary>
    public static float ComputeEffectiveDuration(float x0, float v0, float requestedDurationSeconds)
    {
        if (requestedDurationSeconds <= 0f)
        {
            return 0f;
        }

        if (MathF.Abs(v0) <= float.Epsilon || v0 * x0 > 0f)
        {
            return requestedDurationSeconds;
        }

        var closingTimeSeconds = -5f * x0 / v0;
        return Math.Clamp(closingTimeSeconds, MinDurationSeconds, requestedDurationSeconds);
    }

    /// <summary>
    /// Computes the quintic polynomial coefficients for <c>x(t) = A t^5 + B t^4 + C t^3 + v0 t + x0</c>
    /// (the acceleration term is assumed zero at t=0), constrained so that
    /// <c>x(0) = x0</c>, <c>x'(0) = v0</c>, <c>x''(0) = 0</c>, and
    /// <c>x(T) = x'(T) = x''(T) = 0</c>.
    /// </summary>
    public static void ComputeCoefficients(float x0, float v0, float durationSeconds, out float a, out float b, out float c)
    {
        if (durationSeconds <= 0f)
        {
            a = 0f;
            b = 0f;
            c = 0f;
            return;
        }

        var t2 = durationSeconds * durationSeconds;
        var t3 = t2 * durationSeconds;
        var t4 = t3 * durationSeconds;
        var t5 = t4 * durationSeconds;

        a = -(6f * v0 * durationSeconds + 12f * x0) / (2f * t5);
        b = (16f * v0 * durationSeconds + 30f * x0) / (2f * t4);
        c = -(12f * v0 * durationSeconds + 20f * x0) / (2f * t3);
    }

    /// <summary>
    /// Evaluates the decay curve at time <paramref name="t"/>. Returns exactly zero once
    /// <paramref name="t"/> reaches <paramref name="durationSeconds"/>, rather than
    /// extrapolating the polynomial past its constrained range.
    /// </summary>
    public static float Evaluate(float t, float x0, float v0, float durationSeconds, float a, float b, float c)
    {
        if (durationSeconds <= 0f || t >= durationSeconds)
        {
            return 0f;
        }

        if (t <= 0f)
        {
            return x0;
        }

        var t2 = t * t;
        var t3 = t2 * t;
        var t4 = t3 * t;
        var t5 = t4 * t;
        return a * t5 + b * t4 + c * t3 + v0 * t + x0;
    }
}
