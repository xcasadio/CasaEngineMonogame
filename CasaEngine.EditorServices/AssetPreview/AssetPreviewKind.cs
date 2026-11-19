namespace CasaEngine.EditorServices.AssetPreview;

/// <summary>How the editor previews a file that has no editing document (plan P6).</summary>
public enum AssetPreviewKind
{
    /// <summary>No preview: the file is missing, or its extension is an engine asset format that gets its own document.</summary>
    None,

    /// <summary>A raster image: png, jpg, jpeg, bmp, gif or tga.</summary>
    Image,

    /// <summary>A DDS file, shown as a cube map when its header says so.</summary>
    CubeMap,

    /// <summary>A wav or ogg audio file.</summary>
    Audio,

    /// <summary>A BMFont text descriptor (fnt).</summary>
    BitmapFont,

    /// <summary>A glTF or glb model.</summary>
    Model,

    /// <summary>Any other file whose first bytes decode as UTF-8 without NUL bytes.</summary>
    Text,

    /// <summary>Any other file.</summary>
    Binary,
}
