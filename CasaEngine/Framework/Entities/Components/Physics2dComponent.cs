using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

public abstract class Physics2dComponent : PhysicsBaseComponent
{
    protected Physics2dComponent()
    {
        InitializePhysicsDefinition();
    }

    protected Physics2dComponent(Physics2dComponent other) : base(other)
    {
        InitializePhysicsDefinition();
    }

    private void InitializePhysicsDefinition()
    {
        PhysicsDefinition.LinearFactor = new Vector3(1, 1, 0);
        PhysicsDefinition.AngularFactor = new Vector3(0, 0, 1);
    }
}