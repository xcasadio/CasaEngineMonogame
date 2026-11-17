using CasaEngine.Framework.Audio.Output.OpenAl;
using Xunit;

namespace CasaEngine.Tests.Audio.Output;

public class OpenAlLatencyWindowTests
{
    // Timestamps in milliseconds: the window is 500 ms long.
    private const long TicksPerSecond = 1000;
    private const long Ms22 = 22_000_000;
    private const long Ms60 = 60_000_000;

    private static OpenAlLatencyWindow StartedAt(long timestamp)
    {
        var window = new OpenAlLatencyWindow(TicksPerSecond);
        window.Start(timestamp);
        return window;
    }

    [Fact]
    public void Keeps_the_largest_reading_inside_the_window()
    {
        var window = StartedAt(1000);

        Assert.False(window.Observe(1010, Ms22));
        Assert.False(window.Observe(1020, Ms60));
        Assert.False(window.Observe(1030, Ms22));

        Assert.True(window.HasLatency);
        Assert.Equal(Ms60, window.MaxLatencyNanoseconds);
        Assert.Equal(60.0, window.MaxLatencyMilliseconds, 6);
        Assert.False(window.IsOver);
    }

    [Fact]
    public void A_first_reading_of_zero_or_a_low_one_does_not_change_the_retained_maximum()
    {
        var window = StartedAt(0);

        window.Observe(5, 0);
        window.Observe(10, 1_000_000);
        window.Observe(20, Ms22);
        window.Observe(30, 0);
        window.Observe(40, 3_000_000);

        Assert.True(window.HasLatency);
        Assert.Equal(Ms22, window.MaxLatencyNanoseconds);
    }

    [Fact]
    public void Only_zero_readings_keep_a_known_latency_of_zero()
    {
        var window = StartedAt(0);

        window.Observe(5, 0);

        Assert.True(window.HasLatency);
        Assert.Equal(0, window.MaxLatencyNanoseconds);
    }

    [Fact]
    public void The_window_is_over_at_500_ms_and_later_readings_are_ignored()
    {
        var window = StartedAt(1000);
        window.Observe(1100, Ms22);

        Assert.False(window.Observe(1499, Ms22));
        Assert.False(window.IsOver);

        Assert.True(window.Observe(1500, Ms60));
        Assert.True(window.IsOver);

        Assert.True(window.Observe(1600, 5 * Ms60));
        Assert.Equal(Ms22, window.MaxLatencyNanoseconds);
    }

    [Fact]
    public void Unavailable_readings_give_an_unknown_latency()
    {
        var window = StartedAt(0);

        window.Observe(10, OpenAlLatencyWindow.Unavailable);
        window.Observe(20, OpenAlLatencyWindow.Unavailable);
        window.Observe(500, OpenAlLatencyWindow.Unavailable);

        Assert.True(window.IsOver);
        Assert.False(window.HasLatency);
    }

    [Fact]
    public void An_unavailable_reading_does_not_hide_an_available_one()
    {
        var window = StartedAt(0);

        window.Observe(10, Ms22);
        window.Observe(20, OpenAlLatencyWindow.Unavailable);

        Assert.True(window.HasLatency);
        Assert.Equal(Ms22, window.MaxLatencyNanoseconds);
    }

    [Fact]
    public void The_window_does_not_run_before_Start()
    {
        var window = new OpenAlLatencyWindow(TicksPerSecond);

        Assert.False(window.Observe(10_000, Ms22));
        Assert.False(window.HasLatency);
        Assert.False(window.IsOver);
    }

    [Fact]
    public void Observe_allocates_nothing()
    {
        var window = StartedAt(0);
        window.Observe(1, Ms22);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            window.Observe(2 + i % 400, i);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
