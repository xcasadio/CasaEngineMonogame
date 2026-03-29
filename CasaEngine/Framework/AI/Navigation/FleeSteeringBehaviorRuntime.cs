using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class FleeSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public const float AlwaysFlee = -1.0f;

    public FleeSteeringBehaviorRuntime(string name = "flee", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float PanicDistance { get; set; } = AlwaysFlee;

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!agent.TargetPosition.HasValue)
        {
            return Vector3.Zero;
        }

        Vector3 awayFromTarget = kinematics.Position - agent.TargetPosition.Value;
        float distance = awayFromTarget.Length();

        if (distance <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        if (PanicDistance != AlwaysFlee && distance > PanicDistance)
        {
            return Vector3.Zero;
        }

        Vector3 desiredVelocity = Vector3.Normalize(awayFromTarget) * kinematics.MaxSpeed;
        return desiredVelocity - kinematics.Velocity;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new FleeSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            PanicDistance = PanicDistance,
        };
    }
}