using CasaEngine.Core.Time;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Time;

public sealed class FrameTimeTests
{
    [Fact]
    public void FromElapsedTimeCreatesUnscaledFrameTime()
    {
        FrameTime frameTime = FrameTime.FromElapsedTime(0.25f, 7);

        Assert.Equal(0.25f, frameTime.DeltaTime);
        Assert.Equal(0.25f, frameTime.UnscaledDeltaTime);
        Assert.Equal(0.25f, frameTime.TotalTime);
        Assert.Equal(0.25f, frameTime.UnscaledTotalTime);
        Assert.Equal(1f, frameTime.TimeScale);
        Assert.Equal(7, frameTime.FrameIndex);
    }

    [Fact]
    public void FromGameTimeAppliesTimeScaleToDeltaOnly()
    {
        var gameTime = new GameTime(TimeSpan.FromSeconds(10.0), TimeSpan.FromSeconds(0.5));

        FrameTime frameTime = FrameTime.FromGameTime(gameTime, 0.25f, 3.5f, 42);

        Assert.Equal(0.125f, frameTime.DeltaTime);
        Assert.Equal(0.5f, frameTime.UnscaledDeltaTime);
        Assert.Equal(3.5f, frameTime.TotalTime);
        Assert.Equal(10f, frameTime.UnscaledTotalTime);
        Assert.Equal(0.25f, frameTime.TimeScale);
        Assert.Equal(42, frameTime.FrameIndex);
    }
}