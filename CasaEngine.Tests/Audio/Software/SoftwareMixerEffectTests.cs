using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>Insert effects on the buses of <see cref="SoftwareMixer"/> (plan T5.3): order, removal, capacity, last-value parameters, no allocation.</summary>
public class SoftwareMixerEffectTests
{
    private const int OutputRate = 48000;
    private const float Half = 0.5f;

    private static float[] Render(SoftwareMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    // A constant 0.5 on both channels (explicit gains of 1) on the given bus.
    private static void StartHalfScaleVoice(SoftwareMixer mixer, int bus, short value = 16384)
    {
        var samples = new short[400000];
        Array.Fill(samples, value);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default, 1f, 1f, bus));
    }

    private static int CreateBus(SoftwareMixer mixer, int parent = SoftwareMixer.MasterBus)
    {
        Assert.True(mixer.TryCreateBus(parent, out var bus));
        return bus;
    }

    // Constant gain of 2 (ratio 1: no compression, only the make-up gain).
    private static CompressorEffect Doubler()
    {
        return new CompressorEffect(0f, 1f, 0f, 0.001f, 0.001f, 6.0205999f);
    }

    // A limiter-like compressor: everything above -3 dB is held at -3 dB (ratio 1000, instant attack).
    private static CompressorEffect Clamp()
    {
        return new CompressorEffect(-3f, 1000f, 0f, CompressorEffect.MinTimeSeconds, 0.01f);
    }

    private static float Last(float[] block)
    {
        return block[^2];
    }

    [Fact]
    public void AnEffect_RunsOnTheBusBuffer_BeforeTheBusGain()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var bus = CreateBus(mixer);
        mixer.SetBusGain(bus, 0.5f);
        StartHalfScaleVoice(mixer, bus, short.MaxValue);

        // A hard limiter at -6 dB: the full scale voice is cut to 0.5 before the gain halves it to 0.25.
        // After the gain (0.5) the level would be right at the threshold and pass untouched.
        Assert.True(mixer.TryAddEffect(bus, new CompressorEffect(-6.0206f, 1000f, 0f, CompressorEffect.MinTimeSeconds, 0.01f)));
        Render(mixer, 2400);

        Assert.Equal(0.25f, Last(Render(mixer, 480)), 0.01f);
    }

    [Fact]
    public void TwoEffects_RunInInsertionOrder()
    {
        var first = new SoftwareMixer(OutputRate);
        var firstBus = CreateBus(first);
        StartHalfScaleVoice(first, firstBus);
        Assert.True(first.TryAddEffect(firstBus, Doubler()));
        Assert.True(first.TryAddEffect(firstBus, Clamp()));
        Render(first, 2400);

        // 0.5 doubled to 1.0, then held at -3 dB (0.708).
        Assert.Equal(0.7079f, Last(Render(first, 480)), 0.01f);

        var second = new SoftwareMixer(OutputRate);
        var secondBus = CreateBus(second);
        StartHalfScaleVoice(second, secondBus);
        Assert.True(second.TryAddEffect(secondBus, Clamp()));
        Assert.True(second.TryAddEffect(secondBus, Doubler()));
        Render(second, 2400);

        // 0.5 (-6 dB) passes the limiter untouched, then it is doubled to 1.0.
        Assert.Equal(1f, Last(Render(second, 480)), 0.01f);
    }

    [Fact]
    public void ARemovedEffect_StopsProcessing_AndTheNextOnesMoveUp()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var bus = CreateBus(mixer);
        StartHalfScaleVoice(mixer, bus, 1000);
        var a = Doubler();
        var b = Doubler();
        var c = Doubler();
        Assert.True(mixer.TryAddEffect(bus, a));
        Assert.True(mixer.TryAddEffect(bus, b));
        Assert.True(mixer.TryAddEffect(bus, c));
        Render(mixer, 480);
        var flat = 1000f / 32768f;
        Assert.Equal(flat * 8, Last(Render(mixer, 480)), 1e-3f);

        Assert.True(mixer.TryRemoveEffect(bus, b));
        Render(mixer, 480);
        Assert.Equal(flat * 4, Last(Render(mixer, 480)), 1e-3f);

        Assert.True(mixer.TryRemoveEffect(bus, a));
        Assert.True(mixer.TryRemoveEffect(bus, c));
        Render(mixer, 480);
        Assert.Equal(flat, Last(Render(mixer, 480)), 1e-4f);
    }

    [Fact]
    public void ABusRunsFourEffects_AFifthIsIgnoredByTheAudioThread()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var bus = CreateBus(mixer);
        StartHalfScaleVoice(mixer, bus, 100);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(mixer.TryAddEffect(bus, Doubler()));
        }

        Render(mixer, 480);
        Assert.Equal(100f / 32768f * 16, Last(Render(mixer, 480)), 1e-3f);
    }

    [Fact]
    public void TheEffectsOfAChildBus_AreHeardThroughItsParent_AndAnUnknownBusIsRefused()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var parent = CreateBus(mixer);
        var child = CreateBus(mixer, parent);
        StartHalfScaleVoice(mixer, child, 1000);
        Assert.True(mixer.TryAddEffect(child, Doubler()));
        Render(mixer, 480);

        Assert.Equal(1000f / 32768f * 2, Last(Render(mixer, 480)), 1e-4f);
        Assert.False(mixer.TryAddEffect(7, Doubler()));
        Assert.False(mixer.TryAddEffect(child, null));
    }

    [Fact]
    public void AParameterChange_IsNeverLost_EvenWithAFullCommandRing()
    {
        var mixer = new SoftwareMixer(OutputRate, commandCapacity: 4);
        var bus = CreateBus(mixer);
        var effect = new CompressorEffect(0f, 1f, 0f, 0.001f, 0.001f, 0f);
        Assert.True(mixer.TryAddEffect(bus, effect));
        Render(mixer, 10);
        StartHalfScaleVoice(mixer, bus, 1000);
        Render(mixer, 480);

        while (mixer.TryStop(0, 0))
        {
        }

        effect.MakeupGainDb = 12f;
        effect.MakeupGainDb = 6.0205999f;
        Render(mixer, 10);

        Assert.Equal(1000f / 32768f * 2, Last(Render(mixer, 480)), 1e-4f);
    }

    [Fact]
    public void ALongDecayingTail_KeepsGoingThroughASilentBus()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var bus = CreateBus(mixer);
        StartHalfScaleVoice(mixer, bus);
        Assert.True(mixer.TryAddEffect(bus, new BiquadFilterEffect(BiquadFilterType.LowPass, 200f, 20f)));
        Render(mixer, 4800);
        Assert.True(mixer.TryStop(0, 1));

        // The voice is gone; the resonance still rings in the silent bus for a while.
        var tail = Render(mixer, 480);

        Assert.True(Math.Abs(tail[^2]) > 1e-4f);
    }

    [Fact]
    public void WithFullBusesOfEffectsAndVoices_RenderingAllocatesNothing()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var effects = new List<AudioEffect>();

        for (var i = 1; i < SoftwareMixer.BusCapacity; i++)
        {
            CreateBus(mixer);
        }

        for (var voice = 0; voice < 64; voice++)
        {
            var samples = new short[48000];
            Array.Fill(samples, (short)3000);
            Assert.True(mixer.TryStartResidentStereoVoice(voice, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default.WithLooping(true), 0.2f, 0.2f, 1 + (voice % 31)));
        }

        for (var bus = 0; bus < SoftwareMixer.BusCapacity; bus++)
        {
            effects.Add(new BiquadFilterEffect(BiquadFilterType.Peaking, 1000f, 1f, 6f));
            effects.Add(new BiquadFilterEffect(BiquadFilterType.HighShelf, 6000f, 0.7f, -3f));
            effects.Add(new CompressorEffect());
            effects.Add(new BiquadFilterEffect(BiquadFilterType.LowPass, 12000f));
        }

        var buffer = new float[480 * 2];
        mixer.Render(buffer, 480);

        // The audio thread applies the add commands inside the window: nothing may be allocated there.
        for (var i = 0; i < effects.Count; i++)
        {
            Assert.True(mixer.TryAddEffect(i / 4, effects[i]));
        }

        // Parameter changes allocate their snapshot on this (the game) thread, before the window; the audio thread
        // then recomputes every coefficient inside it.
        ((BiquadFilterEffect)effects[0]).FrequencyHz = 1500f;
        ((CompressorEffect)effects[2]).ThresholdDb = -12f;

        var before = AllocationWindow.Start();

        for (var round = 0; round < 50; round++)
        {
            mixer.Render(buffer, 480);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
