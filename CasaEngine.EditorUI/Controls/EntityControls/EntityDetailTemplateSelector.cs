using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

namespace CasaEngine.EditorUI.Controls.EntityControls;

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