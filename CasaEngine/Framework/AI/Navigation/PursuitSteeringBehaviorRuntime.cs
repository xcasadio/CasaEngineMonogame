using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class PursuitSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public PursuitSteeringBehaviorRuntime(string name = "pursuit", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public string TargetEntityName { get; set; } = string.Empty;

    public Vector3 LastPredictedPosition { get; private set; }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!agent.TryGetEntityMotion(TargetEntityName, out Vector3 targetPosition, out Vector3 targetVelocity, out Vector3 targetForward))
        {
            LastPredictedPosition = Vector3.Zero;
            return Vector3.Zero;
        }

        Vector3 toEvader = targetPosition - kinematics.Position;
        float relativeHeading = Vector3.Dot(kinematics.Forward, targetForward);

        if (relativeHeading < -0.95f && Vector3.Dot(toEvader, kinematics.Forward) > 0.0f)
        {
            LastPredictedPosition = targetPosition;
            return Seek(kinematics, targetPosition);
        }

        float lookAheadTime = toEvader.Length() / Math.Max(1.0f, kinematics.MaxSpeed + targetVelocity.Length());
        LastPredictedPosition = targetPosition + targetVelocity * lookAheadTime;
        return Seek(kinematics, LastPredictedPosition);
    }

    private static Vector3 Seek(SteeringAgentKinematics kinematics, Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - kinematics.Position;
        if (toTarget.LengthSquared() <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        Vector3 desiredVelocity = Vector3.Normalize(toTarget) * kinematics.MaxSpeed;
        return desiredVelocity - kinematics.Velocity;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new PursuitSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            TargetEntityName = TargetEntityName,
        };
    }
}