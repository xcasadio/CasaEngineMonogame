using System.Numerics;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;

namespace CasaEngine.Tests.Audio.Spatial;

public class AudioListenerPoseTests
{
    [Fact]
    public void Default_IsTheSpecificationPose()
    {
        var pose = AudioListenerPose.Default;

        Assert.Equal(Vector3.Zero, pose.Position);
        Assert.Equal(new Vector3(0f, 0f, -1f), pose.Forward);
        Assert.Equal(new Vector3(0f, 1f, 0f), pose.Up);
        Assert.Equal(new Vector3(1f, 0f, 0f), pose.Right);
    }

    [Fact]
    public void Create_NormalizesDirections()
    {
        var pose = AudioListenerPose.Create(new Vector3(1f, 2f, 3f), new Vector3(0f, 0f, -5f), new Vector3(0f, 7f, 0f));

        Assert.Equal(new Vector3(1f, 2f, 3f), pose.Position);
        Assert.Equal(new Vector3(0f, 0f, -1f), pose.Forward);
        Assert.Equal(new Vector3(0f, 1f, 0f), pose.Up);
        Assert.Equal(new Vector3(1f, 0f, 0f), pose.Right);
    }

    [Fact]
    public void Right_DegenerateFallsBackToX()
    {
        var pose = AudioListenerPose.Create(Vector3.Zero, Vector3.UnitY, Vector3.UnitY);

        Assert.Equal(new Vector3(1f, 0f, 0f), pose.Right);
        Assert.Equal(new Vector3(1f, 0f, 0f), default(AudioListenerPose).Right);
    }

    [Fact]
    public void Create_ZeroOrNonFiniteDirections_UseDefaults()
    {
        var pose = AudioListenerPose.Create(Vector3.Zero, Vector3.Zero, new Vector3(float.NaN, 0f, 0f));

        Assert.Equal(AudioListenerPose.Default.Forward, pose.Forward);
        Assert.Equal(AudioListenerPose.Default.Up, pose.Up);
    }
}
