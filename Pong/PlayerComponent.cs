using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Shapes;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Component = CasaEngine.Framework.Entities.Component;

namespace Pong;

[DisplayName("PlayerComponent")]
public class PlayerComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Custom + 1;

    private readonly ShapeRectangle _shapeRectangle;
    private readonly int _width;
    private readonly int _height;

    public PlayerComponent(Entity owner) : base(owner, ComponentId)
    {
        _width = 20;
        _height = 80;
        _shapeRectangle = new ShapeRectangle((int)owner.Coordinates.Position.X, (int)owner.Coordinates.Position.Y, _width, _height);
    }

    public override void Initialize(CasaEngineGame game)
    {
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

    }

    public override void Load(JsonElement element)
    {

    }
}