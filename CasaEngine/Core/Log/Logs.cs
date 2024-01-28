namespace CasaEngine.Core.Log;

public static class Logs
{
    private static readonly List<ILogger> _loggers = new();
    private static LogVerbosity _verbosity = LogVerbosity.Debug;

    public static LogVerbosity Verbosity
    {
        get => _verbosity;
        set => _verbosity = value;
    }

    public static void AddLogger(ILogger logger)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        _loggers.Add(logger);
    }

    public static void Close()
    {
        foreach (var log in _loggers)
        {
            log.Close();
        }

        _loggers.Clear();
    }

    public static void WriteTrace(string msg)
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

    public static void WriteDebug(string msg)
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

    public static void WriteInfo(string msg)
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

    public static void WriteWarning(string msg)
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

    public static void WriteError(string msg)
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

    public static void WriteException(Exception e)
    {
        if (_verbosity > LogVerbosity.Error)
        {
            return;
        }

        WriteError(e.ToString());
    }
}