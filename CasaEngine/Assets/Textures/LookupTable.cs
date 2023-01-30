
/*
 Based in the work of Serge.R (deus.verus@gmail.com)
 http://www.messy-mind.net/2008/fast-gpu-color-transforms
 Modify by: Schneider, José Ignacio
*/


using Microsoft.Xna.Framework.Graphics;

using CasaEngine.Game;

namespace CasaEngine.Asset
{

    public class LookupTable : Asset
    {
        static private readonly string AssetContentManagerCategoryName = "temp";


        public GraphicsDevice GraphicsDevice { get; private set; }

        public Texture3D Resource { get; private set; }

        public int Size { get; private set; }

        public static string[] Filenames { get; private set; }



        public LookupTable(GraphicsDevice graphicsDevice_, string filename)
        {
            Name = filename;
            Filename = Engine.Instance.ProjectManager.ProjectPath + filename;
            if (File.Exists(Filename) == false)
            {
                throw new ArgumentException("Failed to load texture: File " + Filename + " does not exists!", "filename");
            }
            try
            {
                Create(graphicsDevice_, filename);
            }
            catch (ObjectDisposedException)
            {
                throw new InvalidOperationException("Content Manager: Content manager disposed");
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Failed to load lookup texture: " + filename, e);
            }
        } // LookupTable

        private LookupTable()
        {
            Name = "";
            Filename = "";
        } // LookupTable



        private void Create(GraphicsDevice graphicsDevice_, string filename)
        {
            Texture lookupTableTexture2D = new Texture(graphicsDevice_, filename);
            // SideSize is inaccurate because Math.Pow is a bad way to calculate cube roots.
            int sideSize = (int)System.Math.Pow(lookupTableTexture2D.Width * lookupTableTexture2D.Height, 1 / 3.0);
            // hence this second step to snap to nearest power of 2.
            Size = (int)System.Math.Pow(2, System.Math.Round(System.Math.Log(sideSize, 2)));
            //Create the cube lut and dump the 2d lut into it
            Color[] colors = new Color[Size * Size * Size];
            Resource = new Texture3D(graphicsDevice_, Size, Size, Size, false, SurfaceFormat.Color);
            lookupTableTexture2D.Resource.GetData(colors);
            Resource.SetData(colors);
            Resource.Name = filename;

            // Dispose the temporal content manager and restore the user content manager.
            Engine.Instance.AssetContentManager.Unload(AssetContentManagerCategoryName);
        } // Create





        public static LookupTable Identity(GraphicsDevice graphicsDevice_, int size)
        {
            return new LookupTable { Name = "Identity", Filename = "", Resource = IdentityTexture(graphicsDevice_, size), Size = size };
        } // Identity

        private static Texture3D IdentityTexture(GraphicsDevice graphicsDevice_, int size)
        {
            Color[] colors = new Color[size * size * size];
            Texture3D lookupTableTexture = new Texture3D(graphicsDevice_, size, size, size, false, SurfaceFormat.Color);
            for (int redIndex = 0; redIndex < size; redIndex++)
            {
                for (int greenIndex = 0; greenIndex < size; greenIndex++)
                {
                    for (int blueIndex = 0; blueIndex < size; blueIndex++)
                    {
                        float red = (float)redIndex / size;
                        float green = (float)greenIndex / size;
                        float blue = (float)blueIndex / size;
                        Color col = new Color(red, green, blue);
                        colors[redIndex + (greenIndex * size) + (blueIndex * size * size)] = col;
                    }
                }
            }
            lookupTableTexture.SetData(colors);
            return lookupTableTexture;
        } // IdentityTexture



        public static Texture LookupTableToTexture(GraphicsDevice graphicsDevice_, LookupTable lookupTable)
        {
            // Calculate closest to square proportions for 2d table
            // We assume power-of-two sides, otherwise I don't know
            int size = lookupTable.Resource.Width;
            int side1 = size * size;
            int side2 = size;
            while (side1 / 2 >= side2 * 2)
            {
                side1 /= 2;
                side2 *= 2;
            }

            // Dump 3D texture into 2D texture.
            Color[] colors = new Color[size * size * size];
            Texture2D lookupTable2DTexture = new Texture2D(graphicsDevice_, side1, side2, false, SurfaceFormat.Color);
            lookupTable.Resource.GetData(colors);
            lookupTable2DTexture.SetData(colors);
            return new Texture(lookupTable2DTexture) { Name = lookupTable.Name + "-Texture" };
        } // LookupTextureToTexture



        protected override void DisposeManagedResources()
        {
            // This type of resource can be disposed ignoring the content manager.
            base.DisposeManagedResources();
            Resource.Dispose();
        } // DisposeManagedResources



        internal override void OnDeviceReset(GraphicsDevice device_)
        {
            if (string.IsNullOrEmpty(Filename))
            {
                Resource = IdentityTexture(device_, Size);
            }
            else
                Create(device_, Filename.Substring(30)); // Removes "Textures\\"

            GraphicsDevice = device_;
        } // RecreateResource


    } // LookupTable
} // CasaEngine.Asset
