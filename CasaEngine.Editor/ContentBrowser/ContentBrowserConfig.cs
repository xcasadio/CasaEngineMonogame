using System.Collections.Generic;
using CasaEngine.Editor.ContentBrowser.Models;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.ContentBrowser;

public enum ContentViewMode
{
    Grid,
    Detail,
}

public sealed class ContentBrowserConfig
{
    public string RootDirectory { get; set; }

    public string[] ExcludedExtensions { get; set; } = { ".meta", ".tmp" };

    public string[] ExcludedDirectories { get; set; } = { "bin", "obj", ".git", ".casaeditor", ".vs" };

    public int ThumbnailSize { get; set; } = 96;

    public ContentViewMode DefaultViewMode { get; set; } = ContentViewMode.Grid;

    public bool ShowHiddenFiles { get; set; }

    public Dictionary<ContentItemType, Texture2D> CustomIcons { get; } = new();
}