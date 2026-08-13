using System.Reflection;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorServices.Scripting;

/// <summary>
/// Editor-side loader of the gameplay script assembly. The dll is shadow-copied to a
/// unique temp directory and loaded in a collectible context, so the original file
/// stays writable (rebuildable) while the project is open, and the assembly can be
/// unloaded/reloaded after a rebuild.
/// </summary>
public static class EditorScriptAssemblyService
{
    private static readonly ScriptAssemblyHost Host = new();

    public static Assembly? LoadedAssembly => Host.LoadedAssembly;

    /// <summary>Original dll path the current shadow copy was made from.</summary>
    public static string? LoadedSourcePath { get; private set; }

    public static DateTime LoadedSourceWriteTimeUtc { get; private set; }

    public static bool IsLoaded => Host.IsLoaded;

    /// <summary>
    /// Loads a gameplay dll through a shadow copy. Any previously loaded script
    /// assembly is unregistered and unloaded first, so this is also the reload path.
    /// Plug it into <c>GameSettings.AssemblyManager.AssemblyLoader</c>.
    /// </summary>
    public static Assembly LoadShadowCopy(string dllPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dllPath);

        Unload();

        string fullPath = Path.GetFullPath(dllPath);
        string shadowDirectory = Path.Combine(Path.GetTempPath(), "casaeditor-scripts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shadowDirectory);

        string shadowPath = Path.Combine(shadowDirectory, Path.GetFileName(fullPath));
        File.Copy(fullPath, shadowPath);
        CopySiblingIfExists(fullPath, shadowDirectory, ".pdb");
        CopySiblingIfExists(fullPath, shadowDirectory, ".deps.json");

        var assembly = Host.Load(shadowPath);
        ElementFactory.RegisterScriptAssembly(assembly);

        LoadedSourcePath = fullPath;
        LoadedSourceWriteTimeUtc = File.GetLastWriteTimeUtc(fullPath);
        return assembly;
    }

    /// <summary>
    /// Unregisters and unloads the current script assembly. Returns true when the
    /// collectible context was verifiably collected (or nothing was loaded).
    /// </summary>
    public static bool Unload()
    {
        if (!Host.IsLoaded)
        {
            return true;
        }

        ElementFactory.UnregisterScriptAssembly(Host.LoadedAssembly!);
        LoadedSourcePath = null;
        LoadedSourceWriteTimeUtc = default;

        bool collected = Host.Unload();
        if (!collected)
        {
            Logs.WriteWarning("The previous script assembly could not be fully unloaded; a script type is still referenced.");
        }

        return collected;
    }

    private static void CopySiblingIfExists(string dllPath, string shadowDirectory, string extension)
    {
        string source = Path.ChangeExtension(dllPath, extension);
        if (File.Exists(source))
        {
            File.Copy(source, Path.Combine(shadowDirectory, Path.GetFileName(source)));
        }
    }
}
