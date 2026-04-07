using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class WallAvoidanceSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    public WallAvoidanceSteeringBehaviorRuntime(string name = "wall-avoidance", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float FeelerLength { get; set; } = 90.0f;

    public Vector3[] LastFeelers { get; } = [Vector3.Zero, Vector3.Zero, Vector3.Zero];

    public Vector3 LastWallHit { get; private set; }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        Vector3 forward = kinematics.Forward.LengthSquared() > float.Epsilon ? Vector3.Normalize(kinematics.Forward) : Vector3.Right;
        float speedRatio = kinematics.MaxSpeed <= float.Epsilon ? 0.0f : kinematics.Speed / kinematics.MaxSpeed;
        float effectiveFeelerLength = FeelerLength + (FeelerLength * speedRatio);

        LastFeelers[0] = kinematics.Position + forward * effectiveFeelerLength;
        LastFeelers[1] = kinematics.Position + RotatePlanar(forward, -MathF.PI * 0.25f) * effectiveFeelerLength * 0.5f;
        LastFeelers[2] = kinematics.Position + RotatePlanar(forward, MathF.PI * 0.25f) * effectiveFeelerLength * 0.5f;
        LastWallHit = Vector3.Zero;

        float closestDistance = float.MaxValue;
        Vector3 steeringForce = Vector3.Zero;

        for (int index = 0; index < LastFeelers.Length; index++)
        {
            Vector2 origin = new(kinematics.Position.X, kinematics.Position.Y);
            Vector2 feeler = new(LastFeelers[index].X, LastFeelers[index].Y);

            foreach (Entity entity in agent.FindEntitiesByPredicate(candidate => candidate.GetType().Name.Contains("Wall", StringComparison.OrdinalIgnoreCase)))
            {
                if (!agent.TryGetWallSegment(entity, out Vector2 start, out Vector2 end))
                {
                    continue;
                }

                if (!TryGetSegmentIntersection(origin, feeler, start, end, out Vector2 hitPoint))
                {
                    continue;
                }

                float distanceToIntersection = Vector2.Distance(origin, hitPoint);
                if (distanceToIntersection >= closestDistance)
                {
                    continue;
                }

                Vector2 overShoot = feeler - hitPoint;
                Vector2 normal = agent.TryGetWallNormal(entity, out Vector2 wallNormal)
                    ? wallNormal
                    : ComputeFallbackNormal(start, end, origin);

                steeringForce = new Vector3(normal * overShoot.Length(), 0.0f);
                closestDistance = distanceToIntersection;
                LastWallHit = new Vector3(hitPoint, 0.0f);
            }
        }

        return steeringForce;
    }

    private static Vector2 ComputeFallbackNormal(Vector2 start, Vector2 end, Vector2 origin)
    {
        Vector2 segmentDirection = end - start;
        if (segmentDirection.LengthSquared() <= float.Epsilon)
        {
            return Vector2.UnitY;
        }

        segmentDirection.Normalize();
        Vector2 candidate = new(-segmentDirection.Y, segmentDirection.X);
        Vector2 midpoint = (start + end) * 0.5f;
        Vector2 toOrigin = origin - midpoint;
        if (Vector2.Dot(candidate, toOrigin) < 0.0f)
        {
            candidate *= -1.0f;
        }

        return candidate;
    }

    private static bool TryGetSegmentIntersection(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 intersection)
    {
        intersection = Vector2.Zero;
        float denominator = ((a2.X - a1.X) * (b2.Y - b1.Y)) - ((a2.Y - a1.Y) * (b2.X - b1.X));
        if (Math.Abs(denominator) <= float.Epsilon)
        {
            return false;
        }

        float ua = (((b2.X - b1.X) * (a1.Y - b1.Y)) - ((b2.Y - b1.Y) * (a1.X - b1.X))) / denominator;
        float ub = (((a2.X - a1.X) * (a1.Y - b1.Y)) - ((a2.Y - a1.Y) * (a1.X - b1.X))) / denominator;

        if (ua < 0.0f || ua > 1.0f || ub < 0.0f || ub > 1.0f)
        {
            return false;
        }

        intersection = a1 + ((a2 - a1) * ua);
        return true;
    }

    private static Vector3 RotatePlanar(Vector3 vector, float angle)
    {
        Vector3 rotated = Vector3.Transform(vector, Matrix.CreateRotationZ(angle));
        if (rotated.LengthSquared() <= float.Epsilon)
        {
            return Vector3.Right;
        }

        rotated.Normalize();
        return rotated;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new WallAvoidanceSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            FeelerLength = FeelerLength,
        };
    }
}