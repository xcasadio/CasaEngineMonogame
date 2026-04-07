using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class SeparationSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public SeparationSteeringBehaviorRuntime(string name = "separation", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float NeighborRadius { get; set; } = 120.0f;

    public string ExcludedEntityName { get; set; } = string.Empty;

    public int LastNeighborCount { get; private set; }

    public List<Vector3> LastNeighborPositions { get; } = [];

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        Vector3 force = Vector3.Zero;
        LastNeighborPositions.Clear();
        LastNeighborCount = 0;

        foreach (Entity entity in agent.FindNeighborEntities(NeighborRadius))
        {
            if (!string.IsNullOrWhiteSpace(ExcludedEntityName)
                && string.Equals(entity.Name, ExcludedEntityName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!agent.TryGetEntityMotion(entity, out Vector3 otherPosition, out _, out _))
            {
                continue;
            }

            Vector3 away = kinematics.Position - otherPosition;
            float distance = away.Length();
            if (distance <= float.Epsilon)
            {
                continue;
            }

            force += Vector3.Normalize(away) / distance;
            LastNeighborCount++;
            LastNeighborPositions.Add(otherPosition);
        }

        return force;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new SeparationSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            NeighborRadius = NeighborRadius,
            ExcludedEntityName = ExcludedEntityName,
        };
    }
}