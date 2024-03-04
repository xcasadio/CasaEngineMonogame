using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Loaders;

internal class NeoForceCursorLoader : IAssetLoader
{
    [DllImport("User32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr LoadCursorFromFile(string str);

    // Thanks Hans Passant!
    // http://stackoverflow.com/questions/4305800/using-custom-colored-cursors-in-a-c-windows-application
    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        var tPath = Path.GetTempFileName();

        var handle = LoadCursorFromFile(fileName);
        var c = new Cursor(handle);
        var hs = new Vector2(c.HotSpot.X, c.HotSpot.Y);
        var w = c.Size.Width;
        var h = c.Size.Height;
        var neoForceCursor = new GUI.Neoforce.Cursor(tPath, hs, w, h);
        c.Dispose();

        using (var icon = Icon.ExtractAssociatedIcon(fileName))
        {
            using (var b = icon.ToBitmap())
            {
                neoForceCursor.CursorTexture = new Texture2D(assetContentManager.GraphicsDevice, w, h);
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var color = b.GetPixel(x, y);
                        pixels[x + y * w] = new Color(color.R, color.G, color.B, color.A);
                    }
                }

                neoForceCursor.CursorTexture.SetData(pixels);
                b.Dispose();
            }

            icon.Dispose();
        }

        File.Delete(tPath);

        return neoForceCursor;
    }

    public bool IsFileSupported(string fileName)
    {
        return Path.GetExtension(fileName) == ".cur";
    }
}