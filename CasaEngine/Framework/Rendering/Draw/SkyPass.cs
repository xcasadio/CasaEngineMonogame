using CasaEngine.Framework.Rendering.Shaders;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Reserved pass for background sky or environment rendering.
/// The initial implementation is intentionally a no-op until the dedicated sky renderer is wired in.
/// </summary>
public sealed class SkyPass : RenderPass
{
    public SkyPass() : base(RenderPassType.SkyPass)
    {
    }

    public override void Execute(
        RenderContext context,
        IReadOnlyList<RenderItem> items,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        RenderShaderSelector shaderSelector)
    {
        // Intentionally empty in ENV-005.
        // ENV-006 and ENV-007 connect the actual cubemap sky renderer here.
    }
}