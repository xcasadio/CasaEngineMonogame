using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;

namespace CasaEngine.Game
{
    /// <summary>
    /// Same as ContentManager but we can unload specific asset
    /// </summary>
    public class CustomContentManager
        : ContentManager
    {
        // Cache of all the loaded content
        Dictionary<string, object> loaded = new Dictionary<string, object>();
        List<IDisposable> disposableAssets = new List<IDisposable>();

        public CustomContentManager(IServiceProvider services)
            : base(services)
        {
        }

        public CustomContentManager(IServiceProvider services, string rootDirectory)
            : base(services, rootDirectory)
        {
        }

        // Load an asset and cache it
        public override T Load<T>(string assetName)
        {
            // Return the stored instance if there is one
            if (loaded.ContainsKey(assetName))
                return (T)loaded[assetName];

            // If there isn't, load a new one
            T read = base.ReadAsset<T>(assetName, RecordDisposableAsset);
            loaded.Add(assetName, read);

            return read;
        }

        // Load an asset and be guaranteed a clean copy of the object.
        // Note that if this function is used this copy of the asset cannot
        // be individually unloaded
        public T LoadFreshCopy<T>(string assetName)
        {
            return base.ReadAsset<T>(assetName, null);
        }

        void RecordDisposableAsset(IDisposable disposable)
        {
            disposableAssets.Add(disposable);
        }

        // Unload everything
        public override void Unload()
        {
            foreach (IDisposable disposable in disposableAssets)
                disposable.Dispose();

            loaded.Clear();
            disposableAssets.Clear();
        }

        // Unload a single asset
        public void UnloadAsset(string name)
        {
            if (loaded.ContainsKey(name))
            {
                if (loaded[name] is IDisposable && disposableAssets.Contains((IDisposable)loaded[name]))
                {
                    IDisposable disp = (IDisposable)loaded[name];
                    disposableAssets.Remove(disp);
                    disp.Dispose();
                }

                loaded.Remove(name);
            }
        }
    }
}
