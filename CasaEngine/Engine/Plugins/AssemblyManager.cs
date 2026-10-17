using System.Reflection;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Engine.Plugins;

public class AssemblyManager
{
    /// <summary>
    /// Load strategy for the gameplay assembly. The standalone runtime keeps the
    /// default direct load; the editor plugs a shadow-copying, unloadable loader so
    /// the dll file on disk stays writable while the project is open.
    /// </summary>
    public Func<string, Assembly> AssemblyLoader { get; set; } = DefaultAssemblyLoader;

    private static readonly Func<string, Assembly> DefaultAssemblyLoader = Assembly.LoadFile;

    // The assembly this manager registered with the script factory (default loader only), so a reload replaces it.
    private Assembly _registeredAssembly;

    public void Load(string fileName)
    {
        var fullPath = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        if (!File.Exists(fullPath))
        {
            Logs.WriteError($"Gameplay assembly not found: {fullPath}");
            return;
        }

        var gamePlayAssembly = AssemblyLoader(fullPath);
        RegisterScriptAssembly(gamePlayAssembly);
        Type iPluginType = typeof(IPlugin);
        var pluginClass = gamePlayAssembly.GetTypes().FirstOrDefault(t => iPluginType.IsAssignableFrom(t) && !t.IsInterface);
        if (pluginClass == null)
        {
            Logs.WriteWarning($"Gameplay assembly '{fileName}' contains no IPlugin implementation.");
            return;
        }

        var plugin = (IPlugin)Activator.CreateInstance(pluginClass);
        plugin.Initialize();
    }

    // One owner of the registration by path (ADR-0075, D5): with the default loader this manager registers the
    // assembly (script objects are created by full name from registered assemblies only) and replaces its own
    // registration on a reload. A loader the caller provides, such as the editor's shadow copy, registers and
    // unregisters by itself: the manager then registers nothing and keeps no reference.
    private void RegisterScriptAssembly(Assembly gamePlayAssembly)
    {
        if (_registeredAssembly != null)
        {
            ElementFactory.UnregisterScriptAssembly(_registeredAssembly);
            _registeredAssembly = null;
        }

        if (AssemblyLoader == DefaultAssemblyLoader)
        {
            ElementFactory.RegisterScriptAssembly(gamePlayAssembly);
            _registeredAssembly = gamePlayAssembly;
        }
    }
}
