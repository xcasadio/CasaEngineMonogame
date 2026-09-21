using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Audio;

/// <summary>
/// <see cref="IAudioClipProvider"/> backed by the engine asset pipeline: clips go through the normal
/// catalogue lookup. Each distinct clip is acquired once and held through a handle for the lifetime of
/// this provider (ADR-0037, P2), as clips were pinned for the whole game before handles existed; a later
/// pass may free them per map (plan's P2 note). <see cref="Dispose"/> gives every held clip back.
/// </summary>
public sealed class AssetContentManagerAudioClipProvider : IAudioClipProvider, IDisposable
{
    private readonly AssetContentManager _assetContentManager;
    private readonly AudioLogThrottle _log = new();
    private readonly Dictionary<Guid, AssetHandle<IAudioClip>> _clipHandles = new();

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

        if (_clipHandles.TryGetValue(audioFileAssetId, out var existingHandle))
        {
            return existingHandle.Asset;
        }

        try
        {
            var clipHandle = _assetContentManager.Acquire<IAudioClip>(audioFileAssetId);
            _clipHandles.Add(audioFileAssetId, clipHandle);
            return clipHandle.Asset;
        }
        catch (Exception exception)
        {
            _log.WriteError($"Audio: cannot load audio file asset '{audioFileAssetId}'. {exception.Message}");
            return null;
        }
    }

    /// <summary>Gives back every clip handle held by this provider.</summary>
    public void Dispose()
    {
        foreach (var clipHandle in _clipHandles.Values)
        {
            clipHandle.Dispose();
        }

        _clipHandles.Clear();
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
