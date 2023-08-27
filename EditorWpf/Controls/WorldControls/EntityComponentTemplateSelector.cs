using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace EditorWpf.Controls.WorldControls;

public class EntityComponentTemplateSelector : DataTemplateSelector
{
    public DataTemplate MeshComponenTemplate { get; set; }
    public DataTemplate GamePlayComponenTemplate { get; set; }
    public DataTemplate ArcBallCameraComponenTemplate { get; set; }
    public DataTemplate PhysicsComponenTemplate { get; set; }
    public DataTemplate TileMapComponentTemplate { get; set; }
    public DataTemplate StaticSpriteComponentTemplate { get; set; }
    public DataTemplate AnimatedSpriteComponentTemplate { get; set; }


    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is Component component)
        {
            switch ((ComponentIds)component.Type)
            {
                case ComponentIds.Mesh: return MeshComponenTemplate;
                case ComponentIds.ArcBallCamera: return ArcBallCameraComponenTemplate;
                case ComponentIds.GamePlay: return GamePlayComponenTemplate;
                case ComponentIds.Physics: return PhysicsComponenTemplate;
                case ComponentIds.TileMap: return TileMapComponentTemplate;
                case ComponentIds.AnimatedSprite: return AnimatedSpriteComponentTemplate;
                case ComponentIds.StaticSprite: return StaticSpriteComponentTemplate;
            }
        }

        return null;
    }
}