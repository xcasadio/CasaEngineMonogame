using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Asset
{
    public class AssetContentManager
    {

        private readonly List<Asset> _assets = new();

        private readonly Dictionary<string, Dictionary<string, object>> _loadedAssets = new();
        private readonly Dictionary<Type, IAssetLoader> _assetLoader = new();



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
            device.DeviceReset += new EventHandler<EventArgs>(OnDeviceReset);
        }

        public void RegisterAssetLoader(Type type, IAssetLoader loader)
        {
            _assetLoader.Add(type, loader);
        }

        public T Load<T>(string filePath, GraphicsDevice device, string categoryName = "default")
        {
            Type type = null;

            Dictionary<string, object> categoryAssetList = null;

            //find category
            if (_loadedAssets.ContainsKey(categoryName) == true)
            {
                categoryAssetList = _loadedAssets[categoryName];
            }
            else
            {
                categoryAssetList = new Dictionary<string, object>();
                _loadedAssets.Add(categoryName, categoryAssetList);
            }

            //find asset
            if (categoryAssetList.ContainsKey(filePath) == true)
            {
                return (T)categoryAssetList[filePath];
            }
            else
            {
                type = typeof(T);

                if (_assetLoader.ContainsKey(type) == true)
                {
                    T asset = (T)_assetLoader[type].LoadAsset(filePath, device);
                    categoryAssetList.Add(filePath, asset);
                    return asset;
                }
            }

            throw new InvalidOperationException("IAssetLoader not found for the type " + type.FullName);
        }

        public void Unload(string categoryName)
        {
            Dictionary<string, object> categoryAssetList = null;

            //find category
            if (_loadedAssets.ContainsKey(categoryName) == false)
            {
                return;
            }

            categoryAssetList = _loadedAssets[categoryName];

            foreach (var a in categoryAssetList)
            {
                if (a.Value is IDisposable)
                {
                    ((IDisposable)a.Value).Dispose();
                }
            }

            _loadedAssets.Remove(categoryName);
        }

        public void UnloadAll()
        {
            foreach (var pair in _loadedAssets)
            {
                Unload(pair.Key);
            }
        }


        internal void AddAsset(Asset asset)
        {
            _assets.Add(asset);
        }

        internal void OnDeviceReset(object sender, EventArgs e)
        {
            GraphicsDevice device = sender as GraphicsDevice;

            //TODO : useful or buggy ?
            /*foreach (var pair in _LoadedAssets)
            {
                foreach (var pair2 in pair.Value)
                {
                    if (pair2.Value is IDisposable)
                    {
                        ((IDisposable)pair2.Value).Dispose();
                    }
                }
            }*/

            foreach (var a in _assets)
            {
                a.OnDeviceReset(device);
            }
        }


    }
}
