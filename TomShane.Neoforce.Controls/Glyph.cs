using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TomShane.Neoforce.Controls;

public class Glyph
{

    public readonly Texture2D Image;
    public SizeMode SizeMode = SizeMode.Stretched;
    public Color Color = Color.White;
    public Point Offset = Point.Zero;
    public Rectangle SourceRect = Rectangle.Empty;

    public Glyph(Texture2D image)
    {
        Image = image;
    }

    public Glyph(Texture2D image, Rectangle sourceRect) : this(image)
    {
        SourceRect = sourceRect;
    }
}