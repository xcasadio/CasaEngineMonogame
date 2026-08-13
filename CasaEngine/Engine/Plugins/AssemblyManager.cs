using System.Reflection;
using CasaEngine.Core.Logging;

namespace CasaEngine.Engine.Plugins;

public class AssemblyManager
{
    /// <summary>
    /// Load strategy for the gameplay assembly. The standalone runtime keeps the
    /// default direct load; the editor plugs a shadow-copying, unloadable loader so
    /// the dll file on disk stays writable while the project is open.
    /// </summary>
    public Func<string, Assembly> AssemblyLoader { get; set; } = Assembly.LoadFile;

    public void Load(string fileName)
    {
        var fullPath = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        if (!File.Exists(fullPath))
        {
            Logs.WriteError($"Gameplay assembly not found: {fullPath}");
            return;
        }

        var gamePlayAssembly = AssemblyLoader(fullPath);
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
}
