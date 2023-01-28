
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework;
using CasaEngine.Asset;
using Microsoft.Xna.Framework.Graphics;



namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Represents an image that could be used in buttons, image boxs, etc.
    /// </summary>
    public class Glyph
    {


        // Texture.
        private CasaEngine.Asset.Texture texture;

        // Size Mode (Normal, Streched, Centered and Auto).
        // Auto mode changes the control's width and height to the texture's dimentions.
        private SizeMode sizeMode = SizeMode.Normal;

        // Allows to cut the texture.
        private Rectangle sourceRectangle = Rectangle.Empty;

        // Color.
        private Color color = Color.White;

        // Offset.
        private Point offset = Point.Zero;



        /// <summary>
        /// Glyph color (used for coloring the glyph's texture)
        /// </summary>
        public Color Color { get { return color; } set { color = value; } }

        /// <summary>
        /// Glyph offset.
        /// </summary>
        public Point Offset { get { return offset; } set { offset = value; } }

        /// <summary>
        /// Texture.
        /// </summary>
        public CasaEngine.Asset.Texture Texture
        {
            get { return texture; }
            set
            {
                texture = value;
                sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
            }
        } // Texture

        /// <summary>
        /// Allows to cut the texture.
        /// </summary>
        public Rectangle SourceRectangle
        {
            get { return sourceRectangle; }
            set
            {
                if (texture != null)
                {
                    int left = value.Left;
                    int top = value.Top;
                    int width = value.Width;
                    int height = value.Height;

                    if (left < 0)
                        left = 0;
                    if (top < 0)
                        top = 0;
                    if (width > texture.Width)
                        width = texture.Width;
                    if (height > texture.Height)
                        height = texture.Height;
                    if (left + width > texture.Width)
                        width = (texture.Width - left);
                    if (top + height > texture.Height)
                        height = (texture.Height - top);

                    sourceRectangle = new Rectangle(left, top, width, height);
                }
                else
                {
                    sourceRectangle = Rectangle.Empty;
                }
            }
        } // SourceRectangle

        /// <summary>
        /// Size Mode (Normal, Streched, Fit, Centered and Auto).
        /// </summary>
        public SizeMode SizeMode
        {
            get { return sizeMode; }
            set { sizeMode = value; }
        } // SizeMode



        /// <summary>
        /// Represents an image on a button.
        /// </summary>
        public Glyph(CasaEngine.Asset.Texture _texture)
        {
            Texture = _texture;
        } // Glyph


    } // Glyph
} // XNAFinalEngine.UserInterface