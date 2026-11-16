using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// The eviction rule of the HDR scene targets (ADR-0105) and of the bloom targets (ADR-0108): a target key (width, height,
/// surface format, depth format) is released once no view has rendered with it for
/// more than <see cref="MaxIdleRenders"/> renders. The rule itself is pure, so it is tested without a GPU.
/// </summary>
internal static class HdrTargetEviction
{
    /// <summary>
    /// Number of consecutive <c>RenderPipeline.Render</c> calls a size may stay unused before its target is disposed. It is
    /// above one so that a view throttled to a lower rate keeps its target between its renders.
    /// </summary>
    public const int MaxIdleRenders = 4;

    /// <summary>True when a size last used at render <paramref name="lastUsedRender"/> is stale at <paramref name="currentRender"/>.</summary>
    public static bool IsStale(long lastUsedRender, long currentRender, int maxIdleRenders = MaxIdleRenders)
    {
        return currentRender - lastUsedRender > maxIdleRenders;
    }

    /// <summary>
    /// Adds to <paramref name="stale"/> every key of <paramref name="lastUsed"/> that is stale at
    /// <paramref name="currentRender"/>. Does not modify <paramref name="lastUsed"/> (the caller removes the keys afterwards,
    /// a dictionary cannot change while it is enumerated) and does not allocate when <paramref name="stale"/> has room.
    /// </summary>
    public static void CollectStale<TKey>(
        Dictionary<TKey, long> lastUsed,
        long currentRender,
        List<TKey> stale,
        int maxIdleRenders = MaxIdleRenders)
    {
        foreach (var entry in lastUsed)
        {
            if (IsStale(entry.Value, currentRender, maxIdleRenders))
            {
                stale.Add(entry.Key);
            }
        }
    }

    /// <summary>
    /// Sweeps the tracked target keys once per render: removes from <paramref name="lastUsed"/> every key stale at
    /// <paramref name="currentRender"/> and disposes the free targets of that exact key (size, format and depth format) in
    /// <paramref name="pool"/>. The keys whose disposal freed at least one target are added to <paramref name="released"/>
    /// with the number disposed. A key without free target (still acquired) costs nothing. <paramref name="staleScratch"/> is a
    /// reused list: nothing is allocated when it has room.
    /// </summary>
    public static void Sweep(
        Dictionary<(int, int, SurfaceFormat, DepthFormat), long> lastUsed,
        long currentRender,
        List<(int, int, SurfaceFormat, DepthFormat)> staleScratch,
        List<((int, int, SurfaceFormat, DepthFormat) Key, int Disposed)> released,
        RenderTargetPool pool,
        int maxIdleRenders = MaxIdleRenders)
    {
        staleScratch.Clear();
        CollectStale(lastUsed, currentRender, staleScratch, maxIdleRenders);

        for (int i = 0; i < staleScratch.Count; i++)
        {
            var key = staleScratch[i];
            lastUsed.Remove(key);
            int disposed = pool.TrimFree(key.Item1, key.Item2, key.Item3, key.Item4);
            if (disposed > 0)
            {
                released.Add((key, disposed));
            }
        }
    }
}
