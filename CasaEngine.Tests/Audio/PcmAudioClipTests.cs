using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class PcmAudioClipTests
{
    [Fact]
    public void Mono_ExposesFramesDurationAndSamples()
    {
        var samples = new short[22050];
        var clip = new PcmAudioClip(samples, 22050, 1);

        Assert.Equal(22050, clip.FrameCount);
        Assert.Equal(1, clip.ChannelCount);
        Assert.Equal(22050, clip.SampleRate);
        Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
        Assert.Equal(22050, clip.Samples.Length);
        Assert.Equal(22050, clip.MonoSamples.Length);
        Assert.False(clip.IsDisposed);
    }

    [Fact]
    public void Stereo_HasHalfAsManyFramesAndNoMonoSamples()
    {
        var clip = new PcmAudioClip(new short[44100], 44100, 2);

        Assert.Equal(22050, clip.FrameCount);
        Assert.Equal(TimeSpan.FromSeconds(0.5), clip.Duration);
        Assert.Equal(44100, clip.Samples.Length);
        Assert.True(clip.MonoSamples.IsEmpty);
    }

    [Fact]
    public void ImplementsTheClipContracts()
    {
        var clip = new PcmAudioClip(new short[2], 8000, 1);

        Assert.IsAssignableFrom<IAudioClip>(clip);
        Assert.IsAssignableFrom<IAudioClipSamples>(clip);
    }

    [Fact]
    public void Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new PcmAudioClip(null, 22050, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcmAudioClip(new short[2], 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcmAudioClip(new short[2], -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcmAudioClip(new short[2], 22050, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcmAudioClip(new short[6], 22050, 3));
        Assert.Throws<ArgumentException>(() => new PcmAudioClip(new short[3], 22050, 2));
    }

    [Fact]
    public void Dispose_DisposesTheBackendResourceOnce()
    {
        var resource = new CountingDisposable();
        var clip = new PcmAudioClip(new short[2], 22050, 1) { BackendResource = resource };

        clip.Dispose();
        clip.Dispose();

        Assert.True(clip.IsDisposed);
        Assert.Equal(1, resource.DisposeCount);
        Assert.Null(clip.BackendResource);
    }

    [Fact]
    public void Dispose_WithoutBackendResource_Works()
    {
        var clip = new PcmAudioClip(new short[2], 22050, 1);

        clip.Dispose();

        Assert.True(clip.IsDisposed);
    }

    private sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
