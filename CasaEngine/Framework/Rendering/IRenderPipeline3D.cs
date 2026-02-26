using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Defines the interface for a 3-D render pipeline.
/// A pipeline arranges <see cref="RenderPass"/> instances in order and dispatches
/// <see cref="RenderItem"/>s to each pass.
///
/// The interface is intentionally minimal: higher-level systems (shadow maps, post-process)
/// can implement it without being forced into a specific pass structure.
/// </summary>
public interface IRenderPipeline3D
{
    /// <summary>Called once after the <see cref="GraphicsDevice"/> is ready.</summary>
    void Initialize(GraphicsDevice device);

    /// <summary>
    /// Renders <paramref name="items"/> using the passes registered in this pipeline.
    /// </summary>
    void Render(
        RenderContext context,
        IReadOnlyList<RenderItem> items,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        ShaderWrapper legacyShader);
}
