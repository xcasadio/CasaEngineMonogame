using System;
using CasaEngine.Core.Logger;
using System.Collections.ObjectModel;

namespace EditorWpf.Logs;

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

    public void Write(params object[] args)
    {
        AddLogEntry(string.Join(" ", args));
    }

    public void WriteLineDebug(string msg)
    {
        AddLogEntry(msg);
    }

    public void WriteLineWarning(string msg)
    {
        AddLogEntry(msg);
    }

    public void WriteLineError(string msg)
    {
        AddLogEntry(msg);
    }

    private void AddLogEntry(string message)
    {
        _logs.Add(new LogEntry
        {
            DateTime = DateTime.Now,
            Message = message
        });
    }
}