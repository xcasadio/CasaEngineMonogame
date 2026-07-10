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
            return (T)asset;
        }

        var type = typeof(T);

        if (!_assetLoaderByType.ContainsKey(type))
        {
            throw new InvalidOperationException($"IAssetLoader not found for the type {type.FullName}");
        }

        var assetInfo = ResolveAssetInfo(id);

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

        if (cache)
        {
            AddAsset(assetInfo, newAsset, categoryName);
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

    private string ResolveAssetPath(string relativeFileName)
    {
        if (RuntimeContext != null)
        {
            return RuntimeContext.GetAssetPath(relativeFileName);
        }

        return Path.Combine(EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath), relativeFileName);
    }

    public void Unload(string categoryName)
    {
        if (_assetsDictionaryByCategory.TryGetValue(categoryName, out var categoryAssetList) == false)
        {
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

}
