using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>The bus graph of <see cref="SoftwareMixer"/> (plan T5.1): routing, hierarchy, smoothed gains, capacity, block splitting.</summary>
public class SoftwareMixerBusTests
{
    private const int OutputRate = 48000;
    private const float FullScale = 32767f / 32768f;

    private static float[] Render(SoftwareMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    private static PcmAudioClip Constant(short value, int frames, int channels = 1)
    {
        var samples = new short[frames * channels];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, OutputRate, channels);
    }

    // Mono voice with explicit gains of 1: the output is exactly the sample value on both channels.
    private static void StartFullScaleVoice(SoftwareMixer mixer, int slot, int bus)
    {
        Assert.True(mixer.TryStartResidentStereoVoice(slot, 1, Constant(short.MaxValue, 100000), AudioVoiceParameters.Default, 1f, 1f, bus));
    }

    private static int CreateBus(SoftwareMixer mixer, int parent)
    {
        Assert.True(mixer.TryCreateBus(parent, out var bus));
        return bus;
    }

    [Fact]
    public void AVoiceOnAChildBusAtHalfUnderAParentAtHalf_IsHeardAtAQuarter()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var parent = CreateBus(mixer, SoftwareMixer.MasterBus);
        var child = CreateBus(mixer, parent);
        mixer.SetBusGain(parent, 0.5f);
        mixer.SetBusGain(child, 0.5f);
        StartFullScaleVoice(mixer, 0, child);

        var output = Render(mixer, 200);

        for (var i = 0; i < output.Length; i++)
        {
            Assert.Equal(FullScale * 0.25f, output[i], 1e-4f);
        }
    }

    [Fact]
    public void TheMasterGain_AppliesToEveryBus()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var sfx = CreateBus(mixer, SoftwareMixer.MasterBus);
        mixer.SetBusGain(SoftwareMixer.MasterBus, 0.5f);
        StartFullScaleVoice(mixer, 0, sfx);
        StartFullScaleVoice(mixer, 1, SoftwareMixer.MasterBus);
        Render(mixer, 10); // the Master ramp

        var output = Render(mixer, 100);

        // Two voices of the full scale, each halved by Master, clip at 1.
        Assert.All(output, sample => Assert.Equal(1f, sample, 1e-4f));

        mixer.SetBusGain(SoftwareMixer.MasterBus, 0.25f);
        Render(mixer, 10);
        output = Render(mixer, 100);
        Assert.All(output, sample => Assert.Equal(FullScale * 0.5f, sample, 1e-4f));
    }

    [Fact]
    public void ABusWithNoGainPublished_StartsAtFullGain_AndAMutedBusSilencesItsChildren()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var parent = CreateBus(mixer, SoftwareMixer.MasterBus);
        var child = CreateBus(mixer, parent);
        StartFullScaleVoice(mixer, 0, child);

        Assert.Equal(FullScale, Render(mixer, 50)[40], 1e-4f);

        mixer.SetBusGain(parent, 0f); // a muted bus publishes zero
        Render(mixer, 480);

        Assert.All(Render(mixer, 100), sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void AGainChange_IsSmoothedAcrossOneBlock_ThenHeldExactly()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var bus = CreateBus(mixer, SoftwareMixer.MasterBus);
        StartFullScaleVoice(mixer, 0, bus);
        Render(mixer, 100);

        mixer.SetBusGain(bus, 0f);
        var block = Render(mixer, 100);

        // A linear ramp from 1 to 0 over the 100 frames: strictly decreasing, no step.
        var previous = FullScale;
        for (var frame = 0; frame < 100; frame++)
        {
            var left = block[frame * 2];
            Assert.True(left < previous, $"frame {frame}: {left} is not below {previous}");
            Assert.Equal(FullScale * (1f - (frame + 1) / 100f), left, 1e-4f);
            previous = left;
        }

        Assert.All(Render(mixer, 100), sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void AGainPublishedManyTimesBetweenTwoBlocks_KeepsTheLastValue_EvenWithAFullCommandRing()
    {
        var mixer = new SoftwareMixer(OutputRate, commandCapacity: 4);
        var bus = CreateBus(mixer, SoftwareMixer.MasterBus);
        Render(mixer, 10);

        // Saturate the command ring: a queued gain would be dropped, a published one cannot be.
        while (mixer.TryStop(0, 0))
        {
        }

        mixer.SetBusGain(bus, 0.9f);
        mixer.SetBusGain(bus, 0.3f);
        Render(mixer, 10);
        StartFullScaleVoice(mixer, 0, bus);

        Assert.Equal(FullScale * 0.3f, Render(mixer, 480)[900], 1e-4f);
    }

    [Fact]
    public void AStereoSourceAndAStreamingVoice_AreRoutedToTheirBus()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var quiet = CreateBus(mixer, SoftwareMixer.MasterBus);
        mixer.SetBusGain(quiet, 0.5f);

        // Stereo clip: plain resident voice on the quiet bus.
        Assert.True(mixer.TryStartResidentVoice(0, 1, Constant(16384, 100000, channels: 2), AudioVoiceParameters.Default, quiet));
        var stereo = Render(mixer, 100);
        Assert.Equal(0.5f * 0.5f, stereo[100], 1e-4f);
        Assert.Equal(0.5f * 0.5f, stereo[101], 1e-4f);
        Assert.True(mixer.TryStop(0, 1));
        Render(mixer, 10);

        // Streaming voice on the quiet bus, then the same stream on Master.
        var pcm = new byte[4 * 2000];
        for (var i = 0; i < 4000; i++)
        {
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2, 2), (short)16384);
        }

        Assert.True(mixer.TryCreateStreamingVoice(1, 1, 2, OutputRate, AudioVoiceParameters.Default, quiet));
        Assert.True(mixer.TrySubmitStreamingBuffer(1, 1, pcm, 0));
        Assert.True(mixer.TryStartStreamingVoice(1, 1));
        var streamed = Render(mixer, 100);
        Assert.Equal(0.5f * 0.5f, streamed[100], 1e-3f);

        Assert.True(mixer.TryCreateStreamingVoice(2, 1, 2, OutputRate, AudioVoiceParameters.Default));
        Assert.True(mixer.TrySubmitStreamingBuffer(2, 1, pcm, 0));
        Assert.True(mixer.TryStartStreamingVoice(2, 1));
        var both = Render(mixer, 100);
        Assert.Equal(0.5f * 0.5f + 0.5f, both[100], 1e-3f);
    }

    [Fact]
    public void ABusBeyondTheCapacity_IsRefused_AndAVoiceSentToAnUnknownBusPlaysOnMaster()
    {
        var mixer = new SoftwareMixer(OutputRate);

        for (var i = 1; i < SoftwareMixer.BusCapacity; i++)
        {
            Assert.True(mixer.TryCreateBus(SoftwareMixer.MasterBus, out var bus));
            Assert.Equal(i, bus);
        }

        Assert.False(mixer.TryCreateBus(SoftwareMixer.MasterBus, out var extra));
        Assert.Equal(-1, extra);
        Assert.Equal(SoftwareMixer.BusCapacity, mixer.BusCount);

        StartFullScaleVoice(mixer, 0, SoftwareMixer.BusCapacity + 5);
        Assert.Equal(FullScale, Render(mixer, 50)[40], 1e-4f);
    }

    [Fact]
    public void ABusWithAnUnknownParent_IsRefused()
    {
        var mixer = new SoftwareMixer(OutputRate);

        Assert.False(mixer.TryCreateBus(3, out _));
        Assert.Equal(1, mixer.BusCount);
    }

    [Fact]
    public void ARequestLargerThanTheMaximumBlock_IsSplit_AndMatchesTheUnsplitMix()
    {
        var small = new SoftwareMixer(OutputRate, maxBlockFrames: 64);
        var large = new SoftwareMixer(OutputRate, maxBlockFrames: 4096);

        foreach (var mixer in new[] { small, large })
        {
            var bus = CreateBus(mixer, SoftwareMixer.MasterBus);
            mixer.SetBusGain(bus, 0.5f);
            Assert.True(mixer.TryStartResidentVoice(0, 1, Constant(12000, 100000), AudioVoiceParameters.Default, bus));
        }

        Assert.Equal(64, small.MaxBlockFrames);
        var split = Render(small, 1000);
        var whole = Render(large, 1000);

        Assert.Equal(whole.Length, split.Length);
        for (var i = 0; i < whole.Length; i++)
        {
            Assert.Equal(whole[i], split[i], 1e-5f);
        }
    }

    [Fact]
    public void Render_With32BusesAnd64Voices_DoesNotAllocate()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var buses = new int[SoftwareMixer.BusCapacity];

        for (var i = 1; i < SoftwareMixer.BusCapacity; i++)
        {
            // A chain and a fan: every bus has a parent of a lower index.
            buses[i] = CreateBus(mixer, i % 3 == 0 ? buses[i - 1] : SoftwareMixer.MasterBus);
        }

        for (var voice = 0; voice < 64; voice++)
        {
            var bus = voice % SoftwareMixer.BusCapacity;
            Assert.True(mixer.TryStartResidentStereoVoice(voice, 1, Constant(3000, 500000), AudioVoiceParameters.Default, 0.4f, 0.4f, bus));
        }

        var output = new float[480 * 2];

        // Warm up (JIT) with gain changes in flight, then measure.
        for (var round = 0; round < 20; round++)
        {
            mixer.SetBusGain(1 + round % 31, round % 2 == 0 ? 0.5f : 1f);
            mixer.Render(output, 480);
        }

        var before = AllocationWindow.Start();

        for (var round = 0; round < 50; round++)
        {
            mixer.SetBusGain(1 + round % 31, round % 2 == 0 ? 0.25f : 0.75f);
            mixer.Render(output, 480);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
