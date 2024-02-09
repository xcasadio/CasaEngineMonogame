using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;

namespace TomShane.Neoforce.Controls;

public class DrawEventArgs : EventArgs
{

    public IRenderer Renderer;
    public Rectangle Rectangle = Rectangle.Empty;
    public GameTime GameTime;

    public DrawEventArgs()
    {
    }

    public DrawEventArgs(IRenderer renderer, Rectangle rectangle, GameTime gameTime)
    {
        Renderer = renderer;
        Rectangle = rectangle;
        GameTime = gameTime;
    }

}