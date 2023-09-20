using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Game;
using CasaEngine.RPGDemo.Scripts;

namespace CasaEngine.RPGDemo;

public class Plugin : IPlugin
{
    public void Initialize()
    {
        GameSettings.ScriptLoader.Register<ScriptPlayer>(new ScriptPlayer().ExternalComponentId);
        GameSettings.ScriptLoader.Register<ScriptEnemy>(new ScriptEnemy().ExternalComponentId);
        GameSettings.ScriptLoader.Register<ScriptPlayerWeapon>(new ScriptPlayerWeapon().ExternalComponentId);
        GameSettings.ScriptLoader.Register<ScriptEnemyWeapon>(new ScriptEnemyWeapon().ExternalComponentId);
    }
}