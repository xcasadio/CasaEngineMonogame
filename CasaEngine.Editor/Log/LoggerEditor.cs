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
/// An <see cref="ILogger"/> implementation that stores every log entry in memory
/// and fires <see cref="EntryAdded"/> so the UI can react in real time.
/// Register via <c>Logs.AddLogger(new LoggerEditor())</c> at editor startup.
/// </summary>
public sealed class LoggerEditor : ILogger
{
    private readonly List<LogEntry> _entries = new();

    /// <summary>All stored entries in insertion order.</summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <summary>Raised on the calling thread each time a new <see cref="LogEntry"/> is appended.</summary>
    public event EventHandler<LogEntry>? EntryAdded;

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
        _entries.Clear();
    }

    private void Append(LogVerbosity verbosity, string message)
    {
        var entry = new LogEntry(verbosity, message);
        _entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
    }
}
