using System.Runtime.CompilerServices;

namespace CasaEngine.Framework.Entities.Components;

internal static class ModuleInitializerComponents
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ComponentLoader.Register<ArcBallCameraComponent>(ArcBallCameraComponent.ComponentId, owner => new ArcBallCameraComponent(owner));
        ComponentLoader.Register<GamePlayComponent>(GamePlayComponent.ComponentId, owner => new GamePlayComponent(owner));
        ComponentLoader.Register<StaticMeshComponent>(StaticMeshComponent.ComponentId, owner => new StaticMeshComponent(owner));
    }
}