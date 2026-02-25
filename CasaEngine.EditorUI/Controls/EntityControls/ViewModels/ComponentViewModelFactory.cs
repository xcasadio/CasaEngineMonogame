using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public static class ComponentViewModelFactory
{
    public static ComponentViewModel Create(EntityComponent componentChild)
    {
        return componentChild switch
        {
            AnimatedSpriteComponent => new AnimatedSpriteComponentViewModel(componentChild),
            ArcBallCameraComponent => new ArcBallCameraComponentViewModel(componentChild),
            Box2dCollisionComponent => new Box2dCollisionComponentViewModel(componentChild),
            BoxCollisionComponent => new BoxCollisionComponentViewModel(componentChild),
            Camera3dIn2dAxisComponent => new Camera3dIn2dAxisComponentViewModel(componentChild),
            CameraLookAtComponent => new CameraLookAtComponentViewModel(componentChild),
            CameraTargeted2dComponent => new CameraTargeted2dComponentViewModel(componentChild),
            CapsuleCollisionComponent => new CapsuleCollisionComponentViewModel(componentChild),
            CircleCollisionComponent => new CircleCollisionComponentViewModel(componentChild),
            CylinderCollisionComponent => new CylinderCollisionComponentViewModel(componentChild),
            PlayerStartComponent => new PlayerStartComponentViewModel(componentChild),
            SphereCollisionComponent => new SphereCollisionComponentViewModel(componentChild),
            SkinnedMeshComponent => new SkinnedMeshComponentViewModel(componentChild),
            StaticMeshComponent => new StaticMeshComponentViewModel(componentChild),
            StaticModelComponent => new StaticModelComponentViewModel(componentChild),
            StaticSpriteComponent => new StaticSpriteComponentViewModel(componentChild),
            TileMapComponent => new TileMapComponentViewModel(componentChild),
            _ => new ComponentViewModel(componentChild)
        };
    }
}