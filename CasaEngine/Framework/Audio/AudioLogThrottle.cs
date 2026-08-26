using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.Audio;

/// <summary>
/// Rate limiter for the audio error logs. An audio failure usually repeats every frame
/// (a broken asset, saturated hardware voices), and CLAUDE.md forbids log spam in hot paths:
/// the first occurrence is logged, the following ones are counted and summarized later.
/// </summary>
public sealed class AudioLogThrottle
{
    private readonly long _intervalMilliseconds;
    private long _nextAllowedTimestamp;
    private int _suppressedCount;

    public AudioLogThrottle(int intervalMilliseconds = 5000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalMilliseconds);
        _intervalMilliseconds = intervalMilliseconds;
    }

    /// <summary>Number of messages dropped since the last emitted one.</summary>
    public int SuppressedCount => _suppressedCount;

    public void WriteWarning(string message)
    {
        Write(message, isError: false);
    }

    public void WriteError(string message)
    {
        Write(message, isError: true);
    }

    private void Write(string message, bool isError)
    {
        var now = CurrentTimestamp();

        if (now < _nextAllowedTimestamp)
        {
            _suppressedCount++;
            return;
        }

        var suppressed = _suppressedCount;
        _suppressedCount = 0;
        _nextAllowedTimestamp = now + _intervalMilliseconds;

        var text = suppressed > 0
            ? $"{message} ({suppressed} similar message(s) suppressed)"
            : message;

        if (isError)
        {
            Logs.WriteError(text);
        }
        else
        {
            Logs.WriteWarning(text);
        }
    }

    // Monotonic and allocation free; only the elapsed delta matters here.
    private static long CurrentTimestamp() => Environment.TickCount64;
}
