using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorUI.Controls.ContentBrowser;

public class FolderItem : ContentItem
{
    private string _name;

    public IEnumerable<FolderItem> Folders => Contents.Where(x => x is FolderItem).Cast<FolderItem>();
    public ObservableCollection<ContentItem> Contents { get; } = new();

    public override string FullPath
    {
        get
        {
            if (IsRoot())
            {
                return string.Empty;
            }

            if (Parent.IsRoot())
            {
                return Name;
            }

            return System.IO.Path.Combine(Parent.Name, Name);
        }
    }

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
        set
        {
            //we can't rename the root node
            if (Parent == null)
            {
                return;
            }

            var oldFullPath = FullPath;
            if (SetField(ref _name, value))
            {
                var sourceFullPath = System.IO.Path.Combine(EngineEnvironment.ProjectPath, oldFullPath);
                var newFullPath = System.IO.Path.Combine(EngineEnvironment.ProjectPath, FullPath);

                if (!Directory.Exists(newFullPath))
                {
                    Directory.Move(sourceFullPath, newFullPath);
                    Logs.WriteTrace($"Rename folder '{oldFullPath}' to '{FullPath}'");
                }

                AssetCatalog.Save();
            }
        }
    }

    public FolderItem()
    {
        Contents.CollectionChanged += OnCollectionChanged;
    }

    public FolderItem(string folderName, FolderItem parent) : this()
    {
        _name = folderName;
        Parent = parent;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged("Folders");
    }

    public void AddContent(ContentItem item)
    {
        Contents.Add(item);
        item.Parent = this;
        //OnPropertyChanged("Contents");
    }

    public void RemoveContent(ContentItem item)
    {
        Contents.Remove(item);
        OnPropertyChanged("Contents");

        if (item is FolderItem)
        {
            item.Parent = null;
            OnPropertyChanged("Folders");
        }
    }

    public override void Delete()
    {
        //we can't delete root node
        if (Parent == null)
        {
            return;
        }

        var contentItems = new List<ContentItem>(Contents);

        foreach (var contentItem in contentItems)
        {
            contentItem.Delete();
        }

        Directory.Delete(System.IO.Path.Combine(EngineEnvironment.ProjectPath, FullPath), true);
        Logs.WriteTrace($"Remove folder '{FullPath}'");
        Parent.RemoveContent(this);
    }
}