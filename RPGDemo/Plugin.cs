using CasaEngine.Engine.Plugin;
using CasaEngine.Framework.Entities;
using RPGDemo.Components;

namespace RPGDemo;

public class Plugin : IPlugin
{
    public void Initialize()
    {
        ComponentLoader.Register<PlayerComponent>(PlayerComponent.ComponentId, owner => new PlayerComponent(owner));
    }
}