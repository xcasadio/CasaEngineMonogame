using System;
using CasaEngine.Core.Logs;
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

    public void WriteTrace(string msg)
    {
        AddLogEntry(LogVerbosity.Trace, msg);
    }

    public void WriteDebug(string msg)
    {
        AddLogEntry(LogVerbosity.Debug, msg);
    }

    public void WriteInfo(string msg)
    {
        AddLogEntry(LogVerbosity.Info, msg);
    }

    public void WriteWarning(string msg)
    {
        AddLogEntry(LogVerbosity.Warning, msg);
    }

    public void WriteError(string msg)
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