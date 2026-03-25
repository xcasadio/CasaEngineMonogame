using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class ArriveSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public ArriveSteeringBehaviorRuntime(string name = "arrive", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float SlowingDistance { get; set; } = 120.0f;

    public float ArrivalTolerance { get; set; } = 6.0f;

    public float LastTargetDistance { get; private set; }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!agent.TargetPosition.HasValue)
        {
            LastTargetDistance = 0.0f;
            return Vector3.Zero;
        }

        Vector3 toTarget = agent.TargetPosition.Value - kinematics.Position;
        float distance = toTarget.Length();
        LastTargetDistance = distance;

        if (distance <= ArrivalTolerance || distance <= float.Epsilon)
        {
            return -kinematics.Velocity;
        }

        float safeSlowingDistance = Math.Max(SlowingDistance, ArrivalTolerance + 1.0f);
        float rampedSpeed = kinematics.MaxSpeed * (distance / safeSlowingDistance);
        float clippedSpeed = Math.Min(rampedSpeed, kinematics.MaxSpeed);
        Vector3 desiredVelocity = toTarget * (clippedSpeed / distance);
        return desiredVelocity - kinematics.Velocity;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new ArriveSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            SlowingDistance = SlowingDistance,
            ArrivalTolerance = ArrivalTolerance,
        };
    }
}