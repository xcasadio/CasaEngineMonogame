using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class EvadeSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public EvadeSteeringBehaviorRuntime(string name = "evade", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public string ThreatEntityName { get; set; } = string.Empty;

    public float ThreatDistance { get; set; } = 240.0f;

    public Vector3 LastPredictedThreatPosition { get; private set; }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!agent.TryGetEntityMotion(ThreatEntityName, out Vector3 threatPosition, out Vector3 threatVelocity, out _))
        {
            LastPredictedThreatPosition = Vector3.Zero;
            return Vector3.Zero;
        }

        Vector3 toThreat = threatPosition - kinematics.Position;
        if (ThreatDistance > 0.0f && toThreat.LengthSquared() > ThreatDistance * ThreatDistance)
        {
            return Vector3.Zero;
        }

        float lookAheadTime = toThreat.Length() / Math.Max(1.0f, kinematics.MaxSpeed + threatVelocity.Length());
        LastPredictedThreatPosition = threatPosition + threatVelocity * lookAheadTime;

        Vector3 fleeVector = kinematics.Position - LastPredictedThreatPosition;
        if (fleeVector.LengthSquared() <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        Vector3 desiredVelocity = Vector3.Normalize(fleeVector) * kinematics.MaxSpeed;
        return desiredVelocity - kinematics.Velocity;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new EvadeSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            ThreatEntityName = ThreatEntityName,
            ThreatDistance = ThreatDistance,
        };
    }
}