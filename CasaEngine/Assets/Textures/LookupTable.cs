
#region License
/*
 Based in the work of Serge.R (deus.verus@gmail.com)
 http://www.messy-mind.net/2008/fast-gpu-color-transforms
 Modify by: Schneider, José Ignacio
*/
#endregion

#region Using directives
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using CasaEngine.Game;
#endregion

namespace CasaEngine.Asset
{

    /// <summary>
    /// Lookup Table.
    /// </summary>
    /// <remarks>
    /// XNA Final Engine uses three-dimensional lookup tables for real-time color processing and allows having up to two lookup tables at the same time.
    /// If two lookup tables are active then the system will lerp the colors by the LerpLookupTablesAmount property value. 
    /// If the LerpOriginalColorAmount has a value different to 0 the system will lerp between the color corrected by the lookup tables and the original color.
    /// 
    /// Unreal Engine 3 and Cryengine 3 both use a 16x16x16 lookup table.
    /// 
    /// Lookup tables (LUTs) are an excellent technique for optimizing the evaluation of functions that are expensive to compute and inexpensive to cache.
    /// By precomputing the evaluation of a function over a domain of common inputs, expensive runtime operations can be replaced with inexpensive table lookups.
    /// If the table lookups can be performed faster than computing the results from scratch (or if the function is repeatedly queried at the same input),
    /// then the use of a lookup table will yield significant performance gains. 
    /// For data requests that fall between the table's samples, an interpolation algorithm can generate reasonable approximations by averaging nearby samples.
    /// 
    /// About this subject:
    /// http://http.developer.nvidia.com/GPUGems/gpugems_ch22.html
    /// http://http.developer.nvidia.com/GPUGems2/gpugems2_chapter24.html
    /// 
    /// Maybe this paper is useful: Reducing the Cost of Lookup Table Based Color Transformations (Raja Balasubramanian)
    /// I don’t read it yet because I’m happy with the performance of the current lookup tables.
    /// 
    /// One more thing: a lookup table doesn’t reduce color precision thanks to the GPU’s linear interpolation. However you can lose precision in the transformation itself.
    /// </remarks>
    public class LookupTable : Asset
    {
        static private readonly string AssetContentManagerCategoryName = "temp";

        #region Properties

        /// <summary>
        /// Gets GraphicsDevice
        /// </summary>
        public GraphicsDevice GraphicsDevice { get; private set; }

        /// <summary>
        /// Lookup Table Texture.
        /// </summary>
        public Texture3D Resource { get; private set; }

        /// <summary>
        /// Side size.
        /// </summary>
        public int Size { get; private set; }
        
        /// <summary>
        ///  A list with all texture' filenames on the lookup table directory.
        /// </summary>
        /// <remarks>
        /// If there are memory limitations, this list could be eliminated for the release version.
        /// This is use only useful for the editor.
        /// </remarks>
        public static string[] Filenames { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Loads lookup table from image file.
        /// </summary>
        /// <param name="filename">Texture path.</param>
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

        /// <summary>
        /// Dummy constructor for static methods.
        /// </summary>
        private LookupTable()
        {
            Name = "";
            Filename = "";
        } // LookupTable

        #endregion

        #region Create

        /// <summary>
        /// Creates the lookup table.
        /// </summary>
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

        #endregion

        #region Static Constructor

        #endregion

        #region Identity

        /// <summary>
        /// Gives an identity lookup table.
        /// </summary>
        /// <remarks>O(n) operation.</remarks>
        /// <param name="size">Side size. 64 is a good value. Total size is size^3</param>
        public static LookupTable Identity(GraphicsDevice graphicsDevice_, int size)
        {
            return new LookupTable { Name = "Identity", Filename = "", Resource = IdentityTexture(graphicsDevice_, size), Size = size };
        } // Identity

        /// <summary>
        /// Gives the resources of the identity lookup table.
        /// </summary>
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

        #endregion

        #region Lookup Table To Texture

        /// <summary>
        /// Gives a 2D representation of a lookup table.
        /// </summary>
        /// <param name="lookupTable">Lookup table</param>
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

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // This type of resource can be disposed ignoring the content manager.
            base.DisposeManagedResources();
            Resource.Dispose();
        } // DisposeManagedResources

        #endregion

        #region Recreate Resource

        /// <summary>
        /// Useful when the XNA device is disposed.
        /// </summary>
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

        #endregion

    } // LookupTable
} // CasaEngine.Asset
