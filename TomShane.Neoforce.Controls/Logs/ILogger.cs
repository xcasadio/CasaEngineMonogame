
namespace TomShane.Neoforce.Controls.Logs;

public interface ILogger
{
    void WriteTrace(string msg);
    void WriteDebug(string msg);
    void WriteInfo(string msg);
    void WriteWarning(string msg);
    void WriteError(string msg);
}