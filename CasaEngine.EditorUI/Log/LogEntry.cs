﻿using System;

namespace CasaEngine.EditorUI.Log;

public class LogEntry
{
    public string Severity { get; set; }
    public DateTime DateTime { get; set; }
    public int Index { get; set; }
    public string Message { get; set; }
}