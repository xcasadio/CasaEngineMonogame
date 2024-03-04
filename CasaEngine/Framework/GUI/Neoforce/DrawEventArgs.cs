using CasaEngine.Framework.GUI.Neoforce.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.GUI.Neoforce;

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