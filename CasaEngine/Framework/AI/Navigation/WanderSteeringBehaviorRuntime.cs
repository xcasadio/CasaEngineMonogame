using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class WanderSteeringBehaviorRuntime : SteeringBehaviorRuntime
{
    private readonly Random _random = new();
    private Vector3 _wanderTarget = Vector3.UnitX;

    public WanderSteeringBehaviorRuntime(string name = "wander", float weight = 1.0f)
        : base(name, weight)
    {
    }

    public float Distance { get; set; } = 72.0f;

    public float Radius { get; set; } = 36.0f;

    public float JitterPerSecond { get; set; } = 30.0f;

    public Vector3 WanderTarget => _wanderTarget;

    public Vector3 LastTargetPosition { get; private set; }

    protected override Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        float jitter = Math.Max(0.0f, JitterPerSecond) * Math.Max(elapsedTime, 0.016f);
        _wanderTarget += new Vector3(
            ((float)_random.NextDouble() * 2.0f - 1.0f) * jitter,
            ((float)_random.NextDouble() * 2.0f - 1.0f) * jitter,
            0.0f);

        if (_wanderTarget.LengthSquared() <= float.Epsilon)
        {
            _wanderTarget = Vector3.UnitX;
        }

        _wanderTarget.Normalize();
        _wanderTarget *= Math.Max(1.0f, Radius);

        Vector3 forward = kinematics.Forward.LengthSquared() > float.Epsilon
            ? Vector3.Normalize(kinematics.Forward)
            : Vector3.Right;

        Vector3 circleCenter = kinematics.Position + forward * Distance;
        LastTargetPosition = circleCenter + _wanderTarget;
        return LastTargetPosition - kinematics.Position;
    }

    public override SteeringBehaviorRuntime Clone()
    {
        return new WanderSteeringBehaviorRuntime(Name, Weight)
        {
            IsEnabled = IsEnabled,
            Distance = Distance,
            Radius = Radius,
            JitterPerSecond = JitterPerSecond,
        };
    }
}