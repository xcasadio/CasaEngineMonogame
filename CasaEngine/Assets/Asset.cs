using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using XNAFinalEngine.Helpers;
using CasaEngine.Game;
using CasaEngineCommon.Design;
using System.IO;
using System.Xml;

namespace CasaEngine.Asset
{
    /// <summary>
    /// 
    /// </summary>
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
        private static long uniqueIdCounter = long.MinValue;

        // The asset name.
        private string name;



        /// <summary>
        /// Identification number. Every asset has a unique ID.
        /// </summary>
        public long Id { get; private set; }

        /// <summary>
        /// Asset Filename (if any).
        /// </summary>
        public string Filename { get; protected set; }

        /// <summary>
        /// The name of the asset.
        /// </summary>
        /// <remarks>
        /// The name is not unique. 
        /// Consequently it can be used to identify the asset, use Id instead.
        /// </remarks>
        public virtual string Name
        {
            get { return name; }
            set
            {
                if (!string.IsNullOrEmpty(value) && name != value)
                {
                    name = value;
                }
            }
        } // Name



        /// <summary>
        /// 
        /// </summary>
        protected Asset()
        {
            // Create a unique ID
            Id = uniqueIdCounter;
            uniqueIdCounter++;

            Engine.Instance.AssetContentManager.AddAsset(this);
        }



        /// <summary>
        /// Useful when the XNA device is disposed.
        /// </summary>
        internal virtual void OnDeviceReset(GraphicsDevice device_)
        {
            //override
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public virtual void Load(BinaryReader br_, SaveOption option_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Load(XmlElement el_, SaveOption option_)
        {

        }


    }
}
