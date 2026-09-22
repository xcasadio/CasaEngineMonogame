using System.Runtime.CompilerServices;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// One held <see cref="AssetHandle{T}"/> per generated-asset id, scoped to the
/// <see cref="AssetContentManager"/> that produced it (P8, P11): a generator's first build of an id goes
/// through <see cref="AssetContentManager.Register{T}"/> and keeps the hold for the whole game; a later
/// call whose cached instance <paramref name="isStale"/> reports stale rebuilds it, swaps it in with
/// <see cref="AssetContentManager.Replace{T}"/> and moves the single hold onto the new instance, instead of
/// leaking one per rebuild. The stale instance is not disposed here, as <see cref="AssetContentManager.Replace{T}"/>
/// does not dispose it either: the generators' <paramref name="isStale"/> is <c>IsDisposed</c>, so it is
/// already disposed.
/// Scoping by <see cref="ConditionalWeakTable{TKey,TValue}"/> gives each <see cref="AssetContentManager"/>
/// — one per game, several coexist in tests and in the editor — its own generated ids, collected together
/// with it instead of leaking across instances.
/// </summary>
internal static class GeneratedAssetHandleCache<T> where T : class
{
    private static readonly ConditionalWeakTable<AssetContentManager, Dictionary<Guid, AssetHandle<T>>> HandlesByManager = new();

    /// <summary>Returns the cached instance for <paramref name="id"/> without rebuilding it, skipping any
    /// work the caller would otherwise do to decide whether a rebuild is needed.</summary>
    public static bool TryGetCached(AssetContentManager assetContentManager, Guid id, Func<T, bool> isStale, out T asset)
    {
        var handles = HandlesByManager.GetOrCreateValue(assetContentManager);
        if (handles.TryGetValue(id, out var existingHandle) && !isStale(existingHandle.Asset))
        {
            asset = existingHandle.Asset;
            return true;
        }

        asset = null;
        return false;
    }

    /// <summary>Returns the cached instance for <paramref name="id"/>, or builds one with
    /// <paramref name="create"/> and registers or replaces it, keeping one hold on <paramref name="id"/>
    /// for as long as the manager lives.</summary>
    public static T Acquire(AssetContentManager assetContentManager, Guid id, Func<T> create, Func<T, bool> isStale)
    {
        var handles = HandlesByManager.GetOrCreateValue(assetContentManager);

        if (handles.TryGetValue(id, out var existingHandle))
        {
            if (!isStale(existingHandle.Asset))
            {
                return existingHandle.Asset;
            }

            var replacement = create();
            assetContentManager.Replace(id, replacement);

            // Replace does not retarget existing handles: the old one keeps returning the stale instance.
            // Hold the new instance before giving the old hold back, so the id keeps exactly one hold.
            handles[id] = assetContentManager.Acquire<T>(id);
            existingHandle.Dispose();
            return replacement;
        }

        var asset = create();
        handles[id] = assetContentManager.Register(id, asset);
        return asset;
    }
}
