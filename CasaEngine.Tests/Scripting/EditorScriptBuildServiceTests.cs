using CasaEngine.EditorServices.Scripting;
using Xunit;

namespace CasaEngine.Tests.Scripting;

/// <summary>
/// Integration tests: they run a real `dotnet build` on a minimal fixture csproj.
/// </summary>
public class EditorScriptBuildServiceTests
{
    private static string CreateFixtureProject(string sourceCode)
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-script-build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, "FixtureScripts.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Script.cs"), sourceCode);
        return directory;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Build_ValidProject_ProducesTheAssembly()
    {
        string directory = CreateFixtureProject("namespace Fixture; public class Script { public int Value => 42; }");
        try
        {
            var result = EditorScriptBuildService.Build(
                Path.Combine(directory, "FixtureScripts.csproj"),
                Path.Combine(directory, "build-output"));

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.NotNull(result.OutputAssemblyPath);
            Assert.True(File.Exists(result.OutputAssemblyPath));
            Assert.EndsWith("FixtureScripts.dll", result.OutputAssemblyPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Build_SyntaxError_ReportsDiagnostics()
    {
        string directory = CreateFixtureProject("namespace Fixture; public class Script { this is not valid csharp }");
        try
        {
            var result = EditorScriptBuildService.Build(
                Path.Combine(directory, "FixtureScripts.csproj"),
                Path.Combine(directory, "build-output"));

            Assert.False(result.Success);
            Assert.Null(result.OutputAssemblyPath);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, error => error.Contains("error CS", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Build_MissingCsproj_FailsCleanly()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-script-build-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = EditorScriptBuildService.Build(
                Path.Combine(directory, "DoesNotExist.csproj"),
                Path.Combine(directory, "build-output"));

            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }
}
