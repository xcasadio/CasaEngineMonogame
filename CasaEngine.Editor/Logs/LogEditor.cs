using System;
using CasaEngine.Core.Logger;
using System.Collections.ObjectModel;

namespace CasaEngine.Editor.Logs;

public class LogEditor : ILog
{
    private readonly ObservableCollection<LogEntry> _logs;

    public LogEditor(ObservableCollection<LogEntry> logs)
    {
        _logs = logs;
    }

    public void Close()
    {
    }

    public void WriteLineTrace(string msg)
    {
        AddLogEntry(LogVerbosity.Trace, msg);
    }

    public void WriteLineDebug(string msg)
    {
        AddLogEntry(LogVerbosity.Debug, msg);
    }

    public void WriteLineInfo(string msg)
    {
        AddLogEntry(LogVerbosity.Info, msg);
    }

    public void WriteLineWarning(string msg)
    {
        AddLogEntry(LogVerbosity.Warning, msg);
    }

    public void WriteLineError(string msg)
    {
        AddLogEntry(LogVerbosity.Error, msg);
    }

    private void AddLogEntry(LogVerbosity logVerbosity, string message)
    {
        _logs.Add(new LogEntry
        {
            Severity = Enum.GetName(logVerbosity),
            DateTime = DateTime.Now,
            Message = message
        });
    }
}