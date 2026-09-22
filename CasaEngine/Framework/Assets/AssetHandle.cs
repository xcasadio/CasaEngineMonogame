namespace CasaEngine.Framework.Assets;

/// <summary>
/// A counted hold on a shared asset, returned by <see cref="AssetContentManager.Acquire{T}(Guid)"/>.
/// Every handle on the same id shares one instance. Disposing the handle gives the hold back; when the
/// last hold is given back the asset is not freed at once but kept pending, and a later
/// <see cref="AssetContentManager.CollectUnreferenced"/> frees it if nobody acquired it again in between
/// (ADR-0036).
/// </summary>
/// <typeparam name="T">The asset type.</typeparam>
public sealed class AssetHandle<T> : IDisposable where T : class
{
    private AssetContentManager _owner;
    private readonly T _asset;

    internal AssetHandle(AssetContentManager owner, Guid id, T asset)
    {
        _owner = owner;
        Id = id;
        _asset = asset;
    }

    /// <summary>The id of the held asset.</summary>
    public Guid Id { get; }

    /// <summary>The shared asset instance. Reading it after <see cref="Dispose"/> is a programming error.</summary>
    public T Asset
    {
        get
        {
            ObjectDisposedException.ThrowIf(_owner == null, this);
            return _asset;
        }
    }

    /// <summary>True once the hold has been given back.</summary>
    public bool IsDisposed => _owner == null;

    /// <summary>Gives the hold back. Idempotent: only the first call counts.</summary>
    public void Dispose()
    {
        var owner = _owner;
        if (owner == null)
        {
            return;
        }

        _owner = null;
        owner.Release(Id);
    }
}
