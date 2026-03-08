using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;

namespace CasaEngine.Editor.ContentBrowser.Services;

/// <summary>
/// Scans the file system and builds a <see cref="ContentItem"/> tree.
/// </summary>
public static class FileSystemScanner
{
    // Folders and extensions to skip (hidden / build artefacts)
    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".idea"
    };

    // ──────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans <paramref name="rootPath"/> recursively and returns the root
    /// <see cref="ContentItem"/> whose <see cref="ContentItem.Children"/>
    /// contain the full directory tree.
    /// </summary>
    public static ContentItem ScanDirectory(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Root directory not found: {rootPath}");

        var root = new ContentItem(rootPath, isDirectory: true);
        PopulateChildren(root);
        return root;
    }

    /// <summary>
    /// Re-scans a single directory (non-recursive) and updates its
    /// <see cref="ContentItem.Children"/> in place.  Useful after an
    /// external file change.
    /// </summary>
    public static void Refresh(ContentItem directory)
    {
        if (directory == null || !directory.IsDirectory)
            return;

        directory.Children.Clear();
        PopulateChildren(directory);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────────────

    private static void PopulateChildren(ContentItem parent)
    {
        var dirInfo = new DirectoryInfo(parent.FullPath);

        // Sub-directories first (sorted alphabetically)
        try
        {
            foreach (var sub in dirInfo.EnumerateDirectories())
            {
                if (IgnoredFolders.Contains(sub.Name))
                    continue;
                if (sub.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                var child = new ContentItem(sub.FullName, isDirectory: true, parent);
                child.LastModified = sub.LastWriteTime;
                parent.Children.Add(child);

                // Recurse
                PopulateChildren(child);
            }
        }
        catch (UnauthorizedAccessException) { /* skip protected folders */ }

        // Files (sorted alphabetically)
        try
        {
            foreach (var file in dirInfo.EnumerateFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                var child = new ContentItem(file.FullName, isDirectory: false, parent);
                child.Size = file.Length;
                child.LastModified = file.LastWriteTime;
                parent.Children.Add(child);
            }
        }
        catch (UnauthorizedAccessException) { /* skip protected files */ }
    }
}
