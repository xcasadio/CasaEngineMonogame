namespace CasaEngine.Editor.ContentBrowser;

public enum ContentViewMode
{
    Grid,
    Detail,
}

public sealed class ContentBrowserConfig
{
    public string? RootDirectory { get; set; }

    public string[] ExcludedExtensions { get; set; } = { ".meta", ".tmp" };

    public string[] ExcludedDirectories { get; set; } = { "bin", "obj", ".git" };

    public int ThumbnailSize { get; set; } = 96;

    public ContentViewMode DefaultViewMode { get; set; } = ContentViewMode.Grid;

    public bool ShowHiddenFiles { get; set; }
}