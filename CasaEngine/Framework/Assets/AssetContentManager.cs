using System.Collections;
using CasaEngine.Engine;
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

    public void AddAsset(long id, string name, object asset, string categoryName = DefaultCategory)
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

    public T? GetAsset<T>(long id, string categoryName = DefaultCategory)
    {
        _assetsDictionaryByCategory[categoryName].Get(id, out object? asset);
        return (T?)asset;
    }

    /*
    public bool Rename(string oldName, string newName, string categoryName = DefaultCategory)
    {
        if (!_assetsDictionaryByCategory.ContainsKey(categoryName) || !_assetsDictionaryByCategory[categoryName].ContainsKey(oldName))
        {
            return false;
        }

        var asset = _assetsDictionaryByCategory[categoryName][oldName];
        _assetsDictionaryByCategory[categoryName][newName] = asset;
        _assetsDictionaryByCategory[categoryName].Remove(oldName);

        return true;
    }
    */

    public bool IsFileSupported(string fileName)
    {
        return _assetLoaderByType.Values.Any(assetLoader => assetLoader.IsFileSupported(fileName));
    }

    //TODO : remove only create for texture (waiting add assetinfo for texture)
    public T LoadWithoutAdd<T>(string fileName, GraphicsDevice device, string categoryName = DefaultCategory)
    {
        var type = typeof(T);
        return (T)_assetLoaderByType[type].LoadAsset(fileName, device) ?? throw new InvalidOperationException($"IAssetLoader can't load {fileName}");
    }

    public T Load<T>(AssetInfo assetInfo, GraphicsDevice device, string categoryName = DefaultCategory)
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

        if (categoryAssetList.Get(assetInfo.Id, out var asset))
        {
            return (T)asset;
        }

        var type = typeof(T);

        if (!_assetLoaderByType.ContainsKey(type))
        {
            throw new InvalidOperationException("IAssetLoader not found for the type " + type.FullName);
        }

        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
        var newAsset = (T)_assetLoaderByType[type].LoadAsset(fullFileName, device) ?? throw new InvalidOperationException($"IAssetLoader can't load {fullFileName}");
        AddAsset(assetInfo, newAsset, categoryName);
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
        GraphicsDevice = sender as GraphicsDevice;

        foreach (var assetDictionaryByCategory in _assetsDictionaryByCategory)
        {
            foreach (var o in assetDictionaryByCategory.Value)
            {
                /*if (assetByFileName.Value is IDisposable) //TODO : useful or buggy ?
                {
                    ((IDisposable)pair2.Value).Dispose();
                }*/

                if (o is Asset asset)
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
        private readonly Dictionary<long, object> _assetsById = new();

        public void Add(long id, string name, object asset)
        {
            _assetsById[id] = asset;
            _assetsByName[name] = asset;
        }

        public bool Get(long id, out object asset)
        {
            return _assetsById.TryGetValue(id, out asset);
        }

        public bool Get(string name, out object asset)
        {
            return _assetsByName.TryGetValue(name, out asset);
        }

        public object Remove(long id, string name)
        {
            return _assetsById.Remove(id);
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
    }
}
