using System.Linq;
using System.Text.Json;
using CasaEngine.Core.Game;
using CasaEngine.Core.Math.Shape2D;
using CasaEngine.Entities;
using CasaEngine.Entities.Components;
using CasaEngine.Game;
using CasaEngine.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Pong;

public class PlayerComponent : Component
{
    public static readonly int ComponentId = 1001;

    private ShapeRectangle _shapeRectangle;
    private readonly int _width;
    private readonly int _height;
    private ShapeRendererComponent _shapeRendererComponent;

    public PlayerComponent(Entity owner) : base(owner, ComponentId)
    {
        _width = 20;
        _height = 80;
        _shapeRectangle = new ShapeRectangle((int)owner.Coordinates.Position.X, (int)owner.Coordinates.Position.Y, _width, _height);
    }

    public override void Initialize()
    {
        _shapeRendererComponent = Engine.Instance.Game.Components.First(x => x is ShapeRendererComponent) as ShapeRendererComponent;
    }

    public override void Update(float elapsedTime)
    {
        float y = Owner.Coordinates.LocalPosition.Y;

        if (Keyboard.GetState().IsKeyDown(Keys.Down))
        {
            y += 100.0f * elapsedTime;
        }
        else if (Keyboard.GetState().IsKeyDown(Keys.Up))
        {
            y -= 100.0f * elapsedTime;
        }

        Owner.Coordinates.LocalPosition =
            new Vector3(Owner.Coordinates.LocalPosition.X, y, Owner.Coordinates.LocalPosition.Z);
        _shapeRectangle.Location = new Point((int)Owner.Coordinates.LocalPosition.X + _width / 2, (int)Owner.Coordinates.LocalPosition.Y + _height / 2);
    }

    public override void Draw()
    {
        _shapeRendererComponent.AddShape2DObject(_shapeRectangle, Color.Blue);
    }

    public override void Load(JsonElement element)
    {

    }
}