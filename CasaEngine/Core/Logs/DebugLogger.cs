using System.Diagnostics;

namespace CasaEngine.Core.Logs;

public class DebugLogger : ILog
{
    private readonly string _trace = "[Trace] ";
    private readonly string _debug = "[Debug] ";
    private readonly string _info = "[Info] ";
    private readonly string _warning = "[Warning] ";
    private readonly string _error = "[Error] ";

    public void Close()
    {
    }

    private void Write(string msg)
    {
        Debug.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}");
    }

    public void WriteTrace(string msg)
    {
        Write(_trace + msg + Environment.NewLine);
    }

    public void WriteDebug(string msg)
    {
        Write(_debug + msg + Environment.NewLine);
    }

    public void WriteInfo(string msg)
    {
        Write(_info + msg + Environment.NewLine);
    }

    public void WriteWarning(string msg)
    {
        Write(_warning + msg + Environment.NewLine);
    }

    public void WriteError(string msg)
    {
        Write(_error + msg + Environment.NewLine);
    }
}