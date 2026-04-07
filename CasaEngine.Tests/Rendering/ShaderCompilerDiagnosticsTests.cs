using CasaEngine.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ShaderCompilerDiagnosticsTests
{
    [Fact]
    public void FormatCompilerDiagnosticLine_NormalizesAbsoluteDiagnosticLine()
    {
        const string sourceFile = @"D:\Shaders\LitForward.fx";
        const string diagnosticLine = @"D:\Shaders\Lighting.fxh(18,9): warning X3206: implicit truncation of vector type";

        string formattedLine = ShaderCompiler.FormatCompilerDiagnosticLine(diagnosticLine, sourceFile);

        Assert.Equal(diagnosticLine, formattedLine);
    }

    [Fact]
    public void FormatCompilerDiagnosticLine_UsesSourceFileWhenCompilerOmitsDiagnosticPath()
    {
        const string sourceFile = @"D:\Shaders\LitForward.fx";
        const string diagnosticLine = "(42,7): error X3000: syntax error: unexpected token";

        string formattedLine = ShaderCompiler.FormatCompilerDiagnosticLine(diagnosticLine, sourceFile);

        Assert.Equal(@"D:\Shaders\LitForward.fx(42,7): error X3000: syntax error: unexpected token", formattedLine);
    }

    [Fact]
    public void FormatCompilerDiagnosticLine_ResolvesRelativeIncludeAgainstSourceDirectory()
    {
        const string sourceFile = @"D:\Shaders\LitForward.fx";
        const string diagnosticLine = @"Lighting.fxh(8,2): warning X3557: loop only executes for 4 iterations";

        string formattedLine = ShaderCompiler.FormatCompilerDiagnosticLine(diagnosticLine, sourceFile);

        Assert.Equal(@"D:\Shaders\Lighting.fxh(8,2): warning X3557: loop only executes for 4 iterations", formattedLine);
    }

    [Fact]
    public void FormatCompilerDiagnostics_PreservesUnparsedLinesAlongsideDiagnostics()
    {
        const string sourceFile = @"D:\Shaders\LitForward.fx";
        string compilerOutput = string.Join(Environment.NewLine,
            "Compiling effect",
            @"Lighting.fxh(8,2): warning X3557: loop only executes for 4 iterations");

        string formattedDiagnostics = ShaderCompiler.FormatCompilerDiagnostics(compilerOutput, sourceFile);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Compiling effect",
                @"D:\Shaders\Lighting.fxh(8,2): warning X3557: loop only executes for 4 iterations"),
            formattedDiagnostics);
    }

    [Fact]
    public void ProcessErrorsAndWarnings_OnBuildFailure_ThrowsNormalizedDiagnostics()
    {
        const string sourceFile = @"D:\Shaders\LitForward.fx";
        string compilerOutput = string.Join(Environment.NewLine,
            "(12,3): error X3000: syntax error: unexpected token",
            @"Lighting.fxh(8,2): warning X3557: loop only executes for 4 iterations");

        var exception = Assert.Throws<InvalidOperationException>(() => ShaderCompiler.ProcessErrorsAndWarnings(true, compilerOutput, sourceFile));

        Assert.Contains(@"D:\Shaders\LitForward.fx(12,3): error X3000: syntax error: unexpected token", exception.Message);
        Assert.Contains(@"D:\Shaders\Lighting.fxh(8,2): warning X3557: loop only executes for 4 iterations", exception.Message);
    }
}