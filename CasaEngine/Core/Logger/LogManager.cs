using System.Text;

namespace CasaEngine.Core.Logger;

public enum LogVerbosity
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    None = 5
}

public sealed class LogManager
{

    private static LogManager? _instance;
    private readonly List<ILog> _loggers = new();
    private LogVerbosity _verbosity = LogVerbosity.Debug;

    public static LogManager Instance
    {
        get { return _instance ??= new LogManager(); }
    }

    public LogVerbosity Verbosity
    {
        get => _verbosity;
        set => _verbosity = value;
    }

    public void AddLogger(ILog log)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        _loggers.Add(log);
    }

    public void Close()
    {
        foreach (var log in _loggers)
        {
            log.Close();
        }

        _loggers.Clear();
    }

    public void WriteLineTrace(string msg)
    {
        if (_verbosity > LogVerbosity.Trace)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteLineTrace(msg);
        }
    }

    public void WriteLineDebug(string msg)
    {
        if (_verbosity > LogVerbosity.Debug)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteLineDebug(msg);
        }
    }

    public void WriteLineInfo(string msg)
    {
        if (_verbosity > LogVerbosity.Info)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteLineInfo(msg);
        }
    }

    public void WriteLineWarning(string msg)
    {
        if (_verbosity > LogVerbosity.Warning)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteLineWarning(msg);
        }
    }

    public void WriteLineError(string msg)
    {
        if (_verbosity > LogVerbosity.Error)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteLineError(msg);
        }
    }

    public void WriteException(Exception e, bool writeStackTrace = true)
    {
        if (_verbosity > LogVerbosity.Error)
        {
            return;
        }

        WriteLineError(e.ToString());
    }
}