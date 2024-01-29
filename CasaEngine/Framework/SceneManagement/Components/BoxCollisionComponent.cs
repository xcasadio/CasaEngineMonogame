using System.ComponentModel;
using CasaEngine.Core.Shapes;

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("Box collision")]
public class BoxCollisionComponent : PhysicsComponent
{
    public Box? Box
    {
        get => Shape as Box;
        private init => Shape = value;
    }

    public BoxCollisionComponent() : base()
    {
        Box = new Box();
    }

    public BoxCollisionComponent(BoxCollisionComponent other) : base(other)
    {
    }

    public override BoxCollisionComponent Clone()
    {
        return new BoxCollisionComponent(this);
    }
}