using CasaEngine.EditorServices.Scaffolding;
using CasaEngine.EditorServices.Scripting;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

public class EditorGameplayProjectScaffolderTests
{
    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-gameplay-scaffold-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
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

    private static string CreateFakeEngineDirectory(params string[] assemblyFileNames)
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-fake-engine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var name in assemblyFileNames)
        {
            File.WriteAllText(Path.Combine(directory, name), "not a real assembly");
        }

        return directory;
    }

    [Fact]
    public void Scaffold_NewProject_CreatesAllExpectedFilesWithExpectedContent()
    {
        string projectPath = CreateTempDirectory();
        string engineDirectory = CreateFakeEngineDirectory("CasaEngine.dll");
        try
        {
            var result = EditorGameplayProjectScaffolder.Scaffold(projectPath, "MonProjet", engineDirectory);

            Assert.False(result.Skipped);
            Assert.Equal("Gameplay/MonProjet.Gameplay.csproj", result.GameplayCsprojRelativePath);
            Assert.Equal("MonProjet.Gameplay.dll", result.GameplayDllName);

            string gameplayDirectory = Path.Combine(projectPath, "Gameplay");
            string csprojPath = Path.Combine(gameplayDirectory, "MonProjet.Gameplay.csproj");
            string propsPath = Path.Combine(gameplayDirectory, "CasaEngine.EnginePath.props");
            string gitIgnorePath = Path.Combine(gameplayDirectory, ".gitignore");
            string gamePluginPath = Path.Combine(gameplayDirectory, "GamePlugin.cs");
            string sampleProxyPath = Path.Combine(gameplayDirectory, "Scripts", "SampleProxy.cs");
            string slnPath = Path.Combine(projectPath, "MonProjet.sln");

            Assert.True(File.Exists(csprojPath));
            Assert.True(File.Exists(propsPath));
            Assert.True(File.Exists(gitIgnorePath));
            Assert.True(File.Exists(gamePluginPath));
            Assert.True(File.Exists(sampleProxyPath));
            Assert.True(File.Exists(slnPath));

            string csproj = File.ReadAllText(csprojPath);
            Assert.Contains("<TargetFramework>net9.0-windows</TargetFramework>", csproj);
            Assert.Contains("<AssemblyName>MonProjet.Gameplay</AssemblyName>", csproj);
            Assert.Contains("<Import Project=\"CasaEngine.EnginePath.props\" Condition=\"Exists('CasaEngine.EnginePath.props')\" />", csproj);
            Assert.Contains("<HintPath>$(CasaEnginePath)\\CasaEngine.dll</HintPath>", csproj);
            Assert.Contains("<HintPath>$(CasaEnginePath)\\MonoGame.Framework.dll</HintPath>", csproj);
            Assert.Contains("<HintPath>$(CasaEnginePath)\\Newtonsoft.Json.dll</HintPath>", csproj);
            Assert.Contains("Condition=\"'$(CasaEnginePath)' == ''\"", csproj);

            string gitIgnore = File.ReadAllText(gitIgnorePath);
            Assert.Contains("bin/", gitIgnore);
            Assert.Contains("obj/", gitIgnore);
            Assert.Contains("CasaEngine.EnginePath.props", gitIgnore);

            string sln = File.ReadAllText(slnPath);
            Assert.Contains("Gameplay\\MonProjet.Gameplay.csproj", sln);

            string props = File.ReadAllText(propsPath);
            Assert.Contains("'$(CASAENGINE_PATH)' != ''", props);
            Assert.Contains(engineDirectory, props);
        }
        finally
        {
            DeleteDirectory(projectPath);
            DeleteDirectory(engineDirectory);
        }
    }

    [Fact]
    public void Scaffold_WhenCsprojAlreadyExists_DoesNotOverwriteUserOwnedFiles()
    {
        string projectPath = CreateTempDirectory();
        string engineDirectory = CreateFakeEngineDirectory("CasaEngine.dll");
        try
        {
            EditorGameplayProjectScaffolder.Scaffold(projectPath, "MonProjet", engineDirectory);

            string csprojPath = Path.Combine(projectPath, "Gameplay", "MonProjet.Gameplay.csproj");
            string slnPath = Path.Combine(projectPath, "MonProjet.sln");
            string sampleProxyPath = Path.Combine(projectPath, "Gameplay", "Scripts", "SampleProxy.cs");

            const string csprojMarker = "<!-- user edit marker -->";
            File.AppendAllText(csprojPath, csprojMarker);
            string originalSln = File.ReadAllText(slnPath);
            string originalSampleProxy = File.ReadAllText(sampleProxyPath);

            var result = EditorGameplayProjectScaffolder.Scaffold(projectPath, "MonProjet", engineDirectory);

            Assert.True(result.Skipped);
            Assert.Contains(csprojMarker, File.ReadAllText(csprojPath));
            Assert.Equal(originalSln, File.ReadAllText(slnPath));
            Assert.Equal(originalSampleProxy, File.ReadAllText(sampleProxyPath));
        }
        finally
        {
            DeleteDirectory(projectPath);
            DeleteDirectory(engineDirectory);
        }
    }

    [Fact]
    public void EnsureEnginePathProps_MissingFile_WritesIt()
    {
        string gameplayDirectory = CreateTempDirectory();
        string engineDirectory = CreateFakeEngineDirectory("CasaEngine.dll");
        try
        {
            string propsPath = Path.Combine(gameplayDirectory, "CasaEngine.EnginePath.props");
            Assert.False(File.Exists(propsPath));

            bool written = EditorGameplayProjectScaffolder.EnsureEnginePathProps(gameplayDirectory, engineDirectory);

            Assert.True(written);
            Assert.True(File.Exists(propsPath));
            Assert.Contains(engineDirectory, File.ReadAllText(propsPath));
        }
        finally
        {
            DeleteDirectory(gameplayDirectory);
            DeleteDirectory(engineDirectory);
        }
    }

    [Fact]
    public void EnsureEnginePathProps_ExistingValidPath_IsNotRewritten()
    {
        string gameplayDirectory = CreateTempDirectory();
        string engineDirectory = CreateFakeEngineDirectory("CasaEngine.dll");
        try
        {
            EditorGameplayProjectScaffolder.EnsureEnginePathProps(gameplayDirectory, engineDirectory);
            string propsPath = Path.Combine(gameplayDirectory, "CasaEngine.EnginePath.props");
            string originalContent = File.ReadAllText(propsPath);

            bool written = EditorGameplayProjectScaffolder.EnsureEnginePathProps(gameplayDirectory, "C:\\some\\other\\path");

            Assert.False(written);
            Assert.Equal(originalContent, File.ReadAllText(propsPath));
        }
        finally
        {
            DeleteDirectory(gameplayDirectory);
            DeleteDirectory(engineDirectory);
        }
    }

    [Fact]
    public void EnsureEnginePathProps_ExistingPathWithoutCasaEngineDll_IsRewritten()
    {
        string gameplayDirectory = CreateTempDirectory();
        string staleEngineDirectory = CreateFakeEngineDirectory(); // no CasaEngine.dll
        string newEngineDirectory = CreateFakeEngineDirectory("CasaEngine.dll");
        try
        {
            string propsPath = Path.Combine(gameplayDirectory, "CasaEngine.EnginePath.props");
            File.WriteAllText(propsPath, $"""
                <Project>
                  <PropertyGroup>
                    <CasaEnginePath Condition="'$(CASAENGINE_PATH)' != ''">$(CASAENGINE_PATH)</CasaEnginePath>
                    <CasaEnginePath Condition="'$(CasaEnginePath)' == ''">{staleEngineDirectory}</CasaEnginePath>
                  </PropertyGroup>
                </Project>
                """);

            bool written = EditorGameplayProjectScaffolder.EnsureEnginePathProps(gameplayDirectory, newEngineDirectory);

            Assert.True(written);
            string content = File.ReadAllText(propsPath);
            Assert.Contains(newEngineDirectory, content);
            Assert.DoesNotContain(staleEngineDirectory, content);
        }
        finally
        {
            DeleteDirectory(gameplayDirectory);
            DeleteDirectory(staleEngineDirectory);
            DeleteDirectory(newEngineDirectory);
        }
    }

    [Fact]
    public void Scaffold_SanitizesProjectNameIntoValidIdentifier()
    {
        string projectPath = CreateTempDirectory();
        string engineDirectory = CreateFakeEngineDirectory("CasaEngine.dll");
        try
        {
            var result = EditorGameplayProjectScaffolder.Scaffold(projectPath, "My Game 2", engineDirectory);

            Assert.Equal("Gameplay/My_Game_2.Gameplay.csproj", result.GameplayCsprojRelativePath);
            Assert.Equal("My_Game_2.Gameplay.dll", result.GameplayDllName);

            string csprojPath = Path.Combine(projectPath, "Gameplay", "My_Game_2.Gameplay.csproj");
            Assert.True(File.Exists(csprojPath));

            string csproj = File.ReadAllText(csprojPath);
            Assert.Contains("<AssemblyName>My_Game_2.Gameplay</AssemblyName>", csproj);

            string gamePlugin = File.ReadAllText(Path.Combine(projectPath, "Gameplay", "GamePlugin.cs"));
            Assert.Contains("namespace My_Game_2.Gameplay;", gamePlugin);

            // The raw project name ("My Game 2") is still used for the .sln file name.
            Assert.True(File.Exists(Path.Combine(projectPath, "My Game 2.sln")));
        }
        finally
        {
            DeleteDirectory(projectPath);
            DeleteDirectory(engineDirectory);
        }
    }

    [Fact]
    public void Scaffold_WithRealEngineInstallation_ProducesACsprojThatCompiles()
    {
        string projectPath = CreateTempDirectory();
        string engineDirectory = AppContext.BaseDirectory;
        try
        {
            Assert.True(File.Exists(Path.Combine(engineDirectory, "CasaEngine.dll")),
                "This integration test expects the test output directory to contain CasaEngine.dll.");

            var result = EditorGameplayProjectScaffolder.Scaffold(projectPath, "IntegrationGame", engineDirectory);
            Assert.False(result.Skipped);

            string csprojPath = Path.Combine(projectPath, "Gameplay", "IntegrationGame.Gameplay.csproj");
            string buildOutputDirectory = Path.Combine(projectPath, "build-output");

            var buildResult = EditorScriptBuildService.Build(csprojPath, buildOutputDirectory, result.GameplayDllName);

            Assert.True(buildResult.Success, string.Join(Environment.NewLine, buildResult.Errors));
            Assert.NotNull(buildResult.OutputAssemblyPath);
            Assert.True(File.Exists(buildResult.OutputAssemblyPath));
            Assert.Equal(result.GameplayDllName, Path.GetFileName(buildResult.OutputAssemblyPath));
        }
        finally
        {
            DeleteDirectory(projectPath);
        }
    }
}
