using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class EntityComponentTemplateSelector : DataTemplateSelector
{
    public DataTemplate AnimatedSpriteComponentTemplate { get; set; }
    public DataTemplate ArcBallCameraComponenTemplate { get; set; }
    public DataTemplate Box2dCollisionComponentTemplate { get; set; }
    public DataTemplate MeshComponenTemplate { get; set; }
    public DataTemplate PhysicsComponenTemplate { get; set; }
    public DataTemplate SkinnedMeshComponenTemplate { get; set; }
    public DataTemplate StaticSpriteComponentTemplate { get; set; }
    public DataTemplate TileMapComponentTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        switch (item)
        {
            case AnimatedSpriteComponentViewModel: return AnimatedSpriteComponentTemplate;
            case ArcBallCameraComponentViewModel: return ArcBallCameraComponenTemplate;
            case Box2dCollisionComponentViewModel: return Box2dCollisionComponentTemplate;
            case PhysicsBaseComponentViewModel: return PhysicsComponenTemplate;
            case SkinnedMeshComponentViewModel: return SkinnedMeshComponenTemplate;
            case StaticMeshComponentViewModel: return MeshComponenTemplate;
            case StaticSpriteComponentViewModel: return StaticSpriteComponentTemplate;
            case TileMapComponentViewModel: return TileMapComponentTemplate;
        }

        return null;
    }
}