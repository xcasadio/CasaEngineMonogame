using System.Collections.Generic;

namespace CasaEngine.Editor.Logs;

public class CollapsibleLogEntry : LogEntry
{
    public List<LogEntry> Contents { get; set; }
}