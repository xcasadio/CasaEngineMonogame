namespace CasaEngine.Core.Logs;

public enum LogVerbosity
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    None = 5
}

public sealed class LogManager : ILogger
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

    public void WriteTrace(string msg)
    {
        if (_verbosity > LogVerbosity.Trace)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteTrace(msg);
        }
    }

    public void WriteDebug(string msg)
    {
        if (_verbosity > LogVerbosity.Debug)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteDebug(msg);
        }
    }

    public void WriteInfo(string msg)
    {
        if (_verbosity > LogVerbosity.Info)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteInfo(msg);
        }
    }

    public void WriteWarning(string msg)
    {
        if (_verbosity > LogVerbosity.Warning)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteWarning(msg);
        }
    }

    public void WriteError(string msg)
    {
        if (_verbosity > LogVerbosity.Error)
        {
            return;
        }

        foreach (var log in _loggers)
        {
            log.WriteError(msg);
        }
    }

    public void WriteException(Exception e)
    {
        if (_verbosity > LogVerbosity.Error)
        {
            return;
        }

        WriteError(e.ToString());
    }
}