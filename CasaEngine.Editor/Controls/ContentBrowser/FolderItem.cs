using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CasaEngine.Editor.Controls.ContentBrowser;

public class FolderItem : ContentItem
{
    private string _name;

    public IEnumerable<FolderItem> Folders => Contents.Where(x => x is FolderItem).Cast<FolderItem>();
    public ObservableCollection<ContentItem> Contents { get; } = new();

    public override string FullPath => Parent == null || IsRoot(Parent) ? IsRoot(this) ? string.Empty : Name : System.IO.Path.Combine(Parent.Name, Name);

    private bool IsRoot()
    {
        return this is { Parent: null, Name: "All" };
    }

    private bool IsRoot(FolderItem? folder)
    {
        return folder is { Parent: null, Name: "All" };
    }

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