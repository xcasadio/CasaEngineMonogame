using Microsoft.Xna.Framework.Content;

namespace CasaEngine.Game
{
    public class CustomContentManager
        : ContentManager
    {
        // Cache of all the loaded content
        readonly Dictionary<string, object> _loaded = new();
        readonly List<IDisposable> _disposableAssets = new();

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
            if (_loaded.ContainsKey(assetName))
            {
                return (T)_loaded[assetName];
            }

            // If there isn't, load a new one
            T read = ReadAsset<T>(assetName, RecordDisposableAsset);
            _loaded.Add(assetName, read);

            return read;
        }

        // Load an asset and be guaranteed a clean copy of the object.
        // Note that if this function is used this copy of the asset cannot
        // be individually unloaded
        public T LoadFreshCopy<T>(string assetName)
        {
            return ReadAsset<T>(assetName, null);
        }

        void RecordDisposableAsset(IDisposable disposable)
        {
            _disposableAssets.Add(disposable);
        }

        // Unload everything
        public override void Unload()
        {
            foreach (IDisposable disposable in _disposableAssets)
                disposable.Dispose();

            _loaded.Clear();
            _disposableAssets.Clear();
        }

        // Unload a single asset
        public void UnloadAsset(string name)
        {
            if (_loaded.ContainsKey(name))
            {
                if (_loaded[name] is IDisposable && _disposableAssets.Contains((IDisposable)_loaded[name]))
                {
                    IDisposable disp = (IDisposable)_loaded[name];
                    _disposableAssets.Remove(disp);
                    disp.Dispose();
                }

                _loaded.Remove(name);
            }
        }
    }
}
