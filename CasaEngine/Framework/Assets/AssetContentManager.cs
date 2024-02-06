using System.Collections;
using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets;

public class AssetContentManager
{
    public const string DefaultCategory = "default";
    private readonly Dictionary<Type, IAssetLoader> _assetLoaderByType = new();
    private readonly Dictionary<string, AssetDictionary> _assetsDictionaryByCategory = new();

    public GraphicsDevice GraphicsDevice { get; private set; }

    public string RootDirectory { get; set; }

    public AssetContentManager()
    {
        RootDirectory = Environment.CurrentDirectory;

#if EDITOR
        AssetCatalog.AssetRenamed += OnAssetRenamed;
#endif
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

    public T? GetAsset<T>(string name, string categoryName = DefaultCategory)
    {
        _assetsDictionaryByCategory[categoryName].Get(name, out object? asset);
        return (T?)asset;
    }

    public T? GetAsset<T>(Guid id, string categoryName = DefaultCategory)
    {
        _assetsDictionaryByCategory[categoryName].Get(id, out object? asset);
        return (T?)asset;
    }

    public bool IsFileSupported(string fileName)
    {
        return _assetLoaderByType.Values.Any(assetLoader => assetLoader.IsFileSupported(fileName));
    }

    public T Load<T>(Guid id, string categoryName = DefaultCategory) where T : class
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

        if (categoryAssetList.Get(id, out var asset))
        {
            return (T)asset;
        }

        var type = typeof(T);

        if (!_assetLoaderByType.ContainsKey(type))
        {
            throw new InvalidOperationException($"IAssetLoader not found for the type {type.FullName}");
        }

        var assetInfo = AssetCatalog.Get(id);

        if (assetInfo == null)
        {
            throw new InvalidOperationException($"Asset not found with id '{id}'");
        }

        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
        Logs.WriteTrace($"Load asset {fullFileName}");
        var newAsset = (T)_assetLoaderByType[type].LoadAsset(fullFileName, GraphicsDevice) ?? throw new InvalidOperationException($"IAssetLoader can't load {fullFileName}");

        if (newAsset is ObjectBase gameObject)
        {
            gameObject.AssetId = assetInfo.Id;
            gameObject.Name = assetInfo.Name;
            gameObject.FileName = assetInfo.FileName;
        }

        AddAsset(assetInfo, newAsset, categoryName);
        return newAsset;
    }

    [Obsolete("Used only for neoforce controls")]
    public T LoadDirectly<T>(string assetFileName)
    {
        var type = typeof(T);

        if (!_assetLoaderByType.ContainsKey(type))
        {
            throw new InvalidOperationException("IAssetLoader not found for the type " + type.FullName);
        }

        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, assetFileName);
        Logs.WriteTrace($"Load asset {fullFileName}");
        var newAsset = (T)_assetLoaderByType[type].LoadAsset(fullFileName, GraphicsDevice) ?? throw new InvalidOperationException($"IAssetLoader can't load {fullFileName}");
        return newAsset;
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

    internal void OnDeviceReset(object? sender, EventArgs e)
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

#if EDITOR

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

#endif

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
            if (_assetsByName.ContainsKey(oldName))
            {
                _assetsByName.Remove(oldName);
                _assetsByName[assetInfo.Name] = assetInfo;
            }
        }
    }

    private void OnAssetRenamed(object? sender, Core.Design.EventArgs<AssetInfo, string> e)
    {
        foreach (var assetsByCategory in _assetsDictionaryByCategory)
        {
            assetsByCategory.Value.Rename(e.Value, e.Value2);
        }
    }
}
