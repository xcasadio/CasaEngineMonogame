using System.Reflection;
using CasaEngine.Framework.Game;

namespace CasaEngine.Engine.Plugin;

public class PluginManager
{
    private CasaEngineGame _game;

    public PluginManager(CasaEngineGame game)
    {
        _game = game;
    }

    public void Load(string fileName)
    {
        var gamePlayAssembly = Assembly.LoadFile(Path.Combine(Environment.CurrentDirectory, _game.GameManager.ProjectSettings.GameplayDllName));
        Type iPluginType = typeof(IPlugin);
        var pluginClass = gamePlayAssembly.GetTypes().First(t => iPluginType.IsAssignableFrom(t) && !t.IsInterface);
        var plugin = (IPlugin)Activator.CreateInstance(pluginClass);
        plugin.Initialize();
    }
}