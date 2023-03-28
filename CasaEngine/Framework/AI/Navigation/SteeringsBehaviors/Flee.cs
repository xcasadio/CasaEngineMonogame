using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation.SteeringsBehaviors;

public class Flee : SteeringBehavior
{
    public const float AlwaysFlee = -1.0f;
    protected internal float fleeDistance;
    protected internal Vector3 fleePosition;

    public Flee(string name, MovingObject owner, float modifier)
        : this(name, owner, modifier, AlwaysFlee) { }

    public Flee(string name, MovingObject owner, float modifier, float fleeDistance)
        : base(name, owner, modifier)
    {
        this.fleeDistance = fleeDistance;
    }

    public float FleeDistance
    {
        get => fleeDistance;
        set => fleeDistance = value;
    }

    public Vector3 FleePosition
    {
        get => fleePosition;
        set => fleePosition = ConstraintVector(value);
    }

    public override Vector3 Calculate()
    {
        Vector3 desiredVelocity;
        Vector3 toTarget;

        toTarget = ConstraintVector(owner.Position) - fleePosition;

        //If the flee position is too far away, don´t flee from it
        if (fleeDistance != AlwaysFlee && toTarget.Length() > fleeDistance)
        {
            return Vector3.Zero;
        }

        //If the entity should flee, calculate the velocity
        desiredVelocity = Vector3.Normalize(toTarget) * owner.MaxSpeed;
        return desiredVelocity - ConstraintVector(owner.Velocity);
    }

}