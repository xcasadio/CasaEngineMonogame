using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace EditorWpf.Controls;

public class EntityComponentTemplateSelector : DataTemplateSelector
{
    public DataTemplate MeshComponenTemplate { get; set; }
    public DataTemplate GamePlayComponenTemplate { get; set; }
    public DataTemplate ArcBallCameraComponenTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        var component = item as Component;

        switch ((ComponentIds)component.Type)
        {
            case ComponentIds.Mesh: return MeshComponenTemplate;
            case ComponentIds.ArcBallCamera: return ArcBallCameraComponenTemplate;
            case ComponentIds.GamePlay: return GamePlayComponenTemplate;
        }

        return null;
    }
}
