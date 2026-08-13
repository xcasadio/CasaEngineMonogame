using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using CasaEngine.Core.Logging;

namespace CasaEngine.Engine.Plugins;

/// <summary>
/// Hosts the gameplay script assembly in a collectible AssemblyLoadContext so the editor
/// can rebuild and reload it without restarting. Assemblies already loaded in the default
/// context (engine, MonoGame, BCL) always resolve there, keeping type identity intact:
/// a GameplayProxy compiled in the script assembly must be assignable to the engine's
/// GameplayProxy.
/// </summary>
public sealed class ScriptAssemblyHost
{
    private sealed class ScriptLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public ScriptLoadContext(string mainAssemblyPath)
            : base($"Scripts:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Shared assemblies keep their default-context identity; only dependencies
            // shipped next to the script dll and unknown to the default context are
            // loaded in this collectible context.
            foreach (var loaded in Default.Assemblies)
            {
                if (string.Equals(loaded.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }
    }

    private ScriptLoadContext _context;

    public bool IsLoaded => LoadedAssembly != null;

    public Assembly LoadedAssembly { get; private set; }

    public string LoadedAssemblyPath { get; private set; }

    public Assembly Load(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (_context != null)
        {
            throw new InvalidOperationException("A script assembly is already loaded: unload it before loading a new one.");
        }

        var fullPath = Path.GetFullPath(assemblyPath);
        _context = new ScriptLoadContext(fullPath);
        LoadedAssembly = _context.LoadFromAssemblyPath(fullPath);
        LoadedAssemblyPath = fullPath;
        Logs.WriteInfo($"Script assembly loaded: {fullPath}");
        return LoadedAssembly;
    }

    /// <summary>
    /// Unloads the script context and waits for it to be collected. Returns true when
    /// the context is verifiably gone; false when something still pins a script type
    /// (live proxy instance, cached Type, event subscription...).
    /// </summary>
    public bool Unload()
    {
        if (_context == null)
        {
            return true;
        }

        var weakContext = StartUnload();

        for (int i = 0; weakContext.IsAlive && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        if (weakContext.IsAlive)
        {
            Logs.WriteWarning("Script assembly context is still alive after unload: a script type is still referenced somewhere.");
            return false;
        }

        Logs.WriteInfo("Script assembly context unloaded.");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference StartUnload()
    {
        var weakContext = new WeakReference(_context, trackResurrection: true);
        LoadedAssembly = null;
        LoadedAssemblyPath = null;
        _context.Unload();
        _context = null;
        return weakContext;
    }
}
