using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// A looped streamed track loops back to <see cref="SoundAsset.LoopStartSample"/> instead of frame 0 (plan E19.L-a, ADR-0074).
/// Both loop sites are covered: the inline fill of <see cref="MusicPlayer"/> (also the synchronous first fill of
/// <see cref="MusicPlayer.Play"/> in every mode) and the streaming worker, which is the production path. Every player here is
/// built with an explicit queue target of 3, so the default queue depth can change without moving these values.
/// The process-global <see cref="Logs"/> are captured, hence the serialized collection.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class MusicPlayerLoopStartTests
{
    private const int Target = 3;
    private const int BigBuffer = 16384;

    private static readonly AudioVoiceHandle Voice = new(0, 0);

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

    /// <summary>
    /// Stereo 16 bit 44.1 kHz ramp: frame f is (left f, right 10 000 + f), so a frame identifies its own index in both
    /// channels. <paramref name="formatChunkSize"/> and <paramref name="listBytes"/> move the data offset away from 44
    /// (18 gives 46, 18 plus a 6 byte LIST chunk gives 60).
    /// </summary>
    internal static byte[] Ramp(int frames, int formatChunkSize = 16, int listBytes = 0)
    {
        var data = new byte[frames * 4];
        for (var f = 0; f < frames; f++)
        {
            BitConverter.TryWriteBytes(data.AsSpan(f * 4, 2), (short)f);
            BitConverter.TryWriteBytes(data.AsSpan((f * 4) + 2, 2), (short)(10000 + f));
        }

        return WavBuilder.Create(
            WavBuilder.PcmFormatTag,
            44100,
            2,
            16,
            data,
            formatChunkSize,
            listBytes > 0 ? new byte[listBytes] : null,
            listBytes > 0 ? "LIST" : null);
    }

    private static AudioService CreateService(out FakeAudioBackend backend, out FakeAudioClipProvider provider, bool record = true)
    {
        backend = new FakeAudioBackend { RecordSubmittedBuffers = record };
        provider = new FakeAudioClipProvider();
        return new AudioService(backend) { ClipProvider = provider };
    }

    private static SoundAsset CreateAsset(FakeAudioClipProvider provider, byte[] wav, bool looped, long loopStart)
    {
        return new SoundAsset
        {
            Name = "ramp",
            AudioFileAssetId = provider.RegisterStream(wav),
            BusName = AudioBusNames.Music,
            IsStreaming = true,
            IsLooped = looped,
            LoopStartSample = loopStart,
        };
    }

    private static int[] Sizes(FakeAudioBackend backend)
        => backend.GetSubmittedBuffers(Voice).Select(buffer => buffer.Length).ToArray();

    private static List<(short Left, short Right)> Frames(FakeAudioBackend backend)
    {
        var frames = new List<(short, short)>();
        foreach (var buffer in backend.GetSubmittedBuffers(Voice))
        {
            for (var i = 0; i < buffer.Length; i += 4)
            {
                frames.Add((BitConverter.ToInt16(buffer, i), BitConverter.ToInt16(buffer, i + 2)));
            }
        }

        return frames;
    }

    /// <summary>Frame i of the stream is i below the frame count, then loopStart + (i - frames) mod (frames - loopStart).</summary>
    private static void AssertLoopSequence(FakeAudioBackend backend, int frames, int loopStart)
    {
        var sequence = Frames(backend);
        var period = frames - loopStart;
        for (var i = 0; i < sequence.Count; i++)
        {
            var expected = i < frames ? i : loopStart + ((i - frames) % period);
            Assert.True(
                sequence[i].Left == expected && sequence[i].Right == 10000 + expected,
                $"frame {i}: ({sequence[i].Left},{sequence[i].Right}) instead of ({expected},{10000 + expected})");
        }
    }

    /// <summary>The worker fills the ring on its own thread: update until the backend holds <paramref name="wanted"/> buffers.</summary>
    private static void PumpWorker(MusicPlayer player, FakeAudioBackend backend, int wanted)
    {
        var deadline = Environment.TickCount64 + 2000;
        while (backend.GetSubmittedBuffers(Voice).Count < wanted && Environment.TickCount64 < deadline)
        {
            player.Update(0.016f);
            Thread.Sleep(2);
        }
    }

    private static void TopUp(MusicPlayer player, FakeAudioBackend backend, bool worker, int expectedBuffers)
    {
        backend.ConsumeBuffers(Voice, Target);
        if (worker)
        {
            PumpWorker(player, backend, expectedBuffers);
        }
        else
        {
            player.Update(0.016f);
        }
    }

    // L1 and L6, inline and worker: the track loops back to frame 600 of 1000. L6 puts the data at offset 60.
    [Theory]
    [InlineData(false, 18, 0)]
    [InlineData(true, 18, 0)]
    [InlineData(false, 18, 6)]
    [InlineData(true, 18, 6)]
    public void Play_ALoopedTrackLoopsBackToTheLoopStart(bool worker, int formatChunkSize, int listBytes)
    {
        var service = CreateService(out var backend, out var provider);
        using var player = new MusicPlayer(service, worker, BigBuffer, Target);

        Assert.True(player.Play(CreateAsset(provider, Ramp(1000, formatChunkSize, listBytes), true, 600)).IsValid);

        // The first buffer is the whole file, then each seam submits only the loop region (frames 600 to 999).
        Assert.Equal(new[] { 4000, 1600, 1600 }, Sizes(backend));

        TopUp(player, backend, worker, 6);

        Assert.Equal(new[] { 4000, 1600, 1600, 1600, 1600, 1600 }, Sizes(backend));
        AssertLoopSequence(backend, 1000, 600);
    }

    // L2 and L2w: a buffer smaller than the region crosses the seam; the worker crosses it itself.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Update_SmallBuffersCrossTheSeamAtTheLoopStart(bool worker)
    {
        var service = CreateService(out var backend, out var provider);
        using var player = new MusicPlayer(service, worker, 1024, Target);
        player.Play(CreateAsset(provider, Ramp(1000, 18), true, 600));

        Assert.Equal(new[] { 1024, 1024, 1024 }, Sizes(backend));

        TopUp(player, backend, worker, 6);
        TopUp(player, backend, worker, 9);

        Assert.Equal(new[] { 1024, 1024, 1024, 928, 1024, 576, 1024, 576, 1024 }, Sizes(backend));
        AssertLoopSequence(backend, 1000, 600);
    }

    // L3: a loop start of 0 is today's behaviour, byte for byte.
    [Fact]
    public void Play_ALoopStartOfZeroLoopsToTheStartOfTheFile()
    {
        var service = CreateService(out var backend, out var provider);
        using var player = new MusicPlayer(service, BigBuffer, Target);
        player.Play(CreateAsset(provider, Ramp(1000, 18), true, 0));

        Assert.Equal(new[] { 4000, 4000, 4000 }, Sizes(backend));

        TopUp(player, backend, false, 6);

        Assert.Equal(Enumerable.Repeat(4000, 6), Sizes(backend));
        AssertLoopSequence(backend, 1000, 0);
    }

    // L4: a track that is not looped ignores the loop start and ends.
    [Fact]
    public void Play_ATrackThatIsNotLoopedIgnoresTheLoopStartAndEnds()
    {
        var service = CreateService(out var backend, out var provider);
        using var player = new MusicPlayer(service, BigBuffer, Target);
        var track = player.Play(CreateAsset(provider, Ramp(1000, 18), false, 600));

        Assert.Equal(new[] { 4000 }, Sizes(backend));

        backend.ConsumeBuffers(Voice, 1);
        player.Update(0.016f);

        Assert.False(player.IsAlive(track));
    }

    // L5: a loop start at the end of the data or past it falls back to 0 with one warning, and the track still plays.
    [Theory]
    [InlineData(1000L)]
    [InlineData(5_000_000L)]
    public void Play_ALoopStartAtOrPastTheEndFallsBackToZeroWithOneWarning(long loopStart)
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var service = CreateService(out var backend, out var provider);
            using var player = new MusicPlayer(service, BigBuffer, Target);

            var track = player.Play(CreateAsset(provider, Ramp(1000, 18), true, loopStart));

            Assert.True(track.IsValid);
            Assert.Equal(new[] { 4000, 4000, 4000 }, Sizes(backend));
            AssertLoopSequence(backend, 1000, 0);
            Assert.Single(logger.Warnings, warning => warning.Contains("loop start"));
        }
        finally
        {
            Logs.Close();
        }
    }

    // L7: the steady state of a looped track with a loop start allocates nothing.
    [Fact]
    public void Update_ALoopedTrackWithALoopStartAllocatesNothing()
    {
        var service = CreateService(out var backend, out var provider, record: false);
        using var player = new MusicPlayer(service, BigBuffer, Target);
        player.Play(CreateAsset(provider, WavBuilder.CreatePcm16(44100, 2, 65536, 18), true, 32768));
        for (var i = 0; i < 20; i++)
        {
            backend.ConsumeBuffers(Voice, 2);
            player.Update(0.016f);
        }

        var before = AllocationWindow.Start();
        for (var i = 0; i < 200; i++)
        {
            backend.ConsumeBuffers(Voice, 2);
            player.Update(0.016f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // L8: the last frame is a valid loop start; it documents the short-region limit (one frame per buffer).
    [Fact]
    public void Play_TheLastFrameIsAValidLoopStartAndSubmitsOneFrameBuffers()
    {
        var service = CreateService(out var backend, out var provider);
        using var player = new MusicPlayer(service, BigBuffer, Target);
        player.Play(CreateAsset(provider, Ramp(1000, 18), true, 999));

        Assert.Equal(new[] { 4000, 4, 4 }, Sizes(backend));

        TopUp(player, backend, false, 6);

        Assert.Equal(new[] { 4000, 4, 4, 4, 4, 4 }, Sizes(backend));
        AssertLoopSequence(backend, 1000, 999);
    }

    // B2: a backend that never reports a pending buffer must not turn the fill into an endless loop; the per-call cap
    // (at most one queue's worth of buffers) holds across the seam. Without the cap this test would spin, hence the guard.
    [Fact]
    public async Task Play_AndUpdate_AreBoundedPerCallWhenTheBackendReportsNoPendingBuffer()
    {
        var service = CreateService(out var backend, out var provider, record: false);
        backend.ReportsZeroPendingBuffers = true;
        var player = new MusicPlayer(service, BigBuffer, Target);
        var asset = CreateAsset(provider, Ramp(1000, 18), true, 600);
        long afterPlay = -1;
        long afterUpdate = -1;

        var run = Task.Run(() =>
        {
            player.Play(asset);
            afterPlay = backend.GetSubmittedBytes(Voice);
            player.Update(0.016f);
            afterUpdate = backend.GetSubmittedBytes(Voice);
        });

        Assert.True(await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10))) == run, "the per-call bound must stop the fill");
        Assert.Equal(7200, afterPlay);
        Assert.Equal(12000, afterUpdate);
        player.Dispose();
    }
}
