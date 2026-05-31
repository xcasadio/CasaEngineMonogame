using Google.Protobuf;
using Yarn;
using Yarn.Compiler;

namespace CasaEngine.Compiler.Dialogue;

public sealed class YarnDialogueCompiler
{
    public YarnDialogueCompilationResult CompileFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string source = File.ReadAllText(fileName);
        return CompileString(source, fileName);
    }

    public YarnDialogueCompilationResult CompileString(string source, string fileName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        try
        {
            CompilationJob job = CompilationJob.CreateFromString(
                fileName,
                source,
                new Library(),
                global::Yarn.Compiler.Project.CurrentProjectFileVersion);
            CompilationResult result = global::Yarn.Compiler.Compiler.Compile(job);

            return new YarnDialogueCompilationResult(
                EncodeProgram(result.Program),
                BuildLineTextTable(result),
                BuildDiagnostics(result),
                result.ContainsErrors);
        }
        catch (Exception exception)
        {
            return new YarnDialogueCompilationResult(
                Array.Empty<byte>(),
                new Dictionary<string, string>(),
                new List<YarnDialogueCompilationDiagnostic>
                {
                    new(fileName, 0, 0, "Error", string.Empty, exception.Message)
                },
                containsErrors: true);
        }
    }

    private static Dictionary<string, string> BuildLineTextTable(CompilationResult result)
    {
        var lineTexts = new Dictionary<string, string>();
        foreach (var pair in result.StringTable)
        {
            lineTexts[pair.Key] = pair.Value.text ?? string.Empty;
        }

        return lineTexts;
    }

    private static byte[] EncodeProgram(Program program)
        => program == null ? Array.Empty<byte>() : program.ToByteArray();

    private static List<YarnDialogueCompilationDiagnostic> BuildDiagnostics(CompilationResult result)
    {
        var diagnostics = new List<YarnDialogueCompilationDiagnostic>();
        foreach (Diagnostic diagnostic in result.Diagnostics)
        {
            diagnostics.Add(new YarnDialogueCompilationDiagnostic(
                diagnostic.FileName,
                diagnostic.Range.Start.Line,
                diagnostic.Range.Start.Character,
                diagnostic.Severity.ToString(),
                diagnostic.Code,
                diagnostic.Message));
        }

        return diagnostics;
    }
}