using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using CasaEngine.Game;
using Microsoft.Xna.Framework.Graphics;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Asset.Cursors
{
#if (WINDOWS)

    /// <summary>
    /// Cursor
    /// </summary>
    public class Cursor : Asset
    {

        /// <summary>
        /// Gets
        /// </summary>
        public GraphicsDevice GraphicsDevice { get; private set; }

        /// <summary>
        /// Internal X Document.
        /// </summary>
        public System.Windows.Forms.Cursor Resource { get; private set; }



        /// <summary>
        /// Load a cursor.
        /// </summary>
        /// <param name="filename">>The filename must be relative and be a valid file in the Cursors directory</param>
        public Cursor(GraphicsDevice graphicsDevice_, string filename)
        {
            GraphicsDevice = graphicsDevice_;
            Name = Path.GetFileName(filename);
            Filename = filename;
            if (File.Exists(Filename) == false)
            {
                throw new ArgumentException("Failed to load cursor: File " + Filename + " does not exists!", "filename");
            }
            try
            {
                Resource = Engine.Instance.AssetContentManager.Load<System.Windows.Forms.Cursor>(Filename, GraphicsDevice);
            }
            catch (ObjectDisposedException)
            {
                throw new InvalidOperationException("Content Manager: Content manager disposed");
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Failed to load cursor: " + filename, e);
            }
        } // Cursor



        /// <summary>
        /// Useful when the XNA device is disposed.
        /// </summary>
        internal override void OnDeviceReset(GraphicsDevice device_)
        {
            if (Resource == null)
                Resource = Engine.Instance.AssetContentManager.Load<System.Windows.Forms.Cursor>(Filename, GraphicsDevice);
        } // RecreateResource



        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public override void Load(BinaryReader br_, SaveOption option_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(XmlElement el_, SaveOption option_)
        {

        }


    } // Cursor
#endif
}
