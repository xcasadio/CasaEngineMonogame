using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class OffsetPursuitSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public OffsetPursuitSteeringBehaviorRuntime(string name = "offset-pursuit", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public string LeaderEntityName { get; set; } = string.Empty;

    public Vector3 Offset { get; set; } = new(-72.0f, 40.0f, 0.0f);

    public float SlowingDistance { get; set; } = 110.0f;

    public Vector3 LastWorldOffsetTarget { get; private set; }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!agent.TryGetEntityMotion(LeaderEntityName, out Vector3 leaderPosition, out Vector3 leaderVelocity, out Vector3 leaderForward))
        {
            LastWorldOffsetTarget = Vector3.Zero;
            return Vector3.Zero;
        }

        Vector3 leaderRight = Vector3.Cross(Vector3.UnitZ, leaderForward);
        if (leaderRight.LengthSquared() <= float.Epsilon)
        {
            leaderRight = Vector3.Right;
        }
        else
        {
            leaderRight.Normalize();
        }

        Vector3 worldOffset = leaderPosition + (leaderForward * Offset.X) + (leaderRight * Offset.Y) + (Vector3.UnitZ * Offset.Z);
        float lookAheadTime = Vector3.Distance(kinematics.Position, worldOffset) / Math.Max(1.0f, kinematics.MaxSpeed + leaderVelocity.Length());
        LastWorldOffsetTarget = worldOffset + leaderVelocity * lookAheadTime;

        Vector3 toTarget = LastWorldOffsetTarget - kinematics.Position;
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
        return new OffsetPursuitSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            LeaderEntityName = LeaderEntityName,
            Offset = Offset,
            SlowingDistance = SlowingDistance,
        };
    }
}