using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class AlignmentSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public AlignmentSteeringBehaviorRuntime(string name = "alignment", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float NeighborRadius { get; set; } = 140.0f;

    public string ExcludedEntityName { get; set; } = string.Empty;

    public int LastNeighborCount { get; private set; }

    public List<Vector3> LastNeighborPositions { get; } = [];

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        Vector3 heading = Vector3.Zero;
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

            heading += neighbor.Forward;
            LastNeighborCount++;
            LastNeighborPositions.Add(neighbor.Position);
        }

        if (LastNeighborCount == 0 || heading.LengthSquared() <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        heading /= LastNeighborCount;
        heading.Normalize();
        return heading - kinematics.Forward;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new AlignmentSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            NeighborRadius = NeighborRadius,
            ExcludedEntityName = ExcludedEntityName,
        };
    }
}