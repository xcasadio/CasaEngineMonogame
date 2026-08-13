using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;

namespace CasaEngine.EditorServices.Scripting;

/// <summary>
/// Orchestrates the rebuild + reload of the gameplay script assembly:
/// build out of process, tear down every live script object (callback owned by the
/// editor), then unload the old assembly and load the fresh build through
/// <c>GameSettings.AssemblyManager</c> (shadow copy + collectible context in editor).
/// </summary>
public static class EditorScriptReloadCoordinator
{
    public static bool IsRebuildConfigured =>
        !string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.GameplayCsprojName);

    /// <summary>
    /// True when the configured csproj sources are newer than the loaded gameplay dll
    /// (or when nothing is loaded yet while a csproj is configured).
    /// </summary>
    public static bool AreScriptsOutOfDate()
    {
        if (!IsRebuildConfigured)
        {
            return false;
        }

        if (!EditorScriptAssemblyService.IsLoaded)
        {
            return true;
        }

        string csprojPath = ResolveCsprojPath();
        string? rootDirectory = Path.GetDirectoryName(csprojPath);
        if (rootDirectory == null || !Directory.Exists(rootDirectory))
        {
            return false;
        }

        DateTime loadedWriteTime = EditorScriptAssemblyService.LoadedSourceWriteTimeUtc;
        if (File.Exists(csprojPath) && File.GetLastWriteTimeUtc(csprojPath) > loadedWriteTime)
        {
            return true;
        }

        string binSegment = Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar;
        string objSegment = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;

        foreach (var file in Directory.EnumerateFiles(rootDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(binSegment, StringComparison.OrdinalIgnoreCase)
                || file.Contains(objSegment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.GetLastWriteTimeUtc(file) > loadedWriteTime)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rebuilds the scripts and reloads the resulting assembly.
    /// <paramref name="prepareWorldsForReload"/> runs between a successful build and
    /// the assembly swap: it must drop every live script object (proxies of the edit
    /// world) so the old collectible context can be collected.
    /// Returns false when the build failed; the previous assembly stays loaded.
    /// </summary>
    public static bool TryRebuildAndReload(Action? prepareWorldsForReload)
    {
        if (!IsRebuildConfigured)
        {
            Logs.WriteWarning("No gameplay csproj is configured (GameplayCsprojName); nothing to build.");
            return false;
        }

        string csprojPath = ResolveCsprojPath();
        string outputRoot = Path.Combine(EngineEnvironment.ProjectPath, ".casaeditor", "script-build");
        string? expectedAssemblyFileName = string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.GameplayDllName)
            ? null
            : Path.GetFileName(GameSettings.ProjectSettings.GameplayDllName);

        var result = EditorScriptBuildService.Build(csprojPath, outputRoot, expectedAssemblyFileName);
        if (!result.Success)
        {
            return false;
        }

        prepareWorldsForReload?.Invoke();

        // Routed through the pluggable loader: in the editor this unloads the previous
        // collectible context and loads the fresh build (shadow-copied), then runs the
        // IPlugin initialization like any project open.
        GameSettings.AssemblyManager.Load(result.OutputAssemblyPath!);

        Logs.WriteInfo("Gameplay scripts reloaded.");
        return true;
    }

    private static string ResolveCsprojPath()
        => Path.GetFullPath(Path.Combine(
            EngineEnvironment.ProjectPath,
            GameSettings.ProjectSettings.GameplayCsprojName));
}
