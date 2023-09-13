using System.Runtime.CompilerServices;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Entities.Components;

internal static class ModuleInitializerComponents
{
    [ModuleInitializer]
    public static void Initialize()
    {
        GameSettings.ComponentLoader.Register<AnimatedSpriteComponent>(new AnimatedSpriteComponent().ComponentId);
        GameSettings.ComponentLoader.Register<ArcBallCameraComponent>(new ArcBallCameraComponent().ComponentId);
        GameSettings.ComponentLoader.Register<CameraLookAtComponent>(new CameraLookAtComponent().ComponentId);
        GameSettings.ComponentLoader.Register<CameraTargeted2dComponent>(new CameraTargeted2dComponent().ComponentId);
        GameSettings.ComponentLoader.Register<Camera3dIn2dAxisComponent>(new Camera3dIn2dAxisComponent().ComponentId);
        GameSettings.ComponentLoader.Register<GamePlayComponent>(new GamePlayComponent().ComponentId);
        GameSettings.ComponentLoader.Register<SkinnedMeshComponent>(new SkinnedMeshComponent().ComponentId);
        GameSettings.ComponentLoader.Register<StaticMeshComponent>(new StaticMeshComponent().ComponentId);
        GameSettings.ComponentLoader.Register<Physics2dComponent>(new Physics2dComponent().ComponentId);
        GameSettings.ComponentLoader.Register<PhysicsComponent>(new PhysicsComponent().ComponentId);
        GameSettings.ComponentLoader.Register<StaticSpriteComponent>(new StaticSpriteComponent().ComponentId);
        GameSettings.ComponentLoader.Register<TileMapComponent>(new TileMapComponent().ComponentId);

        GameSettings.ComponentLoader.Register<PlayerStartComponent>(new PlayerStartComponent().ComponentId);
    }
}