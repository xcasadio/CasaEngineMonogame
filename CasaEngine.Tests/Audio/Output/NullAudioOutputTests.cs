using CasaEngine.Framework.Audio.Output;
using Xunit;

namespace CasaEngine.Tests.Audio.Output;

public class NullAudioOutputTests
{
    [Fact]
    public void TryOpen_ReturnsFalse_AndOutputIsNeverAvailable()
    {
        using var output = new NullAudioOutput();

        Assert.False(output.TryOpen());
        Assert.False(output.IsAvailable);
        Assert.Equal(0, output.SampleRate);
        Assert.Equal(0, output.UnderrunCount);
    }

    [Fact]
    public void Start_NeverCallsTheCallback()
    {
        using var output = new NullAudioOutput();
        var called = false;

        output.Start((_, _) => called = true);

        Assert.False(called);
    }

    [Fact]
    public void Dispose_CanBeCalledRepeatedly()
    {
        var output = new NullAudioOutput();

        output.Dispose();
        output.Dispose();
    }
}
