using System.Collections;

using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetContentManager
{
    public const string DefaultCategory = "default";
    private readonly Dictionary<Type, IAssetLoader> _assetLoaderByType = new();
    private readonly Dictionary<string, AssetDictionary> _assetsDictionaryByCategory = new();

    // ADR-0036: who holds each asset of the default category. An entry of the default category with no
    // lease was put there by Load<T> or AddAsset and is pinned, as every asset was before handles existed.
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

    public void AddAsset(AssetInfo assetInfo, object asset, string categoryName = DefaultCategory)
    {
        AddAsset(assetInfo.Id, assetInfo.Name, asset, categoryName);
    }

    public void AddAsset(Guid id, string name, object asset, string categoryName = DefaultCategory)
    {
        if (!_assetsDictionaryByCategory.ContainsKey(categoryName))
        {
            _assetsDictionaryByCategory.Add(categoryName, new AssetDictionary());
        }

        _assetsDictionaryByCategory[categoryName].Add(id, name, asset);
        PinIfDefault(id, categoryName);
    }

    public T GetAsset<T>(string name, string categoryName = DefaultCategory)
    {
        _assetsDictionaryByCategory[categoryName].Get(name, out object asset);
        return (T)asset;
    }

    public T GetAsset<T>(Guid id, string categoryName = DefaultCategory)
    {
        _assetsDictionaryByCategory[categoryName].Get(id, out object asset);
        return (T)asset;
    }

    public bool IsFileSupported(string fileName)
    {
        return _assetLoaderByType.Values.Any(assetLoader => assetLoader.IsFileSupported(fileName));
    }

    public T Load<T>(Guid id, string categoryName = DefaultCategory, bool cache = true) where T : class
    {
        if (_assetsDictionaryByCategory.TryGetValue(categoryName, out var categoryAssetList))
        {
            categoryAssetList = _assetsDictionaryByCategory[categoryName];
        }
        else
        {
            categoryAssetList = new AssetDictionary();
            _assetsDictionaryByCategory.Add(categoryName, categoryAssetList);
        }

        if (typeof(T) != typeof(Entity) && categoryAssetList.Get(id, out var asset))
        {
            if (cache)
            {
                PinIfDefault(id, categoryName);
            }

            return (T)asset;
        }

        var newAsset = LoadNew<T>(id, out var assetInfo);

        if (cache)
        {
            AddAsset(assetInfo, newAsset, categoryName);
        }

        return newAsset;
    }

    /// <summary>
    /// Takes a counted hold on the asset <paramref name="id"/> (ADR-0036). Every hold on an id shares one
    /// instance, loaded once: if the asset is already in the default category — held, pending or loaded by
    /// <see cref="Load{T}(Guid, string, bool)"/> — that instance is returned without loading anything.
    /// <para/>
    /// Disposing the returned handle gives the hold back. An asset nobody holds any more stays pending in
    /// memory and is only freed by <see cref="CollectUnreferenced"/>, which the engine calls when a world
    /// change starts; acquiring it again before then returns the same instance. An asset that
    /// <see cref="Load{T}(Guid, string, bool)"/> or <see cref="AddAsset(Guid, string, object, string)"/> also
    /// put in the default category is pinned and never collected.
    /// </summary>
    /// <exception cref="InvalidOperationException">No loader for <typeparamref name="T"/>, an id missing from
    /// the catalog, or an <see cref="Entity"/> (entities are instantiated per use).</exception>
    public AssetHandle<T> Acquire<T>(Guid id) where T : class
    {
        if (typeof(T) == typeof(Entity))
        {
            throw new InvalidOperationException(
                $"Entity assets are instantiated per use and cannot be shared through a handle (asset '{id}').");
        }

        var defaultAssets = GetOrCreateCategory(DefaultCategory);

        T asset;
        if (defaultAssets.Get(id, out var cached))
        {
            asset = (T)cached;

            if (!_leases.ContainsKey(id))
            {
                // Already there through Load<T> or AddAsset: pinned, as it was before this hold.
                _leases.Add(id, new AssetLease { Pinned = true });
            }
        }
        else
        {
            asset = LoadNew<T>(id, out var assetInfo);
            defaultAssets.Add(assetInfo.Id, assetInfo.Name, asset);
            _leases[id] = new AssetLease { Name = assetInfo.Name };
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
    /// nobody holds it. The object is indexed by the catalog name of <paramref name="id"/> when the id resolves
    /// in the catalog, and by no name otherwise.
    /// </summary>
    /// <exception cref="InvalidOperationException">The id is already present, held or not: use
    /// <see cref="Replace{T}(Guid, T)"/> to swap an instance.</exception>
    public AssetHandle<T> Register<T>(Guid id, T asset) where T : class
    {
        ArgumentNullException.ThrowIfNull(asset);

        var defaultAssets = GetOrCreateCategory(DefaultCategory);
        if (defaultAssets.Get(id, out _))
        {
            throw new InvalidOperationException(
                $"An asset is already present under '{id}'; use Replace to swap its instance.");
        }

        var lease = StoreUnpinned(defaultAssets, id, asset);
        lease.HandleCount++;
        return new AssetHandle<T>(this, id, asset);
    }

    /// <summary>
    /// Swaps the shared instance of <paramref name="id"/> for <paramref name="asset"/> (ADR-0037), for hot
    /// reload and editor saves. An id already present keeps its holders and its pinning, and its name now
    /// designates the new instance; later acquisitions return the new instance. Existing handles keep
    /// returning the instance they were given, and the old instance is not disposed: its users are refreshed
    /// by whoever replaces it. An absent id is stored as a pending asset that nobody holds yet.
    /// </summary>
    public void Replace<T>(Guid id, T asset) where T : class
    {
        ArgumentNullException.ThrowIfNull(asset);

        var defaultAssets = GetOrCreateCategory(DefaultCategory);
        if (defaultAssets.Get(id, out var previous))
        {
            defaultAssets.ReplaceInstance(id, previous, asset);
            return;
        }

        StoreUnpinned(defaultAssets, id, asset);
    }

    private AssetLease StoreUnpinned(AssetDictionary defaultAssets, Guid id, object asset)
    {
        var name = ResolveAssetInfo(id)?.Name;
        if (name != null)
        {
            defaultAssets.Add(id, name, asset);
        }
        else
        {
            defaultAssets.AddWithoutName(id, asset);
        }

        var lease = new AssetLease { Name = name };
        _leases[id] = lease;
        return lease;
    }

    /// <summary>
    /// Frees every asset of the default category that nobody holds and that is not pinned (ADR-0036): disposes
    /// it when it is <see cref="IDisposable"/> or <see cref="IAssetable"/> (ADR-0037), once, and drops it from
    /// the cache. An asset freed here may give back holds on its own dependencies; those are freed in the same
    /// call when nobody else holds them. Called by the engine at the very start of every world change; a game
    /// may also call it.
    /// </summary>
    /// <returns>The number of assets freed.</returns>
    public int CollectUnreferenced()
    {
        if (!_assetsDictionaryByCategory.TryGetValue(DefaultCategory, out var defaultAssets))
        {
            return 0;
        }

        var freed = 0;
        bool freedThisPass;
        do
        {
            _collectCandidates.Clear();
            foreach (var pair in _leases)
            {
                if (!pair.Value.Pinned && pair.Value.HandleCount == 0)
                {
                    _collectCandidates.Add(pair.Key);
                }
            }

            freedThisPass = false;
            for (var i = 0; i < _collectCandidates.Count; i++)
            {
                var id = _collectCandidates[i];

                // Freeing an earlier candidate may have acquired this one again.
                if (!_leases.TryGetValue(id, out var lease) || lease.Pinned || lease.HandleCount > 0)
                {
                    continue;
                }

                _leases.Remove(id);
                freedThisPass = true;

                if (defaultAssets.Get(id, out var asset))
                {
                    defaultAssets.RemoveById(id, lease.Name, asset);

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

    private void PinIfDefault(Guid id, string categoryName)
    {
        if (categoryName == DefaultCategory && _leases.TryGetValue(id, out var lease))
        {
            lease.Pinned = true;
        }
    }

    private AssetDictionary GetOrCreateCategory(string categoryName)
    {
        if (!_assetsDictionaryByCategory.TryGetValue(categoryName, out var categoryAssetList))
        {
            categoryAssetList = new AssetDictionary();
            _assetsDictionaryByCategory.Add(categoryName, categoryAssetList);
        }

        return categoryAssetList;
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

    public T Load<T>(JObject element) where T : class, ISerializable, new()
    {
        var asset = new T();
        asset.Load(element);
        return asset;
    }

    /// <summary>
    /// Loads an asset from a file path relative to the active project root
    /// (<see cref="EngineRuntimeContext.ProjectPath"/>), without going through the
    /// project's asset catalog. Intended for demos, samples and tools that load
    /// files which are not declared in an editor project.
    /// The result is not cached and is not tracked for device reset: the caller
    /// owns the returned object's lifetime. For catalogued assets, prefer
    /// <see cref="Load{T}(Guid, string, bool)"/>.
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

    [Obsolete("Use LoadFromFile<T> instead: same behavior, clearer contract (path-based loading outside the asset catalog).")]
    public T LoadDirectly<T>(string assetFileName)
    {
        return LoadFromFile<T>(assetFileName);
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
    /// Full path of an asset file, resolved the same way <see cref="Load{T}(Guid, string, bool)"/>
    /// does. Needed by the systems that must open the file themselves rather than go through a
    /// loader, typically audio streaming.
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

    /// <summary>
    /// Disposes every <see cref="IDisposable"/> asset of the category and drops them. In the default category,
    /// an asset that still has live handles (<see cref="Acquire{T}(Guid)"/>) is kept, with its handles valid
    /// (ADR-0036).
    /// </summary>
    public void Unload(string categoryName)
    {
        if (_assetsDictionaryByCategory.TryGetValue(categoryName, out var categoryAssetList) == false)
        {
            return;
        }

        if (categoryName == DefaultCategory && HasHeldAsset())
        {
            UnloadDefaultCategoryKeepingHeldAssets(categoryAssetList);
            return;
        }

        foreach (var asset in categoryAssetList)
        {
            if (asset is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _assetsDictionaryByCategory.Remove(categoryName);

        if (categoryName == DefaultCategory)
        {
            _leases.Clear();
        }
    }

    private bool HasHeldAsset()
    {
        foreach (var pair in _leases)
        {
            if (pair.Value.HandleCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void UnloadDefaultCategoryKeepingHeldAssets(AssetDictionary defaultAssets)
    {
        var entries = new List<KeyValuePair<Guid, object>>(defaultAssets.Entries);
        foreach (var entry in entries)
        {
            if (_leases.TryGetValue(entry.Key, out var lease) && lease.HandleCount > 0)
            {
                continue;
            }

            defaultAssets.RemoveById(entry.Key, lease?.Name, entry.Value);
            _leases.Remove(entry.Key);

            if (entry.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public void UnloadAll()
    {
        foreach (var pair in _assetsDictionaryByCategory)
        {
            Unload(pair.Key);
        }
    }

    internal void OnDeviceReset(object sender, EventArgs e)
    {
        foreach (var assetDictionaryByCategory in _assetsDictionaryByCategory)
        {
            foreach (var o in assetDictionaryByCategory.Value)
            {
                if (o is IAssetable asset)
                {
                    asset.OnDeviceReset(GraphicsDevice, this);
                }
            }
        }
    }

    public IList<T> GetAssets<T>(string categoryName = DefaultCategory)
    {
        var assets = new List<T>();

        if (_assetsDictionaryByCategory.TryGetValue(categoryName, out var categoryAssetList) == false)
        {
            return assets;
        }

        foreach (var o in categoryAssetList)
        {
            if (o is T asset)
            {
                assets.Add(asset);
            }
        }

        return assets;
    }

    private class AssetDictionary : IEnumerable<object>
    {
        private readonly Dictionary<string, object> _assetsByName = new();
        private readonly Dictionary<Guid, object> _assetsById = new();

        public void Add(Guid id, string name, object asset)
        {
            _assetsById[id] = asset;
            _assetsByName[name] = asset;
        }

        public bool Get(Guid id, out object asset)
        {
            return _assetsById.TryGetValue(id, out asset);
        }

        public bool Get(string name, out object asset)
        {
            return _assetsByName.TryGetValue(name, out asset);
        }

        public object Remove(Guid id, string name)
        {
            _assetsById.Remove(id);
            return _assetsByName.Remove(name);
        }

        public void AddWithoutName(Guid id, object asset)
        {
            _assetsById[id] = asset;
        }

        /// <summary>Points <paramref name="id"/>, and every name that designated <paramref name="previous"/>, at
        /// <paramref name="replacement"/>.</summary>
        public void ReplaceInstance(Guid id, object previous, object replacement)
        {
            _assetsById[id] = replacement;

            List<string> names = null;
            foreach (var pair in _assetsByName)
            {
                if (ReferenceEquals(pair.Value, previous))
                {
                    (names ??= new List<string>()).Add(pair.Key);
                }
            }

            if (names != null)
            {
                foreach (var name in names)
                {
                    _assetsByName[name] = replacement;
                }
            }
        }

        public IEnumerable<KeyValuePair<Guid, object>> Entries => _assetsById;

        /// <summary>Removes the entry <paramref name="id"/>, and its name entry only when that name still
        /// points at this asset: several assets can share a name (e.g. a font's .png, .texture and .fnt).
        /// With no known name, every name entry pointing at this asset is dropped.</summary>
        public void RemoveById(Guid id, string name, object asset)
        {
            _assetsById.Remove(id);

            if (name != null)
            {
                if (_assetsByName.TryGetValue(name, out var named) && ReferenceEquals(named, asset))
                {
                    _assetsByName.Remove(name);
                }

                return;
            }

            List<string> staleNames = null;
            foreach (var pair in _assetsByName)
            {
                if (ReferenceEquals(pair.Value, asset))
                {
                    (staleNames ??= new List<string>()).Add(pair.Key);
                }
            }

            if (staleNames != null)
            {
                foreach (var staleName in staleNames)
                {
                    _assetsByName.Remove(staleName);
                }
            }
        }

        public IEnumerator<object> GetEnumerator()
        {
            return _assetsById.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Rename(AssetInfo assetInfo, string oldName)
        {
            if (_assetsByName.Remove(oldName))
            {
                _assetsByName[assetInfo.Name] = assetInfo;
            }
        }
    }

    private sealed class AssetLease
    {
        public string Name;
        public int HandleCount;
        public bool Pinned;
    }

}
