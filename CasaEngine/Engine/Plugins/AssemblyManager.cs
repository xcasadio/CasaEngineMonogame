using System.Reflection;

namespace CasaEngine.Engine.Plugins;

public class AssemblyManager
{
    public void Load(string fileName)
    {
        var gamePlayAssembly = Assembly.LoadFile(Path.Combine(EngineEnvironment.ProjectPath, fileName));
        Type iPluginType = typeof(IPlugin);
        var pluginClass = gamePlayAssembly.GetTypes().First(t => iPluginType.IsAssignableFrom(t) && !t.IsInterface);
        var plugin = (IPlugin)Activator.CreateInstance(pluginClass);
        plugin.Initialize();
    }
}