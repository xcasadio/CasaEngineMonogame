namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Controls how often a <see cref="RenderView"/> is re-rendered each frame.
/// </summary>
public enum ViewUpdateMode
{
    /// <summary>Re-rendered every frame (default behaviour).</summary>
    RealTime,

    /// <summary>
    /// Re-rendered only when <see cref="RenderView.Invalidate"/> is called.
    /// The previous frame result is kept until then.
    /// Suitable for static or rarely-changing previews (e.g. inspector thumbnails).
    /// </summary>
    OnDemand,

    /// <summary>
    /// Re-rendered at the fixed rate defined by <see cref="RenderView.TargetFrameRate"/>.
    /// Suitable for low-priority views such as mini-maps (e.g. 10 fps).
    /// </summary>
    Throttled,
}
