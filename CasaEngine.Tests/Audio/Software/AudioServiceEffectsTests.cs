using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Insert effects through <see cref="AudioService"/> (plan T5.3, decision P18): routed to the software backend, absent
/// elsewhere with one log line. One pump is one audio block (480 frames, 10 ms).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceEffectsTests
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

    private static PcmAudioClip HalfScaleStereoClip()
    {
        var samples = new short[2 * 200000];
        Array.Fill(samples, (short)16384);
        return new PcmAudioClip(samples, 48000, 2);
    }

    private static float Settled(AudioService service, OfflineAudioOutput output)
    {
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);
        return output.Pump(Block);
    }

    // Makes the bus level 6 dB louder (ratio 1, only the make-up gain).
    private static CompressorEffect Doubler()
    {
        return new CompressorEffect(0f, 1f, 0f, 0.001f, 0.001f, 6.0205999f);
    }

    [Fact]
    public void AnEffectAddedToABus_IsHeard_AndRemovingItRestoresTheSignal()
    {
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        service.MasterLimiter.IsEnabled = false; // the doubled level reaches full scale; the limiter has its own tests
        var sfx = service.Mixer.GetBus(AudioBusNames.Sfx);
        service.PlayClip(HalfScaleStereoClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        Assert.Equal(0.5f, Settled(service, output), 3);

        var effect = Doubler();
        sfx.AddEffect(effect);
        Assert.Equal(1f, Settled(service, output), 3);
        Assert.Same(sfx, effect.Bus);

        Assert.True(sfx.RemoveEffect(effect));
        Assert.Null(effect.Bus);
        Assert.Equal(0.5f, Settled(service, output), 3);
    }

    [Fact]
    public void TheEffectsAddedBeforeTheService_AreSentWhenItIsCreated()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        mixer.GetBus(AudioBusNames.Sfx).AddEffect(Doubler());
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16), mixer);
        service.MasterLimiter.IsEnabled = false; // the doubled level reaches full scale; the limiter has its own tests
        service.PlayClip(HalfScaleStereoClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.Equal(1f, Settled(service, output), 3);
    }

    [Fact]
    public void ABusHoldsFourEffects_InOrder_AndRefusesBadUse()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var bus = mixer.GetBus(AudioBusNames.Sfx);
        var effects = new[] { Doubler(), Doubler(), Doubler(), Doubler() };

        foreach (var effect in effects)
        {
            bus.AddEffect(effect);
        }

        Assert.Equal(effects, bus.Effects);
        Assert.Throws<InvalidOperationException>(() => bus.AddEffect(Doubler()));
        Assert.Throws<InvalidOperationException>(() => mixer.GetBus(AudioBusNames.Music).AddEffect(effects[0]));
        Assert.Throws<ArgumentNullException>(() => bus.AddEffect(null));
        Assert.False(bus.RemoveEffect(Doubler()));
        Assert.True(bus.RemoveEffect(effects[1]));
        Assert.Equal(new[] { effects[0], effects[2], effects[3] }, bus.Effects);
    }

    [Fact]
    public void WithoutTheCapability_EffectsAreAbsent_OneLineIsLogged_AndNothingElseChanges()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var backend = new FakeAudioBackend();
            using var service = new AudioService(backend);
            service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
            var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
            var bus = service.Mixer.GetBus(AudioBusNames.Sfx);

            bus.AddEffect(Doubler());
            bus.AddEffect(new BiquadFilterEffect(BiquadFilterType.LowPass, 1000f));

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

        Assert.Single(logger.Warnings, warning => warning.Contains("insert effects"));
    }
}
