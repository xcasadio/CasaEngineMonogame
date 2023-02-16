using System.Runtime.CompilerServices;
using CasaEngine.Entities.Components;

namespace Pong;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ComponentLoader.Register<PlayerComponent>(PlayerComponent.ComponentId, owner => new PlayerComponent(owner));
    }
}