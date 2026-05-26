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
        var cachedAsset = assetContentManager.GetAsset<T>(assetId);
        if (cachedAsset is not null)
        {
            return cachedAsset;
        }

        if (AssetCatalog.Get(assetId) is null)
        {
            return null;
        }

        try
        {
            return assetContentManager.Load<T>(assetId);
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            return null;
        }
    }
}