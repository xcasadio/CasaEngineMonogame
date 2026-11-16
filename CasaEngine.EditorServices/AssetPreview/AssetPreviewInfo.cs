namespace CasaEngine.EditorServices.AssetPreview;

/// <summary>One label/value line of the information shown above a preview.</summary>
/// <param name="Label">What is described (for example <c>Width</c>).</param>
/// <param name="Value">The value, as text.</param>
public readonly record struct AssetPreviewLine(string Label, string Value);

/// <summary>What the editor shows for a previewed file: kind, information lines and the data a preview area needs.</summary>
public sealed class AssetPreviewInfo
{
    private readonly List<AssetPreviewLine> _lines = new();
    private readonly List<string> _pageFiles = new();

    /// <summary>How the file is previewed.</summary>
    public AssetPreviewKind Kind { get; init; }

    /// <summary>Full path of the file.</summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>File name with its extension.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Lower-case extension with its dot, or an empty string.</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>File length in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>The information lines, file size and extension first.</summary>
    public IReadOnlyList<AssetPreviewLine> Lines => _lines;

    /// <summary>Image or cube map width in pixels, 0 when unknown (a jpeg may not report it).</summary>
    public int Width { get; internal set; }

    /// <summary>Image or cube map height in pixels, 0 when unknown.</summary>
    public int Height { get; internal set; }

    /// <summary>True for a DDS file whose header declares a cube map.</summary>
    public bool IsCubeMap { get; internal set; }

    /// <summary>Page file names of a bitmap font, as written in the descriptor (relative to the descriptor).</summary>
    public IReadOnlyList<string> PageFiles => _pageFiles;

    /// <summary>The first lines of a text file, joined with line feeds.</summary>
    public string TextPreview { get; internal set; } = string.Empty;

    /// <summary>Side n of a binary file whose size is n * n bytes (n from 2 to 8192), 0 otherwise.</summary>
    public int SquareMapSide { get; internal set; }

    /// <summary>Why the header could not be read, or null.</summary>
    public string Problem { get; internal set; }

    internal void AddLine(string label, string value) => _lines.Add(new AssetPreviewLine(label, value));

    internal void AddPageFile(string pageFile) => _pageFiles.Add(pageFile);
}
