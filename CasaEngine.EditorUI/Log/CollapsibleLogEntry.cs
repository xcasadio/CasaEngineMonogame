using System.Collections.Generic;

namespace CasaEngine.EditorUI.Log;

public class CollapsibleLogEntry : LogEntry
{
    public List<LogEntry> Contents { get; set; }
}