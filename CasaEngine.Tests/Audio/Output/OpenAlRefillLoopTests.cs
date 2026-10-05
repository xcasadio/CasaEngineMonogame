using CasaEngine.Framework.Audio.Output;
using CasaEngine.Framework.Audio.Output.OpenAl;
using Xunit;

namespace CasaEngine.Tests.Audio.Output;

public class OpenAlRefillLoopTests
{
    private const int BufferFrames = 480;

    private sealed class FakeStream : IOpenAlStream
    {
        public readonly Queue<uint> Queued = new();
        public readonly List<uint> Submitted = new();
        public int Processed;
        public bool Stopped;
        public int PlayCount;

        public int GetProcessedBufferCount() => Processed;

        public uint UnqueueBuffer()
        {
            Processed--;
            return Queued.Dequeue();
        }

        public void FillAndQueueBuffer(uint buffer, float[] interleavedStereo, int frameCount)
        {
            Submitted.Add(buffer);
            Queued.Enqueue(buffer);
        }

        public bool IsSourceStopped() => Stopped;

        public void PlaySource()
        {
            PlayCount++;
            Stopped = false;
        }
    }

    private static (OpenAlRefillLoop Loop, FakeStream Stream, List<int> FrameCounts) Create(uint[] buffers)
    {
        var stream = new FakeStream();
        var frameCounts = new List<int>();
        var loop = new OpenAlRefillLoop(stream, buffers, BufferFrames, (span, frames) =>
        {
            Assert.True(span.Length >= 2 * frames);
            frameCounts.Add(frames);
        });
        return (loop, stream, frameCounts);
    }

    [Fact]
    public void Prime_QueuesEveryBufferInOrder_ThenPlays()
    {
        var (loop, stream, frameCounts) = Create(new uint[] { 11, 12, 13, 14 });

        loop.Prime();

        Assert.Equal(new uint[] { 11, 12, 13, 14 }, stream.Submitted);
        Assert.Equal(new[] { BufferFrames, BufferFrames, BufferFrames, BufferFrames }, frameCounts);
        Assert.Equal(1, stream.PlayCount);
        Assert.Equal(0, loop.UnderrunCount);
    }

    [Fact]
    public void Pump_RefillsProcessedBuffersInOrder()
    {
        var (loop, stream, frameCounts) = Create(new uint[] { 11, 12, 13, 14 });
        loop.Prime();
        stream.Submitted.Clear();
        frameCounts.Clear();

        stream.Processed = 2;
        loop.Pump();

        Assert.Equal(new uint[] { 11, 12 }, stream.Submitted);
        Assert.Equal(new[] { BufferFrames, BufferFrames }, frameCounts);
        Assert.Equal(1, stream.PlayCount);
        Assert.Equal(0, loop.UnderrunCount);
    }

    [Fact]
    public void Pump_WithNothingProcessed_DoesNotRender()
    {
        var (loop, stream, frameCounts) = Create(new uint[] { 11, 12 });
        loop.Prime();
        frameCounts.Clear();

        loop.Pump();

        Assert.Empty(frameCounts);
    }

    [Fact]
    public void Pump_WithStoppedSource_CountsOneUnderrun_AndRestarts()
    {
        var (loop, stream, _) = Create(new uint[] { 11, 12 });
        loop.Prime();

        stream.Processed = 2;
        stream.Stopped = true;
        loop.Pump();

        Assert.Equal(1, loop.UnderrunCount);
        Assert.Equal(2, stream.PlayCount);
        Assert.False(stream.Stopped);

        loop.Pump();
        Assert.Equal(1, loop.UnderrunCount);
    }

    [Fact]
    public void Pump_AfterStop_DoesNothing()
    {
        var (loop, stream, frameCounts) = Create(new uint[] { 11, 12 });
        loop.Prime();
        frameCounts.Clear();
        loop.Stop();

        stream.Processed = 2;
        stream.Stopped = true;
        loop.Pump();

        Assert.Empty(frameCounts);
        Assert.Equal(0, loop.UnderrunCount);
        Assert.Equal(1, stream.PlayCount);
    }
}
