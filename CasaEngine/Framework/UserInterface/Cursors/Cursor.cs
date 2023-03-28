using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UserInterface.Cursors;
#if (WINDOWS)

public class Cursor : Asset
{

    public GraphicsDevice GraphicsDevice { get; private set; }

    public System.Windows.Forms.Cursor Resource { get; private set; }

    public Cursor(GraphicsDevice graphicsDevice, string fileName)
    {
        GraphicsDevice = graphicsDevice;
        Name = Path.GetFileName(fileName);
        FileName = fileName;
        if (File.Exists(FileName) == false)
        {
            throw new ArgumentException("Failed to load cursor: File " + FileName + " does not exists!", nameof(fileName));
        }
        try
        {
            Resource = EngineComponents.AssetContentManager.Load<System.Windows.Forms.Cursor>(FileName, GraphicsDevice);
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Content Manager: Content manager disposed");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to load cursor: " + fileName, e);
        }
    } // Cursor

    internal override void OnDeviceReset(GraphicsDevice device)
    {
        if (Resource == null)
        {
            Resource = EngineComponents.AssetContentManager.Load<System.Windows.Forms.Cursor>(FileName, GraphicsDevice);
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