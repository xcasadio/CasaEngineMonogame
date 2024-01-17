using System.Collections.Generic;

namespace CasaEngine.EditorUI.Logs;

public class CollapsibleLogEntry : LogEntry
{
    public List<LogEntry> Contents { get; set; }
}