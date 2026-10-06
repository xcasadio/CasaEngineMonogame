using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Level metering through <see cref="AudioService"/> (plan T6.1, decision P22): the bus name to index mapping, the levels read
/// from the software backend, and the backends without the capability (one throttled line, nothing else changes).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceMeteringTests
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

    private static PcmAudioClip ConstantStereoClip(short value)
    {
        var samples = new short[2 * 200000];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, 48000, 2);
    }

    [Fact]
    public void TheLevelOfABus_IsReadByTheIndexTheServiceGives()
    {
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        service.MasterLimiter.IsEnabled = false;
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        service.PlayClip(ConstantStereoClip(16384), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.True(service.IsMeteringAvailable);
        Assert.True(service.TryGetMeterBusIndex(AudioBusNames.Sfx, out var sfx));
        Assert.True(service.TryGetMeterBusIndex(AudioBusNames.Music, out var music));
        Assert.True(service.TryGetMeterBusIndex(AudioBusNames.Master, out var master));
        Assert.Equal(0, master);
        Assert.NotEqual(sfx, music);
        Assert.False(service.TryGetMeterBusIndex("Nope", out var unknown));
        Assert.Equal(-1, unknown);

        var levels = new AudioLevel[CasaEngine.Framework.Audio.Software.SoftwareMixer.BusCapacity];
        var cursor = new AudioMeterCursor();
        service.Update(0.01f);

        for (var i = 0; i < 4; i++)
        {
            output.Pump(Block);
        }

        Assert.True(service.TryReadLevels(ref cursor, levels, out var master0, out _));
        service.Update(0.01f);
        output.Pump(Block);
        Assert.True(service.TryReadLevels(ref cursor, levels, out var final, out var read));

        // 0.5 voice, 0.5 bus gain.
        Assert.Equal(1, read.BlockCount);
        Assert.Equal(0.25f, levels[sfx].Peak, 1e-3f);
        Assert.Equal(0.25f, levels[master].Peak, 1e-3f);
        Assert.Equal(0.25f, final.Peak, 1e-3f);
        Assert.Equal(0.25f, final.Rms, 1e-3f);
        Assert.Equal(0f, levels[music].Peak);
        Assert.True(master0.Peak >= 0f);
    }

    [Fact]
    public void ABusBeyondTheBackendCapacity_IsUnavailableAndNotReportedAsMaster()
    {
        var output = new OfflineAudioOutput();
        var mixer = AudioBusNames.CreateDefaultMixer();

        for (var i = 0; i < 40; i++)
        {
            mixer.CreateBus($"Extra{i}", AudioBusNames.Master);
        }

        using var service = new AudioService(new SoftwareAudioBackend(output, 16), mixer);
        var capacity = ((IAudioBusBackend)service.Backend).BusCapacity;
        var available = 0;

        for (var i = 0; i < 40; i++)
        {
            if (service.TryGetMeterBusIndex($"Extra{i}", out var index))
            {
                Assert.InRange(index, 1, capacity - 1);
                available++;
            }
        }

        // Master plus the default buses plus the extras fill the 32 backend buses; the rest has no level of its own.
        Assert.True(available > 0 && available < 40);
        Assert.False(service.TryGetMeterBusIndex("Extra39", out var last));
        Assert.Equal(-1, last);
    }

    [Fact]
    public void WithoutTheCapability_OneLineSaysSo_AndNothingElseChanges()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var backend = new FakeAudioBackend();
            using var service = new AudioService(backend);
            service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
            var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
            var levels = new AudioLevel[4];
            levels[0] = new AudioLevel(1f, 1f, 1f, 1);
            var cursor = new AudioMeterCursor();

            Assert.False(service.IsMeteringAvailable);
            Assert.False(service.TryGetMeterBusIndex(AudioBusNames.Sfx, out var index));
            Assert.Equal(-1, index);

            for (var i = 0; i < 5; i++)
            {
                Assert.False(service.TryReadLevels(ref cursor, levels, out var output, out var read));
                Assert.Equal(0f, output.Peak);
                Assert.Equal(0, read.BlockCount);
                service.Update(0.016f);
            }

            Assert.Equal(0f, levels[0].Peak);

            // The folded gain of the fallback is untouched.
            Assert.Equal(0.5f, backend.GetParameters(voice).Volume);
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(logger.Warnings, warning => warning.Contains("meters"));
    }
}
