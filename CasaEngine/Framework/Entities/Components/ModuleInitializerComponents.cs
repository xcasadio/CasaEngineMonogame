using System.Runtime.CompilerServices;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Entities.Components;

internal static class ModuleInitializerComponents
{
    [ModuleInitializer]
    public static void Initialize()
    {
        GameSettings.ComponentLoader.Register(AnimatedSpriteComponent.ComponentId, owner => new AnimatedSpriteComponent(owner));
        GameSettings.ComponentLoader.Register(ArcBallCameraComponent.ComponentId, owner => new ArcBallCameraComponent(owner));
        GameSettings.ComponentLoader.Register(CameraLookAtComponent.ComponentId, owner => new CameraLookAtComponent(owner));
        GameSettings.ComponentLoader.Register(CameraTargeted2dComponent.ComponentId, owner => new CameraTargeted2dComponent(owner));
        GameSettings.ComponentLoader.Register(Camera3dIn2dAxisComponent.ComponentId, owner => new Camera3dIn2dAxisComponent(owner));
        GameSettings.ComponentLoader.Register(GamePlayComponent.ComponentId, owner => new GamePlayComponent(owner));
        GameSettings.ComponentLoader.Register(SkinnedMeshComponent.ComponentId, owner => new SkinnedMeshComponent(owner));
        GameSettings.ComponentLoader.Register(StaticMeshComponent.ComponentId, owner => new StaticMeshComponent(owner));
        GameSettings.ComponentLoader.Register(Physics2dComponent.ComponentId, owner => new Physics2dComponent(owner));
        GameSettings.ComponentLoader.Register(PhysicsComponent.ComponentId, owner => new PhysicsComponent(owner));
        GameSettings.ComponentLoader.Register(StaticSpriteComponent.ComponentId, owner => new StaticSpriteComponent(owner));
        GameSettings.ComponentLoader.Register(TileMapComponent.ComponentId, owner => new TileMapComponent(owner));
    }
}