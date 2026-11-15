namespace CasaEngine.Framework.Rendering;

/// <summary>
/// The eviction rule of the HDR scene targets (ADR-0096): a target size is released once no view has rendered with it for
/// more than <see cref="MaxIdleRenders"/> renders. Pure, so it is tested without a GPU.
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
}
