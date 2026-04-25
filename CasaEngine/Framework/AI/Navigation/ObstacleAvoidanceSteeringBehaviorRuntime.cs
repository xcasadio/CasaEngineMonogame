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
        Vector3 side = Vector3.Cross(Vector3.UnitZ, forward);
        if (side.LengthSquared() <= float.Epsilon)
        {
            side = Vector3.Up;
        }
        else
        {
            side.Normalize();
        }

        float speedRatio = kinematics.MaxSpeed <= float.Epsilon ? 0.0f : kinematics.Velocity.Length() / kinematics.MaxSpeed;
        float detectionLength = DetectionLength + (speedRatio * DetectionLength);
        float ownerRadius = agent.Owner != null && agent.TryGetCollisionRadius(agent.Owner, out float collisionRadius)
            ? collisionRadius
            : 0.0f;

        float closestIntersection = float.MaxValue;
        Vector2 localClosestObstacle = Vector2.Zero;
        float closestObstacleRadius = 0.0f;
        LastAvoidanceTarget = Vector3.Zero;

        BoundingBox queryBounds = CreateObstacleQueryBounds(kinematics.Position, forward, side, detectionLength, ClearanceRadius + ownerRadius + 8.0f);
        IReadOnlyList<SteeringObstacleSnapshot> candidateObstacles = agent.FindObstacleSnapshotsInBounds(queryBounds);
        int scannedObstacles = 0;
        int acceptedObstacles = 0;

        for (int index = 0; index < candidateObstacles.Count; index++)
        {
            SteeringObstacleSnapshot obstacle = candidateObstacles[index];
            scannedObstacles++;

            Vector3 obstaclePosition = new(obstacle.Position, 0.0f);
            float obstacleRadius = obstacle.Radius > float.Epsilon ? obstacle.Radius : ClearanceRadius;
            Vector3 toObstacle = obstaclePosition - kinematics.Position;
            Vector2 localPosition = new(Vector3.Dot(toObstacle, forward), Vector3.Dot(toObstacle, side));

            if (localPosition.X < 0.0f)
            {
                continue;
            }

            float expandedRadius = obstacleRadius + ownerRadius;
            if (Math.Abs(localPosition.Y) >= expandedRadius)
            {
                continue;
            }

            float sqrtPart = MathF.Sqrt(MathF.Max(0.0f, (expandedRadius * expandedRadius) - (localPosition.Y * localPosition.Y)));
            float intersectionPoint = localPosition.X - sqrtPart;
            if (intersectionPoint <= 0.0f)
            {
                intersectionPoint = localPosition.X + sqrtPart;
            }

            if (intersectionPoint > detectionLength || intersectionPoint >= closestIntersection)
            {
                continue;
            }

            closestIntersection = intersectionPoint;
            localClosestObstacle = localPosition;
            closestObstacleRadius = obstacleRadius;
            LastAvoidanceTarget = obstaclePosition;
            acceptedObstacles++;
        }

        SteeringPerformanceDiagnostics.RecordBehaviorNeighborhoodScan(Name, scannedObstacles, acceptedObstacles);

        if (closestIntersection == float.MaxValue)
        {
            return Vector3.Zero;
        }

        float multiplier = 1.0f + ((detectionLength - localClosestObstacle.X) / Math.Max(1.0f, detectionLength));
        const float brakingWeight = 0.2f;

        Vector3 steeringLocal = Vector3.Zero;
        steeringLocal.Y = (closestObstacleRadius - localClosestObstacle.Y) * multiplier;
        steeringLocal.X = (closestObstacleRadius - localClosestObstacle.X) * brakingWeight;

        return (forward * steeringLocal.X) + (side * steeringLocal.Y);
    }

    private static BoundingBox CreateObstacleQueryBounds(Vector3 origin, Vector3 forward, Vector3 side, float detectionLength, float clearance)
    {
        Vector3 center = origin + (forward * (detectionLength * 0.5f));
        Vector3 forwardExtent = new(MathF.Abs(forward.X), MathF.Abs(forward.Y), 0.0f);
        Vector3 sideExtent = new(MathF.Abs(side.X), MathF.Abs(side.Y), 0.0f);
        Vector3 extent = (forwardExtent * (detectionLength * 0.5f + clearance)) + (sideExtent * clearance) + new Vector3(clearance, clearance, clearance);

        return new BoundingBox(center - extent, center + extent);
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