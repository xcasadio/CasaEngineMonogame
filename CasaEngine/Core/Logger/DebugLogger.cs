using System.Diagnostics;

namespace CasaEngine.Core.Logger;

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
        Debug.Write($"{DateTime.Now:T} {msg}");
    }

    public void WriteLineTrace(string msg)
    {
        Write(_trace + msg + Environment.NewLine);
    }

    public void WriteLineDebug(string msg)
    {
        Write(_debug + msg + Environment.NewLine);
    }

    public void WriteLineInfo(string msg)
    {
        Write(_info + msg + Environment.NewLine);
    }

    public void WriteLineWarning(string msg)
    {
        Write(_warning + msg + Environment.NewLine);
    }

    public void WriteLineError(string msg)
    {
        Write(_error + msg + Environment.NewLine);
    }
}