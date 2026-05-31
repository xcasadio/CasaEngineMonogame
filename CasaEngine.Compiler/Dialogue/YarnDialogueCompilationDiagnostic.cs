namespace CasaEngine.Compiler.Dialogue;

public sealed class YarnDialogueCompilationDiagnostic
{
    public YarnDialogueCompilationDiagnostic(string fileName, int line, int column, string severity, string code, string message)
    {
        FileName = fileName ?? string.Empty;
        Line = line;
        Column = column;
        Severity = severity ?? string.Empty;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public string FileName { get; }
    public int Line { get; }
    public int Column { get; }
    public string Severity { get; }
    public string Code { get; }
    public string Message { get; }
}