
/*
 Based in the work of Serge.R (deus.verus@gmail.com)
 http://www.messy-mind.net/2008/fast-gpu-color-transforms
 Modify by: Schneider, José Ignacio
*/

using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Objects;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Textures;

public class LookupTable : ObjectBase, IAssetable
{
    private readonly AssetContentManager _assetContentManager;
    private static readonly string AssetContentManagerCategoryName = "LookupTableTemp";

    public GraphicsDevice GraphicsDevice { get; private set; }

    public Texture3D Resource { get; private set; }

    public int Size { get; private set; }

    public static string[] Filenames { get; private set; }

    public LookupTable(GraphicsDevice graphicsDevice, string filename, AssetContentManager assetContentManager)
    {
        _assetContentManager = assetContentManager;
        //Name = Path.GetFileName(filename);
        //FileName = filename;
        if (File.Exists(FileName) == false)
        {
            throw new ArgumentException($"Failed to load texture: File {FileName} does not exists!", nameof(filename));
        }
        try
        {
            Create(graphicsDevice, filename, assetContentManager);
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Content Manager: Content manager disposed");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed to load lookup texture: {filename}", e);
        }
    } // LookupTable

    private LookupTable()
    {
        //AssetInfo.Name = "";
        //AssetInfo.FileName = "";
    } // LookupTable

    private void Create(GraphicsDevice graphicsDevice, string filename, AssetContentManager assetContentManager)
    {
        var lookupTableTexture2D = new Texture(filename, _assetContentManager);
        // SideSize is inaccurate because Math.Pow is a bad way to calculate cube roots.
        var sideSize = (int)Math.Pow(lookupTableTexture2D.Width * lookupTableTexture2D.Height, 1 / 3.0);
        // hence this second step to snap to nearest power of 2.
        Size = (int)Math.Pow(2, Math.Round(Math.Log(sideSize, 2)));
        //Create the cube lut and dump the 2d lut into it
        var colors = new Color[Size * Size * Size];
        Resource = new Texture3D(graphicsDevice, Size, Size, Size, false, SurfaceFormat.Color);
        lookupTableTexture2D.Resource.GetData(colors);
        Resource.SetData(colors);
        Resource.Name = filename;

        // Dispose the temporal content manager and restore the user content manager.
        assetContentManager.Unload(AssetContentManagerCategoryName);
    } // Create

    public static LookupTable Identity(GraphicsDevice graphicsDevice, int size)
    {
        var lookupTable = new LookupTable { Resource = IdentityTexture(graphicsDevice, size), Size = size };
        //lookupTable.Name = "Identity";
        //lookupTable.FileName = "";
        return lookupTable;
    } // Identity

    private static Texture3D IdentityTexture(GraphicsDevice graphicsDevice, int size)
    {
        var colors = new Color[size * size * size];
        var lookupTableTexture = new Texture3D(graphicsDevice, size, size, size, false, SurfaceFormat.Color);
        for (var redIndex = 0; redIndex < size; redIndex++)
        {
            for (var greenIndex = 0; greenIndex < size; greenIndex++)
            {
                for (var blueIndex = 0; blueIndex < size; blueIndex++)
                {
                    var red = (float)redIndex / size;
                    var green = (float)greenIndex / size;
                    var blue = (float)blueIndex / size;
                    var col = new Color(red, green, blue);
                    colors[redIndex + greenIndex * size + blueIndex * size * size] = col;
                }
            }
        }
        lookupTableTexture.SetData(colors);
        return lookupTableTexture;
    } // IdentityTexture

    public static Texture LookupTableToTexture(GraphicsDevice graphicsDevice, LookupTable lookupTable, AssetContentManager assetContentManager)
    {
        // Calculate closest to square proportions for 2d table
        // We assume power-of-two sides, otherwise I don't know
        var size = lookupTable.Resource.Width;
        var side1 = size * size;
        var side2 = size;
        while (side1 / 2 >= side2 * 2)
        {
            side1 /= 2;
            side2 *= 2;
        }

        // Dump 3D texture into 2D texture.
        var colors = new Color[size * size * size];
        var lookupTable2DTexture = new Texture2D(graphicsDevice, side1, side2, false, SurfaceFormat.Color);
        lookupTable.Resource.GetData(colors);
        lookupTable2DTexture.SetData(colors);
        var texture = new Texture(lookupTable2DTexture);
        //texture.Name = $"{lookupTable.Name}-Texture";
        return texture;
    }

    protected void DisposeManagedResources()
    {
        // This type of resource can be disposed ignoring the content manager.
        //base.DisposeManagedResources();
        Resource.Dispose();
    }

    public void Dispose()
    {
        DisposeManagedResources();
    }

    public void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager)
    {
        if (string.IsNullOrEmpty(FileName))
        {
            Resource = IdentityTexture(device, Size);
        }
        else
        {
            Create(device, FileName.Substring(30), assetContentManager); // Removes "Textures\\"
        }

        GraphicsDevice = device;
    }

}
