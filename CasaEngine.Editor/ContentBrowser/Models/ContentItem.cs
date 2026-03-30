using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.ContentBrowser.Models;

/// <summary>
/// Represents a file or folder in the Content Browser.
/// This is a lightweight UI model that wraps real file-system entries.
/// </summary>
public sealed class ContentItem : INotifyPropertyChanged
{
    private string _name;
    private string _fullPath;
    private string _extension;
    private ContentItemType _type;
    private long _size;
    private DateTime _lastModified;
    private ContentItem _parent;
    private Texture2D _icon;
    private Texture2D _thumbnail;

    // ──────────────────────────────────────────────────────────────────────
    //  Properties
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Display name (file/folder name without path, but with extension for files).</summary>
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    /// <summary>Full absolute path on disk.</summary>
    public string FullPath
    {
        get => _fullPath;
        private set => SetField(ref _fullPath, value);
    }

    /// <summary>File extension including dot, or empty for directories.</summary>
    public string Extension
    {
        get => _extension;
        private set => SetField(ref _extension, value);
    }

    /// <summary>True when this item represents a directory.</summary>
    public bool IsDirectory { get; }

    /// <summary>The type of asset deduced from <see cref="Extension"/>.</summary>
    public ContentItemType Type
    {
        get => _type;
        private set => SetField(ref _type, value);
    }

    /// <summary>Optional icon chosen by the editor for this item.</summary>
    public Texture2D Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }

    /// <summary>Optional thumbnail preview chosen by the editor for this item.</summary>
    public Texture2D Thumbnail
    {
        get => _thumbnail;
        set => SetField(ref _thumbnail, value);
    }

    /// <summary>File size in bytes (0 for directories).</summary>
    public long Size
    {
        get => _size;
        set => SetField(ref _size, value);
    }

    /// <summary>Last write time on disk.</summary>
    public DateTime LastModified
    {
        get => _lastModified;
        set => SetField(ref _lastModified, value);
    }

    /// <summary>Parent item (null for the root).</summary>
    public ContentItem Parent
    {
        get => _parent;
        set => SetField(ref _parent, value);
    }

    /// <summary>Children (sub-folders and files). Empty for files.</summary>
    public ObservableCollection<ContentItem> Children { get; } = new();

    public event PropertyChangedEventHandler PropertyChanged;

    // ──────────────────────────────────────────────────────────────────────
    //  Construction
    // ──────────────────────────────────────────────────────────────────────

    public ContentItem(string fullPath, bool isDirectory, ContentItem? parent = null)
    {
        IsDirectory = isDirectory;
        Parent = parent;

        UpdatePath(fullPath);
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

    public void UpdatePath(string newFullPath)
    {
        FullPath = newFullPath;
        Name = Path.GetFileName(newFullPath);
        Extension = IsDirectory ? string.Empty : Path.GetExtension(newFullPath);
        Type = IsDirectory ? ContentItemType.Folder : DeduceType(Extension);
    }

    public void RefreshFileSystemMetadata()
    {
        if (IsDirectory)
        {
            if (Directory.Exists(FullPath))
            {
                var info = new DirectoryInfo(FullPath);
                LastModified = info.LastWriteTime;
            }

            Size = 0;
            return;
        }

        if (!File.Exists(FullPath))
        {
            Size = 0;
            return;
        }

        var fileInfo = new FileInfo(FullPath);
        Size = fileInfo.Length;
        LastModified = fileInfo.LastWriteTime;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
        { ".model", ContentItemType.Model },
        { ".staticModel", ContentItemType.Model },
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
