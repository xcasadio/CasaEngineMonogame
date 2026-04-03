using System;
using System.IO;
using System.Threading;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.EditorServices;

namespace CasaEngine.Editor.ContentBrowser.Services;

public sealed class FileOperationService : IDisposable
{
    private ContentItem _rootItem;
    private FileSystemWatcher _watcher;
    private int _suspendWatcherNotifications;
    private int _hasPendingExternalChanges;

    public event Action<string> ErrorOccurred;

    public bool HasPendingExternalChanges => Volatile.Read(ref _hasPendingExternalChanges) == 1;

    public void SetRoot(ContentItem rootItem)
    {
        _rootItem = rootItem;
        ResetWatcher(rootItem.FullPath);
    }

    public void ClearRoot()
    {
        _rootItem = null;
        DisposeWatcher();
        Interlocked.Exchange(ref _hasPendingExternalChanges, 0);
    }

    public bool ConsumePendingExternalChanges()
        => Interlocked.Exchange(ref _hasPendingExternalChanges, 0) == 1;

    public bool CreateDirectory(string path, string name)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                Directory.CreateDirectory(Path.Combine(path, name));
            }

            RefreshRootModel();
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Cannot create directory '{name}'.", ex);
            return false;
        }
    }

    public bool Delete(ContentItem item)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                if (item.IsDirectory)
                {
                    if (Directory.Exists(item.FullPath))
                    {
                        Directory.Delete(item.FullPath, true);
                    }
                }
                else if (File.Exists(item.FullPath))
                {
                    File.Delete(item.FullPath);
                }
            }

            RefreshRootModel();
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Cannot delete '{item.Name}'.", ex);
            return false;
        }
    }

    public bool Rename(ContentItem item, string newName)
    {
        try
        {
            string targetPath = GetRenameTargetPath(item, newName);
            using (SuspendWatcherNotifications())
            {
                if (item.IsDirectory)
                {
                    Directory.Move(item.FullPath, targetPath);
                }
                else
                {
                    File.Move(item.FullPath, targetPath);
                }
            }

            RefreshRootModel();
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Cannot rename '{item.Name}'.", ex);
            return false;
        }
    }

    public bool Move(ContentItem item, ContentItem targetDirectory)
    {
        try
        {
            string destinationPath = GetUniqueDestinationPath(targetDirectory.FullPath, item.Name, item.IsDirectory);
            using (SuspendWatcherNotifications())
            {
                if (item.IsDirectory)
                {
                    Directory.Move(item.FullPath, destinationPath);
                }
                else
                {
                    File.Move(item.FullPath, destinationPath);
                }
            }

            RefreshRootModel();
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Cannot move '{item.Name}'.", ex);
            return false;
        }
    }

    public bool Copy(ContentItem item, ContentItem targetDirectory)
    {
        try
        {
            string destinationPath = GetUniqueDestinationPath(targetDirectory.FullPath, item.Name, item.IsDirectory);
            using (SuspendWatcherNotifications())
            {
                if (item.IsDirectory)
                {
                    CopyDirectory(item.FullPath, destinationPath);
                }
                else
                {
                    File.Copy(item.FullPath, destinationPath);
                }
            }

            RefreshRootModel();
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Cannot copy '{item.Name}'.", ex);
            return false;
        }
    }

    public bool Import(string[] externalPaths, ContentItem targetDirectory)
    {
        if (externalPaths == null || externalPaths.Length == 0)
        {
            return false;
        }

        try
        {
            bool assetCatalogChanged = false;
            using (SuspendWatcherNotifications())
            {
                foreach (string externalPath in externalPaths)
                {
                    if (string.IsNullOrWhiteSpace(externalPath))
                    {
                        continue;
                    }

                    if (Directory.Exists(externalPath))
                    {
                        string destinationDirectory = GetUniqueDestinationPath(targetDirectory.FullPath, Path.GetFileName(externalPath), true);
                        CopyDirectory(externalPath, destinationDirectory);
                    }
                    else if (File.Exists(externalPath))
                    {
                        string destinationFile = GetUniqueDestinationPath(targetDirectory.FullPath, Path.GetFileName(externalPath), false);
                        File.Copy(externalPath, destinationFile);
                        assetCatalogChanged |= EditorAssetImportService.ImportFile(externalPath, destinationFile);
                    }
                }
            }

            if (assetCatalogChanged)
            {
                EditorAssetCatalogService.Save();
            }

            RefreshRootModel();
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Cannot import into '{targetDirectory.Name}'.", ex);
            return false;
        }
    }

    public void Dispose()
    {
        DisposeWatcher();
    }

    private void RefreshRootModel()
    {
        if (_rootItem == null || !Directory.Exists(_rootItem.FullPath))
        {
            return;
        }

        FileSystemScanner.Refresh(_rootItem);
        _rootItem.RefreshFileSystemMetadata();
    }

    private IDisposable SuspendWatcherNotifications()
    {
        Interlocked.Increment(ref _suspendWatcherNotifications);
        return new WatcherNotificationScope(this);
    }

    private void ResetWatcher(string rootPath)
    {
        DisposeWatcher();

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return;
        }

        _watcher = new FileSystemWatcher(rootPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };

        _watcher.Created += OnWatcherChanged;
        _watcher.Deleted += OnWatcherChanged;
        _watcher.Changed += OnWatcherChanged;
        _watcher.Renamed += OnWatcherRenamed;
        _watcher.Error += OnWatcherError;
    }

    private void DisposeWatcher()
    {
        if (_watcher == null)
        {
            return;
        }

        _watcher.Created -= OnWatcherChanged;
        _watcher.Deleted -= OnWatcherChanged;
        _watcher.Changed -= OnWatcherChanged;
        _watcher.Renamed -= OnWatcherRenamed;
        _watcher.Error -= OnWatcherError;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        if (Volatile.Read(ref _suspendWatcherNotifications) > 0)
        {
            return;
        }

        Interlocked.Exchange(ref _hasPendingExternalChanges, 1);
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        OnWatcherChanged(sender, e);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        ReportError("The content browser watcher encountered an error.", e.GetException());
    }

    private void ReportError(string message, Exception ex)
    {
        string fullMessage = ex == null ? message : $"{message}\n{ex.Message}";
        ErrorOccurred?.Invoke(fullMessage);
    }

    private static string GetRenameTargetPath(ContentItem item, string newName)
    {
        string sanitizedName = newName.Trim();
        if (!item.IsDirectory && string.IsNullOrEmpty(Path.GetExtension(sanitizedName)))
        {
            sanitizedName += item.Extension;
        }

        string parentDirectory = item.Parent != null
            ? item.Parent.FullPath
            : Path.GetDirectoryName(item.FullPath) ?? string.Empty;

        return Path.Combine(parentDirectory, sanitizedName);
    }

    private static string GetUniqueDestinationPath(string targetDirectory, string itemName, bool isDirectory)
    {
        string baseName = isDirectory ? itemName : Path.GetFileNameWithoutExtension(itemName);
        string extension = isDirectory ? string.Empty : Path.GetExtension(itemName);
        string candidate = Path.Combine(targetDirectory, itemName);
        int suffix = 1;

        while (Directory.Exists(candidate) || File.Exists(candidate))
        {
            string uniqueName = isDirectory
                ? $"{baseName} ({suffix++})"
                : $"{baseName} ({suffix++}){extension}";
            candidate = Path.Combine(targetDirectory, uniqueName);
        }

        return candidate;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string filePath in Directory.GetFiles(sourceDirectory))
        {
            string destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationFilePath);
        }

        foreach (string directoryPath in Directory.GetDirectories(sourceDirectory))
        {
            string destinationChildPath = Path.Combine(destinationDirectory, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, destinationChildPath);
        }
    }

    private sealed class WatcherNotificationScope : IDisposable
    {
        private readonly FileOperationService _owner;

        public WatcherNotificationScope(FileOperationService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Decrement(ref _owner._suspendWatcherNotifications);
        }
    }
}