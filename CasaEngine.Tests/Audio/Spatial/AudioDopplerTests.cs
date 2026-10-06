using System.Numerics;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;

namespace CasaEngine.Tests.Audio.Spatial;

/// <summary>Expected values recomputed from OpenAL 1.1 specification section 3.5.2.</summary>
public class AudioDopplerTests
{
    private const float Ss = 343.3f;

    // Listener at the origin, source 100 units away on -X: SL = listener - source = (+100, 0, 0).
    private static readonly Vector3 SourcePosition = new(-100f, 0f, 0f);

    private static float Ratio(Vector3 listenerVelocity, Vector3 sourceVelocity, float dopplerFactor = 1f, float speedOfSound = Ss,
        AudioSpatialMode mode = AudioSpatialMode.Spatial3D)
    {
        return AudioDoppler.Ratio(mode, Vector3.Zero, listenerVelocity, SourcePosition, sourceVelocity, speedOfSound, dopplerFactor);
    }

    [Fact]
    public void Constants()
    {
        Assert.Equal(343.3f, AudioDoppler.DefaultSpeedOfSound);
        Assert.Equal(0.25f, AudioDoppler.MinRatio);
        Assert.Equal(4f, AudioDoppler.MaxRatio);
    }

    [Fact]
    public void SourceApproaching()
    {
        // vss = 34.33: 343.3 / (343.3 - 34.33) = 1.111111
        Assert.Equal(1.111111f, Ratio(Vector3.Zero, new Vector3(34.33f, 0f, 0f)), 4);
    }

    [Fact]
    public void SourceReceding()
    {
        // vss = -34.33: 343.3 / (343.3 + 34.33) = 0.909091
        Assert.Equal(0.909091f, Ratio(Vector3.Zero, new Vector3(-34.33f, 0f, 0f)), 4);
    }

    [Fact]
    public void ListenerApproaching()
    {
        // The listener moves toward the source (-X): vls = -34.33: (343.3 + 34.33) / 343.3 = 1.1
        Assert.Equal(1.1f, Ratio(new Vector3(-34.33f, 0f, 0f), Vector3.Zero), 4);
    }

    [Fact]
    public void ListenerReceding()
    {
        // vls = +34.33: (343.3 - 34.33) / 343.3 = 0.9
        Assert.Equal(0.9f, Ratio(new Vector3(34.33f, 0f, 0f), Vector3.Zero), 4);
    }

    [Fact]
    public void Combined()
    {
        // Source approaching at 34.33 and listener approaching at 34.33: (343.3 + 34.33) / (343.3 - 34.33)
        var expected = (343.3f + 34.33f) / (343.3f - 34.33f);

        Assert.Equal(expected, Ratio(new Vector3(-34.33f, 0f, 0f), new Vector3(34.33f, 0f, 0f)), 4);
    }

    [Fact]
    public void DopplerFactorTwo_Strengthens()
    {
        // DF 2, vss 34.33: 343.3 / (343.3 - 68.66) = 1.25
        Assert.Equal(1.25f, Ratio(Vector3.Zero, new Vector3(34.33f, 0f, 0f), dopplerFactor: 2f), 4);
    }

    [Fact]
    public void NonPositiveDopplerFactorOrSpeedOfSound_IsOne()
    {
        var sv = new Vector3(34.33f, 0f, 0f);

        Assert.Equal(1f, Ratio(Vector3.Zero, sv, dopplerFactor: 0f));
        Assert.Equal(1f, Ratio(Vector3.Zero, sv, dopplerFactor: -1f));
        Assert.Equal(1f, Ratio(Vector3.Zero, sv, speedOfSound: 0f));
        Assert.Equal(1f, Ratio(Vector3.Zero, sv, speedOfSound: -5f));
    }

    [Fact]
    public void SourceFasterThanBound_GivesMaxRatio()
    {
        // vss = 1000 is bounded to SS/DF = 343.3, the denominator is 0: engine bound 4.
        Assert.Equal(4f, Ratio(Vector3.Zero, new Vector3(1000f, 0f, 0f)));
    }

    [Fact]
    public void ResultIsClamped()
    {
        // Listener receding at 340: (343.3 - 340) / 343.3 = 0.0096 -> clamped to 0.25.
        Assert.Equal(0.25f, Ratio(new Vector3(340f, 0f, 0f), Vector3.Zero), 5);

        // Source approaching at 340: 343.3 / 3.3 = 104 -> clamped to 4.
        Assert.Equal(4f, Ratio(Vector3.Zero, new Vector3(340f, 0f, 0f)), 5);
    }

    [Fact]
    public void NoneMode_ZeroDistance_NonFinite_AreOne()
    {
        var sv = new Vector3(34.33f, 0f, 0f);

        Assert.Equal(1f, Ratio(Vector3.Zero, sv, mode: AudioSpatialMode.None));
        Assert.Equal(1f, AudioDoppler.Ratio(AudioSpatialMode.Spatial3D, Vector3.Zero, Vector3.Zero, Vector3.Zero, sv, Ss, 1f));
        Assert.Equal(1f, Ratio(Vector3.Zero, new Vector3(float.NaN, 0f, 0f)));
        Assert.Equal(1f, Ratio(Vector3.Zero, sv, dopplerFactor: float.NaN));
        Assert.Equal(1f, AudioDoppler.Ratio(AudioSpatialMode.Spatial3D, Vector3.Zero, Vector3.Zero,
            new Vector3(float.PositiveInfinity, 0f, 0f), sv, Ss, 1f));
    }

    [Fact]
    public void TwoD_IgnoresZ()
    {
        // A velocity along Z only is invisible in 2D, a Z offset of the source does not change the projection.
        Assert.Equal(1f, Ratio(Vector3.Zero, new Vector3(0f, 0f, 500f), mode: AudioSpatialMode.Spatial2D));

        var ratio = AudioDoppler.Ratio(AudioSpatialMode.Spatial2D, Vector3.Zero, Vector3.Zero,
            new Vector3(-100f, 0f, 9000f), new Vector3(34.33f, 0f, 700f), Ss, 1f);
        Assert.Equal(1.111111f, ratio, 4);
    }

    [Fact]
    public void Ratio_DoesNotAllocate()
    {
        var sum = Ratio(Vector3.Zero, new Vector3(34.33f, 0f, 0f));
        var sv = new Vector3(34.33f, 0f, 0f);

        var before = AllocationWindow.Start();
        for (var i = 0; i < 100; i++)
        {
            sum += AudioDoppler.Ratio(AudioSpatialMode.Spatial3D, Vector3.Zero, Vector3.Zero, SourcePosition, sv, Ss, 1f);
            sum += AudioDoppler.Ratio(AudioSpatialMode.Spatial2D, Vector3.Zero, sv, SourcePosition, sv, Ss, 2f);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(sum > 0f);
    }
}
