
using CasaEngine.Framework.Rendering.Shaders;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Renders all opaque <see cref="RenderItem"/>s (those not in the Transparent queue).
/// Items arrive pre-sorted by state (queue, shader, material, mesh) to minimise GPU
/// state changes; depth is not encoded in the sort key for opaque items.
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

            if ((item.Features & ShaderFeature.Instanced) != 0)
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

        // The sort key already encodes reversed distance (farthest first) for transparent
        // items, so ascending iteration draws back-to-front as required by alpha blending.
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Material.Queue < RenderQueue.Transparent)
            {
                continue;
            }

            if ((item.Features & ShaderFeature.Instanced) != 0)
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
