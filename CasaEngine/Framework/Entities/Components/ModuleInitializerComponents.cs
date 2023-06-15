using System.Runtime.CompilerServices;

namespace CasaEngine.Framework.Entities.Components;

internal static class ModuleInitializerComponents
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ComponentLoader.Register<AnimatedSpriteComponent>(AnimatedSpriteComponent.ComponentId, owner => new AnimatedSpriteComponent(owner));
        ComponentLoader.Register<ArcBallCameraComponent>(ArcBallCameraComponent.ComponentId, owner => new ArcBallCameraComponent(owner));
        ComponentLoader.Register<CameraLookAtComponent>(CameraLookAtComponent.ComponentId, owner => new CameraLookAtComponent(owner));
        ComponentLoader.Register<CameraTargeted2dComponent>(CameraTargeted2dComponent.ComponentId, owner => new CameraTargeted2dComponent(owner));
        ComponentLoader.Register<Camera3dIn2dAxisComponent>(Camera3dIn2dAxisComponent.ComponentId, owner => new Camera3dIn2dAxisComponent(owner));
        ComponentLoader.Register<GamePlayComponent>(GamePlayComponent.ComponentId, owner => new GamePlayComponent(owner));
        ComponentLoader.Register<StaticMeshComponent>(StaticMeshComponent.ComponentId, owner => new StaticMeshComponent(owner));
        ComponentLoader.Register<Physics2dComponent>(Physics2dComponent.ComponentId, owner => new Physics2dComponent(owner));
        ComponentLoader.Register<PhysicsComponent>(PhysicsComponent.ComponentId, owner => new PhysicsComponent(owner));
        ComponentLoader.Register<StaticSpriteComponent>(StaticSpriteComponent.ComponentId, owner => new StaticSpriteComponent(owner));
        ComponentLoader.Register<TiledMapComponent>(TiledMapComponent.ComponentId, owner => new TiledMapComponent(owner));
    }
}