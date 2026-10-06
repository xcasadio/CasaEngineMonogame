using System.Numerics;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;

namespace CasaEngine.Tests.Audio.Spatial;

public class AudioSpatialMathTests
{
    private static readonly AudioListenerPose Origin = AudioListenerPose.Default;

    [Theory]
    [InlineData(10f, 0f, 0f, 1f)]
    [InlineData(-10f, 0f, 0f, -1f)]
    [InlineData(0f, 0f, -10f, 0f)]
    [InlineData(0f, 0f, 10f, 0f)]
    [InlineData(7.07f, 0f, -7.07f, 0.7071f)]
    public void Pan_ListenerAtOriginFacingMinusZ(float x, float y, float z, float expected)
    {
        Assert.Equal(expected, AudioSpatialMath.Pan(AudioSpatialMode.Spatial3D, Origin, new Vector3(x, y, z)), 3);
    }

    [Fact]
    public void Pan_ListenerRotatedNinetyDegreesAboutY()
    {
        var pose = AudioListenerPose.Create(Vector3.Zero, new Vector3(-1f, 0f, 0f), new Vector3(0f, 1f, 0f));

        Assert.Equal(1f, AudioSpatialMath.Pan(AudioSpatialMode.Spatial3D, pose, new Vector3(0f, 0f, -10f)), 4);
        Assert.Equal(0f, AudioSpatialMath.Pan(AudioSpatialMode.Spatial3D, pose, new Vector3(-10f, 0f, 0f)), 4);
    }

    [Fact]
    public void Pan_UsesListenerPosition()
    {
        var pose = AudioListenerPose.Create(new Vector3(5f, 0f, 0f), new Vector3(0f, 0f, -1f), Vector3.UnitY);

        Assert.Equal(-1f, AudioSpatialMath.Pan(AudioSpatialMode.Spatial3D, pose, new Vector3(0f, 0f, 0f)), 4);
    }

    [Fact]
    public void Pan_ZeroDistanceAndNone_AreZero()
    {
        Assert.Equal(0f, AudioSpatialMath.Pan(AudioSpatialMode.Spatial3D, Origin, Vector3.Zero));
        Assert.Equal(0f, AudioSpatialMath.Pan(AudioSpatialMode.None, Origin, new Vector3(10f, 0f, 0f)));
    }

    [Fact]
    public void Pan_StaysInRange()
    {
        var pan = AudioSpatialMath.Pan(AudioSpatialMode.Spatial3D, Origin, new Vector3(1e-3f, 0f, 0f));
        Assert.InRange(pan, -1f, 1f);
    }

    [Fact]
    public void TwoD_IgnoresZ()
    {
        var source = new Vector3(10f, 0f, 5000f);

        Assert.Equal(10f, AudioSpatialMath.Distance(AudioSpatialMode.Spatial2D, Vector3.Zero, source), 4);
        Assert.Equal(1f, AudioSpatialMath.Pan(AudioSpatialMode.Spatial2D, Origin, source), 4);
    }

    [Fact]
    public void TwoD_SourceOnZAxis_HasZeroDistanceAndPan()
    {
        var source = new Vector3(0f, 0f, -10f);

        Assert.Equal(0f, AudioSpatialMath.Distance(AudioSpatialMode.Spatial2D, Vector3.Zero, source));
        Assert.Equal(0f, AudioSpatialMath.Pan(AudioSpatialMode.Spatial2D, Origin, source));
    }

    [Fact]
    public void TwoD_FallsBackToXRightWhenRightIsAlongZ()
    {
        // Looking along +X with up +Y gives right = (0, 0, -1): projected on X/Y it vanishes, fallback (1, 0).
        var pose = AudioListenerPose.Create(Vector3.Zero, new Vector3(1f, 0f, 0f), Vector3.UnitY);

        Assert.Equal(1f, AudioSpatialMath.Pan(AudioSpatialMode.Spatial2D, pose, new Vector3(4f, 0f, 0f)), 4);
    }

    [Fact]
    public void Distance_ThreeDimensional()
    {
        // 3-4-12 triple: sqrt(9 + 16 + 144) = 13
        Assert.Equal(13f, AudioSpatialMath.Distance(AudioSpatialMode.Spatial3D, Vector3.Zero, new Vector3(3f, 4f, 12f)), 4);
        Assert.Equal(0f, AudioSpatialMath.Distance(AudioSpatialMode.None, Vector3.Zero, new Vector3(3f, 4f, 12f)));
    }

    [Fact]
    public void Calls_DoNotAllocate()
    {
        var sum = AudioSpatialMath.Pan(AudioSpatialMode.Spatial3D, Origin, new Vector3(3f, 0f, -4f));
        var source = new Vector3(3f, 2f, -4f);

        var before = AllocationWindow.Start();
        for (var i = 0; i < 100; i++)
        {
            sum += AudioSpatialMath.Pan(AudioSpatialMode.Spatial3D, Origin, source);
            sum += AudioSpatialMath.Pan(AudioSpatialMode.Spatial2D, Origin, source);
            sum += AudioSpatialMath.Distance(AudioSpatialMode.Spatial3D, Vector3.Zero, source);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(sum > 0f);
    }
}
