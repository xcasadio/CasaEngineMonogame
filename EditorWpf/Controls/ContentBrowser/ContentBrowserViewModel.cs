using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls.ContentBrowser;

public class ContentBrowserViewModel : INotifyPropertyChanged
{
    public List<ContentItem> ContentItems { get; } = new();

    public ContentBrowserViewModel()
    {
        ContentItems.Add(new FolderItem { Name = "All" });
    }

    public void Initialize(GameEditor gameEditor)
    {
        gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        GameSettings.ProjectManager.ProjectLoaded += OnProjectLoaded;
        GameSettings.ProjectManager.ProjectClosed += OnProjectClosed;
        OnProjectLoaded(sender, EventArgs.Empty);
    }

    private void OnProjectLoaded(object? sender, EventArgs e)
    {
        Clear();
        var rootFolder = new FolderItem { Name = "All" };
        ContentItems.Add(rootFolder);
        AddContent(GameSettings.ProjectManager.ProjectPath!, rootFolder);

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

    private void AddContent(string path, FolderItem folderItem)
    {
        foreach (var fileName in Directory.GetFiles(path))
        {
            var contentItem = new ContentItem { Name = Path.GetFileName(fileName), Parent = folderItem };
            folderItem.Contents.Add(contentItem);
        }

        foreach (var folder in Directory.GetDirectories(path))
        {
            var folderName = folder.Substring(folder.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            var subFolderItem = new FolderItem { Name = folderName, Parent = folderItem };
            folderItem.Contents.Add(subFolderItem);

            AddContent(Path.Combine(path, folder), subFolderItem);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}