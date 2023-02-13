using CasaEngine.Core_Systems.Math.Shape2D;
using CasaEngine.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Pong;

public class Player
{
    private ShapeRectangle _shapeRectangle;
    public Vector2 Position;
    private readonly int _width;
    private readonly int _height;

    public Player()
    {
        _width = 20;
        _height = 80;
        _shapeRectangle = new ShapeRectangle((int)Position.X, (int)Position.Y, _width, _height);
    }

    public void Update(float elapsedTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Down))
        {
            Position.Y -= 100.0f * elapsedTime;
        }
        else if (Keyboard.GetState().IsKeyDown(Keys.Up))
        {
            Position.Y += 100.0f * elapsedTime;
        }

        _shapeRectangle.Location = new Point((int)Position.X + _width / 2, (int)Position.Y + _height / 2);
    }

    public void Draw(float elapsedTime, ShapeRendererComponent shapeRendererComponent)
    {
        shapeRendererComponent.AddShape2DObject(_shapeRectangle, Color.Blue);
    }
}