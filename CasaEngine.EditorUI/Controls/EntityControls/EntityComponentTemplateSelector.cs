using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class EntityComponentTemplateSelector : DataTemplateSelector
{
    public DataTemplate MeshComponenTemplate { get; set; }
    public DataTemplate GamePlayComponenTemplate { get; set; }
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
            case StaticMeshComponent: return MeshComponenTemplate;
            case ArcBallCameraComponent: return ArcBallCameraComponenTemplate;
            //case ComponentIds.GamePlay: return GamePlayComponenTemplate;
            case PhysicsComponent: return PhysicsComponenTemplate;
            case Physics2dComponent: return Physics2dComponenTemplate;
            case TileMapComponent: return TileMapComponentTemplate;
            case AnimatedSpriteComponent: return AnimatedSpriteComponentTemplate;
            case StaticSpriteComponent: return StaticSpriteComponentTemplate;
        }

        return null;
    }
}


public class EntityDetailTemplateSelector : DataTemplateSelector
{
    public DataTemplate ComponenTemplate { get; set; }
    public DataTemplate EntityTemplate { get; set; }
    public DataTemplate EmptyTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        switch (item)
        {
            case RootNodeComponentViewModel: return EntityTemplate;
            case ComponentViewModel: return ComponenTemplate;
        }

        return EmptyTemplate;
    }
}