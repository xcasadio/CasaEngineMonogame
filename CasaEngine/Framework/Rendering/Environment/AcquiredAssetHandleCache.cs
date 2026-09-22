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
/// </summary>
internal static class AcquiredAssetHandleCache<T> where T : class
{
    private static readonly ConditionalWeakTable<AssetContentManager, Dictionary<Guid, AssetHandle<T>>> HandlesByManager = new();

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

    public static T Acquire(AssetContentManager assetContentManager, Guid id)
    {
        var handles = HandlesByManager.GetOrCreateValue(assetContentManager);
        var handle = assetContentManager.Acquire<T>(id);
        handles[id] = handle;
        return handle.Asset;
    }
}
