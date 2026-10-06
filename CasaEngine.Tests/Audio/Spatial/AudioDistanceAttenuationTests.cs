using CasaEngine.Framework.Audio.Spatial;
using Xunit;

namespace CasaEngine.Tests.Audio.Spatial;

/// <summary>Expected values are recomputed by hand from OpenAL 1.1 specification section 3.4.</summary>
public class AudioDistanceAttenuationTests
{
    private const float Ref = 2f;
    private const float Max = 10f;

    private static float Eval(AudioDistanceModel model, float distance, float rolloff = 1f, float reference = Ref, float max = Max)
    {
        return AudioDistanceAttenuation.Evaluate(model, distance, reference, max, rolloff);
    }

    [Fact]
    public void EnumValues_AreExplicit()
    {
        Assert.Equal(0, (int)AudioDistanceModel.None);
        Assert.Equal(1, (int)AudioDistanceModel.InverseDistance);
        Assert.Equal(2, (int)AudioDistanceModel.InverseDistanceClamped);
        Assert.Equal(3, (int)AudioDistanceModel.LinearDistance);
        Assert.Equal(4, (int)AudioDistanceModel.LinearDistanceClamped);
        Assert.Equal(5, (int)AudioDistanceModel.ExponentDistance);
        Assert.Equal(6, (int)AudioDistanceModel.ExponentDistanceClamped);
        Assert.Equal(0, (int)AudioSpatialMode.None);
        Assert.Equal(1, (int)AudioSpatialMode.Spatial2D);
        Assert.Equal(2, (int)AudioSpatialMode.Spatial3D);
    }

    [Fact]
    public void None_IsOne()
    {
        Assert.Equal(1f, Eval(AudioDistanceModel.None, 100f));
    }

    [Fact]
    public void Inverse_Unclamped()
    {
        // 2 / (2 + 1 * (6 - 2)) = 1/3
        Assert.Equal(1f / 3f, Eval(AudioDistanceModel.InverseDistance, 6f), 5);

        // 2 / (2 + 1 * (1 - 2)) = 2, clamped to 1
        Assert.Equal(1f, Eval(AudioDistanceModel.InverseDistance, 1f));
    }

    [Fact]
    public void InverseClamped_ClampsDistanceToReferenceAndMax()
    {
        Assert.Equal(1f, Eval(AudioDistanceModel.InverseDistanceClamped, 1f));

        // distance 50 -> 10: 2 / (2 + 8) = 0.2
        Assert.Equal(0.2f, Eval(AudioDistanceModel.InverseDistanceClamped, 50f), 5);
    }

    [Fact]
    public void Linear_Unclamped_And_Clamped()
    {
        // 1 - 1 * (6 - 2) / (10 - 2) = 0.5
        Assert.Equal(0.5f, Eval(AudioDistanceModel.LinearDistance, 6f), 5);
        Assert.Equal(0.5f, Eval(AudioDistanceModel.LinearDistanceClamped, 6f), 5);

        // distance 50 -> 10: 1 - 8/8 = 0
        Assert.Equal(0f, Eval(AudioDistanceModel.LinearDistance, 50f));
        Assert.Equal(0f, Eval(AudioDistanceModel.LinearDistanceClamped, 50f));

        // Below the reference distance: unclamped gives 1 - (1-2)/8 = 1.125 -> clamped to 1; clamped model gives 1.
        Assert.Equal(1f, Eval(AudioDistanceModel.LinearDistance, 1f));
        Assert.Equal(1f, Eval(AudioDistanceModel.LinearDistanceClamped, 1f));
    }

    [Fact]
    public void Exponent_Unclamped_And_Clamped()
    {
        // (4 / 2) ^ -2 = 0.25
        Assert.Equal(0.25f, Eval(AudioDistanceModel.ExponentDistance, 4f, rolloff: 2f), 5);
        Assert.Equal(0.25f, Eval(AudioDistanceModel.ExponentDistanceClamped, 4f, rolloff: 2f), 5);

        // Clamped: distance 50 -> 10: (10 / 2) ^ -2 = 0.04
        Assert.Equal(0.04f, Eval(AudioDistanceModel.ExponentDistanceClamped, 50f, rolloff: 2f), 5);

        // Unclamped: (50 / 2) ^ -2 = 0.0016
        Assert.Equal(0.0016f, Eval(AudioDistanceModel.ExponentDistance, 50f, rolloff: 2f), 5);
    }

    [Theory]
    [InlineData(AudioDistanceModel.InverseDistance)]
    [InlineData(AudioDistanceModel.InverseDistanceClamped)]
    [InlineData(AudioDistanceModel.LinearDistance)]
    [InlineData(AudioDistanceModel.LinearDistanceClamped)]
    [InlineData(AudioDistanceModel.ExponentDistance)]
    [InlineData(AudioDistanceModel.ExponentDistanceClamped)]
    public void ZeroRolloff_IsOneForEveryModel(AudioDistanceModel model)
    {
        Assert.Equal(1f, Eval(model, 6f, rolloff: 0f));
        Assert.Equal(1f, Eval(model, 50f, rolloff: 0f));
    }

    [Fact]
    public void NotEvaluable_IsOne()
    {
        // Linear with reference == max divides by zero.
        Assert.Equal(1f, Eval(AudioDistanceModel.LinearDistance, 6f, reference: 5f, max: 5f));
        Assert.Equal(1f, Eval(AudioDistanceModel.LinearDistanceClamped, 6f, reference: 5f, max: 5f));

        // Inverse with ref 2, rolloff 3, distance 1: denominator 2 + 3 * (1 - 2) = -1.
        Assert.Equal(1f, Eval(AudioDistanceModel.InverseDistance, 1f, rolloff: 3f));

        // Exponent with reference 0 divides by zero.
        Assert.Equal(1f, Eval(AudioDistanceModel.ExponentDistance, 6f, reference: 0f));

        // NaN inputs.
        Assert.Equal(1f, Eval(AudioDistanceModel.InverseDistanceClamped, float.NaN));
        Assert.Equal(1f, Eval(AudioDistanceModel.InverseDistanceClamped, 6f, rolloff: float.NaN));
        Assert.Equal(1f, Eval(AudioDistanceModel.LinearDistance, 6f, max: float.NaN));
        Assert.Equal(1f, Eval(AudioDistanceModel.LinearDistance, 6f, reference: float.NaN));
    }

    [Fact]
    public void Evaluate_DoesNotAllocate()
    {
        var sum = 0f;
        sum += Eval(AudioDistanceModel.InverseDistanceClamped, 6f);

        var before = AllocationWindow.Start();
        for (var i = 0; i < 100; i++)
        {
            sum += AudioDistanceAttenuation.Evaluate(AudioDistanceModel.InverseDistanceClamped, i, 2f, 10f, 1f);
            sum += AudioDistanceAttenuation.Evaluate(AudioDistanceModel.ExponentDistance, i, 2f, 10f, 1f);
            sum += AudioDistanceAttenuation.Evaluate(AudioDistanceModel.LinearDistance, i, 2f, 10f, 1f);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(sum > 0f);
    }
}
