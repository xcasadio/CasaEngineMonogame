using System.Runtime.CompilerServices;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// One held <see cref="AssetHandle{T}"/> per catalog asset id, scoped to the
/// <see cref="AssetContentManager"/> that resolved it (P11): the first resolution of an id acquires it
/// through <see cref="AssetContentManager.Acquire{T}"/> and keeps the hold for the whole game; a later
/// resolution of the same id, even after the view's resolved-environment cache misses for another
/// reason, does not acquire again. Scoping by <see cref="ConditionalWeakTable{TKey,TValue}"/> gives each
/// <see cref="AssetContentManager"/> — one per game, several coexist in tests and in the editor — its own
/// held ids, collected together with it instead of leaking across instances.
/// An id whose acquisition threw is remembered too, per manager, next to the handles: a reflection probe resolves its cube on
/// every frame, so a failure must not be tried and logged again on each of them. Only the failure of an acquisition is remembered,
/// never an id that is absent from the catalog (it can be catalogued later).
/// </summary>
internal static class AcquiredAssetHandleCache<T> where T : class
{
    private static readonly ConditionalWeakTable<AssetContentManager, Dictionary<Guid, AssetHandle<T>>> HandlesByManager = new();
    private static readonly ConditionalWeakTable<AssetContentManager, HashSet<Guid>> FailedIdsByManager = new();

    public static bool TryGetCached(AssetContentManager assetContentManager, Guid id, out T asset)
    {
        var handles = HandlesByManager.GetOrCreateValue(assetContentManager);
        if (handles.TryGetValue(id, out var existingHandle))
        {
            asset = existingHandle.Asset;
            return true;
        }

        asset = null;
        return false;
    }

    public static bool HasFailed(AssetContentManager assetContentManager, Guid id)
        => FailedIdsByManager.TryGetValue(assetContentManager, out var failedIds) && failedIds.Contains(id);

    public static void RememberFailure(AssetContentManager assetContentManager, Guid id)
        => FailedIdsByManager.GetOrCreateValue(assetContentManager).Add(id);

    public static T Acquire(AssetContentManager assetContentManager, Guid id)
    {
        var handles = HandlesByManager.GetOrCreateValue(assetContentManager);
        var handle = assetContentManager.Acquire<T>(id);
        handles[id] = handle;
        return handle.Asset;
    }
}
