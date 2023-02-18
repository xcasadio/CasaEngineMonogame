using System.Reflection;

namespace CasaEngine.Engine.Plugin;

public class PluginManager
{
    public void Load(string fileName)
    {
        var gamePlayAssembly = Assembly.LoadFile(Path.Combine(Environment.CurrentDirectory, Framework.Game.Engine.Instance.ProjectSettings.GameplayDllName));
        Type iPluginType = typeof(IPlugin);
        var pluginClass = gamePlayAssembly.GetTypes().First(t => iPluginType.IsAssignableFrom(t) && !t.IsInterface);
        var plugin = (IPlugin)Activator.CreateInstance(pluginClass);
        plugin.Initialize();
    }
}