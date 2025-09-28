using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class EntityComponentTemplateSelector : DataTemplateSelector
{
    public DataTemplate MeshComponenTemplate { get; set; }
    public DataTemplate SkinnedMeshComponenTemplate { get; set; }
    public DataTemplate ArcBallCameraComponenTemplate { get; set; }
    public DataTemplate PhysicsComponenTemplate { get; set; }
    public DataTemplate Physics2dComponenTemplate { get; set; }
    public DataTemplate TileMapComponentTemplate { get; set; }
    public DataTemplate StaticSpriteComponentTemplate { get; set; }
    public DataTemplate AnimatedSpriteComponentTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        switch (item)
        {
            case StaticMeshComponentViewModel: return MeshComponenTemplate;
            case SkinnedMeshComponentViewModel: return SkinnedMeshComponenTemplate;
            case ArcBallCameraComponentViewModel: return ArcBallCameraComponenTemplate;
            case PhysicsBaseComponentViewModel: return PhysicsComponenTemplate;
            //case Physics2dComponent: return Physics2dComponenTemplate;
            case TileMapComponentViewModel: return TileMapComponentTemplate;
            case AnimatedSpriteComponentViewModel: return AnimatedSpriteComponentTemplate;
            case StaticSpriteComponentViewModel: return StaticSpriteComponentTemplate;
        }

        return null;
    }
}