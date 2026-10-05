using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Psx;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>Without the SPU capability (fake backend), <see cref="AudioService.TryCreatePsxSpu"/> returns false and logs once.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServicePsxSpuLoggingTests
{
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

    [Fact]
    public void WithoutTheCapability_ReturnsFalse_AndLogsOnce()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            using var service = new AudioService(new FakeAudioBackend());
            var tables = new PsxSpuHardwareTables(new int[5], new int[5]);

            Assert.False(service.TryCreatePsxSpu(tables, AudioBusNames.Music, out var first));
            Assert.False(service.TryCreatePsxSpu(tables, AudioBusNames.Music, out var second));
            Assert.Null(first);
            Assert.Null(second);
        }
        finally
        {
            Logs.Close();
        }

        var messages = logger.Warnings.Where(message => message.Contains("SPU unavailable")).ToList();
        Assert.Single(messages);
    }
}
