using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UserInterface.Cursors
{
#if (WINDOWS)

    public class Cursor : Asset
    {

        public GraphicsDevice GraphicsDevice { get; private set; }

        public System.Windows.Forms.Cursor Resource { get; private set; }

        public Cursor(GraphicsDevice graphicsDevice, string filename)
        {
            GraphicsDevice = graphicsDevice;
            Name = Path.GetFileName(filename);
            Filename = filename;
            if (File.Exists(Filename) == false)
            {
                throw new ArgumentException("Failed to load cursor: File " + Filename + " does not exists!", nameof(filename));
            }
            try
            {
                Resource = Game.Engine.Instance.AssetContentManager.Load<System.Windows.Forms.Cursor>(Filename, GraphicsDevice);
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

        internal override void OnDeviceReset(GraphicsDevice device)
        {
            if (Resource == null)
            {
                Resource = Game.Engine.Instance.AssetContentManager.Load<System.Windows.Forms.Cursor>(Filename, GraphicsDevice);
            }
        } // RecreateResource

        public override void Load(BinaryReader br, SaveOption option)
        {

        }

        public override void Load(XmlElement el, SaveOption option)
        {

        }

    } // Cursor
#endif
}
