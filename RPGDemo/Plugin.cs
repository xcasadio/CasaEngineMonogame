using CasaEngine.Engine.Plugin;
using CasaEngine.Framework.Game;
using RPGDemo.Components;
using RPGDemo.Scripts;

namespace RPGDemo;

public class Plugin : IPlugin
{
    public void Initialize()
    {
        GameSettings.ComponentLoader.Register(PlayerComponent.ComponentId, owner => new PlayerComponent(owner));
        GameSettings.ComponentLoader.Register(EnemyComponent.ComponentId, owner => new EnemyComponent(owner));

        GameSettings.ScriptLoader.Register(ScriptPlayerWeapon.ScriptId, owner => new ScriptPlayerWeapon(owner));
        GameSettings.ScriptLoader.Register(ScriptEnemyWeapon.ScriptId, owner => new ScriptEnemyWeapon(owner));
    }
}