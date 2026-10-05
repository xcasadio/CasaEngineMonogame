using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Psx;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// <see cref="AudioService"/> on the software backend (<see cref="IAudioBusBackend"/>, plan T5.1): voices and the SPU are
/// routed to their bus, the bus gain is applied once by the mix, and a backend without the capability keeps the
/// folded gain. One <c>Pump</c> is one audio block (480 frames, 10 ms).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceBusRoutingTests
{
    private const int Block = 480;

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    private static AudioService CreateService(out OfflineAudioOutput output, AudioMixer mixer = null)
    {
        output = new OfflineAudioOutput();
        return new AudioService(new SoftwareAudioBackend(output, 16), mixer);
    }

    // Stereo clip of a constant 0.5: a stereo voice at full volume and centre plays 0.5 on both channels.
    private static PcmAudioClip HalfScaleStereoClip()
    {
        var samples = new short[2 * 200000];
        Array.Fill(samples, (short)16384);
        return new PcmAudioClip(samples, 48000, 2);
    }

    // Peak of a settled block: two blocks first, so a gain ramp has finished.
    private static float Settled(AudioService service, OfflineAudioOutput output)
    {
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);
        return output.Pump(Block);
    }

    [Fact]
    public void TheBusGain_IsAppliedOnce_ByTheMix()
    {
        using var service = CreateService(out var output);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        service.PlayClip(HalfScaleStereoClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.Equal(0.25f, Settled(service, output), 3);
    }

    [Fact]
    public void AVoiceOnAChildBusUnderAParent_FollowsTheWholeChain_AndALaterBusIsPickedUp()
    {
        using var service = CreateService(out var output);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        service.Mixer.CreateBus("Footsteps", AudioBusNames.Sfx).Volume = 0.5f;
        service.PlayClip(HalfScaleStereoClip(), "Footsteps", AudioVoiceParameters.Default);

        Assert.Equal(0.125f, Settled(service, output), 3);

        // A bus created after the voices keeps working, and a gain change is heard after an Update.
        service.Mixer.CreateBus("Impacts", AudioBusNames.Master);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 1f;
        Assert.Equal(0.25f, Settled(service, output), 3);
    }

    [Fact]
    public void AnUnknownBusName_PlaysOnTheRoot()
    {
        using var service = CreateService(out var output);
        service.Mixer.GetBus(AudioBusNames.Master).Volume = 0.5f;
        service.PlayClip(HalfScaleStereoClip(), "NoSuchBus", AudioVoiceParameters.Default);

        Assert.Equal(0.25f, Settled(service, output), 3);
    }

    [Fact]
    public void TheMasterMute_SilencesEveryBus_AndUnmutingBringsThemBack()
    {
        using var service = CreateService(out var output);
        service.PlayClip(HalfScaleStereoClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        service.PlayClip(HalfScaleStereoClip(), AudioBusNames.Editor, AudioVoiceParameters.Default);

        service.Mixer.GetBus(AudioBusNames.Master).IsMuted = true;
        Assert.Equal(0f, Settled(service, output));

        service.Mixer.GetBus(AudioBusNames.Master).IsMuted = false;
        Assert.Equal(1f, Settled(service, output), 3);
    }

    [Fact]
    public void AVoiceFadeAndTheBusGain_AreNotAppliedTwice()
    {
        using var service = CreateService(out var output);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        var voice = service.PlayClip(HalfScaleStereoClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        service.SetVoiceVolume(voice, 0.5f);

        Assert.Equal(0.125f, Settled(service, output), 3);
        Assert.Equal(0.5f, service.GetVoiceVolume(voice));
    }

    [Fact]
    public void ThePlayStationSpu_IsRoutedToItsBus_AndItsOwnGainIsLeftAlone()
    {
        using var service = CreateService(out var output);
        service.Mixer.CreateBus("Cinematics", AudioBusNames.Music);
        var tables = new PsxSpuHardwareTables(new int[5], new int[5]);
        Assert.True(service.TryCreatePsxSpu(tables, "Cinematics", out var port));

        var block = new byte[16];
        block[1] = 7;
        Array.Fill(block, (byte)0x44, 2, 14);
        Assert.True(port.TryUpload(0x1000, block));
        Assert.True(port.TrySetStartAddress(0, 0x1000 / 8));
        Assert.True(port.TrySetPitch(0, 0x1000));
        Assert.True(port.TrySetAdsr(0, 0x1FC0000F));
        Assert.True(port.TrySetVolume(0, false, 0x3FFF));
        Assert.True(port.TrySetVolume(0, true, 0x3FFF));
        Assert.True(port.TryKeyOn(1));
        output.Pump(Block);
        output.Pump(Block);
        var full = Settled(service, output);
        Assert.True(full > 0.1f);

        service.Mixer.GetBus(AudioBusNames.Music).Volume = 0.5f;
        service.Mixer.GetBus("Cinematics").Volume = 0.5f;

        Assert.Equal(full * 0.25f, Settled(service, output), 3);
        Assert.Equal(1f, port.Gain);
    }

    [Fact]
    public void The33rdBus_IsAttachedToMaster_WithOneWarning()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var mixer = AudioBusNames.CreateDefaultMixer();

            for (var i = mixer.Buses.Count; i < 33; i++)
            {
                mixer.CreateBus($"Extra{i}", AudioBusNames.Master);
            }

            using var service = CreateService(out var output, mixer);
            Assert.Equal(33, mixer.Buses.Count);

            // The 33rd bus has its own gain, which the mix cannot honour: it plays at the level of Master.
            mixer.GetBus("Extra32").Volume = 0.25f;
            mixer.GetBus("Extra31").Volume = 0.5f;
            var last = service.PlayClip(HalfScaleStereoClip(), "Extra32", AudioVoiceParameters.Default);
            var regular = service.PlayClip(HalfScaleStereoClip(), "Extra31", AudioVoiceParameters.Default);

            Assert.True(last.IsValid);
            Assert.True(regular.IsValid);
            service.Stop(regular);
            Assert.Equal(0.5f, Settled(service, output), 3);
            Settled(service, output);

            var capacityWarnings = logger.Warnings.Count(warning => warning.Contains("mixing buses"));
            Assert.Equal(1, capacityWarnings);
        }
        finally
        {
            Logs.Close();
        }
    }

    [Fact]
    public void WithoutTheCapability_TheBusGainIsStillFoldedIntoTheVoiceVolume()
    {
        var backend = new FakeAudioBackend();
        using var service = new AudioService(backend);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.Equal(0.5f, backend.GetParameters(voice).Volume);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.25f;
        service.Update(0.016f);
        Assert.Equal(0.25f, backend.GetParameters(voice).Volume);
    }
}
