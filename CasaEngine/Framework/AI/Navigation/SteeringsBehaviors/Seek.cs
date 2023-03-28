using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation.SteeringsBehaviors;

public class Seek : SteeringBehavior
{
    protected internal Vector3 targetPosition;
    protected internal Vector3 force;

    public Seek(string name, MovingObject owner, float modifier)
        : base(name, owner, modifier) { }

    public Vector3 TargetPosition
    {
        get => targetPosition;
        set => targetPosition = ConstraintVector(value);
    }

    public Vector3 Force => force;

    public override Vector3 Calculate()
    {
        Vector3 desiredVelocity;

        desiredVelocity = Vector3.Normalize(targetPosition - ConstraintVector(owner.Position)) * owner.MaxSpeed;
        force = desiredVelocity - ConstraintVector(owner.Velocity);

        return force;
    }

}