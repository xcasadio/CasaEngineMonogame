using System.Collections.ObjectModel;
using System.Windows.Controls;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Log;

namespace CasaEngine.EditorUI.Controls;

public partial class LogsControl : UserControl
{
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public LogsControl()
    {
        Logs.AddLogger(new DebugLogger());
        Logs.Verbosity = LogVerbosity.Trace;

        Logs.AddLogger(new LoggerEditor(LogEntries));
        InitializeComponent();
    }
}