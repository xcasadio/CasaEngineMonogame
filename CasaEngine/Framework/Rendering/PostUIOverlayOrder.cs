namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Named orders of <see cref="RenderView.RegisterPostUIOverlay"/>: lower draws first, then
/// registration order (E19.TR-4a, D-TR4A-4).
/// </summary>
public static class PostUIOverlayOrder
{
    /// <summary>The frozen frame is always below the screen effect veil.</summary>
    public const int FrozenFrame = -100;

    /// <summary>The order of every overlay registered without one.</summary>
    public const int Default = 0;
}
