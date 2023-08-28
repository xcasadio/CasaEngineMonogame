using System.Windows;
using System.Windows.Controls;

namespace EditorWpf.Controls.ContentBrowser;

public class ContentBrowserItemDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate ContentItemTemplate { get; set; }
    public DataTemplate FolderItemTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item == null) return null;

        if (item is FolderItem)
        {
            return FolderItemTemplate;
        }

        return ContentItemTemplate;
    }
}