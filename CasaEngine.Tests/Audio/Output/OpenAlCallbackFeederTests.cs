using System.Runtime.InteropServices;
using CasaEngine.Framework.Audio.Output;
using CasaEngine.Framework.Audio.Output.OpenAl;
using Xunit;

namespace CasaEngine.Tests.Audio.Output;

public class OpenAlCallbackFeederTests
{
    // Timestamps in microseconds: 1000 ticks are 1 ms. 48 kHz, blocks of 480 frames (10 ms).
    private const long TicksPerSecond = 1_000_000;
    private const int SampleRate = 48000;
    private const int BlockFrames = 480;
    private const float Sentinel = 123.5f;

    private sealed class FakeClock
    {
        public long Now;

        public long Read() => Now;
    }

    private sealed class Memory : IDisposable
    {
        private readonly IntPtr _pointer;

        public Memory(int bytes)
        {
            Bytes = bytes;
            _pointer = Marshal.AllocHGlobal(bytes);
            Fill(Sentinel);
        }

        public int Bytes { get; }

        public IntPtr Pointer => _pointer;

        public void Fill(float value)
        {
            var floats = new float[Bytes / sizeof(float)];
            Array.Fill(floats, value);
            Marshal.Copy(floats, 0, _pointer, floats.Length);
            for (var i = floats.Length * sizeof(float); i < Bytes; i++)
            {
                Marshal.WriteByte(_pointer, i, 0xAB);
            }
        }

        public float ReadFloat(int index)
        {
            var one = new float[1];
            Marshal.Copy(IntPtr.Add(_pointer, index * sizeof(float)), one, 0, 1);
            return one[0];
        }

        public byte ReadByte(int index) => Marshal.ReadByte(_pointer, index);

        public void Dispose() => Marshal.FreeHGlobal(_pointer);
    }

    // Writes the running count of the frame, as the same value on both channels, so a test sees the order of the blocks.
    private sealed class CountingRender
    {
        public int NextFrame;
        public int Calls;
        public int LargestBlock;

        public void Render(Span<float> interleavedStereo, int frameCount)
        {
            Calls++;
            LargestBlock = Math.Max(LargestBlock, frameCount);
            for (var i = 0; i < frameCount; i++)
            {
                interleavedStereo[2 * i] = NextFrame;
                interleavedStereo[2 * i + 1] = -NextFrame;
                NextFrame++;
            }
        }
    }

    private static OpenAlCallbackFeeder NewFeeder(AudioRenderCallback render, FakeClock clock)
    {
        return new OpenAlCallbackFeeder(render, SampleRate, BlockFrames, TicksPerSecond, clock.Read);
    }

    [Fact]
    public void Renders_the_frames_asked_into_the_given_memory_and_returns_the_byte_count()
    {
        var render = new CountingRender();
        var feeder = NewFeeder(render.Render, new FakeClock());
        using var memory = new Memory(100 * OpenAlCallbackFeeder.BytesPerFrame);

        var written = feeder.Fill(memory.Pointer, memory.Bytes);

        Assert.Equal(memory.Bytes, written);
        Assert.Equal(0f, memory.ReadFloat(0));
        Assert.Equal(-0f, memory.ReadFloat(1));
        Assert.Equal(99f, memory.ReadFloat(2 * 99));
        Assert.Equal(-99f, memory.ReadFloat(2 * 99 + 1));
        Assert.Equal(1, render.Calls);
        Assert.Null(feeder.Failure);
    }

    [Fact]
    public void A_remainder_shorter_than_a_frame_is_zero_filled()
    {
        var render = new CountingRender();
        var feeder = NewFeeder(render.Render, new FakeClock());
        using var memory = new Memory(10 * OpenAlCallbackFeeder.BytesPerFrame + 7);

        var written = feeder.Fill(memory.Pointer, memory.Bytes);

        Assert.Equal(memory.Bytes, written);
        Assert.Equal(10, render.NextFrame);
        Assert.Equal(9f, memory.ReadFloat(2 * 9));
        for (var i = 0; i < 7; i++)
        {
            Assert.Equal(0, memory.ReadByte(10 * OpenAlCallbackFeeder.BytesPerFrame + i));
        }
    }

    [Fact]
    public void A_request_shorter_than_a_frame_is_all_zero()
    {
        var render = new CountingRender();
        var feeder = NewFeeder(render.Render, new FakeClock());
        using var memory = new Memory(16);

        var written = feeder.Fill(memory.Pointer, 5);

        Assert.Equal(5, written);
        Assert.Equal(0, render.Calls);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(0, memory.ReadByte(i));
        }
    }

    [Fact]
    public void A_request_longer_than_the_array_is_rendered_in_blocks_and_copied_in_order()
    {
        var render = new CountingRender();
        var feeder = NewFeeder(render.Render, new FakeClock());
        const int frames = 2 * BlockFrames + 37;
        using var memory = new Memory(frames * OpenAlCallbackFeeder.BytesPerFrame);

        var written = feeder.Fill(memory.Pointer, memory.Bytes);

        Assert.Equal(memory.Bytes, written);
        Assert.Equal(3, render.Calls);
        Assert.Equal(BlockFrames, render.LargestBlock);
        for (var frame = 0; frame < frames; frame++)
        {
            Assert.Equal((float)frame, memory.ReadFloat(2 * frame));
            Assert.Equal((float)-frame, memory.ReadFloat(2 * frame + 1));
        }
    }

    [Fact]
    public void A_render_that_throws_gives_silence_is_reported_and_later_calls_give_silence()
    {
        var calls = 0;
        var feeder = NewFeeder((Span<float> span, int frames) =>
        {
            calls++;
            span.Fill(0.5f);
            throw new InvalidOperationException("render failed");
        }, new FakeClock());
        using var memory = new Memory(3 * BlockFrames * OpenAlCallbackFeeder.BytesPerFrame);

        var written = feeder.Fill(memory.Pointer, memory.Bytes);

        Assert.Equal(memory.Bytes, written);
        Assert.IsType<InvalidOperationException>(feeder.Failure);
        AssertSilence(memory);

        memory.Fill(Sentinel);
        written = feeder.Fill(memory.Pointer, memory.Bytes);

        Assert.Equal(memory.Bytes, written);
        Assert.Equal(1, calls);
        AssertSilence(memory);
    }

    [Fact]
    public void A_call_that_finds_the_render_taken_gives_silence()
    {
        FakeClock clock = new();
        OpenAlCallbackFeeder feeder = null;
        using var inner = new Memory(50 * OpenAlCallbackFeeder.BytesPerFrame);
        var innerWritten = -1;
        var outerCalls = 0;
        feeder = NewFeeder((Span<float> span, int frames) =>
        {
            outerCalls++;
            span.Fill(1f);
            // Re-entry stands for a second thread arriving while the render runs.
            innerWritten = feeder.Fill(inner.Pointer, inner.Bytes);
        }, clock);
        using var outer = new Memory(20 * OpenAlCallbackFeeder.BytesPerFrame);

        var written = feeder.Fill(outer.Pointer, outer.Bytes);

        Assert.Equal(outer.Bytes, written);
        Assert.Equal(1, outerCalls);
        Assert.Equal(inner.Bytes, innerWritten);
        AssertSilence(inner);
        Assert.Equal(1f, outer.ReadFloat(0));
        Assert.Null(feeder.Failure);

        // The guard was released: the next call renders again.
        feeder.Fill(outer.Pointer, outer.Bytes);
        Assert.Equal(2, outerCalls);
    }

    [Fact]
    public void The_threshold_is_twice_the_largest_request_without_a_device_clock()
    {
        var clock = new FakeClock();
        var feeder = NewFeeder((span, frames) => { }, clock);
        using var memory = new Memory(504 * OpenAlCallbackFeeder.BytesPerFrame);

        feeder.Fill(memory.Pointer, 480 * OpenAlCallbackFeeder.BytesPerFrame);
        feeder.Fill(memory.Pointer, 504 * OpenAlCallbackFeeder.BytesPerFrame);
        feeder.Fill(memory.Pointer, 100 * OpenAlCallbackFeeder.BytesPerFrame);

        Assert.True(feeder.CompleteWindow(OpenAlLatencyWindow.Unavailable));
        Assert.Equal(504, feeder.MaxRequestFrames);
        // 2 * 504 / 48000 s = 21 ms = 21000 ticks.
        Assert.Equal(21_000, feeder.ThresholdTicks);
        Assert.Equal(21.0, feeder.ThresholdMilliseconds, 6);
        Assert.True(feeder.IsCounting);
    }

    [Fact]
    public void The_threshold_is_the_larger_of_the_window_latency_and_twice_the_largest_request()
    {
        var feeder = NewFeeder((span, frames) => { }, new FakeClock());
        using var memory = new Memory(480 * OpenAlCallbackFeeder.BytesPerFrame);
        feeder.Fill(memory.Pointer, memory.Bytes);

        // 60 ms of latency beats 20 ms of requests.
        Assert.True(feeder.CompleteWindow(60_000_000));
        Assert.Equal(60.0, feeder.ThresholdMilliseconds, 6);

        // A latency below the 20 ms of requests (5 ms, or 0) does not lower the threshold.
        var second = NewFeeder((span, frames) => { }, new FakeClock());
        second.Fill(memory.Pointer, memory.Bytes);
        Assert.True(second.CompleteWindow(5_000_000));
        Assert.Equal(20.0, second.ThresholdMilliseconds, 6);

        var third = NewFeeder((span, frames) => { }, new FakeClock());
        third.Fill(memory.Pointer, memory.Bytes);
        Assert.True(third.CompleteWindow(0));
        Assert.Equal(20.0, third.ThresholdMilliseconds, 6);
    }

    [Fact]
    public void A_window_that_saw_no_request_sets_no_threshold()
    {
        var feeder = NewFeeder((span, frames) => { }, new FakeClock());

        Assert.False(feeder.CompleteWindow(22_000_000));
        Assert.False(feeder.IsCounting);
        Assert.Equal(0, feeder.ThresholdTicks);
    }

    [Fact]
    public void A_first_latency_reading_of_zero_or_low_goes_through_the_window_into_the_threshold()
    {
        // The window keeps the largest reading: 0 first, then 22 ms.
        var window = new OpenAlLatencyWindow(TicksPerSecond);
        window.Start(0);
        window.Observe(1_000, 0);
        window.Observe(10_000, 22_000_000);
        window.Observe(20_000, 3_000_000);
        window.Observe(600_000, 99_000_000);

        var feeder = NewFeeder((span, frames) => { }, new FakeClock());
        using var memory = new Memory(240 * OpenAlCallbackFeeder.BytesPerFrame);
        feeder.Fill(memory.Pointer, memory.Bytes);

        Assert.True(window.IsOver);
        Assert.True(feeder.CompleteWindow(window.HasLatency ? window.MaxLatencyNanoseconds : OpenAlLatencyWindow.Unavailable));
        Assert.Equal(22.0, feeder.ThresholdMilliseconds, 6);
    }

    [Fact]
    public void Nothing_is_counted_during_the_window()
    {
        var clock = new FakeClock();
        var feeder = NewFeeder((span, frames) => { }, clock);
        using var memory = new Memory(480 * OpenAlCallbackFeeder.BytesPerFrame);

        feeder.Fill(memory.Pointer, memory.Bytes);
        clock.Now += 400_000;
        feeder.Fill(memory.Pointer, memory.Bytes);
        clock.Now += 400_000;
        feeder.Fill(memory.Pointer, memory.Bytes);

        Assert.Equal(0, feeder.UnderrunCount);
        Assert.False(feeder.IsCounting);
    }

    [Fact]
    public void A_late_call_is_counted_above_the_threshold_and_not_at_or_below_it()
    {
        var clock = new FakeClock();
        var feeder = NewFeeder((span, frames) => { }, clock);
        using var memory = new Memory(480 * OpenAlCallbackFeeder.BytesPerFrame);
        feeder.Fill(memory.Pointer, memory.Bytes);
        Assert.True(feeder.CompleteWindow(OpenAlLatencyWindow.Unavailable));
        Assert.Equal(20_000, feeder.ThresholdTicks);

        clock.Now += 10_000;
        feeder.Fill(memory.Pointer, memory.Bytes);
        clock.Now += 20_000;
        feeder.Fill(memory.Pointer, memory.Bytes);
        Assert.Equal(0, feeder.UnderrunCount);

        clock.Now += 20_001;
        feeder.Fill(memory.Pointer, memory.Bytes);
        Assert.Equal(1, feeder.UnderrunCount);

        clock.Now += 15_000;
        feeder.Fill(memory.Pointer, memory.Bytes);
        Assert.Equal(1, feeder.UnderrunCount);

        clock.Now += 100_000;
        feeder.Fill(memory.Pointer, memory.Bytes);
        Assert.Equal(2, feeder.UnderrunCount);
    }

    [Fact]
    public void A_thousand_calls_allocate_nothing()
    {
        var clock = new FakeClock();
        var render = new CountingRender();
        var feeder = NewFeeder(render.Render, clock);
        using var memory = new Memory(1100 * OpenAlCallbackFeeder.BytesPerFrame + 3);
        feeder.Fill(memory.Pointer, memory.Bytes);
        feeder.CompleteWindow(22_000_000);
        feeder.Fill(memory.Pointer, memory.Bytes);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            clock.Now += 10_000 + (i % 7 == 0 ? 50_000 : 0);
            feeder.Fill(memory.Pointer, memory.Bytes);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(feeder.UnderrunCount > 0);
    }

    [Fact]
    public void Drain_renders_the_frames_due_and_keeps_the_fraction_for_the_next_call()
    {
        var clock = new FakeClock { Now = 5_000 };
        var render = new CountingRender();
        var feeder = NewFeeder(render.Render, clock);
        feeder.StartDrain(clock.Now);

        Assert.Equal(0, feeder.Drain());

        // 10 ms at 48 kHz: 480 frames, one block.
        clock.Now += 10_000;
        Assert.Equal(480, feeder.Drain());
        Assert.Equal(480, render.NextFrame);
        Assert.Equal(1, render.Calls);

        // 25 ms more: 1200 frames, three blocks of at most 480.
        clock.Now += 25_000;
        Assert.Equal(1200, feeder.Drain());
        Assert.Equal(1680, render.NextFrame);
        Assert.Equal(4, render.Calls);
        Assert.Equal(BlockFrames, render.LargestBlock);

        // 1 tick is 0.048 frame: nothing is due, then the total stays exact (35 ms = 1680 frames, 0 left).
        clock.Now += 1;
        Assert.Equal(0, feeder.Drain());
        Assert.Equal(1680, render.NextFrame);
    }

    [Fact]
    public void Drain_renders_at_most_one_second_of_frames_and_skips_the_excess()
    {
        var clock = new FakeClock();
        var render = new CountingRender();
        var feeder = NewFeeder(render.Render, clock);
        feeder.StartDrain(clock.Now);

        clock.Now += 5 * TicksPerSecond;
        Assert.Equal(SampleRate, feeder.Drain());
        Assert.Equal(SampleRate, render.NextFrame);

        // The skipped 4 s are not owed: only the time after the drain is due.
        clock.Now += 10_000;
        Assert.Equal(480, feeder.Drain());
    }

    [Fact]
    public void Drain_does_nothing_before_it_is_started_and_after_a_failure()
    {
        var clock = new FakeClock { Now = 1_000_000 };
        var render = new CountingRender();
        var feeder = NewFeeder(render.Render, clock);

        Assert.Equal(0, feeder.Drain());
        Assert.Equal(0, render.Calls);

        var failing = NewFeeder((span, frames) => throw new InvalidOperationException("boom"), clock);
        failing.StartDrain(clock.Now);
        clock.Now += 10_000;
        Assert.Equal(0, failing.Drain());
        Assert.IsType<InvalidOperationException>(failing.Failure);
        clock.Now += 10_000;
        Assert.Equal(0, failing.Drain());
    }

    [Fact]
    public void Drain_finds_the_render_taken_and_renders_nothing()
    {
        var clock = new FakeClock();
        OpenAlCallbackFeeder feeder = null;
        var drained = -1;
        var calls = 0;
        feeder = NewFeeder((span, frames) =>
        {
            calls++;
            drained = feeder.Drain();
        }, clock);
        using var memory = new Memory(10 * OpenAlCallbackFeeder.BytesPerFrame);
        feeder.StartDrain(clock.Now);
        clock.Now += 10_000;

        feeder.Fill(memory.Pointer, memory.Bytes);

        Assert.Equal(0, drained);
        Assert.Equal(1, calls);
    }

    private static void AssertSilence(Memory memory)
    {
        for (var i = 0; i < memory.Bytes; i++)
        {
            Assert.Equal(0, memory.ReadByte(i));
        }
    }
}
