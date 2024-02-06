namespace CasaEngine.Core.Log;

public class FileLogger : ILogger
{
    private StreamWriter _stream;
    private readonly string _trace = "[Trace] ";
    private readonly string _debug = "[Debug] ";
    private readonly string _info = "[Info] ";
    private readonly string _warning = "[Warning] ";
    private readonly string _error = "[Error] ";

    public FileLogger(string fileName)
    {
        _stream = new StreamWriter(fileName, false);
        _stream.AutoFlush = true;
    }

    public void Close()
    {
        _stream.Close();
        _stream = null;
    }

    private void Write(string msg, bool displayTime)
    {
        if (displayTime)
        {
            _stream.Write($"{DateTime.Now:T} ");
        }

        _stream.Write(msg);
    }

    private void Write(params object[]? args)
    {
        bool first = true;

        if (args != null)
        {
            foreach (var arg in args)
            {
                if (arg is string msg)
                {
                    Write(msg, first);
                    first = false;
                }
            }
        }
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