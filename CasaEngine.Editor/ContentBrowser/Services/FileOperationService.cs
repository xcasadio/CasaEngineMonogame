using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Tiled;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Editor.ContentBrowser.Services;

public sealed class FileOperationService : IDisposable
{
    private readonly ContentBrowserTrashService _trashService = new();
    private ContentItem _rootItem;
    private FileSystemWatcher _watcher;
    private int _suspendWatcherNotifications;
    private int _hasPendingExternalChanges;

    public event Action<string> ErrorOccurred;
    public event Action<string> WarningOccurred;

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

    public bool TryCreateDirectoryOperation(string parentPath, string name, out ReversibleFileOperation operation)
    {
        operation = null!;
        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string createdPath = Path.Combine(parentPath, name);
        if (!CreateDirectory(parentPath, name))
        {
            return false;
        }

        operation = new ReversibleFileOperation(
            service => service.TryCreateDirectoryExact(createdPath),
            service => service.TryDeleteEmptyDirectoryExact(createdPath),
            selectionAfterExecute: new[] { createdPath },
            selectionAfterUndo: Array.Empty<string>());
        return true;
    }

    public bool TryDeleteOperation(IReadOnlyList<string> sourcePaths, out ReversibleFileOperation operation)
    {
        operation = null!;

        var normalizedPaths = GetTopLevelExistingPaths(sourcePaths);
        if (normalizedPaths.Count == 0)
        {
            return false;
        }

        var removedAssetEntries = CaptureAssetEntriesUnderPaths(normalizedPaths);
        if (!TryMovePathsToTrash(normalizedPaths, out var trashEntries))
        {
            return false;
        }

        if (!TryRemoveAssetEntries(removedAssetEntries))
        {
            TryRestoreTrashEntries(trashEntries);
            RefreshRootModel();
            return false;
        }

        RefreshRootModel();
        operation = new ReversibleFileOperation(
            service => service.TryRedoDeleteOperation(trashEntries, removedAssetEntries),
            service => service.TryUndoDeleteOperation(trashEntries, removedAssetEntries),
            selectionAfterExecute: Array.Empty<string>(),
            selectionAfterUndo: normalizedPaths);
        return true;
    }

    public bool TryRenameOperation(string sourcePath, string newName, out ReversibleFileOperation operation)
    {
        operation = null!;
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(newName) || !PathExists(sourcePath))
        {
            return false;
        }

        bool isDirectory = Directory.Exists(sourcePath);
        string targetPath = GetRenameTargetPath(sourcePath, isDirectory, newName);
        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var pathMoves = new List<PathMove> { new(sourcePath, targetPath, isDirectory) };
        var previousAssetEntries = CaptureAssetEntriesUnderPaths(new[] { sourcePath });
        var nextAssetEntries = BuildMovedAssetEntries(previousAssetEntries, pathMoves, renameTopLevelFile: !isDirectory);
        if (!TryApplyPathMoves(pathMoves))
        {
            return false;
        }

        if (!TryReplaceAssetEntries(previousAssetEntries, nextAssetEntries))
        {
            TryApplyPathMovesReverse(pathMoves);
            RefreshRootModel();
            return false;
        }

        RefreshRootModel();
        operation = new ReversibleFileOperation(
            service => service.TryRedoMoveOperation(pathMoves, previousAssetEntries, nextAssetEntries),
            service => service.TryUndoMoveOperation(pathMoves, previousAssetEntries, nextAssetEntries),
            selectionAfterExecute: new[] { targetPath },
            selectionAfterUndo: new[] { sourcePath });
        return true;
    }

    public bool TryMoveOperation(IReadOnlyList<string> sourcePaths, string targetDirectoryPath, out ReversibleFileOperation operation)
    {
        operation = null!;
        if (string.IsNullOrWhiteSpace(targetDirectoryPath) || !Directory.Exists(targetDirectoryPath))
        {
            return false;
        }

        var normalizedPaths = GetTopLevelExistingPaths(sourcePaths);
        if (normalizedPaths.Count == 0)
        {
            return false;
        }

        var pathMoves = BuildPathMoves(normalizedPaths, targetDirectoryPath);
        var previousAssetEntries = CaptureAssetEntriesUnderPaths(normalizedPaths);
        var nextAssetEntries = BuildMovedAssetEntries(previousAssetEntries, pathMoves, renameTopLevelFile: false);
        if (!TryApplyPathMoves(pathMoves))
        {
            return false;
        }

        if (!TryReplaceAssetEntries(previousAssetEntries, nextAssetEntries))
        {
            TryApplyPathMovesReverse(pathMoves);
            RefreshRootModel();
            return false;
        }

        RefreshRootModel();
        operation = new ReversibleFileOperation(
            service => service.TryRedoMoveOperation(pathMoves, previousAssetEntries, nextAssetEntries),
            service => service.TryUndoMoveOperation(pathMoves, previousAssetEntries, nextAssetEntries),
            selectionAfterExecute: GetDestinationPaths(pathMoves),
            selectionAfterUndo: normalizedPaths);
        return true;
    }

    public bool TryCopyOperation(IReadOnlyList<string> sourcePaths, string targetDirectoryPath, out ReversibleFileOperation operation)
    {
        operation = null!;
        if (string.IsNullOrWhiteSpace(targetDirectoryPath) || !Directory.Exists(targetDirectoryPath))
        {
            return false;
        }

        var normalizedPaths = GetTopLevelExistingPaths(sourcePaths);
        if (normalizedPaths.Count == 0)
        {
            return false;
        }

        var createdPaths = BuildCopyDestinationPaths(normalizedPaths, targetDirectoryPath);
        if (!TryApplyCopies(normalizedPaths, createdPaths))
        {
            return false;
        }

        var addedAssetEntries = RegisterAssetsUnderPaths(createdPaths);
        RefreshRootModel();

        List<ContentBrowserTrashEntry>? trashEntries = null;
        operation = new ReversibleFileOperation(
            service => service.TryRedoCopyLikeOperation(trashEntries, addedAssetEntries, createdPaths),
            service =>
            {
                if (trashEntries == null && !service.TryMovePathsToTrash(createdPaths, out trashEntries))
                {
                    return false;
                }

                if (trashEntries != null && !service.TryRemoveAssetEntries(addedAssetEntries))
                {
                    service.TryRestoreTrashEntries(trashEntries);
                    service.RefreshRootModel();
                    return false;
                }

                service.RefreshRootModel();
                return true;
            },
            selectionAfterExecute: createdPaths,
            selectionAfterUndo: normalizedPaths);
        return true;
    }

    public bool TryImportOperation(string[] externalPaths, string targetDirectoryPath, out ReversibleFileOperation operation)
    {
        operation = null!;
        if (externalPaths == null || externalPaths.Length == 0 || string.IsNullOrWhiteSpace(targetDirectoryPath) || !Directory.Exists(targetDirectoryPath))
        {
            return false;
        }

        var snapshotBeforeImport = CaptureDirectorySnapshot(targetDirectoryPath);
        var targetDirectory = new ContentItem(targetDirectoryPath, true);
        if (!Import(externalPaths, targetDirectory))
        {
            return false;
        }

        var snapshotAfterImport = CaptureDirectorySnapshot(targetDirectoryPath);
        var createdPaths = GetCreatedRootPaths(snapshotBeforeImport, snapshotAfterImport);
        var addedAssetEntries = GetAddedAssetEntries(snapshotBeforeImport, snapshotAfterImport);
        RefreshRootModel();

        List<ContentBrowserTrashEntry>? trashEntries = null;
        operation = new ReversibleFileOperation(
            service => service.TryRedoCopyLikeOperation(trashEntries, addedAssetEntries, createdPaths),
            service =>
            {
                if (trashEntries == null && !service.TryMovePathsToTrash(createdPaths, out trashEntries))
                {
                    return false;
                }

                if (trashEntries != null && !service.TryRemoveAssetEntries(addedAssetEntries))
                {
                    service.TryRestoreTrashEntries(trashEntries);
                    service.RefreshRootModel();
                    return false;
                }

                service.RefreshRootModel();
                return true;
            },
            selectionAfterExecute: createdPaths,
            selectionAfterUndo: Array.Empty<string>());
        return true;
    }

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
                        ReportTiledMapImportWarnings(EditorAssetImportService.LastTiledMapImportResult);
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

    private void ReportTiledMapImportWarnings(TiledMapImportResult importResult)
    {
        if (importResult == null || importResult.Warnings.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine("Imported Tiled map with warnings:");
        for (var index = 0; index < importResult.Warnings.Count; index++)
        {
            message.Append("- ");
            message.Append(importResult.Warnings[index]);
            if (index + 1 < importResult.Warnings.Count)
            {
                message.AppendLine();
            }
        }

        WarningOccurred?.Invoke(message.ToString());
    }

    private bool TryRedoCopyLikeOperation(
        List<ContentBrowserTrashEntry>? trashEntries,
        IReadOnlyList<AssetInfo> assetEntries,
        IReadOnlyList<string> selectionAfterExecute)
    {
        if (trashEntries == null || !TryRestoreTrashEntries(trashEntries) || !TryRestoreAssetEntries(assetEntries))
        {
            return false;
        }

        RefreshRootModel();
        return true;
    }

    private bool TryRedoDeleteOperation(IReadOnlyList<ContentBrowserTrashEntry> trashEntries, IReadOnlyList<AssetInfo> removedAssetEntries)
    {
        if (!TryMovePathsToTrash(trashEntries) || !TryRemoveAssetEntries(removedAssetEntries))
        {
            return false;
        }

        RefreshRootModel();
        return true;
    }

    private bool TryUndoDeleteOperation(IReadOnlyList<ContentBrowserTrashEntry> trashEntries, IReadOnlyList<AssetInfo> removedAssetEntries)
    {
        if (!TryRestoreTrashEntries(trashEntries) || !TryRestoreAssetEntries(removedAssetEntries))
        {
            return false;
        }

        RefreshRootModel();
        return true;
    }

    private bool TryRedoMoveOperation(IReadOnlyList<PathMove> pathMoves, IReadOnlyList<AssetInfo> previousAssetEntries, IReadOnlyList<AssetInfo> nextAssetEntries)
    {
        if (!TryApplyPathMoves(pathMoves) || !TryReplaceAssetEntries(previousAssetEntries, nextAssetEntries))
        {
            return false;
        }

        RefreshRootModel();
        return true;
    }

    private bool TryUndoMoveOperation(IReadOnlyList<PathMove> pathMoves, IReadOnlyList<AssetInfo> previousAssetEntries, IReadOnlyList<AssetInfo> nextAssetEntries)
    {
        if (!TryApplyPathMovesReverse(pathMoves) || !TryReplaceAssetEntries(nextAssetEntries, previousAssetEntries))
        {
            return false;
        }

        RefreshRootModel();
        return true;
    }

    private bool TryCreateDirectoryExact(string fullPath)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                Directory.CreateDirectory(fullPath);
            }

            RefreshRootModel();
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Cannot create directory '{Path.GetFileName(fullPath)}'.", ex);
            return false;
        }
    }

    private bool TryDeleteEmptyDirectoryExact(string fullPath)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, false);
                }
            }

            RefreshRootModel();
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Cannot remove directory '{Path.GetFileName(fullPath)}'.", ex);
            return false;
        }
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
#if DEBUG
        string fullMessage = ex == null ? message : $"{message}\n{ex}";
#else
        string fullMessage = ex == null ? message : $"{message}\n{ex.Message}";
#endif
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

    private static string GetRenameTargetPath(string sourcePath, bool isDirectory, string newName)
    {
        string sanitizedName = newName.Trim();
        if (!isDirectory && string.IsNullOrEmpty(Path.GetExtension(sanitizedName)))
        {
            sanitizedName += Path.GetExtension(sourcePath);
        }

        string parentDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
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

    private static string GetUniqueDestinationPath(string targetDirectory, string itemName, bool isDirectory, HashSet<string> reservedPaths)
    {
        string candidate = GetUniqueDestinationPath(targetDirectory, itemName, isDirectory);
        while (reservedPaths.Contains(candidate))
        {
            string baseName = isDirectory ? Path.GetFileName(candidate) : Path.GetFileNameWithoutExtension(candidate);
            string extension = isDirectory ? string.Empty : Path.GetExtension(candidate);
            candidate = Path.Combine(targetDirectory, isDirectory
                ? $"{baseName} Copy"
                : $"{baseName} Copy{extension}");
            candidate = GetUniqueDestinationPath(targetDirectory, Path.GetFileName(candidate), isDirectory);
        }

        reservedPaths.Add(candidate);
        return candidate;
    }

    private List<AssetInfo> CaptureAssetEntriesUnderPaths(IReadOnlyList<string> sourcePaths)
    {
        var assetEntries = new List<AssetInfo>();
        if (string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            return assetEntries;
        }

        foreach (var assetInfo in AssetCatalog.AssetInfos)
        {
            string assetFullPath = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                if (!IsSamePathOrDescendant(assetFullPath, sourcePaths[i]))
                {
                    continue;
                }

                assetEntries.Add(CloneAssetInfo(assetInfo));
                break;
            }
        }

        return assetEntries;
    }

    private static List<AssetInfo> BuildMovedAssetEntries(IReadOnlyList<AssetInfo> previousAssetEntries, IReadOnlyList<PathMove> pathMoves, bool renameTopLevelFile)
    {
        var movedAssetEntries = new List<AssetInfo>(previousAssetEntries.Count);
        if (string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            return movedAssetEntries;
        }

        for (int i = 0; i < previousAssetEntries.Count; i++)
        {
            var movedAssetEntry = CloneAssetInfo(previousAssetEntries[i]);
            string assetFullPath = Path.Combine(EngineEnvironment.ProjectPath, movedAssetEntry.FileName);
            for (int j = 0; j < pathMoves.Count; j++)
            {
                var pathMove = pathMoves[j];
                if (!IsSamePathOrDescendant(assetFullPath, pathMove.SourcePath))
                {
                    continue;
                }

                string relativeSuffix = assetFullPath.Length == pathMove.SourcePath.Length
                    ? string.Empty
                    : assetFullPath[pathMove.SourcePath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string newFullPath = string.IsNullOrWhiteSpace(relativeSuffix)
                    ? pathMove.DestinationPath
                    : Path.Combine(pathMove.DestinationPath, relativeSuffix);
                movedAssetEntry.FileName = GetRelativeProjectPath(newFullPath);
                if (renameTopLevelFile && !pathMove.IsDirectory && string.Equals(assetFullPath, pathMove.SourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    movedAssetEntry.Name = Path.GetFileNameWithoutExtension(newFullPath);
                }

                break;
            }

            movedAssetEntries.Add(movedAssetEntry);
        }

        return movedAssetEntries;
    }

    private bool TryRemoveAssetEntries(IReadOnlyList<AssetInfo> assetEntries)
    {
        try
        {
            bool changed = false;
            for (int i = 0; i < assetEntries.Count; i++)
            {
                if (EditorAssetCatalogService.TryRemoveEntry(assetEntries[i].Id, out _))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                EditorAssetCatalogService.Save();
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportError("Cannot update the asset catalog.", ex);
            return false;
        }
    }

    private bool TryRestoreAssetEntries(IReadOnlyList<AssetInfo> assetEntries)
    {
        try
        {
            bool changed = false;
            for (int i = 0; i < assetEntries.Count; i++)
            {
                if (AssetCatalog.Get(assetEntries[i].Id) != null)
                {
                    continue;
                }

                EditorAssetCatalogService.RestoreEntry(CloneAssetInfo(assetEntries[i]));
                changed = true;
            }

            if (changed)
            {
                EditorAssetCatalogService.Save();
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportError("Cannot restore the asset catalog.", ex);
            return false;
        }
    }

    private bool TryReplaceAssetEntries(IReadOnlyList<AssetInfo> previousAssetEntries, IReadOnlyList<AssetInfo> nextAssetEntries)
    {
        try
        {
            for (int i = 0; i < previousAssetEntries.Count; i++)
            {
                EditorAssetCatalogService.TryRemoveEntry(previousAssetEntries[i].Id, out _);
            }

            for (int i = 0; i < nextAssetEntries.Count; i++)
            {
                EditorAssetCatalogService.RestoreEntry(CloneAssetInfo(nextAssetEntries[i]));
            }

            if (previousAssetEntries.Count > 0 || nextAssetEntries.Count > 0)
            {
                EditorAssetCatalogService.Save();
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportError("Cannot replace asset catalog entries.", ex);
            return false;
        }
    }

    private static AssetInfo CloneAssetInfo(AssetInfo assetInfo)
    {
        return new AssetInfo(assetInfo.Id)
        {
            Name = assetInfo.Name,
            FileName = assetInfo.FileName,
            AssetType = assetInfo.AssetType,
        };
    }

    private bool TryMovePathsToTrash(IReadOnlyList<string> sourcePaths, out List<ContentBrowserTrashEntry> trashEntries)
    {
        trashEntries = new List<ContentBrowserTrashEntry>();
        try
        {
            using (SuspendWatcherNotifications())
            {
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    if (!_trashService.TryMoveToTrash(sourcePaths[i], out var trashEntry))
                    {
                        throw new IOException($"Failed to move '{sourcePaths[i]}' to trash.");
                    }

                    trashEntries.Add(trashEntry);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            TryRestoreTrashEntries(trashEntries);
            ReportError("Cannot move items to the editor trash.", ex);
            return false;
        }
    }

    private bool TryMovePathsToTrash(IReadOnlyList<ContentBrowserTrashEntry> trashEntries)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                for (int i = 0; i < trashEntries.Count; i++)
                {
                    if (!_trashService.TryMoveToTrash(trashEntries[i]))
                    {
                        throw new IOException($"Failed to move '{trashEntries[i].OriginalPath}' to trash.");
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportError("Cannot move items to the editor trash.", ex);
            return false;
        }
    }

    private bool TryRestoreTrashEntries(IReadOnlyList<ContentBrowserTrashEntry> trashEntries)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                for (int i = 0; i < trashEntries.Count; i++)
                {
                    if (!_trashService.TryRestore(trashEntries[i]))
                    {
                        throw new IOException($"Failed to restore '{trashEntries[i].OriginalPath}' from trash.");
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportError("Cannot restore items from the editor trash.", ex);
            return false;
        }
    }

    private static List<string> GetTopLevelExistingPaths(IReadOnlyList<string> sourcePaths)
    {
        var normalizedPaths = new List<string>();
        if (sourcePaths == null)
        {
            return normalizedPaths;
        }

        for (int i = 0; i < sourcePaths.Count; i++)
        {
            string sourcePath = sourcePaths[i];
            if (string.IsNullOrWhiteSpace(sourcePath) || !PathExists(sourcePath))
            {
                continue;
            }

            string normalizedPath = Path.GetFullPath(sourcePath);
            bool skipPath = false;
            for (int j = normalizedPaths.Count - 1; j >= 0; j--)
            {
                string existingPath = normalizedPaths[j];
                if (IsSamePathOrDescendant(normalizedPath, existingPath))
                {
                    skipPath = true;
                    break;
                }

                if (IsSamePathOrDescendant(existingPath, normalizedPath))
                {
                    normalizedPaths.RemoveAt(j);
                }
            }

            if (!skipPath)
            {
                normalizedPaths.Add(normalizedPath);
            }
        }

        return normalizedPaths;
    }

    private static bool PathExists(string path)
        => Directory.Exists(path) || File.Exists(path);

    private static bool IsSamePathOrDescendant(string path, string rootPath)
    {
        if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedRootPath = rootPath.EndsWith(Path.DirectorySeparatorChar) || rootPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRelativeProjectPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            throw new InvalidOperationException("Project path is not configured.");
        }

        return Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
    }

    private static List<PathMove> BuildPathMoves(IReadOnlyList<string> sourcePaths, string targetDirectoryPath)
    {
        var pathMoves = new List<PathMove>(sourcePaths.Count);
        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sourcePaths.Count; i++)
        {
            string sourcePath = sourcePaths[i];
            bool isDirectory = Directory.Exists(sourcePath);
            string destinationPath = GetUniqueDestinationPath(targetDirectoryPath, Path.GetFileName(sourcePath), isDirectory, reservedPaths);
            pathMoves.Add(new PathMove(sourcePath, destinationPath, isDirectory));
        }

        return pathMoves;
    }

    private static List<string> BuildCopyDestinationPaths(IReadOnlyList<string> sourcePaths, string targetDirectoryPath)
    {
        var destinationPaths = new List<string>(sourcePaths.Count);
        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sourcePaths.Count; i++)
        {
            string sourcePath = sourcePaths[i];
            bool isDirectory = Directory.Exists(sourcePath);
            destinationPaths.Add(GetUniqueDestinationPath(targetDirectoryPath, Path.GetFileName(sourcePath), isDirectory, reservedPaths));
        }

        return destinationPaths;
    }

    private bool TryApplyPathMoves(IReadOnlyList<PathMove> pathMoves)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                for (int i = 0; i < pathMoves.Count; i++)
                {
                    MovePathExact(pathMoves[i].SourcePath, pathMoves[i].DestinationPath, pathMoves[i].IsDirectory);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportError("Cannot move items.", ex);
            return false;
        }
    }

    private bool TryApplyPathMovesReverse(IReadOnlyList<PathMove> pathMoves)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                for (int i = pathMoves.Count - 1; i >= 0; i--)
                {
                    MovePathExact(pathMoves[i].DestinationPath, pathMoves[i].SourcePath, pathMoves[i].IsDirectory);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportError("Cannot restore moved items.", ex);
            return false;
        }
    }

    private bool TryApplyCopies(IReadOnlyList<string> sourcePaths, IReadOnlyList<string> destinationPaths)
    {
        try
        {
            using (SuspendWatcherNotifications())
            {
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    CopyPathExact(sourcePaths[i], destinationPaths[i], Directory.Exists(sourcePaths[i]));
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportError("Cannot copy items.", ex);
            return false;
        }
    }

    private static void MovePathExact(string sourcePath, string destinationPath, bool isDirectory)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (isDirectory)
        {
            Directory.Move(sourcePath, destinationPath);
            return;
        }

        File.Move(sourcePath, destinationPath);
    }

    private static void CopyPathExact(string sourcePath, string destinationPath, bool isDirectory)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (isDirectory)
        {
            CopyDirectory(sourcePath, destinationPath);
            return;
        }

        File.Copy(sourcePath, destinationPath);
    }

    private List<AssetInfo> RegisterAssetsUnderPaths(IReadOnlyList<string> createdPaths)
    {
        var addedAssetEntries = new List<AssetInfo>();
        bool changed = false;
        for (int i = 0; i < createdPaths.Count; i++)
        {
            RegisterAssetsUnderPath(createdPaths[i], addedAssetEntries, ref changed);
        }

        if (changed)
        {
            EditorAssetCatalogService.Save();
        }

        return addedAssetEntries;
    }

    private void RegisterAssetsUnderPath(string path, List<AssetInfo> addedAssetEntries, ref bool changed)
    {
        if (Directory.Exists(path))
        {
            foreach (string filePath in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                RegisterAssetFile(filePath, addedAssetEntries, ref changed);
            }

            return;
        }

        if (File.Exists(path))
        {
            RegisterAssetFile(path, addedAssetEntries, ref changed);
        }
    }

    private static void RegisterAssetFile(string filePath, List<AssetInfo> addedAssetEntries, ref bool changed)
    {
        bool catalogChanged = EditorAssetImportService.EnsureFileAssetRegistered(filePath);
        if (!catalogChanged)
        {
            return;
        }

        string relativeFilePath = GetRelativeProjectPath(filePath);
        var assetInfo = AssetCatalog.GetByFileName(relativeFilePath)
            ?? AssetCatalog.GetByFileName(relativeFilePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (assetInfo == null)
        {
            return;
        }

        addedAssetEntries.Add(CloneAssetInfo(assetInfo));
        changed = true;
    }

    private static DirectorySnapshot CaptureDirectorySnapshot(string directoryPath)
    {
        var snapshot = new DirectorySnapshot();
        if (!Directory.Exists(directoryPath))
        {
            return snapshot;
        }

        foreach (string childDirectory in Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories))
        {
            snapshot.Paths.Add(childDirectory);
        }

        foreach (string childFile in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            snapshot.Paths.Add(childFile);
        }

        if (!string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            foreach (var assetInfo in AssetCatalog.AssetInfos)
            {
                string assetFullPath = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
                if (IsSamePathOrDescendant(assetFullPath, directoryPath))
                {
                    snapshot.AssetEntriesById[assetInfo.Id] = CloneAssetInfo(assetInfo);
                }
            }
        }

        return snapshot;
    }

    private static List<string> GetCreatedRootPaths(DirectorySnapshot snapshotBefore, DirectorySnapshot snapshotAfter)
    {
        var createdPaths = new List<string>();
        foreach (string path in snapshotAfter.Paths)
        {
            if (snapshotBefore.Paths.Contains(path))
            {
                continue;
            }

            bool hasCreatedParent = false;
            for (int i = 0; i < createdPaths.Count; i++)
            {
                if (IsSamePathOrDescendant(path, createdPaths[i]))
                {
                    hasCreatedParent = true;
                    break;
                }
            }

            if (!hasCreatedParent)
            {
                createdPaths.Add(path);
            }
        }

        return createdPaths;
    }

    private static List<AssetInfo> GetAddedAssetEntries(DirectorySnapshot snapshotBefore, DirectorySnapshot snapshotAfter)
    {
        var addedAssetEntries = new List<AssetInfo>();
        foreach (var pair in snapshotAfter.AssetEntriesById)
        {
            if (!snapshotBefore.AssetEntriesById.ContainsKey(pair.Key))
            {
                addedAssetEntries.Add(CloneAssetInfo(pair.Value));
            }
        }

        return addedAssetEntries;
    }

    private static List<string> GetDestinationPaths(IReadOnlyList<PathMove> pathMoves)
    {
        var destinationPaths = new List<string>(pathMoves.Count);
        for (int i = 0; i < pathMoves.Count; i++)
        {
            destinationPaths.Add(pathMoves[i].DestinationPath);
        }

        return destinationPaths;
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

    private sealed class DirectorySnapshot
    {
        public HashSet<string> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<Guid, AssetInfo> AssetEntriesById { get; } = new();
    }

    private sealed class PathMove
    {
        public PathMove(string sourcePath, string destinationPath, bool isDirectory)
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            IsDirectory = isDirectory;
        }

        public string SourcePath { get; }

        public string DestinationPath { get; }

        public bool IsDirectory { get; }
    }
}