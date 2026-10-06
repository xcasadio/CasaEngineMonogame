using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>The published (generation, consumed buffers) value of <see cref="SoftwareMixer"/> (T2.2).</summary>
public class SoftwareMixerConsumedCountTests
{
    private const int OutputRate = 48000;

    private static void Render(SoftwareMixer mixer, int frames)
    {
        mixer.Render(new float[frames * 2], frames);
    }

    [Fact]
    public void SaturatedEventRing_DoesNotLoseTheConsumedCount()
    {
        var mixer = new SoftwareMixer(OutputRate, voiceCapacity: 2, eventCapacity: 64);
        mixer.TryCreateStreamingVoice(0, 1, 1, OutputRate, AudioVoiceParameters.Default);
        mixer.TryStartStreamingVoice(0, 1);
        var pcm = new byte[16];
        var total = 0;

        // 20 rounds of 20 one-chunk buffers, with the event ring never drained: it overflows.
        for (var round = 0; round < 20; round++)
        {
            for (var i = 0; i < 20; i++)
            {
                Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, pcm, total++));
            }

            Render(mixer, 400);
        }

        Assert.True(mixer.DroppedEventCount > 0);
        Assert.Equal(total, mixer.GetConsumedBufferCount(0, 1));

        // Drained afterwards, the count is still exact.
        while (mixer.TryDequeueEvent(out _))
        {
        }

        Assert.Equal(total, mixer.GetConsumedBufferCount(0, 1));
    }

    [Fact]
    public void ChunkDroppedOnQueueOverflow_CountsAsConsumed()
    {
        var mixer = new SoftwareMixer(OutputRate, voiceCapacity: 1);
        mixer.TryCreateStreamingVoice(0, 1, 1, OutputRate, AudioVoiceParameters.Default);
        var pcm = new byte[16];
        var submitted = SoftwareMixer.VoiceChunkQueueCapacity + 5;

        for (var i = 0; i < submitted; i++)
        {
            Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, pcm, i));
        }

        Render(mixer, 1);

        Assert.Equal(5, mixer.DroppedChunkCount);
        Assert.Equal(5, mixer.GetConsumedBufferCount(0, 1));

        mixer.TryStartStreamingVoice(0, 1);
        Render(mixer, 2000);

        Assert.Equal(submitted, mixer.GetConsumedBufferCount(0, 1));
    }

    [Fact]
    public void ACountOfAnOlderGeneration_IsIgnored()
    {
        var mixer = new SoftwareMixer(OutputRate, voiceCapacity: 1);
        mixer.TryCreateStreamingVoice(0, 1, 1, OutputRate, AudioVoiceParameters.Default);
        mixer.TryStartStreamingVoice(0, 1);
        mixer.TrySubmitStreamingBuffer(0, 1, new byte[16], 0);
        Render(mixer, 100);
        Assert.Equal(1, mixer.GetConsumedBufferCount(0, 1));
        Assert.Equal(0, mixer.GetConsumedBufferCount(0, 2));

        mixer.TryCreateStreamingVoice(0, 2, 1, OutputRate, AudioVoiceParameters.Default);
        Render(mixer, 10);

        Assert.Equal(0, mixer.GetConsumedBufferCount(0, 2));
        Assert.Equal(0, mixer.GetConsumedBufferCount(0, 1));
    }
}
