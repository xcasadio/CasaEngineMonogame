namespace CasaEngine.Core.Logger;

public interface ILog
{
    void Close();
    void Write(params object[] args);
    void WriteLineDebug(string msg);
    void WriteLineWarning(string msg);
    void WriteLineError(string msg);
}