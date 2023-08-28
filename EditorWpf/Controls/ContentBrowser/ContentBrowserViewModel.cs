using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls.ContentBrowser;

public class ContentBrowserViewModel : INotifyPropertyChanged
{
    private FolderItem _rootFolder;
    public List<ContentItem> ContentItems { get; } = new();

    public ContentBrowserViewModel()
    {
        ContentItems.Add(new FolderItem { Name = "All" });

        GameSettings.AssetInfoManager.AssetAdded += OnAssetAdded;
        GameSettings.AssetInfoManager.AssetRemoved += OnAssetRemoved;
        GameSettings.AssetInfoManager.AssetCleared += OnAssetCleared;
    }

    private void OnAssetAdded(object? sender, AssetInfo assetInfo)
    {
        var folderItem = GetOrCreateFolders(_rootFolder, assetInfo.FileName);
        folderItem.AddContent(new ContentItem(assetInfo));
    }

    private void OnAssetRemoved(object? sender, AssetInfo assetInfo)
    {

    }

    private void OnAssetCleared(object? sender, EventArgs e)
    {
        //_rootFolder.Clear();
    }

    public void Initialize(GameEditor gameEditor)
    {
        gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        GameSettings.ProjectSettings.ProjectLoaded += OnProjectLoaded;
        GameSettings.ProjectSettings.ProjectClosed += OnProjectClosed;
        OnProjectLoaded(sender, EventArgs.Empty);
    }

    private void OnProjectLoaded(object? sender, EventArgs e)
    {
        Clear();
        _rootFolder = new FolderItem { Name = "All" };
        ContentItems.Add(_rootFolder);
        AddContent(_rootFolder);

        OnPropertyChanged(nameof(ContentItems));
    }

    private void OnProjectClosed(object? sender, EventArgs e)
    {
        Clear();
    }

    private void Clear()
    {
        ContentItems.Clear();
        OnPropertyChanged(nameof(ContentItems));
    }

    private void AddContent(FolderItem rootFolderItem)
    {
        foreach (var assetInfo in GameSettings.AssetInfoManager.AssetInfos)
        {
            var folderItem = GetOrCreateFolders(rootFolderItem, assetInfo.FileName);
            var contentItem = new ContentItem(assetInfo) { Parent = folderItem };
            folderItem.Contents.Add(contentItem);
        }
    }

    private FolderItem GetOrCreateFolders(FolderItem rootFolderItem, string fullFileName)
    {
        fullFileName = fullFileName
            .Replace(EngineEnvironment.ProjectPath, string.Empty)
            .TrimStart(Path.DirectorySeparatorChar);

        var folders = Path.GetDirectoryName(fullFileName).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        FolderItem parentFolderItem = rootFolderItem;
        FolderItem folderItem = null;

        foreach (var folder in folders)
        {
            folderItem = GetOrCreateFolder(parentFolderItem, folder);
            parentFolderItem = folderItem;
        }

        return folderItem;
    }

    private FolderItem GetOrCreateFolder(FolderItem parentFolderItem, string folderName)
    {
        if (parentFolderItem.Folders.FirstOrDefault(x => x.Name == folderName && x is FolderItem) is not FolderItem folderItem)
        {
            folderItem = new FolderItem { Name = folderName, Parent = parentFolderItem };
            parentFolderItem.Contents.Add(folderItem);
        }

        return folderItem;
    }

    //private void AddContent(string path, FolderItem folderItem)
    //{
    //    foreach (var fileName in Directory.GetFiles(path))
    //    {
    //        var contentItem = new ContentItem { Name = Path.GetFileName(fileName), Parent = folderItem };
    //        folderItem.Contents.Add(contentItem);
    //    }
    //
    //    foreach (var folder in Directory.GetDirectories(path))
    //    {
    //        var folderName = folder.Substring(folder.LastIndexOf(Path.DirectorySeparatorChar) + 1);
    //        var subFolderItem = new FolderItem { Name = folderName, Parent = folderItem };
    //        folderItem.Contents.Add(subFolderItem);
    //
    //        AddContent(Path.Combine(path, folder), subFolderItem);
    //    }
    //}

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}