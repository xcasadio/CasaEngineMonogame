using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class AlignmentSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public AlignmentSteeringBehaviorRuntime(string name = "alignment", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float NeighborRadius { get; set; } = 140.0f;

    public int LastNeighborCount { get; private set; }

    public List<Vector3> LastNeighborPositions { get; } = [];

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        Vector3 heading = Vector3.Zero;
        LastNeighborPositions.Clear();
        LastNeighborCount = 0;

        foreach (Entity entity in agent.FindNeighborEntities(NeighborRadius))
        {
            if (!agent.TryGetEntityMotion(entity, out Vector3 otherPosition, out _, out Vector3 otherForward))
            {
                continue;
            }

            heading += otherForward;
            LastNeighborCount++;
            LastNeighborPositions.Add(otherPosition);
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
        };
    }
}