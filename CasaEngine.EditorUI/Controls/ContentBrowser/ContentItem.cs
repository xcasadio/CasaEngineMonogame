using System;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Editor.Controls.ContentBrowser;

public class ContentItem : NotifyPropertyChangeBase
{
    private FolderItem? _parent;
    public AssetInfo? AssetInfo { get; }

    public virtual string FullPath => System.IO.Path.Combine(EngineEnvironment.ProjectPath, AssetInfo.FileName);
    public string FileExtension => System.IO.Path.GetExtension(AssetInfo.FileName);

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

    public Type Type => GetType();

    public FolderItem? Parent
    {
        get => _parent;
        set => SetField(ref _parent, value);
    }

    public virtual string Name
    {
        get => AssetInfo.Name;
        set
        {
            if (AssetInfo.Name != value)
            {
                AssetInfo.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public ContentItem(AssetInfo? assetInfo = null)
    {
        AssetInfo = assetInfo;
    }

    private bool IsRoot(FolderItem parent)
    {
        return parent is { Name: "All", Parent: null };
    }
}