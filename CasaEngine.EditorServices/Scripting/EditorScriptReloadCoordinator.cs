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
        var result = BuildForCanonicalDll();
        if (result == null)
        {
            return false;
        }

        prepareWorldsForReload?.Invoke();

        RefreshCanonicalGameplayDll(result.OutputAssemblyPath!);

        // Routed through the pluggable loader: in the editor this unloads the previous
        // collectible context and loads the fresh build (shadow-copied), then runs the
        // IPlugin initialization like any project open.
        GameSettings.AssemblyManager.Load(result.OutputAssemblyPath!);

        Logs.WriteInfo("Gameplay scripts reloaded.");
        return true;
    }

    /// <summary>
    /// Builds the gameplay scripts and refreshes the canonical gameplay dll on disk, without
    /// loading the assembly into the running process. Used right after project creation, when
    /// the editor's shadow-copy <c>AssemblyManager.AssemblyLoader</c> is not guaranteed to be
    /// wired yet (and never is for callers outside a running <c>GameEditor</c>, e.g. tests or
    /// tooling): loading through the default <c>Assembly.LoadFile</c> loader would lock the
    /// canonical dll on disk. Fail-soft: returns false and logs on build failure, same as
    /// <see cref="TryRebuildAndReload"/>.
    /// </summary>
    public static bool TryBuildCanonicalDll()
    {
        var result = BuildForCanonicalDll();
        if (result == null)
        {
            return false;
        }

        RefreshCanonicalGameplayDll(result.OutputAssemblyPath!);

        Logs.WriteInfo("Gameplay scripts built (canonical dll refreshed, not loaded).");
        return true;
    }

    private static ScriptBuildResult? BuildForCanonicalDll()
    {
        if (!IsRebuildConfigured)
        {
            Logs.WriteWarning("No gameplay csproj is configured (GameplayCsprojName); nothing to build.");
            return null;
        }

        string csprojPath = ResolveCsprojPath();
        string outputRoot = Path.Combine(EngineEnvironment.ProjectPath, ".casaeditor", "script-build");
        string? expectedAssemblyFileName = string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.GameplayDllName)
            ? null
            : Path.GetFileName(GameSettings.ProjectSettings.GameplayDllName);

        var result = EditorScriptBuildService.Build(csprojPath, outputRoot, expectedAssemblyFileName);
        return result.Success ? result : null;
    }

    /// <summary>
    /// Copies the fresh build over the project's canonical gameplay dll (GameplayDllName).
    /// The canonical file is never locked (the editor loads shadow copies), so the next
    /// editor session and the standalone runtime pick up the rebuilt scripts, and the
    /// out-of-date detection stays accurate across sessions. Best effort.
    /// </summary>
    private static void RefreshCanonicalGameplayDll(string builtAssemblyPath)
    {
        string dllName = GameSettings.ProjectSettings.GameplayDllName;
        if (string.IsNullOrWhiteSpace(dllName))
        {
            return;
        }

        try
        {
            string canonicalPath = Path.GetFullPath(Path.Combine(EngineEnvironment.ProjectPath, dllName));
            if (string.Equals(canonicalPath, Path.GetFullPath(builtAssemblyPath), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.Copy(builtAssemblyPath, canonicalPath, overwrite: true);

            string builtPdb = Path.ChangeExtension(builtAssemblyPath, ".pdb");
            if (File.Exists(builtPdb))
            {
                File.Copy(builtPdb, Path.ChangeExtension(canonicalPath, ".pdb"), overwrite: true);
            }

            Logs.WriteInfo($"Gameplay dll refreshed: {canonicalPath}");
        }
        catch (Exception ex)
        {
            Logs.WriteWarning($"Could not refresh the canonical gameplay dll: {ex.Message}");
        }
    }

    private static string ResolveCsprojPath()
        => Path.GetFullPath(Path.Combine(
            EngineEnvironment.ProjectPath,
            GameSettings.ProjectSettings.GameplayCsprojName));
}
