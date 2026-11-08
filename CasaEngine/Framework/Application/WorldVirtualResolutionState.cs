using CasaEngine.Framework.Configuration.Project;

namespace CasaEngine.Framework.Application;

/// <summary>
/// The virtual resolution in force for the loaded world (ADR-0087): the world's own declaration when it has one,
/// composed with the project's <c>ScaleUI</c>, otherwise the project's setting. The composition is built once per world
/// load, so reading it every frame allocates nothing.
/// </summary>
internal sealed class WorldVirtualResolutionState
{
    private VirtualResolutionSettings _composed;

    /// <summary>Called when a world is loaded, before its views are built. A null block falls back to the project.</summary>
    public void UseWorld(VirtualResolutionSettings project, VirtualResolutionSettings worldBlock)
        => _composed = worldBlock == null ? null : Compose(project, worldBlock);

    /// <summary>The resolution the views and screens use, or null under external view management (the editor).</summary>
    public VirtualResolutionSettings Active(VirtualResolutionSettings project, bool externalViewManagement)
        => externalViewManagement ? null : _composed ?? project;

    /// <summary>A world block keeps its size and mode and takes <c>ScaleUI</c> from the project, false without one.</summary>
    public static VirtualResolutionSettings Compose(VirtualResolutionSettings project, VirtualResolutionSettings worldBlock)
        => new()
        {
            Width = worldBlock.Width,
            Height = worldBlock.Height,
            Mode = worldBlock.Mode,
            ScaleUI = project?.ScaleUI ?? false,
        };
}
