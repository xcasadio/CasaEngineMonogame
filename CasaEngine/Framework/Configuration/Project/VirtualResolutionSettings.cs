namespace CasaEngine.Framework.Configuration.Project;

/// <summary>How a virtual resolution is scaled into the window (ADR-0048).</summary>
public enum VirtualResolutionMode
{
    /// <summary>The largest whole factor that fits, the image centered, the rest of the window black.</summary>
    IntegerFit,
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
}
