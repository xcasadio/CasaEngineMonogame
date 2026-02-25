using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class EntityComponentTemplateSelector : DataTemplateSelector
{
    public DataTemplate AnimatedSpriteComponentTemplate { get; set; }
    public DataTemplate ArcBallCameraComponentTemplate { get; set; }
    public DataTemplate Box2dCollisionComponentTemplate { get; set; }
    public DataTemplate BoxCollisionComponentTemplate { get; set; }
    public DataTemplate CapsuleCollisionComponentTemplate { get; set; }
    public DataTemplate Camera3dIn2dAxisComponentTemplate { get; set; }
    public DataTemplate CameraLookAtComponentTemplate { get; set; }
    public DataTemplate CameraTargeted2dComponentTemplate { get; set; }
    public DataTemplate CircleCollisionComponentTemplate { get; set; }
    public DataTemplate CylinderCollisionComponentTemplate { get; set; }
    public DataTemplate MeshComponentTemplate { get; set; }
    public DataTemplate PhysicsComponentTemplate { get; set; }
    public DataTemplate PlayerStartComponentTemplate { get; set; }
    public DataTemplate SkinnedMeshComponentTemplate { get; set; }
    public DataTemplate StaticModelComponentTemplate { get; set; }
    public DataTemplate SphereCollisionComponentTemplate { get; set; }
    public DataTemplate StaticSpriteComponentTemplate { get; set; }
    public DataTemplate TileMapComponentTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        switch (item)
        {
            case AnimatedSpriteComponentViewModel: return AnimatedSpriteComponentTemplate;
            case ArcBallCameraComponentViewModel: return ArcBallCameraComponentTemplate;
            case Camera3dIn2dAxisComponentViewModel: return Camera3dIn2dAxisComponentTemplate;
            case CameraLookAtComponentViewModel: return CameraLookAtComponentTemplate;
            case CameraTargeted2dComponentViewModel: return CameraTargeted2dComponentTemplate;
            case Box2dCollisionComponentViewModel: return Box2dCollisionComponentTemplate;
            case BoxCollisionComponentViewModel: return BoxCollisionComponentTemplate;
            case CapsuleCollisionComponentViewModel: return CapsuleCollisionComponentTemplate;
            case CircleCollisionComponentViewModel: return CircleCollisionComponentTemplate;
            case CylinderCollisionComponentViewModel: return CylinderCollisionComponentTemplate;
            case SphereCollisionComponentViewModel: return SphereCollisionComponentTemplate;
            case PlayerStartComponentViewModel: return PlayerStartComponentTemplate;
            case SkinnedMeshComponentViewModel: return SkinnedMeshComponentTemplate;
            case StaticModelComponentViewModel: return StaticModelComponentTemplate;
            case StaticMeshComponentViewModel: return MeshComponentTemplate;
            case StaticSpriteComponentViewModel: return StaticSpriteComponentTemplate;
            case TileMapComponentViewModel: return TileMapComponentTemplate;
            case PhysicsBaseComponentViewModel: return PhysicsComponentTemplate;
        }

        return null;
    }
}