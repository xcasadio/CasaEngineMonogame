using System;
using System.IO;
using CasaEngine.Engine;

namespace CasaEngine.Editor.ContentBrowser.Services;

public sealed class ContentBrowserTrashEntry
{
    public ContentBrowserTrashEntry(string originalPath, string storagePath, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        OriginalPath = originalPath;
        StoragePath = storagePath;
        IsDirectory = isDirectory;
    }

    public string OriginalPath { get; }

    public string StoragePath { get; }

    public bool IsDirectory { get; }
}

public sealed class ContentBrowserTrashService
{
    private const string TrashDirectoryName = ".editor-trash";
    private const string ContentBrowserDirectoryName = "content-browser";

    public bool TryMoveToTrash(string sourcePath, out ContentBrowserTrashEntry trashEntry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        trashEntry = null!;
        if (!PathExists(sourcePath))
        {
            return false;
        }

        string storagePath = BuildUniqueStoragePath(sourcePath);
        bool isDirectory = Directory.Exists(sourcePath);
        if (!TryMove(sourcePath, storagePath, isDirectory))
        {
            return false;
        }

        trashEntry = new ContentBrowserTrashEntry(sourcePath, storagePath, isDirectory);
        return true;
    }

    public bool TryMoveToTrash(ContentBrowserTrashEntry trashEntry)
    {
        ArgumentNullException.ThrowIfNull(trashEntry);

        if (!PathExists(trashEntry.OriginalPath))
        {
            return false;
        }

        return TryMove(trashEntry.OriginalPath, trashEntry.StoragePath, trashEntry.IsDirectory);
    }

    public bool TryRestore(ContentBrowserTrashEntry trashEntry)
    {
        ArgumentNullException.ThrowIfNull(trashEntry);

        if (!PathExists(trashEntry.StoragePath))
        {
            return false;
        }

        string? parentDirectory = Path.GetDirectoryName(trashEntry.OriginalPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        if (!TryMove(trashEntry.StoragePath, trashEntry.OriginalPath, trashEntry.IsDirectory))
        {
            return false;
        }

        DeleteEmptyTrashDirectories(Path.GetDirectoryName(trashEntry.StoragePath));
        return true;
    }

    private static bool PathExists(string path)
        => Directory.Exists(path) || File.Exists(path);

    private static bool TryMove(string sourcePath, string destinationPath, bool isDirectory)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (isDirectory)
        {
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }

            Directory.Move(sourcePath, destinationPath);
            return true;
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(sourcePath, destinationPath);
        return true;
    }

    private static string BuildUniqueStoragePath(string sourcePath)
    {
        string leafName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.Combine(GetTrashRootPath(), Guid.NewGuid().ToString("N"), leafName);
    }

    private static string GetTrashRootPath()
    {
        if (string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            throw new InvalidOperationException("Project path is not configured.");
        }

        return Path.Combine(EngineEnvironment.ProjectPath, TrashDirectoryName, ContentBrowserDirectoryName);
    }

    private static void DeleteEmptyTrashDirectories(string? directoryPath)
    {
        string trashRootPath = GetTrashRootPath();
        while (!string.IsNullOrWhiteSpace(directoryPath)
               && directoryPath.StartsWith(trashRootPath, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(directoryPath, trashRootPath, StringComparison.OrdinalIgnoreCase)
               && Directory.Exists(directoryPath)
               && Directory.GetFiles(directoryPath).Length == 0
               && Directory.GetDirectories(directoryPath).Length == 0)
        {
            string? parentDirectory = Path.GetDirectoryName(directoryPath);
            Directory.Delete(directoryPath);
            directoryPath = parentDirectory;
        }
    }
}