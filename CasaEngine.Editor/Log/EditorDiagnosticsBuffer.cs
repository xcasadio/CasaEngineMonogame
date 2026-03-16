using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Core.Log;

namespace CasaEngine.Editor.Log;

public readonly record struct DiagnosticEntry(DateTime Timestamp, LogVerbosity Verbosity, string Message);

public static class EditorDiagnosticsBuffer
{
    private const int MaxEntries = 256;
    private static readonly object Sync = new();
    private static readonly List<DiagnosticEntry> Entries = new();

    public static void Append(LogVerbosity verbosity, string message)
    {
        lock (Sync)
        {
            Entries.Add(new DiagnosticEntry(DateTime.Now, verbosity, message));
            if (Entries.Count > MaxEntries)
            {
                Entries.RemoveRange(0, Entries.Count - MaxEntries);
            }
        }
    }

    public static IReadOnlyList<DiagnosticEntry> GetEntriesSnapshot()
    {
        lock (Sync)
        {
            return Entries.ToArray();
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Entries.Clear();
        }
    }
}