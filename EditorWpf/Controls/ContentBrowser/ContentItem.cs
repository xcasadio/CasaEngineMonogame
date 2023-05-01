using System;

namespace EditorWpf.Controls.ContentBrowser;

public class ContentItem : NotifyPropertyChangeBase
{
    private FolderItem? _parent;
    private string _name;

    public string Path
    {
        get
        {
            if (Parent != null && !IsRoot(Parent))
            {
                return System.IO.Path.Combine(Parent.Path ?? ".", Name);
            }

            return Name;
        }
    }

    private bool IsRoot(FolderItem parent)
    {
        return parent.Name == "All" && parent.Parent == null;
    }

    public Type Type => GetType();

    public FolderItem? Parent
    {
        get => _parent;
        set => SetField(ref _parent, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }
}