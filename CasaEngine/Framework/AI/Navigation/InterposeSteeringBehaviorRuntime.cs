using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class InterposeSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public InterposeSteeringBehaviorRuntime(string name = "interpose", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public string AgentAName { get; set; } = string.Empty;

    public string AgentBName { get; set; } = string.Empty;

    public float SlowingDistance { get; set; } = 90.0f;

    public Vector3 LastMidPoint { get; private set; }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!agent.TryGetEntityMotion(AgentAName, out Vector3 positionA, out Vector3 velocityA, out _)
            || !agent.TryGetEntityMotion(AgentBName, out Vector3 positionB, out Vector3 velocityB, out _))
        {
            LastMidPoint = Vector3.Zero;
            return Vector3.Zero;
        }

        Vector3 midPoint = (positionA + positionB) * 0.5f;
        float timeToReachMidPoint = Vector3.Distance(kinematics.Position, midPoint) / Math.Max(1.0f, kinematics.MaxSpeed);
        Vector3 projectedA = positionA + velocityA * timeToReachMidPoint;
        Vector3 projectedB = positionB + velocityB * timeToReachMidPoint;
        LastMidPoint = (projectedA + projectedB) * 0.5f;

        Vector3 toTarget = LastMidPoint - kinematics.Position;
        float distance = toTarget.Length();
        if (distance <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        float rampedSpeed = kinematics.MaxSpeed * (distance / Math.Max(1.0f, SlowingDistance));
        float clippedSpeed = Math.Min(rampedSpeed, kinematics.MaxSpeed);
        Vector3 desiredVelocity = toTarget * (clippedSpeed / distance);
        return desiredVelocity - kinematics.Velocity;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new InterposeSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            AgentAName = AgentAName,
            AgentBName = AgentBName,
            SlowingDistance = SlowingDistance,
        };
    }
}