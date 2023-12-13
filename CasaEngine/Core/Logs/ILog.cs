namespace CasaEngine.Core.Logs;

public interface ILog
{
    void Close();
    void WriteTrace(string msg);
    void WriteDebug(string msg);
    void WriteInfo(string msg);
    void WriteWarning(string msg);
    void WriteError(string msg);
}