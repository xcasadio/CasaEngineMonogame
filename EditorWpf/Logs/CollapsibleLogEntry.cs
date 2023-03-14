using System.Collections.Generic;

namespace EditorWpf.Logs;

public class CollapsibleLogEntry : LogEntry
{
    public List<LogEntry> Contents { get; set; }
}