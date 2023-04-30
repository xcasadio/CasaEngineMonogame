namespace CasaEngine.Core.Logger;

public interface ILog
{
    void Close();
    void WriteLineTrace(string msg);
    void WriteLineDebug(string msg);
    void WriteLineInfo(string msg);
    void WriteLineWarning(string msg);
    void WriteLineError(string msg);
}