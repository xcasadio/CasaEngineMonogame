using System.Collections.Generic;
using System.Linq;

namespace EditorWpf.Controls.ContentBrowser;

public class FolderItem : ContentItem
{
    private string _name;

    public IEnumerable<ContentItem> Folders => Contents.Where(x => x is FolderItem);
    public List<ContentItem> Contents { get; } = new();

    public override string FullPath => Parent == null ? Name : System.IO.Path.Combine(Parent.Name, Name);

    public override string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public void AddContent(ContentItem item)
    {
        Contents.Add(item);
        OnPropertyChanged("Contents");

        if (item is FolderItem)
        {
            OnPropertyChanged("Folders");
        }
    }

    public void RemoveContent(ContentItem item)
    {
        Contents.Remove(item);
        OnPropertyChanged("Contents");

        if (item is FolderItem)
        {
            OnPropertyChanged("Folders");
        }
    }
}