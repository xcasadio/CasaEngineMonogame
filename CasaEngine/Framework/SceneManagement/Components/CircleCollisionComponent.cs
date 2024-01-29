using System.ComponentModel;
using CasaEngine.Core.Shapes;

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("Circle collision")]
public class CircleCollisionComponent : Physics2dComponent
{
    public ShapeCircle? Circle
    {
        get => Shape as ShapeCircle;
        private init => Shape = value;
    }

    public CircleCollisionComponent() : base()
    {
        Circle = new ShapeCircle();
    }

    public CircleCollisionComponent(CircleCollisionComponent other) : base(other)
    {
        Circle = other.Circle;
    }

    public override CircleCollisionComponent Clone()
    {
        return new CircleCollisionComponent(this);
    }
}