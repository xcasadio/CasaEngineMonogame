using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UserInterface.Cursors;

#if (WINDOWS)

public class Cursor : Asset
{
    public GraphicsDevice GraphicsDevice { get; private set; }

    public System.Windows.Forms.Cursor Resource { get; private set; }

    public Cursor(GraphicsDevice graphicsDevice, string fileName, AssetContentManager assetContentManager)
    {
        GraphicsDevice = graphicsDevice;
        AssetInfo.Name = Path.GetFileName(fileName);
        AssetInfo.FileName = fileName;
        if (File.Exists(AssetInfo.FileName) == false)
        {
            throw new ArgumentException($"Failed to load cursor: File {AssetInfo.FileName} does not exists!", nameof(fileName));
        }
        try
        {
            Resource = assetContentManager.Load<System.Windows.Forms.Cursor>(AssetInfo.FileName, GraphicsDevice);
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Content Manager: Content manager disposed");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed to load cursor: {fileName}", e);
        }
    } // Cursor

    internal override void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager)
    {
        if (Resource == null)
        {
            Resource = assetContentManager.Load<System.Windows.Forms.Cursor>(AssetInfo.FileName, GraphicsDevice);
        }
    } // RecreateResource
} // Cursor
#endif