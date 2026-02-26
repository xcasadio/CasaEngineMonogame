using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Caches compiled <see cref="ShaderWrapper"/> instances keyed by their asset Guid.
/// Use <see cref="GetShader"/> to obtain a wrapper that is loaded on first access
/// and reused on subsequent calls.
/// </summary>
public sealed class ShaderManager : IDisposable
{
    // -----------------------------------------------------------------------
    //  Fields
    // -----------------------------------------------------------------------

    private readonly AssetContentManager _assetContentManager;
    private readonly Dictionary<Guid, ShaderWrapper> _cache = new();
    private bool _disposed;

    // -----------------------------------------------------------------------
    //  Constructor
    // -----------------------------------------------------------------------

    public ShaderManager(AssetContentManager assetContentManager)
    {
        _assetContentManager = assetContentManager ?? throw new ArgumentNullException(nameof(assetContentManager));
    }

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the <see cref="ShaderWrapper"/> for <paramref name="shaderAssetId"/>,
    /// loading the underlying <see cref="Effect"/> if it is not yet cached.
    /// Returns <c>null</c> when <paramref name="shaderAssetId"/> is empty or the asset
    /// cannot be found.
    /// </summary>
    public ShaderWrapper? GetShader(Guid shaderAssetId)
    {
        if (shaderAssetId == Guid.Empty)
            return null;

        if (_cache.TryGetValue(shaderAssetId, out var cached))
            return cached;

        Effect? effect = null;
        try
        {
            effect = _assetContentManager.Load<Effect>(shaderAssetId);
        }
        catch (Exception ex)
        {
            Core.Log.Logs.WriteException(ex);
            return null;
        }

        if (effect is null)
            return null;

        var wrapper = new ShaderWrapper(effect);
        _cache[shaderAssetId] = wrapper;
        return wrapper;
    }

    /// <summary>
    /// Evicts a single shader from the cache so it will be reloaded next time.
    /// </summary>
    public void Invalidate(Guid shaderAssetId) => _cache.Remove(shaderAssetId);

    /// <summary>
    /// Clears all cached shaders. Call when the graphics device is reset.
    /// </summary>
    public void Clear() => _cache.Clear();

    // -----------------------------------------------------------------------
    //  IDisposable
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // ShaderWrapper does not own the Effect (the AssetContentManager does),
        // so we just drop our references.
        _cache.Clear();
    }
}
