namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Margins, in back-buffer pixels, that <see cref="ViewManager"/> keeps free of back-buffer views (ADR-0070): the
/// automatic layout, the default view and the single resized view all fit inside the window minus these margins, so a
/// window-level UI can sit beside the scene instead of over it.
/// </summary>
public readonly record struct ViewLayoutInsets(int Left, int Top, int Right, int Bottom)
{
    /// <summary>No margin: the views cover the whole window, as they always did.</summary>
    public static ViewLayoutInsets Zero => default;
}
