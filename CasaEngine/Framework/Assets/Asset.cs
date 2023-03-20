using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets
{
    public abstract class Asset : Disposable
    {
        public event EventHandler? Disposed;

        private static int _uniqueIdCounter = int.MinValue;

        // The asset name.
        private string _name;

        public long Id { get; private set; }

        public string Filename { get; protected set; }

        public string Name
        {
            get => _name;
            set
            {
                if (!string.IsNullOrEmpty(value) && _name != value)
                {
                    _name = value;
                }
            }
        } // Name

        protected Asset()
        {
            // Create a unique ID
            Id = _uniqueIdCounter;
            _uniqueIdCounter++;

            EngineComponents.AssetContentManager.AddAsset(this);
        }

        internal virtual void OnDeviceReset(GraphicsDevice device)
        {
            //override
        }

        public virtual void Load(BinaryReader br, SaveOption option)
        {

        }

        public virtual void Load(XmlElement el, SaveOption option)
        {

        }
    }
}
