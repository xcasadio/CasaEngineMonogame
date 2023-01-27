using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Asset
{
    /// <summary>
    /// 
    /// </summary>
    public class AssetContentManager
    {
        #region Fields

        private List<Asset> m_Assets = new List<Asset>();

        private Dictionary<string, Dictionary<string, object>> m_LoadedAssets = new Dictionary<string, Dictionary<string, object>>();
        private Dictionary<Type, IAssetLoader> m_AssetLoader = new Dictionary<Type, IAssetLoader>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string RootDirectory
        {
            get;
            set;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device_"></param>
        public AssetContentManager()
        {
            RootDirectory = Environment.CurrentDirectory;            
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device_"></param>
        public void Initialize(GraphicsDevice device_)
        {
            device_.DeviceReset += new EventHandler<EventArgs>(OnDeviceReset);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        /// <param name="loader_"></param>
        public void RegisterAssetLoader(Type type_, IAssetLoader loader_)
        {
            m_AssetLoader.Add(type_, loader_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath_"></param>
        /// <returns></returns>
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
                    T asset = (T) m_AssetLoader[type].LoadAsset(filePath_, device_);
                    categoryAssetList.Add(filePath_, asset);
                    return asset;
                }
            }

            throw new InvalidOperationException("IAssetLoader not found for the type " + type.FullName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="categoryName_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        public void UnloadAll()
        {
            foreach (var pair in m_LoadedAssets)
            {
                Unload(pair.Key);
            }
        }

        #region Asset

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asset_"></param>
        internal void AddAsset(Asset asset_)
        {
            m_Assets.Add(asset_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

            foreach (var a in  m_Assets)
            {
                a.OnDeviceReset(device);
            }
        }

        #endregion

        #endregion
    }
}
