using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Sends, return buses and the Master limiter through <see cref="AudioService"/> (plan T5.4, decision P18): routed to the
/// software backend, absent elsewhere with one log line each. One pump is one audio block (480 frames, 10 ms).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceSendsTests
{
    private const int Block = 480;
    private const float Ceiling = 0.89125094f; // -1 dBFS

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

    private static PcmAudioClip ConstantStereoClip(short value)
    {
        var samples = new short[2 * 200000];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, 48000, 2);
    }

    private static float Settled(AudioService service, OfflineAudioOutput output)
    {
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);
        return output.Pump(Block);
    }

    [Fact]
    public void ASendToAReturnBus_IsHeard_AndRemovingItRestoresTheSignal()
    {
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        var sfx = service.Mixer.GetBus(AudioBusNames.Sfx);
        var returnBus = service.Mixer.CreateBus("Return", AudioBusNames.Master);
        service.PlayClip(ConstantStereoClip(8192), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        Assert.Equal(0.25f, Settled(service, output), 3);

        sfx.SetSend(returnBus, 0.5f);
        Assert.Equal(0.375f, Settled(service, output), 3);

        sfx.SetSend(returnBus, 0f);
        Assert.Equal(0.25f, Settled(service, output), 3);
    }

    [Fact]
    public void TheSendsSetBeforeTheService_AreSentWhenItIsCreated()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var returnBus = mixer.CreateBus("Return", AudioBusNames.Master);
        mixer.GetBus(AudioBusNames.Sfx).SetSend(returnBus, 1f);
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16), mixer);
        service.PlayClip(ConstantStereoClip(8192), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.Equal(0.5f, Settled(service, output), 3);
    }

    [Fact]
    public void AMutedBus_SilencesItsSend()
    {
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        var sfx = service.Mixer.GetBus(AudioBusNames.Sfx);
        var returnBus = service.Mixer.CreateBus("Return", AudioBusNames.Master);
        sfx.SetSend(returnBus, 1f);
        service.PlayClip(ConstantStereoClip(8192), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        Assert.Equal(0.5f, Settled(service, output), 3);

        sfx.IsMuted = true;
        Assert.Equal(0f, Settled(service, output), 3);
    }

    [Fact]
    public void TheMasterLimiter_IsOnByDefault_HoldsALoudMixUnderMinusOneDbfs_AndCanBeSwitchedOff()
    {
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        Assert.True(service.MasterLimiter.IsEnabled);
        Assert.Equal(-1f, service.MasterLimiter.CeilingDb);

        for (var i = 0; i < 3; i++)
        {
            service.PlayClip(ConstantStereoClip(25000), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        }

        service.Update(0.01f);

        for (var i = 0; i < 20; i++)
        {
            output.Pump(Block);
        }

        Assert.True(output.Pump(Block) <= Ceiling + 1e-3f);

        service.MasterLimiter.IsEnabled = false;
        service.Update(0.01f);
        Assert.Equal(1f, output.Pump(Block));
    }

    [Fact]
    public void WithoutTheCapability_SendsAndTheLimiterAreAbsent_OneLineEach_AndNothingElseChanges()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var backend = new FakeAudioBackend();
            using var service = new AudioService(backend);
            service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
            var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
            var returnBus = service.Mixer.CreateBus("Return", AudioBusNames.Master);

            service.Mixer.GetBus(AudioBusNames.Sfx).SetSend(returnBus, 0.5f);
            returnBus.AddEffect(new ReverbEffect());
            service.MasterLimiter.CeilingDb = -3f;
            _ = service.MasterLimiter;

            for (var i = 0; i < 5; i++)
            {
                service.Update(0.016f);
            }

            // The folded gain of the fallback is untouched.
            Assert.Equal(0.5f, backend.GetParameters(voice).Volume);
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(logger.Warnings, warning => warning.Contains("sends"));
        Assert.Single(logger.Warnings, warning => warning.Contains("limiter"));
        Assert.Single(logger.Warnings, warning => warning.Contains("insert effects"));
    }
}
