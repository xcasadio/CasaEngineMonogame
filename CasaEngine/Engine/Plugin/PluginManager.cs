using System.Reflection;
using CasaEngine.Framework;

namespace CasaEngine.Engine.Plugin;

public class PluginManager
{
    public void Load(string fileName)
    {
        var gamePlayAssembly = Assembly.LoadFile(Path.Combine(Environment.CurrentDirectory, EngineComponents.ProjectSettings.GameplayDllName));
        Type iPluginType = typeof(IPlugin);
        var pluginClass = gamePlayAssembly.GetTypes().First(t => iPluginType.IsAssignableFrom(t) && !t.IsInterface);
        var plugin = (IPlugin)Activator.CreateInstance(pluginClass);
        plugin.Initialize();
    }
}