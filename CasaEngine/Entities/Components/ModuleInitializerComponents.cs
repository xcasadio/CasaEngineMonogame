using System.Runtime.CompilerServices;

namespace CasaEngine.Entities.Components;

internal static class ModuleInitializerComponents
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ComponentLoader.Register<GamePlayComponent>(GamePlayComponent.ComponentId, owner => new GamePlayComponent(owner));
    }
}