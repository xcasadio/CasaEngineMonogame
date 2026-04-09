using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class CohesionSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public CohesionSteeringBehaviorRuntime(string name = "cohesion", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float NeighborRadius { get; set; } = 150.0f;

    public string ExcludedEntityName { get; set; } = string.Empty;

    public int LastNeighborCount { get; private set; }

    public Vector3 LastCenterOfMass { get; private set; }

    public List<Vector3> LastNeighborPositions { get; } = [];

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        Vector3 centerOfMass = Vector3.Zero;
        LastNeighborPositions.Clear();
        LastNeighborCount = 0;

        foreach (SteeringNeighborSnapshot neighbor in agent.FindNeighborSnapshots(NeighborRadius))
        {
            Entity entity = neighbor.Entity;
            if (!string.IsNullOrWhiteSpace(ExcludedEntityName)
                && string.Equals(entity.Name, ExcludedEntityName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            centerOfMass += neighbor.Position;
            LastNeighborCount++;
            LastNeighborPositions.Add(neighbor.Position);
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
        Vector3 steering = desiredVelocity - kinematics.Velocity;
        if (steering.LengthSquared() <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        steering.Normalize();
        return steering;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new CohesionSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            NeighborRadius = NeighborRadius,
            ExcludedEntityName = ExcludedEntityName,
        };
    }
}