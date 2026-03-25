using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class FollowPathSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    private readonly List<Vector3> _waypoints = [];

    public FollowPathSteeringBehaviorRuntime(string name = "follow-path", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public IReadOnlyList<Vector3> Waypoints => _waypoints;

    public int CurrentWaypointIndex { get; private set; }

    public bool Loop { get; set; }

    public float WaypointTolerance { get; set; } = 12.0f;

    public void SetWaypoints(IEnumerable<Vector3> waypoints)
    {
        _waypoints.Clear();
        _waypoints.AddRange(waypoints);
        CurrentWaypointIndex = 0;
    }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (_waypoints.Count == 0)
        {
            return Vector3.Zero;
        }

        Vector3 currentWaypoint = _waypoints[CurrentWaypointIndex];
        if (Vector3.DistanceSquared(kinematics.Position, currentWaypoint) <= WaypointTolerance * WaypointTolerance)
        {
            if (CurrentWaypointIndex < _waypoints.Count - 1)
            {
                CurrentWaypointIndex++;
            }
            else if (Loop)
            {
                CurrentWaypointIndex = 0;
            }
        }

        currentWaypoint = _waypoints[CurrentWaypointIndex];
        bool finalWaypoint = !Loop && CurrentWaypointIndex == _waypoints.Count - 1;

        Vector3 toTarget = currentWaypoint - kinematics.Position;
        float distance = toTarget.Length();
        if (distance <= float.Epsilon)
        {
            return Vector3.Zero;
        }

        if (finalWaypoint)
        {
            float slowingDistance = 120.0f;
            float rampedSpeed = kinematics.MaxSpeed * (distance / slowingDistance);
            float clippedSpeed = Math.Min(rampedSpeed, kinematics.MaxSpeed);
            Vector3 desiredVelocity = toTarget * (clippedSpeed / distance);
            return desiredVelocity - kinematics.Velocity;
        }

        Vector3 desired = Vector3.Normalize(toTarget) * kinematics.MaxSpeed;
        return desired - kinematics.Velocity;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        FollowPathSteeringBehaviorRuntime clone = new(Name, Weight)
        {
            IsEnabled = IsEnabled,
            Loop = Loop,
            WaypointTolerance = WaypointTolerance,
        };

        clone.SetWaypoints(_waypoints);
        return clone;
    }
}