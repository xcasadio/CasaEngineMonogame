namespace CasaEngine.Compiler.Dialogue;

public sealed class YarnDialogueCompilationResult
{
    public YarnDialogueCompilationResult(byte[] programBytes, Dictionary<string, string> lineTexts, List<YarnDialogueCompilationDiagnostic> diagnostics, bool containsErrors)
    {
        ProgramBytes = programBytes ?? Array.Empty<byte>();
        LineTexts = lineTexts ?? new Dictionary<string, string>();
        Diagnostics = diagnostics ?? new List<YarnDialogueCompilationDiagnostic>();
        ContainsErrors = containsErrors;
    }

    public byte[] ProgramBytes { get; }
    public Dictionary<string, string> LineTexts { get; }
    public List<YarnDialogueCompilationDiagnostic> Diagnostics { get; }
    public bool ContainsErrors { get; }
    public bool Success => !ContainsErrors;
}