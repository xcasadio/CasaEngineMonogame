using CasaEngine.Framework.Animations;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// Exercises <see cref="InertializationMath"/> directly (accessible from this assembly via
/// <c>InternalsVisibleTo</c>) against the boundary conditions of Bollo's quintic offset-decay
/// curve: x(0)=x0, x'(0)=v0, x(T)=x'(T)=x''(T)=0.
/// </summary>
public class InertializationMathTests
{
    [Fact]
    public void Evaluate_AtStart_ReturnsInitialOffset()
    {
        const float x0 = 2.5f;
        const float v0 = -1.5f;
        const float duration = 0.4f;

        InertializationMath.ComputeCoefficients(x0, v0, duration, out var a, out var b, out var c);

        Assert.Equal(x0, InertializationMath.Evaluate(0f, x0, v0, duration, a, b, c), tolerance: 1e-5f);
    }

    [Fact]
    public void Evaluate_DerivativeAtStart_MatchesInitialVelocity()
    {
        const float x0 = 2.5f;
        const float v0 = -1.5f;
        const float duration = 0.4f;
        const float epsilon = 1e-4f;

        InertializationMath.ComputeCoefficients(x0, v0, duration, out var a, out var b, out var c);

        var x0Value = InertializationMath.Evaluate(0f, x0, v0, duration, a, b, c);
        var xEpsilon = InertializationMath.Evaluate(epsilon, x0, v0, duration, a, b, c);
        var measuredVelocity = (xEpsilon - x0Value) / epsilon;

        Assert.Equal(v0, measuredVelocity, tolerance: 1e-2f);
    }

    [Fact]
    public void Evaluate_AtDuration_IsExactlyZero()
    {
        const float x0 = 2.5f;
        const float v0 = -1.5f;
        const float duration = 0.4f;

        InertializationMath.ComputeCoefficients(x0, v0, duration, out var a, out var b, out var c);

        Assert.Equal(0f, InertializationMath.Evaluate(duration, x0, v0, duration, a, b, c));
        Assert.Equal(0f, InertializationMath.Evaluate(duration * 2f, x0, v0, duration, a, b, c));
    }

    [Fact]
    public void Evaluate_DerivativeNearDuration_IsCloseToZero()
    {
        const float x0 = 3f;
        const float v0 = 2f;
        const float duration = 0.5f;
        const float epsilon = 1e-4f;

        InertializationMath.ComputeCoefficients(x0, v0, duration, out var a, out var b, out var c);

        // Evaluate the raw polynomial just below T (Evaluate() itself clamps to 0 at/after T,
        // which would hide the boundary condition being tested here).
        var xNearEnd = InertializationMath.Evaluate(duration - epsilon, x0, v0, duration, a, b, c);
        var xBeforeThat = InertializationMath.Evaluate(duration - 2f * epsilon, x0, v0, duration, a, b, c);
        var measuredVelocity = (xNearEnd - xBeforeThat) / epsilon;

        Assert.True(MathF.Abs(measuredVelocity) < 0.05f, $"Expected near-zero velocity at T, measured {measuredVelocity}.");
    }

    [Fact]
    public void ComputeEffectiveDuration_MovingAwayFromZero_KeepsRequestedDuration()
    {
        // x0 and v0 share a sign: the offset is growing, not closing.
        var effectiveDuration = InertializationMath.ComputeEffectiveDuration(x0: 1f, v0: 1f, requestedDurationSeconds: 0.5f);

        Assert.Equal(0.5f, effectiveDuration);
    }

    [Fact]
    public void ComputeEffectiveDuration_StationaryOffset_KeepsRequestedDuration()
    {
        var effectiveDuration = InertializationMath.ComputeEffectiveDuration(x0: 1f, v0: 0f, requestedDurationSeconds: 0.5f);

        Assert.Equal(0.5f, effectiveDuration);
    }

    [Fact]
    public void ComputeEffectiveDuration_FastClosingOffset_IsClampedShorterThanRequested()
    {
        // Closing time estimate: -5 * x0 / v0 = -5 * 1 / -100 = 0.05, well under the requested 1s.
        var effectiveDuration = InertializationMath.ComputeEffectiveDuration(x0: 1f, v0: -100f, requestedDurationSeconds: 1f);

        Assert.True(effectiveDuration < 1f, $"Expected the duration to be clamped shorter, got {effectiveDuration}.");
        Assert.Equal(0.05f, effectiveDuration, tolerance: 1e-4f);
    }

    [Fact]
    public void ComputeEffectiveDuration_SlowClosingOffset_IsNotClampedBelowRequested()
    {
        // Closing time estimate: -5 * x0 / v0 = -5 * 1 / -1 = 5s, longer than the requested 1s.
        var effectiveDuration = InertializationMath.ComputeEffectiveDuration(x0: 1f, v0: -1f, requestedDurationSeconds: 1f);

        Assert.Equal(1f, effectiveDuration);
    }
}
