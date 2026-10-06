using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>Explicit duration ramps of voices and buses in <see cref="SoftwareMixer"/> (plan T5.2, decision P21).</summary>
public class SoftwareMixerRampTests
{
    private const int OutputRate = 48000;
    private const float FullScale = 32767f / 32768f;
    private const float Tolerance = 1e-5f;

    private static float[] Render(SoftwareMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    private static PcmAudioClip Constant(int frames)
    {
        var samples = new short[frames];
        Array.Fill(samples, short.MaxValue);
        return new PcmAudioClip(samples, OutputRate, 1);
    }

    // Mono voice with explicit gains of 1: the output is exactly the sample value on both channels, times the volume.
    private static SoftwareMixer MixerWithVoice(int bus = SoftwareMixer.MasterBus, SoftwareMixer mixer = null)
    {
        mixer ??= new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, Constant(400000), AudioVoiceParameters.Default, 1f, 1f, bus));
        Render(mixer, 64);
        return mixer;
    }

    [Fact]
    public void ARampFromOneToZeroOverAQuarterSecond_IsLinear_SampleBySample_FromTheNextBlock()
    {
        var mixer = MixerWithVoice();
        const int frames = OutputRate / 4;

        Assert.True(mixer.TryRampVoiceVolume(0, 1, 0f, frames));

        // One call, several blocks: the ramp starts with the first block and carries across the splits.
        var output = Render(mixer, frames + 100);

        for (var i = 0; i < frames; i++)
        {
            var expected = FullScale * (1f - (i + 1f) / frames);
            Assert.Equal(expected, output[i * 2], Tolerance);
            Assert.Equal(expected, output[i * 2 + 1], Tolerance);
        }

        // Held at the target after the ramp.
        Assert.Equal(0f, output[frames * 2 + 20], Tolerance);
    }

    [Fact]
    public void ARampInterruptedByAnother_ResumesFromTheCurrentValue()
    {
        var mixer = MixerWithVoice();

        Assert.True(mixer.TryRampVoiceVolume(0, 1, 0f, 1000));
        Render(mixer, 500); // value 0.5

        Assert.True(mixer.TryRampVoiceVolume(0, 1, 1f, 1000));
        var output = Render(mixer, 1000);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(FullScale * (0.5f + 0.5f * (i + 1f) / 1000f), output[i * 2], Tolerance);
        }
    }

    [Fact]
    public void Freeze_StopsTheEnvelopeAtTheCurrentValue()
    {
        var mixer = MixerWithVoice();

        Assert.True(mixer.TryRampVoiceVolume(0, 1, 0f, 1000));
        Render(mixer, 400);
        Assert.True(mixer.TryFreezeVoiceVolume(0, 1));
        var output = Render(mixer, 500);

        Assert.All(output, sample => Assert.Equal(FullScale * 0.6f, sample, Tolerance));
    }

    [Fact]
    public void ARampKeepsRunningOnAPausedVoice()
    {
        var mixer = MixerWithVoice();

        Assert.True(mixer.TryPause(0, 1));
        Assert.True(mixer.TryRampVoiceVolume(0, 1, 0f, 1000));
        Assert.All(Render(mixer, 600), sample => Assert.Equal(0f, sample));

        Assert.True(mixer.TryResume(0, 1));
        var output = Render(mixer, 400);

        for (var i = 0; i < 400; i++)
        {
            Assert.Equal(FullScale * (0.4f - 0.4f * (i + 1f) / 400f), output[i * 2], Tolerance);
        }
    }

    [Fact]
    public void WhileARampRuns_SetVolumeEndsIt_AndTheVolumeOfSetParametersIsIgnored()
    {
        var mixer = MixerWithVoice();

        Assert.True(mixer.TryRampVoiceVolume(0, 1, 0f, 1000));
        Render(mixer, 500);

        // Another pan and volume: the ramp goes on, the volume asked for is not applied.
        Assert.True(mixer.TrySetParameters(0, 1, AudioVoiceParameters.Default.WithVolume(0.9f)));
        var output = Render(mixer, 100);
        Assert.Equal(FullScale * (0.5f - 0.5f * 1f / 500f), output[0], Tolerance);

        Assert.True(mixer.TrySetVolume(0, 1, 0.9f));
        output = Render(mixer, 480);
        Assert.Equal(FullScale * 0.9f, output[^2], Tolerance);
    }

    [Fact]
    public void ARampOnAVoiceThatWasReplaced_IsIgnored()
    {
        var mixer = MixerWithVoice();

        Assert.True(mixer.TryRampVoiceVolume(0, 99, 0f, 100));
        var output = Render(mixer, 300);

        Assert.All(output, sample => Assert.Equal(FullScale, sample, Tolerance));
    }

    [Fact]
    public void ARampWithNoFrames_JumpsToTheTargetAtTheNextBlock()
    {
        var mixer = MixerWithVoice();

        Assert.True(mixer.TryRampVoiceVolume(0, 1, 0.25f, 0));
        var output = Render(mixer, 10);

        Assert.Equal(FullScale * 0.25f, output[0], Tolerance);
    }

    [Fact]
    public void ABusRamp_IsLinear_SampleBySample_AndHoldsTheTarget()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryCreateBus(SoftwareMixer.MasterBus, out var bus));
        MixerWithVoice(bus, mixer);
        const int frames = OutputRate / 4;

        Assert.True(mixer.TryRampBusGain(bus, 0f, frames));
        var output = Render(mixer, frames + 3000);

        for (var i = 0; i < frames; i++)
        {
            Assert.Equal(FullScale * (1f - (i + 1f) / frames), output[i * 2], Tolerance);
        }

        // Nothing was published: the bus stays at the ramp target.
        Assert.All(output.AsSpan((frames + 10) * 2).ToArray(), sample => Assert.Equal(0f, sample, Tolerance));
    }

    [Fact]
    public void ABusRampInterrupted_ResumesFromTheCurrentValue_AndFreezeStopsIt()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryCreateBus(SoftwareMixer.MasterBus, out var bus));
        MixerWithVoice(bus, mixer);

        Assert.True(mixer.TryRampBusGain(bus, 0f, 1000));
        Render(mixer, 500); // 0.5

        Assert.True(mixer.TryRampBusGain(bus, 1f, 1000));
        var output = Render(mixer, 200); // 0.5 -> 0.6

        for (var i = 0; i < 200; i++)
        {
            Assert.Equal(FullScale * (0.5f + 0.5f * (i + 1f) / 1000f), output[i * 2], Tolerance);
        }

        Assert.True(mixer.TryFreezeBusGain(bus));
        output = Render(mixer, 300);
        Assert.All(output, sample => Assert.Equal(FullScale * 0.6f, sample, Tolerance));
    }

    [Fact]
    public void AGainPublishedAfterTheRampWasSent_ButBeforeItIsApplied_WinsAtTheNextBlock()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryCreateBus(SoftwareMixer.MasterBus, out var bus));
        MixerWithVoice(bus, mixer);

        Assert.True(mixer.TryRampBusGain(bus, 0f, 1000));
        mixer.SetBusGain(bus, 0.5f);
        Render(mixer, 10); // smoothing to the published gain
        var output = Render(mixer, 2000);

        // The ramp to 0 was dropped, not held.
        Assert.All(output, sample => Assert.Equal(FullScale * 0.5f, sample, Tolerance));
    }

    [Fact]
    public void ABusRampStartsFromItsCarriedStartGain_NotFromTheGainTheAudioThreadHolds()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryCreateBus(SoftwareMixer.MasterBus, out var bus));
        MixerWithVoice(bus, mixer); // the bus is at 1 on the audio thread

        Assert.True(mixer.TryRampBusGain(bus, 0.5f, 1f, 1000));
        var output = Render(mixer, 1000);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(FullScale * (0.5f + 0.5f * (i + 1f) / 1000f), output[i * 2], Tolerance);
        }
    }

    [Fact]
    public void ABusRamp_YieldsToAGainPublishedAfterIt_EvenOfTheSameValue()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryCreateBus(SoftwareMixer.MasterBus, out var bus));
        MixerWithVoice(bus, mixer);

        Assert.True(mixer.TryRampBusGain(bus, 0f, 100));
        Render(mixer, 300);
        Assert.Equal(0f, Render(mixer, 10)[0], Tolerance);

        // The same value as before the ramp: still an explicit publish, it wins (smoothed over a block).
        mixer.SetBusGain(bus, 1f);
        Render(mixer, 10);
        var output = Render(mixer, 10);

        Assert.Equal(FullScale, output[0], Tolerance);
    }

    [Fact]
    public void ARampOfABusThatWasNotCreated_IsRefused()
    {
        var mixer = new SoftwareMixer(OutputRate);

        Assert.False(mixer.TryRampBusGain(5, 0f, 100));
        Assert.False(mixer.TryFreezeBusGain(5));
    }

    [Fact]
    public void RampsAllocateNothing_WithManyVoicesAndBuses()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var buses = new int[31];

        for (var i = 0; i < buses.Length; i++)
        {
            Assert.True(mixer.TryCreateBus(SoftwareMixer.MasterBus, out buses[i]));
        }

        var clip = Constant(400000);

        for (var v = 0; v < 64; v++)
        {
            Assert.True(mixer.TryStartResidentStereoVoice(v, 1, clip, AudioVoiceParameters.Default, 1f, 1f, buses[v % buses.Length]));
        }

        var output = new float[480 * 2];

        for (var round = 0; round < 20; round++)
        {
            RampAll(mixer, buses, round);
            mixer.Render(output, 480);
        }

        var before = AllocationWindow.Start();

        for (var round = 0; round < 50; round++)
        {
            RampAll(mixer, buses, round);
            mixer.Render(output, 480);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void RampAll(SoftwareMixer mixer, int[] buses, int round)
    {
        for (var v = 0; v < 64; v++)
        {
            Assert.True(mixer.TryRampVoiceVolume(v, 1, round % 2 == 0 ? 0.2f : 0.9f, 1200 + v));
        }

        for (var b = 0; b < buses.Length; b++)
        {
            Assert.True(mixer.TryRampBusGain(buses[b], round % 2 == 0 ? 0.3f : 1f, 900 + b));
        }
    }
}
