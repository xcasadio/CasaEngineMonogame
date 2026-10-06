using System.Collections.Concurrent;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>Streamed music read on the background worker (T2.5 / P13), through the real worker thread.</summary>
public class MusicPlayerWorkerTests
{
    // 65536 stereo 16 bit frames: 16 blocks of 16 KB.
    private const int SampleCount = 65536;

    /// <summary>Seekable stream that records which thread reads, and can fail reads made off the test thread.</summary>
    private sealed class RecordingStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly int _testThreadId;
        private int _readsOnTestThread;
        private int _readsOffTestThread;
        private long _bytesRead;
        private volatile bool _isClosed;

        public RecordingStream(byte[] content, int testThreadId)
        {
            _inner = new MemoryStream(content, writable: false);
            _testThreadId = testThreadId;
        }

        public bool FailReadsOffTestThread { get; set; }

        public int ReadsOnTestThread => Volatile.Read(ref _readsOnTestThread);

        public int ReadsOffTestThread => Volatile.Read(ref _readsOffTestThread);

        public long BytesRead => Interlocked.Read(ref _bytesRead);

        public bool IsClosed => _isClosed;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Record();
            var read = _inner.Read(buffer, offset, count);
            Interlocked.Add(ref _bytesRead, read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            Record();
            var read = _inner.Read(buffer);
            Interlocked.Add(ref _bytesRead, read);
            return read;
        }

        private void Record()
        {
            if (Environment.CurrentManagedThreadId == _testThreadId)
            {
                Interlocked.Increment(ref _readsOnTestThread);
                return;
            }

            Interlocked.Increment(ref _readsOffTestThread);

            if (FailReadsOffTestThread)
            {
                throw new IOException("simulated disk failure");
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _isClosed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class RecordingProvider : IAudioClipProvider
    {
        private readonly Dictionary<Guid, byte[]> _files = new();
        private readonly int _testThreadId = Environment.CurrentManagedThreadId;

        public ConcurrentBag<RecordingStream> Streams { get; } = new();

        public bool FailReadsOffTestThread { get; set; }

        public Guid Register(byte[] content)
        {
            var id = Guid.NewGuid();
            _files.Add(id, content);
            return id;
        }

        public IAudioClip GetClip(Guid audioFileAssetId) => null;

        public Stream OpenStream(Guid audioFileAssetId)
        {
            var stream = new RecordingStream(_files[audioFileAssetId], _testThreadId)
            {
                FailReadsOffTestThread = FailReadsOffTestThread,
            };
            Streams.Add(stream);
            return stream;
        }
    }

    private sealed class Rig : IDisposable
    {
        public Rig()
        {
            Output = new OfflineAudioOutput();
            Backend = new SoftwareAudioBackend(Output, 8);
            Provider = new RecordingProvider();
            Service = new AudioService(Backend) { ClipProvider = Provider };
        }

        public OfflineAudioOutput Output { get; }
        public SoftwareAudioBackend Backend { get; }
        public RecordingProvider Provider { get; }
        public AudioService Service { get; }

        public SoundAsset CreateAsset(bool isLooped, int sampleCount = SampleCount)
        {
            var wav = WavBuilder.CreatePcm16(sampleRate: 22050, channelCount: 2, sampleCount: sampleCount, formatChunkSize: 18);
            return new SoundAsset
            {
                Name = "theme",
                AudioFileAssetId = Provider.Register(wav),
                BusName = AudioBusNames.Music,
                IsStreaming = true,
                IsLooped = isLooped,
            };
        }

        /// <summary>One game frame: the audio thread consumes some audio, then the game updates.</summary>
        public void Frame()
        {
            Output.Pump(8000);
            Service.Update(0.016f);
        }

        public bool WaitFor(Func<bool> condition, int timeoutMilliseconds = 2000)
        {
            var deadline = Environment.TickCount64 + timeoutMilliseconds;
            while (Environment.TickCount64 < deadline)
            {
                Frame();
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(2);
            }

            return condition();
        }

        public void Dispose() => Service.Dispose();
    }

    [Fact]
    public void Play_StaysSynchronous_AndLaterReadsNeverHappenOnTheGameThread()
    {
        using var rig = new Rig();
        var asset = rig.CreateAsset(isLooped: true);

        var track = rig.Service.Music.Play(asset);

        Assert.True(track.IsValid);
        Assert.True(rig.Service.Music.IsPlaying(track));
        Assert.Equal(MusicPlayer.DefaultQueuedBufferTarget, rig.Service.Music.GetPendingBufferCount(track));

        var stream = Assert.Single(rig.Provider.Streams);
        var readsAfterPlay = stream.ReadsOnTestThread;
        Assert.True(readsAfterPlay > 0, "the header and the first fill are read on the calling thread");

        Assert.True(rig.WaitFor(() => stream.ReadsOffTestThread > 0));
        for (var i = 0; i < 40; i++)
        {
            rig.Frame();
            Thread.Sleep(2);
        }

        Assert.Equal(readsAfterPlay, stream.ReadsOnTestThread);
        Assert.True(rig.Service.Music.IsPlaying(track));
        Assert.True(rig.Service.Music.GetPosition(track) > TimeSpan.Zero);
    }

    [Fact]
    public void ALoopedTrack_KeepsBeingFed_BeyondTheLengthOfTheFile()
    {
        using var rig = new Rig();
        var asset = rig.CreateAsset(isLooped: true);
        var track = rig.Service.Music.Play(asset);
        var stream = Assert.Single(rig.Provider.Streams);
        var fileBytes = SampleCount * 4L;

        var looped = rig.WaitFor(() => stream.BytesRead > fileBytes * 2);

        Assert.True(looped, "the worker must rewind and keep reading");
        Assert.True(rig.Service.Music.IsAlive(track));
        Assert.Equal(1, rig.Service.Music.ActiveTrackCount);
    }

    [Fact]
    public void ANonLoopedTrack_EndsThroughTheWorker_AndReleasesItsFile()
    {
        using var rig = new Rig();
        var asset = rig.CreateAsset(isLooped: false);
        var track = rig.Service.Music.Play(asset);
        var stream = Assert.Single(rig.Provider.Streams);

        var ended = rig.WaitFor(() => rig.Service.Music.ActiveTrackCount == 0);

        Assert.True(ended, "the track must end once every block was played");
        Assert.False(rig.Service.Music.IsAlive(track));
        Assert.True(rig.WaitFor(() => stream.IsClosed), "the worker disposes the reader");
    }

    [Fact]
    public void AReadErrorOnTheWorker_StopsTheTrack_WithoutThrowingOnTheGameThread()
    {
        using var rig = new Rig();
        rig.Provider.FailReadsOffTestThread = true;
        var asset = rig.CreateAsset(isLooped: true);
        var track = rig.Service.Music.Play(asset);
        Assert.True(track.IsValid);

        var stopped = false;
        var exception = Record.Exception(() => stopped = rig.WaitFor(() => rig.Service.Music.ActiveTrackCount == 0));

        Assert.Null(exception);
        Assert.True(stopped, "a failed track must be dropped");
        Assert.False(rig.Service.Music.IsAlive(track));
        Assert.All(rig.Provider.Streams, s => Assert.True(s.ReadsOffTestThread > 0));
    }

    [Fact]
    public void Stop_ReleasesTheReaderOnTheWorker_AndTheSlotIsReusable()
    {
        using var rig = new Rig();
        var asset = rig.CreateAsset(isLooped: true);
        var first = rig.Service.Music.Play(asset);
        var firstStream = rig.Provider.Streams.Single();

        rig.Service.Music.Stop(first);

        Assert.False(rig.Service.Music.IsAlive(first));
        Assert.True(rig.WaitFor(() => firstStream.IsClosed), "the worker must dispose the stopped track reader");

        var second = rig.Service.Music.Play(asset);
        Assert.True(second.IsValid);
        Assert.Equal(MusicPlayer.DefaultQueuedBufferTarget, rig.Service.Music.GetPendingBufferCount(second));
        Assert.True(rig.WaitFor(() => rig.Provider.Streams.Any(s => !ReferenceEquals(s, firstStream) && s.ReadsOffTestThread > 0)));
    }

    [Fact]
    public void Dispose_WithTracksInFlight_EndsTheWorkerThread_AndClosesTheFiles()
    {
        var rig = new Rig();
        rig.Service.Music.Play(rig.CreateAsset(isLooped: true));
        rig.Service.Music.Play(rig.CreateAsset(isLooped: true));
        Assert.True(rig.Service.Music.IsWorkerThreadAlive);
        Assert.True(rig.WaitFor(() => rig.Provider.Streams.All(s => s.ReadsOffTestThread > 0)));

        rig.Service.Dispose();

        Assert.False(rig.Service.Music.IsWorkerThreadAlive);
        Assert.Equal(2, rig.Provider.Streams.Count);
        Assert.All(rig.Provider.Streams, s => Assert.True(s.IsClosed));
    }

    [Fact]
    public void SteadyUpdate_OfAWorkerFedTrack_DoesNotAllocate()
    {
        using var rig = new Rig();
        var asset = rig.CreateAsset(isLooped: true);
        var track = rig.Service.Music.Play(asset);
        var stream = Assert.Single(rig.Provider.Streams);

        // Warm up: the worker has produced, the output buffer is sized, every code path was jitted.
        Assert.True(rig.WaitFor(() => stream.BytesRead > SampleCount * 4L));
        for (var i = 0; i < 20; i++)
        {
            rig.Frame();
            Thread.Sleep(2);
        }

        var before = AllocationWindow.Start();
        var positionBefore = rig.Service.Music.GetPosition(track);

        for (var i = 0; i < 100; i++)
        {
            rig.Output.Pump(8000);
            rig.Service.Update(0.016f);
            Thread.Sleep(2);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var positionAfter = rig.Service.Music.GetPosition(track);

        Assert.Equal(0, allocated);
        Assert.NotEqual(positionBefore, positionAfter);
    }
}
