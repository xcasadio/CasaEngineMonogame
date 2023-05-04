using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets;

public class AssetContentManager
{
    private const string DefaultCategory = "default";
    private readonly Dictionary<Type, IAssetLoader> _assetLoader = new();
    private readonly Dictionary<string, Dictionary<string, object>> _assetsByFileNameByCategory = new();
    public GraphicsDevice GraphicsDevice { get; private set; }

    public string RootDirectory
    {
        get;
        set;
    }

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
        _assetLoader.Add(type, loader);
    }

    public void AddAsset(string fileName, object asset, string categoryName = DefaultCategory)
    {
        if (!_assetsByFileNameByCategory.ContainsKey(categoryName))
        {
            _assetsByFileNameByCategory.Add(categoryName, new Dictionary<string, object>());
        }

        _assetsByFileNameByCategory[categoryName][fileName] = asset;
    }

    public T? GetAsset<T>(string fileName, string categoryName = DefaultCategory)
    {
        _assetsByFileNameByCategory[categoryName].TryGetValue(fileName, out object? asset);
        return (T?)asset;
    }

    public bool Rename(string oldName, string newName, string categoryName = DefaultCategory)
    {
        if (!_assetsByFileNameByCategory.ContainsKey(categoryName) || !_assetsByFileNameByCategory[categoryName].ContainsKey(oldName))
        {
            return false;
        }

        var asset = _assetsByFileNameByCategory[categoryName][oldName];
        _assetsByFileNameByCategory[categoryName][newName] = asset;
        _assetsByFileNameByCategory[categoryName].Remove(oldName);

        return true;
    }

    public T Load<T>(string filePath, GraphicsDevice device, string categoryName = DefaultCategory)
    {
        if (_assetsByFileNameByCategory.TryGetValue(categoryName, out var categoryAssetList))
        {
            categoryAssetList = _assetsByFileNameByCategory[categoryName];
        }
        else
        {
            categoryAssetList = new Dictionary<string, object>();
            _assetsByFileNameByCategory.Add(categoryName, categoryAssetList);
        }

        if (categoryAssetList.ContainsKey(filePath))
        {
            return (T)categoryAssetList[filePath];
        }

        var type = typeof(T);

        if (!_assetLoader.ContainsKey(type))
        {
            throw new InvalidOperationException("IAssetLoader not found for the type " + type.FullName);
        }

        var asset = (T)_assetLoader[type].LoadAsset(filePath, device) ?? throw new InvalidOperationException($"IAssetLoader can't load {filePath}");
        AddAsset(filePath, asset, categoryName);
        return asset;

    }

    public void Unload(string categoryName)
    {
        if (_assetsByFileNameByCategory.TryGetValue(categoryName, out var categoryAssetList) == false)
        {
            return;
        }

        //categoryAssetList = _assetsByFileNameByCategory[categoryName];

        foreach (var a in categoryAssetList)
        {
            if (a.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _assetsByFileNameByCategory.Remove(categoryName);
    }

    public void UnloadAll()
    {
        foreach (var pair in _assetsByFileNameByCategory)
        {
            Unload(pair.Key);
        }
    }

    internal void OnDeviceReset(object? sender, EventArgs e)
    {
        GraphicsDevice = sender as GraphicsDevice;

        foreach (var assetsByCategory in _assetsByFileNameByCategory)
        {
            foreach (var assetByFileName in assetsByCategory.Value)
            {
                /*if (assetByFileName.Value is IDisposable) //TODO : useful or buggy ?
                {
                    ((IDisposable)pair2.Value).Dispose();
                }*/

                if (assetByFileName.Value is Asset asset)
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

        if (_assetsByFileNameByCategory.TryGetValue(categoryName, out var categoryAssetList) == false)
        {
            return assets;
        }

        foreach (var pair in categoryAssetList)
        {
            if (pair.Value is T asset)
            {
                assets.Add(asset);
            }
        }

        return assets;
    }
#endif
}