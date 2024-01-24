using System;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Game;
using CasaEngine.RPGDemo.Scripts;

namespace CasaEngine.RPGDemo;

public class Plugin : IPlugin
{
    public void Initialize()
    {
        GameSettings.ElementFactory.Register<ScriptPlayer>(Guid.Parse("F050E15D-38FE-47C1-B46E-6F49D717B81B"));
        GameSettings.ElementFactory.Register<ScriptEnemy>(Guid.Parse("324CBF1C-EA04-4742-A692-3D4F3F4E756F"));
        GameSettings.ElementFactory.Register<ScriptPlayerWeapon>(Guid.Parse("A80596C2-0FD0-41F5-899D-D4577605B112"));
        GameSettings.ElementFactory.Register<ScriptEnemyWeapon>(Guid.Parse("4623C8EC-8825-4BDC-8B78-C4D9FD9D563B"));
        GameSettings.ElementFactory.Register<ScriptWorld>(Guid.Parse("4A0E16E6-33F3-4D7B-A9E1-22A11F9EC910"));
    }
}