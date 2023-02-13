using System.Xml;
using CasaEngine.Game;
using CasaEngine.Helpers;
using CasaEngineCommon.Design;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Assets
{
    public abstract
#if EDITOR
    partial
#endif
    class Asset
        : Disposable, ISaveLoad
    {

        public event EventHandler Disposed;



        // A simple but effective way of having unique ids.
        // We can have 18.446.744.073.709.551.616 game object creations before the system "collapse". Almost infinite in practice. 
        // If a more robust system is needed (networking/threading) then you can use the guid structure: http://msdn.microsoft.com/en-us/library/system.guid.aspx
        // However this method is slightly simpler, slightly faster and has slightly lower memory requirements.
        // If performance is critical consider the int type (4.294.967.294 unique values).
        private static long _uniqueIdCounter = long.MinValue;

        // The asset name.
        private string _name;



        public long Id { get; private set; }

        public string Filename { get; protected set; }

        public virtual string Name
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

            Engine.Instance.AssetContentManager.AddAsset(this);
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
