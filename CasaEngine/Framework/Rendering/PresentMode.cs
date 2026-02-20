namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Controls how a view's rendered image is presented in its destination rectangle.
/// Used by <see cref="BackBufferPresenter"/> for letterboxing and scaling.
/// </summary>
public enum PresentMode
{
    /// <summary>Stretch to fill the destination, changing aspect ratio if needed.</summary>
    Stretch,

    /// <summary>
    /// Scale uniformly to fit inside the destination while preserving aspect ratio.
    /// Black bars (letterbox/pillarbox) fill the unused area.
    /// </summary>
    Fit,

    /// <summary>
    /// Scale uniformly to fill the destination while preserving aspect ratio.
    /// The image may be cropped on the edges.
    /// </summary>
    Fill,

    /// <summary>
    /// One rendered pixel maps to exactly one screen pixel, centered in
    /// the destination rectangle.
    /// </summary>
    PixelPerfect,
}
