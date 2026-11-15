using System;
using System.Collections.Generic;
using CasaEngine.Editor.Log;

namespace CasaEngine.Editor.Remote;

/// <summary>
/// Reads the editor log (<see cref="LoggerEditor"/>, already registered by the editor) from a cursor that survives
/// trims (ADR-0092, plan T2.2): entry <c>i</c> of <see cref="LoggerEditor.Entries"/> has sequence
/// <c>trimmed + i</c>, where <c>trimmed</c> counts every entry dropped by <see cref="LoggerEditor.EntriesTrimmed"/>.
/// Game thread only, like the logger.
/// </summary>
internal sealed class EditorRemoteLogReader : IDisposable
{
    public const int MaxEntriesPerRead = 200;
    public const int MaxCharactersPerRead = 64 * 1024;

    private readonly LoggerEditor _logger;
    private long _trimmed;

    public EditorRemoteLogReader(LoggerEditor logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _logger.EntriesTrimmed += OnEntriesTrimmed;
    }

    /// <summary>Sequence of the oldest entry still held.</summary>
    public long FirstSequence => _trimmed;

    /// <summary>Sequence the next appended entry will get.</summary>
    public long NextSequence => _trimmed + _logger.Entries.Count;

    /// <summary>
    /// Entries from <paramref name="fromSequence"/> (null: the latest <paramref name="maxEntries"/>), capped by count and
    /// by total message length. <see cref="RemoteLogPage.Lost"/> counts entries trimmed before they could be read.
    /// </summary>
    public RemoteLogPage Read(long? fromSequence, int maxEntries)
    {
        maxEntries = Math.Clamp(maxEntries, 1, MaxEntriesPerRead);
        IReadOnlyList<LogEntry> entries = _logger.Entries;
        long next = NextSequence;
        long start = fromSequence ?? Math.Max(_trimmed, next - maxEntries);
        long lost = 0;
        if (start < _trimmed)
        {
            lost = _trimmed - start;
            start = _trimmed;
        }

        var page = new List<RemoteLogLine>();
        int characters = 0;
        long sequence = Math.Min(start, next);
        while (sequence < next && page.Count < maxEntries)
        {
            LogEntry entry = entries[(int)(sequence - _trimmed)];
            string message = EditorRemoteWorldModel.Truncate(entry.Message ?? string.Empty);
            if (page.Count > 0 && characters + message.Length > MaxCharactersPerRead)
            {
                break;
            }

            characters += message.Length;
            page.Add(new RemoteLogLine
            {
                Sequence = sequence,
                Time = entry.Timestamp.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                Level = entry.Verbosity.ToString().ToLowerInvariant(),
                Message = message,
            });
            sequence++;
        }

        return new RemoteLogPage { Entries = page, NextSequence = sequence, Lost = lost };
    }

    public void Dispose() => _logger.EntriesTrimmed -= OnEntriesTrimmed;

    private void OnEntriesTrimmed(object sender, int removed) => _trimmed += removed;
}

internal sealed class RemoteLogPage
{
    public IReadOnlyList<RemoteLogLine> Entries { get; init; }

    /// <summary>Cursor for the next read.</summary>
    public long NextSequence { get; init; }

    public long Lost { get; init; }
}

internal sealed class RemoteLogLine
{
    public long Sequence { get; init; }

    public string Time { get; init; }

    public string Level { get; init; }

    public string Message { get; init; }
}
