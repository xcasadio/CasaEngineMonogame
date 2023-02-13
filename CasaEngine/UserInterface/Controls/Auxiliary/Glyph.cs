
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Assets.Textures;

namespace CasaEngine.UserInterface.Controls.Auxiliary
{

    public class Glyph
    {


        // Texture.
        private Texture _texture;

        // Size Mode (Normal, Streched, Centered and Auto).
        // Auto mode changes the control's width and height to the texture's dimentions.
        private SizeMode _sizeMode = SizeMode.Normal;

        // Allows to cut the texture.
        private Rectangle _sourceRectangle = Rectangle.Empty;

        // Color.
        private Color _color = Color.White;

        // Offset.
        private Point _offset = Point.Zero;



        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        public Point Offset
        {
            get => _offset;
            set => _offset = value;
        }

        public Texture Texture
        {
            get => _texture;
            set
            {
                _texture = value;
                _sourceRectangle = new Rectangle(0, 0, _texture.Width, _texture.Height);
            }
        } // Texture

        public Rectangle SourceRectangle
        {
            get => _sourceRectangle;
            set
            {
                if (_texture != null)
                {
                    var left = value.Left;
                    var top = value.Top;
                    var width = value.Width;
                    var height = value.Height;

                    if (left < 0)
                    {
                        left = 0;
                    }

                    if (top < 0)
                    {
                        top = 0;
                    }

                    if (width > _texture.Width)
                    {
                        width = _texture.Width;
                    }

                    if (height > _texture.Height)
                    {
                        height = _texture.Height;
                    }

                    if (left + width > _texture.Width)
                    {
                        width = (_texture.Width - left);
                    }

                    if (top + height > _texture.Height)
                    {
                        height = (_texture.Height - top);
                    }

                    _sourceRectangle = new Rectangle(left, top, width, height);
                }
                else
                {
                    _sourceRectangle = Rectangle.Empty;
                }
            }
        } // SourceRectangle

        public SizeMode SizeMode
        {
            get => _sizeMode;
            set => _sizeMode = value;
        } // SizeMode



        public Glyph(Texture texture)
        {
            Texture = texture;
        } // Glyph


    } // Glyph
} // XNAFinalEngine.UserInterface