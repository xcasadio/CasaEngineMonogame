using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Asset
{
    public class AssetContentManager
    {

        private readonly List<Asset> m_Assets = new List<Asset>();

        private readonly Dictionary<string, Dictionary<string, object>> m_LoadedAssets = new Dictionary<string, Dictionary<string, object>>();
        private readonly Dictionary<Type, IAssetLoader> m_AssetLoader = new Dictionary<Type, IAssetLoader>();



        public string RootDirectory
        {
            get;
            set;
        }



        public AssetContentManager()
        {
            RootDirectory = Environment.CurrentDirectory;
        }



        public void Initialize(GraphicsDevice device_)
        {
            device_.DeviceReset += new EventHandler<EventArgs>(OnDeviceReset);
        }

        public void RegisterAssetLoader(Type type_, IAssetLoader loader_)
        {
            m_AssetLoader.Add(type_, loader_);
        }

        public T Load<T>(string filePath_, GraphicsDevice device_, string categoryName_ = "default")
        {
            Type type = null;

            Dictionary<string, object> categoryAssetList = null;

            //find category
            if (m_LoadedAssets.ContainsKey(categoryName_) == true)
            {
                categoryAssetList = m_LoadedAssets[categoryName_];
            }
            else
            {
                categoryAssetList = new Dictionary<string, object>();
                m_LoadedAssets.Add(categoryName_, categoryAssetList);
            }

            //find asset
            if (categoryAssetList.ContainsKey(filePath_) == true)
            {
                return (T)categoryAssetList[filePath_];
            }
            else
            {
                type = typeof(T);

                if (m_AssetLoader.ContainsKey(type) == true)
                {
                    T asset = (T)m_AssetLoader[type].LoadAsset(filePath_, device_);
                    categoryAssetList.Add(filePath_, asset);
                    return asset;
                }
            }

            throw new InvalidOperationException("IAssetLoader not found for the type " + type.FullName);
        }

        public void Unload(string categoryName_)
        {
            Dictionary<string, object> categoryAssetList = null;

            //find category
            if (m_LoadedAssets.ContainsKey(categoryName_) == false)
            {
                return;
            }

            categoryAssetList = m_LoadedAssets[categoryName_];

            foreach (var a in categoryAssetList)
            {
                if (a.Value is IDisposable)
                {
                    ((IDisposable)a.Value).Dispose();
                }
            }

            m_LoadedAssets.Remove(categoryName_);
        }

        public void UnloadAll()
        {
            foreach (var pair in m_LoadedAssets)
            {
                Unload(pair.Key);
            }
        }


        internal void AddAsset(Asset asset_)
        {
            m_Assets.Add(asset_);
        }

        internal void OnDeviceReset(object sender, EventArgs e)
        {
            GraphicsDevice device = sender as GraphicsDevice;

            //TODO : useful or buggy ?
            /*foreach (var pair in m_LoadedAssets)
            {
                foreach (var pair2 in pair.Value)
                {
                    if (pair2.Value is IDisposable)
                    {
                        ((IDisposable)pair2.Value).Dispose();
                    }
                }
            }*/

            foreach (var a in m_Assets)
            {
                a.OnDeviceReset(device);
            }
        }


    }
}
