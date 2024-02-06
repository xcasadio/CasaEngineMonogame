namespace CasaEngine.Core.Log;

public interface ILogger
{
    void Close();
    void WriteTrace(string msg);
    void WriteDebug(string msg);
    void WriteInfo(string msg);
    void WriteWarning(string msg);
    void WriteError(string msg);
}