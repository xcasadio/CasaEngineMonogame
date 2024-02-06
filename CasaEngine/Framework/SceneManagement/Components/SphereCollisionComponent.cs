using System.ComponentModel;
using CasaEngine.Core.Shapes;

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("Sphere collision")]
public class SphereCollisionComponent : PhysicsComponent
{
    public Sphere? Sphere
    {
        get => Shape as Sphere;
        private init => Shape = value;
    }

    public SphereCollisionComponent() : base()
    {
        Sphere = new Sphere();
    }

    public SphereCollisionComponent(SphereCollisionComponent other) : base(other)
    {
    }

    public override SphereCollisionComponent Clone()
    {
        return new SphereCollisionComponent(this);
    }
}