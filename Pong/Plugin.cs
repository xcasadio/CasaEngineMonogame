using CasaEngine.Entities.Components;
using CasaEngine.Plugin;

namespace Pong;

public class Plugin : IPlugin
{
    public void Initialize()
    {
        ComponentLoader.Register<PlayerComponent>(PlayerComponent.ComponentId, owner => new PlayerComponent(owner));
    }
}