using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;

namespace CasaEngine.Editor.Log;

/// <summary>
/// An individual log entry captured by <see cref="LoggerEditor"/>.
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; }
    public LogVerbosity Verbosity { get; }
    public string Message { get; }

    public LogEntry(LogVerbosity verbosity, string message)
    {
        Timestamp = DateTime.Now;
        Verbosity = verbosity;
        Message = message;
    }

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss}] [{Verbosity}] {Message}";
}

/// <summary>
/// An <see cref="ILogger"/> implementation that keeps the most recent log entries in memory
/// and fires <see cref="EntryAdded"/> so the UI can react in real time.
/// Retention is bounded by <see cref="MaxEntries"/>.
/// Register via <c>Logs.AddLogger(new LoggerEditor())</c> at editor startup.
/// <para/>
/// Not thread-safe: entries must be appended from a single thread, and both events are raised on the calling thread.
/// </summary>
public sealed class LoggerEditor : ILogger
{
    /// <summary>Default value of <see cref="MaxEntries"/>.</summary>
    public const int DefaultMaxEntries = 10_000;

    /// <summary>How many entries are discarded in one go once <see cref="MaxEntries"/> is exceeded, as a fraction of
    /// <see cref="MaxEntries"/>.<para/>
    /// Trimming in batches is what keeps the amortized cost per entry constant: evicting a single entry per new entry
    /// would force every listener to resynchronize on every single log line.</summary>
    private const int TrimBatchDivisor = 10;

    private readonly List<LogEntry> _entries = new();
    private int _maxEntries = DefaultMaxEntries;

    /// <summary>All stored entries in insertion order.</summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <summary>Upper bound on how many entries are retained. Once exceeded, the oldest entries are discarded in
    /// batches and <see cref="EntriesTrimmed"/> is raised.<para/>
    /// Default value: <see cref="DefaultMaxEntries"/></summary>
    public int MaxEntries
    {
        get => _maxEntries;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxEntries), "MaxEntries must be greater than zero.");
            }

            if (_maxEntries != value)
            {
                _maxEntries = value;
                TrimIfNeeded();
            }
        }
    }

    /// <summary>Raised on the calling thread each time a new <see cref="LogEntry"/> is appended.</summary>
    public event EventHandler<LogEntry> EntryAdded;

    /// <summary>Raised after the oldest entries have been discarded, with the number of entries removed from the front
    /// of <see cref="Entries"/>. Also raised by <see cref="Clear"/>.<para/>
    /// Listeners that mirror <see cref="Entries"/> must resynchronize when this fires.</summary>
    public event EventHandler<int> EntriesTrimmed;

    // ── ILogger implementation ───────────────────────────────────────────────

    public void Close() { }

    public void WriteTrace(string msg)   => Append(LogVerbosity.Trace,   msg);
    public void WriteDebug(string msg)   => Append(LogVerbosity.Debug,   msg);
    public void WriteInfo(string msg)    => Append(LogVerbosity.Info,    msg);
    public void WriteWarning(string msg) => Append(LogVerbosity.Warning, msg);
    public void WriteError(string msg)   => Append(LogVerbosity.Error,   msg);

    // ── Helpers ───────────────────────────────────────────────────────────────

    public void Clear()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        int removedCount = _entries.Count;
        _entries.Clear();
        EntriesTrimmed?.Invoke(this, removedCount);
    }

    private void Append(LogVerbosity verbosity, string message)
    {
        var entry = new LogEntry(verbosity, message);
        _entries.Add(entry);

        //  Announce the new entry before trimming so listeners can append incrementally, then resynchronize
        //  if the trim actually fired. The reverse order would make them see the entry twice.
        EntryAdded?.Invoke(this, entry);
        TrimIfNeeded();
    }

    private void TrimIfNeeded()
    {
        if (_entries.Count <= _maxEntries)
        {
            return;
        }

        int batchSize = Math.Max(1, _maxEntries / TrimBatchDivisor);
        int removedCount = Math.Min(_entries.Count, _entries.Count - _maxEntries + batchSize);
        _entries.RemoveRange(0, removedCount);
        EntriesTrimmed?.Invoke(this, removedCount);
    }
}
