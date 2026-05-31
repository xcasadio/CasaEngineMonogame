using CasaEngine.Compiler.Dialogue;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

public sealed class YarnDialogueCompilerTests
{
    [Fact]
    public void CompileFile_CompilesGreetingFixture()
    {
        string fileName = Path.Combine(FindRepositoryRoot(), "CasaEngine.Tests", "Dialogue", "Fixtures", "greeting.yarn");
        var compiler = new YarnDialogueCompiler();

        YarnDialogueCompilationResult result = compiler.CompileFile(fileName);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.NotEmpty(result.ProgramBytes);
        Assert.Contains(result.LineTexts.Values, text => text == "Bonjour depuis CasaEngine.");
    }

    [Fact]
    public void CompileString_CapturesCompilationErrors()
    {
        var compiler = new YarnDialogueCompiler();

        YarnDialogueCompilationResult result = compiler.CompileString("title: Start\n---\n<<if>>\n===", "invalid.yarn");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
    }

    private static string FindRepositoryRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "CasaEngine.MonoGame.sln")))
        {
            DirectoryInfo parent = Directory.GetParent(directory);
            if (parent == null)
            {
                throw new InvalidOperationException("Cannot find repository root.");
            }

            directory = parent.FullName;
        }

        return directory;
    }
}