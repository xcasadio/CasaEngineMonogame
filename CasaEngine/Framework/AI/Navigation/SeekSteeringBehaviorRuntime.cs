using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class SeekSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public SeekSteeringBehaviorRuntime(string name = "seek", float weight = 1.0f)
        : base(name, weight)
    {
    }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!agent.TargetPosition.HasValue)
        {
            return Vector3.Zero;
        }

        Vector3 toTarget = agent.TargetPosition.Value - kinematics.Position;

        if (toTarget.LengthSquared() <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        Vector3 desiredVelocity = Vector3.Normalize(toTarget) * kinematics.MaxSpeed;
        return desiredVelocity - kinematics.Velocity;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new SeekSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
        };
    }
}