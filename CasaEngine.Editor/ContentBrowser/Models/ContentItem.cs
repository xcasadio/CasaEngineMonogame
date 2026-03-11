using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace CasaEngine.Editor.ContentBrowser.Models;

/// <summary>
/// Represents a file or folder in the Content Browser.
/// This is a lightweight UI model that wraps real file-system entries.
/// </summary>
public sealed class ContentItem
{
    // ──────────────────────────────────────────────────────────────────────
    //  Properties
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Display name (file/folder name without path, but with extension for files).</summary>
    public string Name { get; set; }

    /// <summary>Full absolute path on disk.</summary>
    public string FullPath { get; }

    /// <summary>File extension including dot, or empty for directories.</summary>
    public string Extension { get; }

    /// <summary>True when this item represents a directory.</summary>
    public bool IsDirectory { get; }

    /// <summary>The type of asset deduced from <see cref="Extension"/>.</summary>
    public ContentItemType Type { get; }

    /// <summary>File size in bytes (0 for directories).</summary>
    public long Size { get; set; }

    /// <summary>Last write time on disk.</summary>
    public DateTime LastModified { get; set; }

    /// <summary>Parent item (null for the root).</summary>
    public ContentItem? Parent { get; set; }

    /// <summary>Children (sub-folders and files). Empty for files.</summary>
    public ObservableCollection<ContentItem> Children { get; } = new();

    // ──────────────────────────────────────────────────────────────────────
    //  Construction
    // ──────────────────────────────────────────────────────────────────────

    public ContentItem(string fullPath, bool isDirectory, ContentItem? parent = null)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Parent = parent;

        Name = Path.GetFileName(fullPath);
        Extension = isDirectory ? string.Empty : Path.GetExtension(fullPath);
        Type = isDirectory ? ContentItemType.Folder : DeduceType(Extension);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Returns all child items that are directories.</summary>
    public IEnumerable<ContentItem> SubFolders
    {
        get
        {
            foreach (var c in Children)
                if (c.IsDirectory)
                {
                    yield return c;
                }
        }
    }

    /// <summary>Returns all child items that are files.</summary>
    public IEnumerable<ContentItem> Files
    {
        get
        {
            foreach (var c in Children)
                if (!c.IsDirectory)
                {
                    yield return c;
                }
        }
    }

    /// <summary>Returns the path relative to a given root folder.</summary>
    public string GetRelativePath(string rootPath)
    {
        if (FullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            var relative = FullPath[rootPath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(relative) ? Name : relative;
        }
        return Name;
    }

    public override string ToString() => Name;

    // ──────────────────────────────────────────────────────────────────────
    //  Extension → ContentItemType mapping
    // ──────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, ContentItemType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Textures
        { ".png",   ContentItemType.Texture },
        { ".jpg",   ContentItemType.Texture },
        { ".jpeg",  ContentItemType.Texture },
        { ".bmp",   ContentItemType.Texture },
        { ".tga",   ContentItemType.Texture },
        { ".dds",   ContentItemType.Texture },
        // Models
        { ".fbx",   ContentItemType.Model },
        { ".obj",   ContentItemType.Model },
        { ".gltf",  ContentItemType.Model },
        { ".glb",   ContentItemType.Model },
        { ".dae",   ContentItemType.Model },
        { ".x",     ContentItemType.Model },
        // Sound
        { ".wav",   ContentItemType.Sound },
        { ".mp3",   ContentItemType.Sound },
        { ".ogg",   ContentItemType.Sound },
        // Scripts
        { ".cs",    ContentItemType.Script },
        // Shaders
        { ".fx",    ContentItemType.Shader },
        { ".hlsl",  ContentItemType.Shader },
        { ".fxh",   ContentItemType.Shader },
        // Fonts
        { ".spritefont", ContentItemType.Font },
        { ".ttf",   ContentItemType.Font },
        { ".otf",   ContentItemType.Font },
        // Scenes / Worlds
        { ".world", ContentItemType.World },
        { ".scene", ContentItemType.Scene },
        // Materials
        { ".material", ContentItemType.Material },
        // Prefabs / Entities
        { ".entity", ContentItemType.Prefab },
        { ".prefab", ContentItemType.Prefab },
        // Animations
        { ".anim",       ContentItemType.Animation },
        { ".animation",  ContentItemType.Animation },
        { ".sprite",     ContentItemType.Animation },
    };

    private static ContentItemType DeduceType(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return ContentItemType.Unknown;
        }

        return ExtensionMap.TryGetValue(extension, out var type) ? type : ContentItemType.Unknown;
    }
}
