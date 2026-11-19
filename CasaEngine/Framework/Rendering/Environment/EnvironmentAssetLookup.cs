using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

internal static class EnvironmentAssetLookup
{
    public static EnvironmentAsset TryLoadEnvironmentAsset(RenderView view, Guid assetId)
        => TryLoadAsset<EnvironmentAsset>(view, assetId);

    public static XnaTextureCube TryLoadTextureCube(RenderView view, Guid assetId)
        => TryLoadAsset<XnaTextureCube>(view, assetId);

    private static T TryLoadAsset<T>(RenderView view, Guid assetId) where T : class
    {
        if (assetId == Guid.Empty)
        {
            return null;
        }

        var assetContentManager = view.World.Game.AssetContentManager;
        if (AcquiredAssetHandleCache<T>.TryGetCached(assetContentManager, assetId, out var cachedAsset))
        {
            return cachedAsset;
        }

        if (AcquiredAssetHandleCache<T>.HasFailed(assetContentManager, assetId))
        {
            return null;
        }

        if (AssetCatalog.Get(assetId) is null)
        {
            return null;
        }

        try
        {
            // Acquired once and held for the whole game (P11): a resolution's cache miss (settings
            // changed, clear color changed...) must not re-acquire an id it already holds.
            return AcquiredAssetHandleCache<T>.Acquire(assetContentManager, assetId);
        }
        catch (Exception exception)
        {
            // Logged once: the id is not tried again for this asset content manager (the lookup runs every frame for a probe).
            AcquiredAssetHandleCache<T>.RememberFailure(assetContentManager, assetId);
            Logs.WriteException(exception);
            return null;
        }
    }
}