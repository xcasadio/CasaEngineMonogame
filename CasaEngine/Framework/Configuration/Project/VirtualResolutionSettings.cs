namespace CasaEngine.Framework.Configuration.Project;

/// <summary>How a virtual resolution is scaled into the window (ADR-0048, ADR-0087).</summary>
public enum VirtualResolutionMode
{
    /// <summary>
    /// Drawn straight into the window at the largest whole factor that fits, the image centered, the rest of the
    /// window black (ADR-0048).
    /// </summary>
    IntegerFit,

    /// <summary>
    /// Rendered into a target of the virtual size, then shown at the largest whole factor that fits with point
    /// sampling, the image centered, the rest of the window black (ADR-0087).
    /// </summary>
    BufferedIntegerFit,

    /// <summary>
    /// Rendered into a target of the virtual size, then shown at the largest factor, whole or not, that fits while
    /// keeping the aspect ratio, with linear sampling, the image centered, the rest of the window black (ADR-0087).
    /// </summary>
    BufferedFit,
}

/// <summary>Helpers on <see cref="VirtualResolutionMode"/>.</summary>
public static class VirtualResolutionModeExtensions
{
    /// <summary>True for the modes that render into a target of the virtual size before showing it (ADR-0087).</summary>
    public static bool IsBuffered(this VirtualResolutionMode mode)
        => mode is VirtualResolutionMode.BufferedIntegerFit or VirtualResolutionMode.BufferedFit;
}

/// <summary>
/// A fixed logical resolution the runtime fits into the window (ADR-0048). Declared by
/// <see cref="ProjectSettings.VirtualResolution"/>; a project without it keeps the full-window view.
/// </summary>
public sealed class VirtualResolutionSettings
{
    public int Width { get; set; }

    public int Height { get; set; }

    public VirtualResolutionMode Mode { get; set; } = VirtualResolutionMode.IntegerFit;

    /// <summary>
    /// True when the data-driven screens are authored at this resolution and follow its integer factor (ADR-0079).
    /// Optional in the project file, and written back only when true.
    /// </summary>
    public bool ScaleUI { get; set; }
}
