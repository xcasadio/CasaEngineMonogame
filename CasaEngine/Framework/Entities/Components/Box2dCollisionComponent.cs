using System.ComponentModel;
using CasaEngine.Core.Shapes;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Box 2d collision")]
public class Box2dCollisionComponent : Physics2dComponent
{
    public ShapeRectangle? Rectangle
    {
        get => Shape as ShapeRectangle;
        private init => Shape = value;
    }

    public Box2dCollisionComponent() : base()
    {
        Rectangle = new ShapeRectangle();
    }

    public Box2dCollisionComponent(Box2dCollisionComponent other) : base(other)
    {
        Rectangle = other.Rectangle;
    }

    public override Box2dCollisionComponent Clone()
    {
        return new Box2dCollisionComponent(this);
    }
}