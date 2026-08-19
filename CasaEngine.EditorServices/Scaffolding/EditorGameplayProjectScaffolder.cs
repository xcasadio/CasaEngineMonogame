using System.Text;
using CasaEngine.Core.Logging;

namespace CasaEngine.EditorServices.Scaffolding;

/// <summary>
/// Generates the gameplay C# project (csproj, sln, starter sources) inside a CasaEngine
/// project folder. Phase 1 of docs/editor/gameplay-csproj-scaffolding.md: the generated
/// csproj references the DLL of the editor installation directly.
/// </summary>
/// <remarks>
/// "Generated once, owned by the user" — see the design doc. Once the gameplay csproj
/// exists, <see cref="Scaffold"/> never rewrites it, the sln, the starter sources or the
/// .gitignore; only <see cref="EnsureEnginePathProps"/> is allowed to rewrite the
/// machine-local engine path props.
/// </remarks>
public static class EditorGameplayProjectScaffolder
{
    /// <summary>
    /// Target framework written into the generated csproj. Kept in sync with
    /// <c>DotNetVersion</c>/<c>WindowsTargetFramework</c> in the repo's Directory.Build.props —
    /// the generated project lives outside the repo and does not inherit it.
    /// </summary>
    public const string TargetFramework = "net9.0-windows";

    private const string EnginePathPropsFileName = "CasaEngine.EnginePath.props";
    private const string GitIgnoreFileName = ".gitignore";
    private const string GamePluginFileName = "GamePlugin.cs";
    private const string SampleProxyFileName = "SampleProxy.cs";
    private static readonly string[] EngineReferenceAssemblies = { "CasaEngine", "MonoGame.Framework", "Newtonsoft.Json" };

    /// <summary>
    /// Generates the <c>Gameplay/</c> folder and the solution file inside <paramref name="projectPath"/>
    /// if they do not already exist. Fully idempotent: when the gameplay csproj is already present,
    /// no user-owned file is rewritten and the same paths are returned with <see cref="ScaffoldResult.Skipped"/> set.
    /// </summary>
    /// <param name="projectPath">Root directory of the CasaEngine project (where the .json project file lives).</param>
    /// <param name="projectName">Raw project name, used for the .sln file name.</param>
    /// <param name="enginePath">Directory of the editor installation containing CasaEngine.dll, used to seed the engine path props.</param>
    public static ScaffoldResult Scaffold(string projectPath, string projectName, string enginePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(enginePath);

        string id = SanitizeIdentifier(projectName);
        string gameplayDirectory = Path.Combine(projectPath, "Gameplay");
        string csprojFileName = $"{id}.Gameplay.csproj";
        string csprojPath = Path.Combine(gameplayDirectory, csprojFileName);
        string gameplayCsprojRelativePath = "Gameplay/" + csprojFileName;
        string gameplayDllName = $"{id}.Gameplay.dll";
        string slnPath = Path.Combine(projectPath, projectName + ".sln");

        bool csprojAlreadyExists = File.Exists(csprojPath);

        if (csprojAlreadyExists)
        {
            Logs.WriteInfo($"Gameplay csproj already exists, scaffolding skipped: {csprojPath}");
            EnsureEnginePathProps(gameplayDirectory, enginePath);

            return new ScaffoldResult
            {
                GameplayCsprojRelativePath = gameplayCsprojRelativePath,
                GameplayDllName = gameplayDllName,
                Skipped = true,
            };
        }

        Directory.CreateDirectory(gameplayDirectory);
        Directory.CreateDirectory(Path.Combine(gameplayDirectory, "Scripts"));

        File.WriteAllText(csprojPath, BuildCsproj(id));
        File.WriteAllText(Path.Combine(gameplayDirectory, GitIgnoreFileName), BuildGitIgnore());
        File.WriteAllText(Path.Combine(gameplayDirectory, GamePluginFileName), BuildGamePlugin(id));
        File.WriteAllText(Path.Combine(gameplayDirectory, "Scripts", SampleProxyFileName), BuildSampleProxy(id));
        File.WriteAllText(slnPath, BuildSolution(id, gameplayCsprojRelativePath.Replace('/', '\\')));

        EnsureEnginePathProps(gameplayDirectory, enginePath);

        Logs.WriteInfo($"Gameplay project scaffolded: {csprojPath}");

        return new ScaffoldResult
        {
            GameplayCsprojRelativePath = gameplayCsprojRelativePath,
            GameplayDllName = gameplayDllName,
            Skipped = false,
        };
    }

    /// <summary>
    /// Writes or regenerates <c>CasaEngine.EnginePath.props</c> in <paramref name="gameplayDirectory"/>
    /// when it is missing, or when the hard-coded path it contains no longer points to a directory
    /// containing CasaEngine.dll. This is the only generated file the editor is allowed to rewrite
    /// after scaffolding, so the project stays portable across machines.
    /// </summary>
    /// <returns><c>true</c> if the props file was written.</returns>
    public static bool EnsureEnginePathProps(string gameplayDirectory, string enginePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameplayDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(enginePath);

        string propsPath = Path.Combine(gameplayDirectory, EnginePathPropsFileName);

        if (File.Exists(propsPath) && IsExistingEnginePathStillValid(propsPath))
        {
            return false;
        }

        Directory.CreateDirectory(gameplayDirectory);
        File.WriteAllText(propsPath, BuildEnginePathProps(enginePath));
        Logs.WriteInfo($"Gameplay engine path props (re)written: {propsPath} -> {enginePath}");
        return true;
    }

    private static bool IsExistingEnginePathStillValid(string propsPath)
    {
        string existingPath = ExtractHardCodedEnginePath(File.ReadAllText(propsPath));
        return existingPath != null
            && Directory.Exists(existingPath)
            && File.Exists(Path.Combine(existingPath, "CasaEngine.dll"));
    }

    private static string? ExtractHardCodedEnginePath(string propsContent)
    {
        const string marker = "'$(CasaEnginePath)' == ''\">";
        int markerIndex = propsContent.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        int valueStart = markerIndex + marker.Length;
        int valueEnd = propsContent.IndexOf('<', valueStart);
        if (valueEnd < 0)
        {
            return null;
        }

        return propsContent[valueStart..valueEnd].Trim();
    }

    /// <summary>
    /// Sanitizes a raw project name into a valid C# identifier: invalid characters become
    /// underscores, a leading digit is prefixed with an underscore, and an empty result falls
    /// back to "Game".
    /// </summary>
    private static string SanitizeIdentifier(string projectName)
    {
        var builder = new StringBuilder(projectName.Length);
        foreach (char c in projectName)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        string id = builder.ToString();

        if (id.Length == 0)
        {
            return "Game";
        }

        if (char.IsDigit(id[0]))
        {
            id = "_" + id;
        }

        return id;
    }

    private static string BuildCsproj(string id)
    {
        var builder = new StringBuilder();
        builder.Append("""<Project Sdk="Microsoft.NET.Sdk">""").Append('\n');
        builder.Append($"""  <Import Project="{EnginePathPropsFileName}" Condition="Exists('{EnginePathPropsFileName}')" />""").Append('\n');
        builder.Append("  <PropertyGroup>\n");
        builder.Append($"    <TargetFramework>{TargetFramework}</TargetFramework>\n");
        builder.Append("    <PlatformTarget>x64</PlatformTarget>\n");
        builder.Append("    <OutputType>Library</OutputType>\n");
        builder.Append("    <ImplicitUsings>enable</ImplicitUsings>\n");
        builder.Append($"    <AssemblyName>{id}.Gameplay</AssemblyName>\n");
        builder.Append($"    <RootNamespace>{id}.Gameplay</RootNamespace>\n");
        builder.Append("  </PropertyGroup>\n");
        builder.Append("""  <Target Name="EnsureCasaEnginePath" BeforeTargets="ResolveAssemblyReferences" Condition="'$(CasaEnginePath)' == ''">""").Append('\n');
        builder.Append("""    <Error Text="CasaEnginePath est introuvable : ouvrez le projet dans l'éditeur CasaEngine (le fichier CasaEngine.EnginePath.props sera régénéré) ou définissez la variable d'environnement CASAENGINE_PATH." />""").Append('\n');
        builder.Append("  </Target>\n");
        builder.Append("  <ItemGroup>\n");
        foreach (string assembly in EngineReferenceAssemblies)
        {
            builder.Append($"""    <Reference Include="{assembly}">""").Append('\n');
            builder.Append($"      <HintPath>$(CasaEnginePath)\\{assembly}.dll</HintPath>\n");
            builder.Append("      <Private>false</Private>\n");
            builder.Append("    </Reference>\n");
        }
        builder.Append("  </ItemGroup>\n");
        builder.Append("</Project>\n");
        return builder.ToString();
    }

    private static string BuildGitIgnore()
    {
        return "bin/\nobj/\n" + EnginePathPropsFileName + "\n";
    }

    private static string BuildGamePlugin(string id)
    {
        return $$"""
            using CasaEngine.Engine.Plugins;

            namespace {{id}}.Gameplay;

            public class GamePlugin : IPlugin
            {
                public void Initialize()
                {
                    // Enregistrements globaux du jeu.
                }
            }

            """;
    }

    private static string BuildSampleProxy(string id)
    {
        return $$"""
            using CasaEngine.Framework.Physics;
            using CasaEngine.Framework.Scripting;
            using CasaEngine.Framework.Scene.World;

            namespace {{id}}.Gameplay.Scripts;

            public class SampleProxy : GameplayProxy
            {
                public override void InitializeWithWorld(World world)
                {
                }

                public override void Update(float elapsedTime)
                {
                }

                public override void Draw()
                {
                }

                public override void OnHit(Collision collision)
                {
                }

                public override void OnHitEnded(Collision collision)
                {
                }

                public override void OnBeginPlay(World world)
                {
                }

                public override void OnEndPlay(World world)
                {
                }

                public override IGameplayProxy Clone()
                {
                    return new SampleProxy();
                }
            }

            """;
    }

    private static string BuildEnginePathProps(string enginePath)
    {
        return $"""
            <!-- Généré par l'éditeur CasaEngine. Machine-local : ne pas committer, ne pas éditer. -->
            <Project>
              <PropertyGroup>
                <CasaEnginePath Condition="'$(CASAENGINE_PATH)' != ''">$(CASAENGINE_PATH)</CasaEnginePath>
                <CasaEnginePath Condition="'$(CasaEnginePath)' == ''">{enginePath}</CasaEnginePath>
              </PropertyGroup>
            </Project>

            """;
    }

    private static string BuildSolution(string id, string csprojRelativePath)
    {
        string projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        const string projectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";

        return $"""
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{projectTypeGuid}") = "{id}.Gameplay", "{csprojRelativePath}", "{projectGuid}"
            EndProject
            Global
            	GlobalSection(SolutionConfigurationPlatforms) = preSolution
            		Debug|Any CPU = Debug|Any CPU
            		Release|Any CPU = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(ProjectConfigurationPlatforms) = postSolution
            		{projectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            		{projectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU
            		{projectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU
            		{projectGuid}.Release|Any CPU.Build.0 = Release|Any CPU
            	EndGlobalSection
            EndGlobal

            """;
    }
}

/// <summary>
/// Result of <see cref="EditorGameplayProjectScaffolder.Scaffold"/>.
/// </summary>
public sealed class ScaffoldResult
{
    /// <summary>Path of the generated csproj relative to the project root, using '/' as separator.</summary>
    public required string GameplayCsprojRelativePath { get; init; }

    /// <summary>File name of the compiled gameplay DLL (e.g. "MonProjet.Gameplay.dll").</summary>
    public required string GameplayDllName { get; init; }

    /// <summary>True when the gameplay csproj already existed and scaffolding of user-owned files was skipped.</summary>
    public bool Skipped { get; init; }
}
