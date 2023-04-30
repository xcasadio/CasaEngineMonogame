using System;
using System.ComponentModel;
using System.Windows;

namespace EditorWpf.Logs
{
    public class LogEntry
    {
        public string Severity { get; set; }
        public DateTime DateTime { get; set; }
        public int Index { get; set; }
        public string Message { get; set; }
    }
}
