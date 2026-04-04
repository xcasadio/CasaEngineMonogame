using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering.Shaders;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Renders all opaque <see cref="RenderItem"/>s (those not in the Transparent queue).
/// Items are expected to arrive pre-sorted front-to-back (by sort key).
/// </summary>
public sealed class OpaquePass : RenderPass
{
    public OpaquePass() : base(RenderPassType.OpaquePass) { }

    public override void Execute(
        RenderContext context,
        IReadOnlyList<RenderItem> items,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        RenderShaderSelector shaderSelector)
    {
        var stats = context.Stats;

        foreach (var item in items)
        {
            // Only draw opaque/alpha-tested items
            if (item.Material.Queue >= RenderQueue.Transparent)
            {
                continue;
            }

            if (item.Mesh.VertexBuffer is null || item.Mesh.IndexBuffer is null)
            {
                continue;
            }

            if (stats is not null)
            {
                stats.OpaqueItems++;
            }

            DrawItem(in item, in context, stateCache, shaderCache, shaderSelector, stats);
        }
    }
}

/// <summary>
/// Renders all transparent <see cref="RenderItem"/>s back-to-front.
/// Items are expected to arrive pre-sorted with the farthest first.
/// </summary>
public sealed class TransparentPass : RenderPass
{
    public TransparentPass() : base(RenderPassType.TransparentPass) { }

    public override void Execute(
        RenderContext context,
        IReadOnlyList<RenderItem> items,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        RenderShaderSelector shaderSelector)
    {
        var stats = context.Stats;

        // Collect transparent items sorted back-to-front (already encoded in sort key)
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.Material.Queue < RenderQueue.Transparent)
            {
                continue;
            }

            if (item.Mesh.VertexBuffer is null || item.Mesh.IndexBuffer is null)
            {
                continue;
            }

            if (stats is not null)
            {
                stats.TransparentItems++;
            }

            DrawItem(in item, in context, stateCache, shaderCache, shaderSelector, stats);
        }
    }
}
