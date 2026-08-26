using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Audio;

/// <summary>
/// <see cref="IAudioClipProvider"/> backed by the engine asset pipeline: clips go through the
/// normal catalogue lookup, per-category cache and disposal on Unload.
/// </summary>
public sealed class AssetContentManagerAudioClipProvider : IAudioClipProvider
{
    private readonly AssetContentManager _assetContentManager;
    private readonly AudioLogThrottle _log = new();

    public AssetContentManagerAudioClipProvider(AssetContentManager assetContentManager)
    {
        _assetContentManager = assetContentManager ?? throw new ArgumentNullException(nameof(assetContentManager));
    }

    public IAudioClip GetClip(Guid audioFileAssetId)
    {
        if (audioFileAssetId == Guid.Empty)
        {
            return null;
        }

        try
        {
            return _assetContentManager.Load<IAudioClip>(audioFileAssetId);
        }
        catch (Exception exception)
        {
            _log.WriteError($"Audio: cannot load audio file asset '{audioFileAssetId}'. {exception.Message}");
            return null;
        }
    }

    public Stream OpenStream(Guid audioFileAssetId)
    {
        if (audioFileAssetId == Guid.Empty)
        {
            return null;
        }

        try
        {
            var assetInfo = ResolveAssetInfo(audioFileAssetId);
            if (assetInfo == null)
            {
                _log.WriteError($"Audio: audio file asset '{audioFileAssetId}' is not in the catalogue.");
                return null;
            }

            var fullPath = _assetContentManager.ResolveAssetFullPath(assetInfo.FileName);
            if (!File.Exists(fullPath))
            {
                _log.WriteError($"Audio: audio file '{fullPath}' does not exist.");
                return null;
            }

            return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception exception)
        {
            _log.WriteError($"Audio: cannot open audio file asset '{audioFileAssetId}'. {exception.Message}");
            return null;
        }
    }

    private AssetInfo ResolveAssetInfo(Guid audioFileAssetId)
    {
        var runtimeResolver = _assetContentManager.RuntimeContext?.ResolveAssetInfo;
        return runtimeResolver != null
            ? runtimeResolver(audioFileAssetId)
            : AssetCatalog.Get(audioFileAssetId);
    }
}
