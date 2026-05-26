using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Renders the background sky or environment before scene geometry.
/// </summary>
public sealed class SkyPass : RenderPass
{
    public SkyCubemapRenderer Renderer { get; set; }

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
        if (Renderer is null || !Renderer.CanDraw(context.Environment))
        {
            return;
        }

        Renderer.Draw(in context);
    }
}