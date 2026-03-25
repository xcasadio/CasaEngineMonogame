using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class ObstacleAvoidanceSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public ObstacleAvoidanceSteeringBehaviorRuntime(string name = "obstacle-avoidance", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float DetectionLength { get; set; } = 110.0f;

    public float ClearanceRadius { get; set; } = 42.0f;

    public Vector3 LastAvoidanceTarget { get; private set; }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        Vector3 forward = kinematics.Forward.LengthSquared() > float.Epsilon ? Vector3.Normalize(kinematics.Forward) : Vector3.Right;
        Vector3 ahead = kinematics.Position + forward * DetectionLength;
        Vector3 bestForce = Vector3.Zero;
        float bestDistance = float.MaxValue;
        LastAvoidanceTarget = Vector3.Zero;

        foreach (Entity entity in agent.FindEntitiesByPredicate(candidate => candidate.GetType().Name.Contains("Obstacle", StringComparison.OrdinalIgnoreCase)))
        {
            if (!agent.TryGetEntityMotion(entity, out Vector3 obstaclePosition, out _, out _))
            {
                continue;
            }

            float radius = agent.TryGetCollisionRadius(entity, out float entityRadius) ? entityRadius : ClearanceRadius;
            float distanceToSegment = DistancePointToSegment(obstaclePosition, kinematics.Position, ahead);
            if (distanceToSegment > radius + ClearanceRadius)
            {
                continue;
            }

            float distance = Vector3.DistanceSquared(kinematics.Position, obstaclePosition);
            if (distance >= bestDistance)
            {
                continue;
            }

            Vector3 away = ahead - obstaclePosition;
            if (away.LengthSquared() <= float.Epsilon)
            {
                away = Vector3.Cross(Vector3.Up, forward);
            }

            away.Normalize();
            bestForce = away * kinematics.MaxForce;
            bestDistance = distance;
            LastAvoidanceTarget = obstaclePosition;
        }

        return bestForce;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            return Vector3.Distance(point, segmentStart);
        }

        float t = Vector3.Dot(point - segmentStart, segment) / lengthSquared;
        t = MathHelper.Clamp(t, 0.0f, 1.0f);
        Vector3 projection = segmentStart + segment * t;
        return Vector3.Distance(point, projection);
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new ObstacleAvoidanceSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            DetectionLength = DetectionLength,
            ClearanceRadius = ClearanceRadius,
        };
    }
}