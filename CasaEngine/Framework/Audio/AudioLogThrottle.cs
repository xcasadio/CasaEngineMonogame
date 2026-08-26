using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.Audio;

/// <summary>
/// Rate limiter for the audio error logs. An audio failure usually repeats every frame
/// (a broken asset, saturated hardware voices), and CLAUDE.md forbids log spam in hot paths:
/// the first occurrence is logged, the following ones are counted and summarized later.
/// </summary>
/// <remarks>
/// For a message built by string interpolation, gate it so the string is not even built when it
/// would be dropped:
/// <code>
/// if (log.ShouldWrite())
/// {
///     log.WriteNow($"Audio: sound '{asset.Name}' has no file.");
/// }
/// </code>
/// <see cref="WriteWarning"/> and <see cref="WriteError"/> are the shorthand for constant messages,
/// where building the string costs nothing.
/// </remarks>
public sealed class AudioLogThrottle
{
    private readonly long _intervalMilliseconds;
    private long _nextAllowedTimestamp;
    private int _suppressedCount;
    private int _pendingSuppressedCount;

    public AudioLogThrottle(int intervalMilliseconds = 5000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalMilliseconds);
        _intervalMilliseconds = intervalMilliseconds;
    }

    /// <summary>Number of messages dropped since the last emitted one.</summary>
    public int SuppressedCount => _suppressedCount;

    /// <summary>
    /// True when a message may be emitted now; otherwise the occurrence is counted and dropped.
    /// A true answer arms the next window, so it must be followed by a <see cref="WriteNow"/>.
    /// </summary>
    public bool ShouldWrite()
    {
        var now = Environment.TickCount64;

        if (now < _nextAllowedTimestamp)
        {
            _suppressedCount++;
            return false;
        }

        _pendingSuppressedCount = _suppressedCount;
        _suppressedCount = 0;
        _nextAllowedTimestamp = now + _intervalMilliseconds;
        return true;
    }

    /// <summary>Emits a message allowed by <see cref="ShouldWrite"/>, with the suppressed count.</summary>
    public void WriteNow(string message, bool isError = false)
    {
        var suppressed = _pendingSuppressedCount;
        _pendingSuppressedCount = 0;

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

    /// <summary>Shorthand for a constant message.</summary>
    public void WriteWarning(string message)
    {
        if (ShouldWrite())
        {
            WriteNow(message);
        }
    }

    /// <summary>Shorthand for a constant message.</summary>
    public void WriteError(string message)
    {
        if (ShouldWrite())
        {
            WriteNow(message, isError: true);
        }
    }
}
