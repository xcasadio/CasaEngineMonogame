namespace CasaEngine.Framework.Audio.Output.OpenAl;

/// <summary>
/// Settling window of the device latency (ADR-0094): keeps the largest latency read during the first
/// <see cref="WindowMilliseconds"/> after a start timestamp, because the value OpenAL Soft reports is the fill of
/// the device buffer at the instant of the read (0 before the first mix, then it drops between two refills).
/// No native code, and it allocates nothing after construction.
/// </summary>
internal sealed class OpenAlLatencyWindow
{
    /// <summary>Length of the window.</summary>
    public const int WindowMilliseconds = 500;

    /// <summary>Reading to pass when the device latency could not be read.</summary>
    public const long Unavailable = -1;

    private readonly long _windowTicks;
    private long _endTimestamp;
    private long _maxLatencyNanoseconds;
    private bool _hasLatency;
    private bool _started;
    private bool _over;

    /// <param name="ticksPerSecond">Unit of the timestamps given to <see cref="Start"/> and <see cref="Observe"/>
    /// (<c>Stopwatch.Frequency</c> for <c>Stopwatch.GetTimestamp()</c>).</param>
    public OpenAlLatencyWindow(long ticksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ticksPerSecond, 0);
        _windowTicks = ticksPerSecond * WindowMilliseconds / 1000;
    }

    /// <summary>True once the window has ended (a timestamp at or after its end was observed).</summary>
    public bool IsOver => _over;

    /// <summary>True when at least one reading was available inside the window.</summary>
    public bool HasLatency => _hasLatency;

    /// <summary>Largest reading inside the window, in nanoseconds (0 when no reading was available).</summary>
    public long MaxLatencyNanoseconds => _maxLatencyNanoseconds;

    /// <summary>Largest reading inside the window, in milliseconds (0 when no reading was available).</summary>
    public double MaxLatencyMilliseconds => _maxLatencyNanoseconds / 1_000_000.0;

    /// <summary>Opens the window at <paramref name="timestamp"/>.</summary>
    public void Start(long timestamp)
    {
        _endTimestamp = timestamp + _windowTicks;
        _started = true;
    }

    /// <summary>
    /// Feeds one pass. A reading taken before the end of the window (and after <see cref="Start"/>) can only raise
    /// the retained maximum; a negative reading (<see cref="Unavailable"/>) is ignored. The first call at or after
    /// the end of the window closes it and its reading is ignored, as is every later call.
    /// </summary>
    /// <returns>True once the window is over.</returns>
    public bool Observe(long timestamp, long latencyNanoseconds)
    {
        if (!_started || _over)
        {
            return _over;
        }

        if (timestamp >= _endTimestamp)
        {
            _over = true;
            return true;
        }

        if (latencyNanoseconds >= 0)
        {
            _hasLatency = true;
            if (latencyNanoseconds > _maxLatencyNanoseconds)
            {
                _maxLatencyNanoseconds = latencyNanoseconds;
            }
        }

        return false;
    }
}
