
using CasaEngine.Framework.Rendering.Shaders;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Renders all opaque <see cref="RenderItem"/>s (those not in the Transparent queue).
/// Items arrive pre-sorted by state (queue, shader, material, mesh) to minimise GPU
/// state changes; depth is not encoded in the sort key for opaque items.
/// Items must arrive sorted ascending by SortKey (as produced by SortKeyGenerator).
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

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            // The list is sorted by key, so once we reach a transparent/overlay item,
            // every remaining item is also transparent/overlay: stop scanning.
            if (item.Queue >= RenderQueue.Transparent)
            {
                break;
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
/// Items must arrive sorted ascending by SortKey (as produced by SortKeyGenerator).
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
        // Since the list is sorted by key, all transparent/overlay items form a contiguous
        // tail: locate its start with a binary search instead of scanning from the front.
        int startIndex = LowerBound(items, SortKeyGenerator.MinKeyFor(RenderQueue.Transparent));

        for (int i = startIndex; i < items.Count; i++)
        {
            var item = items[i];

            // Everything at/after the lower bound is transparent/overlay by construction,
            // no per-item queue filter needed.
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

    /// <summary>
    /// Returns the first index whose SortKey is greater than or equal to
    /// <paramref name="key"/>, or <c>items.Count</c> if none satisfy that.
    /// </summary>
    private static int LowerBound(IReadOnlyList<RenderItem> items, ulong key)
    {
        int low = 0;
        int high = items.Count;

        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (items[mid].SortKey < key)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
