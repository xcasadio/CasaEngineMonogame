using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Sends, return buses and the Master limiter on <see cref="SoftwareMixer"/> (plan T5.4, decision P20): post-fader sends,
/// mix order whatever the creation order, send cycles refused, the limiter holding a +12 dB sum under -1 dBFS, no allocation.
/// </summary>
public class SoftwareMixerSendTests
{
    private const int OutputRate = 48000;
    private const double CeilingLinear = 0.89125093813374556; // -1 dBFS

    private static float[] Render(SoftwareMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    private static float Last(float[] block)
    {
        return block[^2];
    }

    private static int CreateBus(SoftwareMixer mixer, int parent = SoftwareMixer.MasterBus)
    {
        Assert.True(mixer.TryCreateBus(parent, out var bus));
        return bus;
    }

    private static void StartConstantVoice(SoftwareMixer mixer, int slot, int bus, short value = 16384)
    {
        var samples = new short[400000];
        Array.Fill(samples, value);
        Assert.True(mixer.TryStartResidentStereoVoice(slot, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default, 1f, 1f, bus));
    }

    private static PcmAudioClip SineClip(double amplitude, double frequency = 1000.0)
    {
        var samples = new short[OutputRate];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(amplitude * 32767.0 * Math.Sin(2.0 * Math.PI * frequency * i / OutputRate));
        }

        return new PcmAudioClip(samples, OutputRate, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASend_AddsThePostFaderSignalToTheReturnBus_WhateverTheCreationOrder(bool returnBeforeSender)
    {
        var mixer = new SoftwareMixer(OutputRate);
        int sender;
        int returnBus;

        if (returnBeforeSender)
        {
            returnBus = CreateBus(mixer);
            sender = CreateBus(mixer);
        }
        else
        {
            sender = CreateBus(mixer);
            returnBus = CreateBus(mixer);
        }

        mixer.SetBusGain(sender, 0.5f);
        StartConstantVoice(mixer, 0, sender);
        Assert.True(mixer.TrySetSend(sender, returnBus, 0.5f));
        Render(mixer, 960);

        // 0.5 voice, 0.5 gain: 0.25 to Master; the send adds 0.25 * 0.5 through the return bus.
        Assert.Equal(0.375f, Last(Render(mixer, 480)), 1e-4f);

        // The return bus gain applies to what it received.
        mixer.SetBusGain(returnBus, 0.5f);
        Render(mixer, 480);
        Assert.Equal(0.3125f, Last(Render(mixer, 480)), 1e-4f);

        // Level 0 removes the send.
        Assert.True(mixer.TrySetSend(sender, returnBus, 0f));
        Render(mixer, 480);
        Assert.Equal(0.25f, Last(Render(mixer, 480)), 1e-4f);
    }

    [Fact]
    public void ASendLevelChange_IsRampedAcrossTheBlock()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var sender = CreateBus(mixer);
        var returnBus = CreateBus(mixer);
        StartConstantVoice(mixer, 0, sender);
        Assert.True(mixer.TrySetSend(sender, returnBus, 0.2f));
        Render(mixer, 480);

        Assert.True(mixer.TrySetSend(sender, returnBus, 1f));
        var block = Render(mixer, 480);

        // 0.5 + 0.5 * level, the level going from 0.2 to 1 in a straight line.
        Assert.Equal(0.5f + (0.5f * 0.2f), block[0], 2e-3f);
        Assert.Equal(1f, Last(block), 1e-4f);
        Assert.True(block[480] > block[0] && block[480] < Last(block));
    }

    [Fact]
    public void ASendCycle_IsRefused_ButASendToAnAncestorOrASiblingIsNot()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var a = CreateBus(mixer);
        var b = CreateBus(mixer);
        var child = CreateBus(mixer, a);

        Assert.False(mixer.TrySetSend(a, a, 0.5f));
        Assert.False(mixer.TrySetSend(a, child, 0.5f));
        Assert.False(mixer.TrySetSend(SoftwareMixer.MasterBus, b, 0.5f));
        Assert.True(mixer.TrySetSend(a, b, 0.5f));
        Assert.False(mixer.TrySetSend(b, a, 0.5f));
        Assert.False(mixer.TrySetSend(b, child, 0.5f));
        Assert.True(mixer.TrySetSend(child, b, 0.5f));
        Assert.True(mixer.TrySetSend(child, SoftwareMixer.MasterBus, 0.5f));
        Assert.False(mixer.IsSendAccepted(b, a, 0.5f));

        // A refused send leaves the mix valid.
        StartConstantVoice(mixer, 0, child);
        Render(mixer, 480);
        Assert.True(float.IsFinite(Last(Render(mixer, 480))));
    }

    [Fact]
    public void ABusHoldsFourSends()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var sender = CreateBus(mixer);
        var returns = new int[5];

        for (var i = 0; i < returns.Length; i++)
        {
            returns[i] = CreateBus(mixer);
        }

        for (var i = 0; i < 4; i++)
        {
            Assert.True(mixer.TrySetSend(sender, returns[i], 0.1f));
        }

        Assert.False(mixer.TrySetSend(sender, returns[4], 0.1f));
        Assert.True(mixer.TrySetSend(sender, returns[0], 0.2f));
        Assert.True(mixer.TrySetSend(sender, returns[0], 0f));
        Assert.True(mixer.TrySetSend(sender, returns[4], 0.1f));
    }

    [Fact]
    public void AChainOfReturnBuses_IsMixedInDependencyOrder()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var sender = CreateBus(mixer);
        var second = CreateBus(mixer);
        var first = CreateBus(mixer);

        // sender -> first -> second, the opposite of the index order.
        StartConstantVoice(mixer, 0, sender, 8192);
        mixer.SetBusGain(sender, 0f);
        Assert.True(mixer.TrySetSend(first, second, 1f));
        Assert.True(mixer.TrySetSend(sender, first, 1f));
        mixer.SetBusGain(first, 0f);
        Render(mixer, 960);

        // The sender is silent post-fader: only the voice through the sends of a bus with a gain would be heard.
        Assert.Equal(0f, Last(Render(mixer, 480)), 1e-6f);

        mixer.SetBusGain(sender, 1f);
        mixer.SetBusGain(first, 1f);
        mixer.SetBusGain(second, 1f);
        Render(mixer, 960);

        // Sender 0.25 to Master, 0.25 through first to Master and 0.25 through first and second to Master.
        Assert.Equal(0.75f, Last(Render(mixer, 480)), 1e-4f);
    }

    [Fact]
    public void AReverbOnTheReturnBus_KeepsRingingAfterTheVoiceEnds()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var sender = CreateBus(mixer);
        var returnBus = CreateBus(mixer);
        Assert.True(mixer.TryAddEffect(returnBus, new ReverbEffect(0.8f)));
        Assert.True(mixer.TrySetSend(sender, returnBus, 1f));
        var burst = new short[2400];
        Array.Fill(burst, (short)16000);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, new PcmAudioClip(burst, OutputRate, 1), AudioVoiceParameters.Default, 1f, 1f, sender));

        var peakAfterTheVoice = 0f;

        for (var block = 0; block < 60; block++)
        {
            var rendered = Render(mixer, 480);

            if (block >= 8)
            {
                peakAfterTheVoice = Math.Max(peakAfterTheVoice, rendered.Max(Math.Abs));
            }
        }

        Assert.True(peakAfterTheVoice > 1e-3f, $"peak {peakAfterTheVoice}");
    }

    [Fact]
    public void TheSumOfVoicesAtPlusTwelveDecibels_AlongTheMasterGain_StaysUnderTheCeiling()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TrySetMasterLimiter(new LimiterEffect()));

        // Five stereo voices of a 1 kHz sine at 0.8 on Master: the sum peaks at 4.0 (+12 dB).
        for (var voice = 0; voice < 5; voice++)
        {
            Assert.True(mixer.TryStartResidentStereoVoice(voice, 1, SineClip(0.8), AudioVoiceParameters.Default.WithLooping(true), 1f, 1f));
        }

        var unlimited = new SoftwareMixer(OutputRate);

        for (var voice = 0; voice < 5; voice++)
        {
            Assert.True(unlimited.TryStartResidentStereoVoice(voice, 1, SineClip(0.8), AudioVoiceParameters.Default.WithLooping(true), 1f, 1f));
        }

        var bare = Render(unlimited, OutputRate / 2);
        Assert.Equal(1f, bare.Max(Math.Abs));

        Render(mixer, OutputRate / 10);
        var peak = Render(mixer, OutputRate).Max(Math.Abs);

        Assert.True(peak <= CeilingLinear + 1e-4, $"peak {peak} over the -1 dBFS ceiling");
        Assert.True(peak > 0.85, $"peak {peak}: the limiter must not over-reduce");
    }

    [Fact]
    public void WithoutALimiter_TheMixerOnlyHardClips_AndRemovingItRestoresThat()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var limiter = new LimiterEffect();
        Assert.True(mixer.TrySetMasterLimiter(limiter));
        StartConstantVoice(mixer, 0, SoftwareMixer.MasterBus, short.MaxValue);
        StartConstantVoice(mixer, 1, SoftwareMixer.MasterBus, short.MaxValue);
        Render(mixer, 4800);
        Assert.True(Last(Render(mixer, 480)) <= CeilingLinear + 1e-4);

        Assert.True(mixer.TrySetMasterLimiter(null));
        Assert.Equal(1f, Last(Render(mixer, 480)));
    }

    [Fact]
    public void WithSendsReverbAndLimiterOnFullBuses_RenderingAllocatesNothing()
    {
        var mixer = new SoftwareMixer(OutputRate);

        for (var i = 1; i < SoftwareMixer.BusCapacity; i++)
        {
            CreateBus(mixer);
        }

        for (var voice = 0; voice < 64; voice++)
        {
            var samples = new short[48000];
            Array.Fill(samples, (short)3000);
            Assert.True(mixer.TryStartResidentStereoVoice(voice, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default.WithLooping(true), 0.3f, 0.3f, 2 + (voice % 30)));
        }

        var reverb = new ReverbEffect(0.7f);
        Assert.True(mixer.TryAddEffect(1, reverb));
        Assert.True(mixer.TrySetMasterLimiter(new LimiterEffect()));

        for (var bus = 2; bus < SoftwareMixer.BusCapacity; bus++)
        {
            Assert.True(mixer.TrySetSend(bus, 1, 0.2f));
            Assert.True(mixer.TrySetSend(bus, SoftwareMixer.MasterBus, 0.1f));
        }

        var buffer = new float[480 * 2];

        // Applies the commands (the reverb memory was built on this thread when the add command was made).
        mixer.Render(buffer, 480);
        reverb.RoomSize = 0.9f;

        var before = AllocationWindow.Start();

        for (var round = 0; round < 50; round++)
        {
            mixer.Render(buffer, 480);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(buffer.All(float.IsFinite));
    }

    [Fact]
    public void TheBusApi_RefusesCyclesAndBadUse_OnTheGameThread()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var master = mixer.GetBus(AudioBusNames.Master);
        var sfx = mixer.GetBus(AudioBusNames.Sfx);
        var music = mixer.GetBus(AudioBusNames.Music);
        var reverb = mixer.CreateBus("Reverb", AudioBusNames.Master);
        var other = new AudioMixer();
        var foreign = other.CreateBus("Foreign", null);

        Assert.Throws<InvalidOperationException>(() => sfx.SetSend(sfx, 0.5f));
        Assert.Throws<InvalidOperationException>(() => master.SetSend(reverb, 0.5f));
        Assert.Throws<ArgumentNullException>(() => sfx.SetSend(null, 0.5f));
        Assert.Throws<ArgumentException>(() => sfx.SetSend(foreign, 0.5f));

        var version = mixer.SendsVersion;
        sfx.SetSend(reverb, 0.4f);
        Assert.Equal(0.4f, sfx.GetSend(reverb));
        Assert.Equal(version + 1, mixer.SendsVersion);
        sfx.SetSend(reverb, 0.4f);
        Assert.Equal(version + 1, mixer.SendsVersion);
        sfx.SetSend(reverb, float.NaN);
        Assert.Equal(0.4f, sfx.GetSend(reverb));
        sfx.SetSend(reverb, 7f);
        Assert.Equal(1f, sfx.GetSend(reverb));

        // sfx -> reverb -> music: music -> sfx and reverb -> sfx would close a cycle through the sends.
        reverb.SetSend(music, 0.5f);
        Assert.Throws<InvalidOperationException>(() => reverb.SetSend(sfx, 0.5f));
        Assert.Throws<InvalidOperationException>(() => music.SetSend(sfx, 0.5f));

        sfx.SetSend(reverb, 0f);
        Assert.Equal(0f, sfx.GetSend(reverb));
        Assert.Empty(sfx.Sends);

        var full = mixer.GetBus(AudioBusNames.Voice);
        full.SetSend(sfx, 0.1f);
        full.SetSend(music, 0.1f);
        full.SetSend(reverb, 0.1f);
        full.SetSend(master, 0.1f);
        Assert.Equal(AudioBus.MaxSends, full.Sends.Count);
        Assert.Throws<InvalidOperationException>(() => full.SetSend(mixer.GetBus(AudioBusNames.Ui), 0.1f));
    }
}
