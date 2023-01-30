using CasaEngine.Game;
using Microsoft.Xna.Framework.Graphics;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Asset.Cursors
{
#if (WINDOWS)

    public class Cursor : Asset
    {

        public GraphicsDevice GraphicsDevice { get; private set; }

        public System.Windows.Forms.Cursor Resource { get; private set; }



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



        internal override void OnDeviceReset(GraphicsDevice device_)
        {
            if (Resource == null)
                Resource = Engine.Instance.AssetContentManager.Load<System.Windows.Forms.Cursor>(Filename, GraphicsDevice);
        } // RecreateResource



        public override void Load(BinaryReader br_, SaveOption option_)
        {

        }

        public override void Load(XmlElement el_, SaveOption option_)
        {

        }


    } // Cursor
#endif
}
