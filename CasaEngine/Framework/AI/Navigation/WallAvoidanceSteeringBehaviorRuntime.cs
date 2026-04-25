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
        float ownerRadius = agent.Owner != null && agent.TryGetCollisionRadius(agent.Owner, out float collisionRadius)
            ? collisionRadius
            : 0.0f;

        LastFeelers[0] = kinematics.Position + forward * effectiveFeelerLength;
        LastFeelers[1] = kinematics.Position + RotatePlanar(forward, -MathF.PI * 0.25f) * effectiveFeelerLength * 0.5f;
        LastFeelers[2] = kinematics.Position + RotatePlanar(forward, MathF.PI * 0.25f) * effectiveFeelerLength * 0.5f;
        LastWallHit = Vector3.Zero;

        BoundingBox queryBounds = CreateFeelerQueryBounds(kinematics.Position, LastFeelers, padding: 12.0f + ownerRadius);
        IReadOnlyList<SteeringWallSnapshot> candidateWalls = agent.FindWallSnapshotsInBounds(queryBounds);
        int scannedWalls = 0;
        int intersectedWalls = 0;

        float closestDistance = float.MaxValue;
        Vector3 steeringForce = Vector3.Zero;

        for (int index = 0; index < LastFeelers.Length; index++)
        {
            Vector2 origin = new(kinematics.Position.X, kinematics.Position.Y);
            Vector2 feeler = new(LastFeelers[index].X, LastFeelers[index].Y);

            for (int wallIndex = 0; wallIndex < candidateWalls.Count; wallIndex++)
            {
                SteeringWallSnapshot wall = candidateWalls[wallIndex];
                scannedWalls++;

                Vector2 start = wall.Start;
                Vector2 end = wall.End;

                if (!TryGetSegmentIntersection(origin, feeler, start, end, out Vector2 hitPoint))
                {
                    Vector2 expandedWallNormal = wall.Normal.LengthSquared() > float.Epsilon
                        ? wall.Normal
                        : ComputeFallbackNormal(start, end, origin);
                    float wallThickness = wall.Thickness;
                    float clearance = ownerRadius + wallThickness * 0.5f;

                    if (TryResolveImmediatePenetration(origin, start, end, expandedWallNormal, clearance, out Vector2 penetrationNormal, out Vector2 penetrationPoint, out float penetrationDepth))
                    {
                        intersectedWalls++;
                        steeringForce = new Vector3(penetrationNormal * MathF.Max(penetrationDepth + ownerRadius, 1.0f), 0.0f);
                        closestDistance = 0.0f;
                        LastWallHit = new Vector3(penetrationPoint, 0.0f);
                        continue;
                    }

                    if (!TryGetExpandedWallIntersection(origin, feeler, start, end, expandedWallNormal, clearance, out hitPoint, out Vector2 hitNormal))
                    {
                        continue;
                    }

                    intersectedWalls++;

                    float expandedDistanceToIntersection = Vector2.Distance(origin, hitPoint);
                    if (expandedDistanceToIntersection >= closestDistance)
                    {
                        continue;
                    }

                    Vector2 expandedOverShoot = feeler - hitPoint;
                    steeringForce = new Vector3(hitNormal * MathF.Max(expandedOverShoot.Length(), MathF.Max(clearance, 1.0f)), 0.0f);
                    closestDistance = expandedDistanceToIntersection;
                    LastWallHit = new Vector3(hitPoint, 0.0f);
                    continue;
                }

                intersectedWalls++;

                float distanceToIntersection = Vector2.Distance(origin, hitPoint);
                if (distanceToIntersection >= closestDistance)
                {
                    continue;
                }

                Vector2 overShoot = feeler - hitPoint;
                Vector2 normal = wall.Normal.LengthSquared() > float.Epsilon
                    ? OrientNormalAwayFromPoint(wall.Normal, hitPoint, origin)
                    : ComputeFallbackNormal(start, end, origin);

                steeringForce = new Vector3(normal * overShoot.Length(), 0.0f);
                closestDistance = distanceToIntersection;
                LastWallHit = new Vector3(hitPoint, 0.0f);
            }
        }

        SteeringPerformanceDiagnostics.RecordBehaviorNeighborhoodScan(Name, scannedWalls, intersectedWalls);

        return steeringForce;
    }

    private static BoundingBox CreateFeelerQueryBounds(Vector3 origin, Vector3[] feelers, float padding)
    {
        float minX = origin.X;
        float minY = origin.Y;
        float maxX = origin.X;
        float maxY = origin.Y;

        for (int index = 0; index < feelers.Length; index++)
        {
            Vector3 feeler = feelers[index];
            minX = MathF.Min(minX, feeler.X);
            minY = MathF.Min(minY, feeler.Y);
            maxX = MathF.Max(maxX, feeler.X);
            maxY = MathF.Max(maxY, feeler.Y);
        }

        return new BoundingBox(
            new Vector3(minX - padding, minY - padding, -padding),
            new Vector3(maxX + padding, maxY + padding, padding));
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

    private static bool TryResolveImmediatePenetration(Vector2 point, Vector2 start, Vector2 end, Vector2 wallNormal, float clearance, out Vector2 pushNormal, out Vector2 closestPoint, out float penetrationDepth)
    {
        closestPoint = ClosestPointOnSegment(point, start, end);
        pushNormal = wallNormal;
        penetrationDepth = 0.0f;

        if (clearance <= float.Epsilon)
        {
            return false;
        }

        Vector2 offset = point - closestPoint;
        float distance = offset.Length();
        if (distance >= clearance)
        {
            return false;
        }

        if (distance > float.Epsilon)
        {
            pushNormal = offset / distance;
        }
        else
        {
            pushNormal = wallNormal.LengthSquared() > float.Epsilon ? wallNormal : Vector2.UnitY;
        }

        penetrationDepth = clearance - distance;
        return true;
    }

    private static bool TryGetExpandedWallIntersection(Vector2 origin, Vector2 feeler, Vector2 start, Vector2 end, Vector2 wallNormal, float clearance, out Vector2 hitPoint, out Vector2 hitNormal)
    {
        hitPoint = Vector2.Zero;
        hitNormal = Vector2.Zero;

        Vector2 bestPoint = Vector2.Zero;
        Vector2 bestNormal = Vector2.Zero;
        float bestDistanceSquared = float.MaxValue;

        if (clearance <= float.Epsilon)
        {
            return false;
        }

        wallNormal = wallNormal.LengthSquared() > float.Epsilon ? Vector2.Normalize(wallNormal) : Vector2.UnitY;

        TryRegisterHit(origin, start + wallNormal * clearance, end + wallNormal * clearance, wallNormal);
        TryRegisterHit(origin, start - wallNormal * clearance, end - wallNormal * clearance, -wallNormal);
        TryRegisterCapHit(origin, feeler, start, clearance);
        TryRegisterCapHit(origin, feeler, end, clearance);

        if (bestDistanceSquared == float.MaxValue)
        {
            return false;
        }

        hitPoint = bestPoint;
        hitNormal = bestNormal;
        return true;

        void TryRegisterHit(Vector2 segmentOrigin, Vector2 expandedStart, Vector2 expandedEnd, Vector2 normal)
        {
            if (!TryGetSegmentIntersection(segmentOrigin, feeler, expandedStart, expandedEnd, out Vector2 intersection))
            {
                return;
            }

            float distanceSquared = Vector2.DistanceSquared(segmentOrigin, intersection);
            if (distanceSquared >= bestDistanceSquared)
            {
                return;
            }

            bestDistanceSquared = distanceSquared;
            bestPoint = intersection;
            bestNormal = normal;
        }

        void TryRegisterCapHit(Vector2 segmentOrigin, Vector2 feelerEnd, Vector2 capCenter, float radius)
        {
            if (!TryGetSegmentCircleIntersection(segmentOrigin, feelerEnd, capCenter, radius, out Vector2 intersection))
            {
                return;
            }

            float distanceSquared = Vector2.DistanceSquared(segmentOrigin, intersection);
            if (distanceSquared >= bestDistanceSquared)
            {
                return;
            }

            Vector2 normal = intersection - capCenter;
            if (normal.LengthSquared() <= float.Epsilon)
            {
                normal = wallNormal;
            }
            else
            {
                normal.Normalize();
            }

            bestDistanceSquared = distanceSquared;
            bestPoint = intersection;
            bestNormal = normal;
        }
    }

    private static bool TryGetSegmentCircleIntersection(Vector2 segmentStart, Vector2 segmentEnd, Vector2 center, float radius, out Vector2 intersection)
    {
        intersection = Vector2.Zero;

        Vector2 delta = segmentEnd - segmentStart;
        float a = Vector2.Dot(delta, delta);
        if (a <= float.Epsilon)
        {
            return false;
        }

        Vector2 offset = segmentStart - center;
        float b = 2.0f * Vector2.Dot(offset, delta);
        float c = Vector2.Dot(offset, offset) - radius * radius;
        float discriminant = b * b - 4.0f * a * c;
        if (discriminant < 0.0f)
        {
            return false;
        }

        float sqrtDiscriminant = MathF.Sqrt(discriminant);
        float inverseDenominator = 1.0f / (2.0f * a);
        float t1 = (-b - sqrtDiscriminant) * inverseDenominator;
        float t2 = (-b + sqrtDiscriminant) * inverseDenominator;

        float t = float.MaxValue;
        if (t1 >= 0.0f && t1 <= 1.0f)
        {
            t = t1;
        }
        else if (t2 >= 0.0f && t2 <= 1.0f)
        {
            t = t2;
        }

        if (t == float.MaxValue)
        {
            return false;
        }

        intersection = segmentStart + delta * t;
        return true;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            return start;
        }

        float t = Vector2.Dot(point - start, segment) / lengthSquared;
        t = Math.Clamp(t, 0.0f, 1.0f);
        return start + segment * t;
    }

    private static Vector2 OrientNormalAwayFromPoint(Vector2 normal, Vector2 wallPoint, Vector2 referencePoint)
    {
        if (normal.LengthSquared() <= float.Epsilon)
        {
            return Vector2.UnitY;
        }

        normal.Normalize();
        return Vector2.Dot(normal, referencePoint - wallPoint) < 0.0f
            ? -normal
            : normal;
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