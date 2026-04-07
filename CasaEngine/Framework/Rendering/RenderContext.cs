using CasaEngine.Framework.Rendering.Environment;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Aggregates all per-frame rendering data passed down to materials, passes and draw calls.
/// </summary>
public struct RenderContext
{
    /// <summary>The active graphics device.</summary>
    public Microsoft.Xna.Framework.Graphics.GraphicsDevice Device;

    /// <summary>Current game time.</summary>
    public Microsoft.Xna.Framework.GameTime GameTime;

    /// <summary>Camera matrices and viewport for the current view.</summary>
    public RenderFrame Frame;

    /// <summary>Lighting data (directional lights, ambient). Null until Phase 5.</summary>
    public LightingContext? Lighting;

    /// <summary>Effective environment data resolved for the current view.</summary>
    public ResolvedEnvironmentSettings Environment;

    /// <summary>Per-frame rendering statistics (draw calls, binds, etc.).</summary>
    public RenderStats? Stats;
}
