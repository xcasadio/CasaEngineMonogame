using CasaEngine.Core.Logging;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets;

/// <summary>
/// Loads catalogued assets and keeps one shared instance of each, for as long as someone holds it
/// (ADR-0036, ADR-0037).
/// <list type="bullet">
/// <item><description><see cref="Acquire{T}(Guid)"/> gives a counted hold on the shared instance of an asset.</description></item>
/// <item><description><see cref="LoadCopy{T}(Guid)"/> reads a fresh copy that the caller alone owns (templates).</description></item>
/// <item><description><see cref="Register{T}(Guid, T)"/> and <see cref="Replace{T}(Guid, T)"/> store objects made at run time.</description></item>
/// <item><description><see cref="CollectUnreferenced"/> frees what nobody holds; the engine calls it when a world change starts.</description></item>
/// <item><description><see cref="LoadFromFile{T}(string)"/> reads an uncatalogued file for tools and demos.</description></item>
/// </list>
/// </summary>
public class AssetContentManager
{
    private readonly Dictionary<Type, IAssetLoader> _assetLoaderByType = new();

    // One shared instance per id, and who holds it. Every entry has a lease: nothing is pinned.
    private readonly Dictionary<Guid, object> _assets = new();
    private readonly Dictionary<Guid, AssetLease> _leases = new();
    private readonly List<Guid> _collectCandidates = new();

    public GraphicsDevice GraphicsDevice { get; private set; }

    public string RootDirectory { get; set; }

    public EngineRuntimeContext RuntimeContext { get; set; }

    public AssetContentManager()
    {
        RootDirectory = Environment.CurrentDirectory;
    }

    public void Initialize(GraphicsDevice device)
    {
        GraphicsDevice = device;
        GraphicsDevice.DeviceReset += OnDeviceReset;
    }

    public void RegisterAssetLoader(Type type, IAssetLoader loader)
    {
        _assetLoaderByType.Add(type, loader);
    }

    public bool IsFileSupported(string fileName)
    {
        return _assetLoaderByType.Values.Any(assetLoader => assetLoader.IsFileSupported(fileName));
    }

    /// <summary>
    /// Takes a counted hold on the asset <paramref name="id"/> (ADR-0036). Every hold on an id shares one
    /// instance, loaded once: an asset already present — held, or pending since its last hold was given back —
    /// is returned without loading anything.
    /// <para/>
    /// Disposing the returned handle gives the hold back. An asset nobody holds any more stays pending in
    /// memory and is only freed by <see cref="CollectUnreferenced"/>, which the engine calls when a world
    /// change starts; acquiring it again before then returns the same instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">No loader for <typeparamref name="T"/>, an id missing from
    /// the catalog, or an <see cref="Entity"/> (entities are instantiated per use: see
    /// <see cref="LoadCopy{T}(Guid)"/>).</exception>
    public AssetHandle<T> Acquire<T>(Guid id) where T : class
    {
        if (typeof(T) == typeof(Entity))
        {
            throw new InvalidOperationException(
                $"Entity assets are instantiated per use and cannot be shared through a handle (asset '{id}').");
        }

        T asset;
        if (_assets.TryGetValue(id, out var cached))
        {
            asset = (T)cached;
        }
        else
        {
            asset = LoadNew<T>(id, out _);
            _assets[id] = asset;
            _leases[id] = new AssetLease();
        }

        _leases[id].HandleCount++;
        return new AssetHandle<T>(this, id, asset);
    }

    /// <summary>
    /// Reads a fresh <typeparamref name="T"/> from the asset's file (ADR-0037), for templates that every use
    /// needs its own copy of: entities, worlds, cutscenes, authoring materials, hot-reload reads. The copy is
    /// neither cached nor counted: the caller alone owns it.
    /// </summary>
    /// <exception cref="InvalidOperationException">No loader for <typeparamref name="T"/>, or an id missing
    /// from the catalog.</exception>
    public T LoadCopy<T>(Guid id) where T : class
    {
        return LoadNew<T>(id, out _);
    }

    /// <summary>
    /// Stores an object made at run time (a generated cubemap, the default texture) under <paramref name="id"/>
    /// and returns a hold on it (ADR-0037). Its maker holds it, and it is freed like any acquired asset once
    /// nobody holds it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The id is already present, held or pending: use
    /// <see cref="Replace{T}(Guid, T)"/> to swap an instance.</exception>
    public AssetHandle<T> Register<T>(Guid id, T asset) where T : class
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (_assets.ContainsKey(id))
        {
            throw new InvalidOperationException(
                $"An asset is already present under '{id}'; use Replace to swap its instance.");
        }

        _assets[id] = asset;
        _leases[id] = new AssetLease { HandleCount = 1 };
        return new AssetHandle<T>(this, id, asset);
    }

    /// <summary>
    /// Swaps the shared instance of <paramref name="id"/> for <paramref name="asset"/> (ADR-0037), for hot
    /// reload and editor saves. An id already present keeps its holders; later acquisitions return the new
    /// instance. Existing handles keep returning the instance they were given, and the old instance is not
    /// disposed: its users are refreshed by whoever replaces it. An absent id is stored as a pending asset that
    /// nobody holds yet.
    /// </summary>
    public void Replace<T>(Guid id, T asset) where T : class
    {
        ArgumentNullException.ThrowIfNull(asset);

        _assets[id] = asset;
        if (!_leases.ContainsKey(id))
        {
            _leases[id] = new AssetLease();
        }
    }

    /// <summary>
    /// Frees every asset that nobody holds (ADR-0036): disposes it when it is <see cref="IDisposable"/> or
    /// <see cref="IAssetable"/> (ADR-0037), once, and drops it from the cache. An asset freed here may give back
    /// holds on its own dependencies; those are freed in the same call when nobody else holds them. Called by
    /// the engine at the very start of every world change; a game may also call it.
    /// </summary>
    /// <returns>The number of assets freed.</returns>
    public int CollectUnreferenced()
    {
        var freed = 0;
        bool freedThisPass;
        do
        {
            _collectCandidates.Clear();
            foreach (var pair in _leases)
            {
                if (pair.Value.HandleCount == 0)
                {
                    _collectCandidates.Add(pair.Key);
                }
            }

            freedThisPass = false;
            for (var i = 0; i < _collectCandidates.Count; i++)
            {
                var id = _collectCandidates[i];

                // Freeing an earlier candidate may have acquired this one again.
                if (!_leases.TryGetValue(id, out var lease) || lease.HandleCount > 0)
                {
                    continue;
                }

                _leases.Remove(id);
                freedThisPass = true;

                if (_assets.Remove(id, out var asset))
                {
                    // Disposing may release holds on dependencies: the next pass frees them.
                    DisposeFreedAsset(asset);
                }

                freed++;
            }
        }
        while (freedThisPass);

        _collectCandidates.Clear();
        return freed;
    }

    // IAssetable declares its own Dispose without deriving from IDisposable (IAssetable.cs); a type that
    // implements both usually satisfies them with one method, so it is called once.
    private static void DisposeFreedAsset(object asset)
    {
        if (asset is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (asset is IAssetable assetable)
        {
            assetable.Dispose();
        }
    }

    internal void Release(Guid id)
    {
        if (_leases.TryGetValue(id, out var lease) && lease.HandleCount > 0)
        {
            lease.HandleCount--;
        }
    }

    private T LoadNew<T>(Guid id, out AssetInfo assetInfo) where T : class
    {
        var type = typeof(T);

        if (!_assetLoaderByType.ContainsKey(type))
        {
            throw new InvalidOperationException($"IAssetLoader not found for the type {type.FullName}");
        }

        assetInfo = ResolveAssetInfo(id);

        if (assetInfo == null)
        {
            throw new InvalidOperationException($"Asset not found with id '{id}'");
        }

        var fullFileName = ResolveAssetPath(assetInfo.FileName);
        Logs.WriteTrace($"Load asset {fullFileName}");
        var newAsset = (T)_assetLoaderByType[type].LoadAsset(fullFileName, this) ?? throw new InvalidOperationException($"IAssetLoader can't load {fullFileName}");

        if (newAsset is ObjectBase gameObject)
        {
            gameObject.AssetId = id;
            gameObject.Name = assetInfo.Name;
            gameObject.FileName = assetInfo.FileName;
        }

        return newAsset;
    }

    /// <summary>
    /// Loads an asset from a file path relative to the active project root
    /// (<see cref="EngineRuntimeContext.ProjectPath"/>), without going through the
    /// project's asset catalog. Intended for demos, samples and tools that load
    /// files which are not declared in an editor project.
    /// The result is not cached and is not tracked for device reset: the caller
    /// owns the returned object's lifetime. For catalogued assets, prefer
    /// <see cref="Acquire{T}(Guid)"/> or <see cref="LoadCopy{T}(Guid)"/>.
    /// </summary>
    public T LoadFromFile<T>(string assetFileName)
    {
        var type = typeof(T);

        if (!_assetLoaderByType.ContainsKey(type))
        {
            throw new InvalidOperationException("IAssetLoader not found for the type " + type.FullName);
        }

        var fullFileName = ResolveAssetPath(assetFileName);
        Logs.WriteTrace($"Load asset {fullFileName}");
        var newAsset = (T)_assetLoaderByType[type].LoadAsset(fullFileName, this) ?? throw new InvalidOperationException($"IAssetLoader can't load {fullFileName}");

        if (newAsset is ObjectBase gameObject && string.IsNullOrEmpty(gameObject.FileName))
        {
            gameObject.FileName = assetFileName;
        }

        return newAsset;
    }

    private AssetInfo ResolveAssetInfo(Guid id)
    {
        if (RuntimeContext?.ResolveAssetInfo != null)
        {
            return RuntimeContext.ResolveAssetInfo(id);
        }

        return AssetCatalog.Get(id);
    }

    /// <summary>
    /// Full path of an asset file, resolved the same way the loaders' paths are. Needed by the systems that
    /// must open the file themselves rather than go through a loader, typically audio streaming.
    /// </summary>
    public string ResolveAssetFullPath(string relativeFileName)
    {
        return ResolveAssetPath(relativeFileName);
    }

    private string ResolveAssetPath(string relativeFileName)
    {
        if (RuntimeContext != null)
        {
            return RuntimeContext.GetAssetPath(relativeFileName);
        }

        return Path.Combine(ProjectRoot, relativeFileName);
    }

    private string ProjectRoot => RuntimeContext != null
        ? RuntimeContext.ProjectPath
        : EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath);

    /// <summary>
    /// The catalog entry of a file that an asset file references by a path relative to its own folder, such
    /// as a font's page texture (ADR-0036). Null when the catalog has no such file.
    /// </summary>
    /// <param name="fullFileName">The full path of the referencing asset file, as loaders receive it.</param>
    /// <param name="relativeFileName">The referenced path, relative to the folder of <paramref name="fullFileName"/>.</param>
    internal AssetInfo ResolveAssetInfoNextTo(string fullFileName, string relativeFileName)
    {
        var catalogFileName = Fonts.BitmapFontDescriptor.CatalogFileNameNextTo(ProjectRoot, fullFileName, relativeFileName);
        return RuntimeContext?.ResolveAssetInfoByFileName != null
            ? RuntimeContext.ResolveAssetInfoByFileName(catalogFileName)
            : AssetCatalog.GetByFileName(catalogFileName);
    }

    internal void OnDeviceReset(object sender, EventArgs e)
    {
        // Snapshot: a texture recovering its image acquires and replaces entries of this cache.
        foreach (var asset in _assets.Values.ToArray())
        {
            if (asset is IAssetable assetable)
            {
                assetable.OnDeviceReset(GraphicsDevice, this);
            }
        }
    }

    private sealed class AssetLease
    {
        public int HandleCount;
    }
}
