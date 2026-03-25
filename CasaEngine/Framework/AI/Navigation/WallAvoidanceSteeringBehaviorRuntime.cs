using CasaEngine.Framework.Entities;
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
        Vector3 right = Vector3.Cross(Vector3.Up, forward);
        if (right.LengthSquared() <= float.Epsilon)
        {
            right = Vector3.Up;
        }
        else
        {
            right.Normalize();
        }

        LastFeelers[0] = kinematics.Position + forward * FeelerLength;
        LastFeelers[1] = kinematics.Position + Vector3.Normalize(forward - right) * FeelerLength * 0.7f;
        LastFeelers[2] = kinematics.Position + Vector3.Normalize(forward + right) * FeelerLength * 0.7f;
        LastWallHit = Vector3.Zero;

        Vector3 accumulatedForce = Vector3.Zero;

        foreach (Entity entity in agent.FindEntitiesByPredicate(candidate => candidate.GetType().Name.Contains("Wall", StringComparison.OrdinalIgnoreCase)))
        {
            if (!agent.TryGetEntityMotion(entity, out Vector3 wallCenter, out _, out _))
            {
                continue;
            }

            if (!agent.TryGetWallSegment(entity, out Vector2 start, out Vector2 end))
            {
                continue;
            }

            for (int index = 0; index < LastFeelers.Length; index++)
            {
                Vector2 origin = new(kinematics.Position.X, kinematics.Position.Y);
                Vector2 feeler = new(LastFeelers[index].X, LastFeelers[index].Y);
                if (!TryGetSegmentIntersection(origin, feeler, start, end, out Vector2 hitPoint))
                {
                    continue;
                }

                Vector2 overShoot = feeler - hitPoint;
                Vector2 segmentDirection = end - start;
                Vector2 normal = segmentDirection.LengthSquared() <= float.Epsilon
                    ? Vector2.UnitY
                    : Vector2.Normalize(new Vector2(-segmentDirection.Y, segmentDirection.X));

                accumulatedForce += new Vector3(normal * overShoot.Length(), 0.0f);
                LastWallHit = new Vector3(hitPoint, 0.0f);
            }
        }

        return accumulatedForce;
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

    public override SteeringBehaviorRuntime Clone()
    {
        return new WallAvoidanceSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            FeelerLength = FeelerLength,
        };
    }
}