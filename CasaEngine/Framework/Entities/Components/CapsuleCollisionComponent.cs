using System.ComponentModel;
using CasaEngine.Core.Shapes;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Capsule collision")]
public class CapsuleCollisionComponent : PhysicsComponent
{
    public Capsule? Capsule
    {
        get => Shape as Capsule;
        private init => Shape = value;
    }

    public CapsuleCollisionComponent() : base()
    {
        Capsule = new Capsule();
    }

    public CapsuleCollisionComponent(CapsuleCollisionComponent other) : base(other)
    {
    }

    public override CapsuleCollisionComponent Clone()
    {
        return new CapsuleCollisionComponent(this);
    }
}