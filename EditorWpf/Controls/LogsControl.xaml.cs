using System.Collections.ObjectModel;
using System.Windows.Controls;
using CasaEngine.Core.Logger;
using EditorWpf.Logs;

namespace EditorWpf.Controls
{
    public partial class LogsControl : UserControl
    {
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public LogsControl()
        {
            LogManager.Instance.AddLogger(new DebugLogger());
            LogManager.Instance.Verbosity = LogVerbosity.Trace;

            LogManager.Instance.AddLogger(new LogEditor(LogEntries));
            InitializeComponent();
        }
    }
}
