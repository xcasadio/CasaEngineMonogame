using CasaEngine.Core.Logging;
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

    // Handles acquired through GetShader (ADR-0037): released in Clear and Dispose. A ShaderWrapper
    // registered directly through RegisterShader has no handle here — its Effect is not this manager's
    // to hold, e.g. built-in shaders loaded through the MonoGame ContentManager.
    private readonly Dictionary<Guid, AssetHandle<Effect>> _handles = new();
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
    public ShaderWrapper GetShader(Guid shaderAssetId)
    {
        if (shaderAssetId == Guid.Empty)
        {
            return null;
        }

        if (_cache.TryGetValue(shaderAssetId, out var cached))
        {
            return cached;
        }

        AssetHandle<Effect> handle;
        try
        {
            handle = _assetContentManager.Acquire<Effect>(shaderAssetId);
        }
        catch (Exception ex)
        {
            Logs.WriteException(ex);
            return null;
        }

        var wrapper = new ShaderWrapper(handle.Asset);
        _cache[shaderAssetId] = wrapper;
        _handles[shaderAssetId] = handle;
        return wrapper;
    }

    /// <summary>
    /// Registers an already created shader wrapper under a stable id.
    /// Useful for built-in shaders that are loaded by content path instead of asset catalog id.
    /// </summary>
    public void RegisterShader(Guid shaderAssetId, ShaderWrapper shader)
    {
        ArgumentNullException.ThrowIfNull(shader);

        if (shaderAssetId == Guid.Empty)
        {
            throw new ArgumentException("A stable shader id is required.", nameof(shaderAssetId));
        }

        _cache[shaderAssetId] = shader;

        // Overwriting an id previously loaded through GetShader: give its handle back, this wrapper's
        // Effect is not ours to hold.
        if (_handles.Remove(shaderAssetId, out var previousHandle))
        {
            previousHandle.Dispose();
        }
    }

    /// <summary>
    /// Evicts a single shader from the cache so it will be reloaded next time, giving back its handle
    /// if <see cref="GetShader"/> acquired one.
    /// </summary>
    public void Invalidate(Guid shaderAssetId)
    {
        _cache.Remove(shaderAssetId);

        if (_handles.Remove(shaderAssetId, out var handle))
        {
            handle.Dispose();
        }
    }

    /// <summary>
    /// Clears all cached shaders, giving back every handle acquired through <see cref="GetShader"/>.
    /// Call when the graphics device is reset.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();

        foreach (var handle in _handles.Values)
        {
            handle.Dispose();
        }

        _handles.Clear();
    }

    // -----------------------------------------------------------------------
    //  IDisposable
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
    }
}
