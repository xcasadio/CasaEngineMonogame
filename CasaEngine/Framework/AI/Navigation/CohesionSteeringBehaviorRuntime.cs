using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class CohesionSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public CohesionSteeringBehaviorRuntime(string name = "cohesion", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float NeighborRadius { get; set; } = 150.0f;

    public int LastNeighborCount { get; private set; }

    public Vector3 LastCenterOfMass { get; private set; }

    public List<Vector3> LastNeighborPositions { get; } = [];

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        Vector3 centerOfMass = Vector3.Zero;
        LastNeighborPositions.Clear();
        LastNeighborCount = 0;

        foreach (Entity entity in agent.FindNeighborEntities(NeighborRadius))
        {
            if (!agent.TryGetEntityMotion(entity, out Vector3 otherPosition, out _, out _))
            {
                continue;
            }

            centerOfMass += otherPosition;
            LastNeighborCount++;
            LastNeighborPositions.Add(otherPosition);
        }

        if (LastNeighborCount == 0)
        {
            LastCenterOfMass = Vector3.Zero;
            return Vector3.Zero;
        }

        centerOfMass /= LastNeighborCount;
        LastCenterOfMass = centerOfMass;

        Vector3 toCenter = centerOfMass - kinematics.Position;
        if (toCenter.LengthSquared() <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        Vector3 desiredVelocity = Vector3.Normalize(toCenter) * kinematics.MaxSpeed;
        return desiredVelocity - kinematics.Velocity;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new CohesionSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            NeighborRadius = NeighborRadius,
        };
    }
}