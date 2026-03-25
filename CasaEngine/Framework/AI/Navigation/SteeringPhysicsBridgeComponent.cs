using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class SteeringPhysicsBridgeComponent : EntityComponent
{
    private SteeringAgentComponent? _agentComponent;
    private PhysicsBaseComponent? _physicsComponent;
    private SceneComponent? _sceneComponent;

    public bool AutoOrient { get; set; } = true;

    public float MinimumFacingSpeed { get; set; } = 0.05f;

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ResolveDependencies();
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveDependencies();
    }

    public override void Update(float elapsedTime)
    {
        ResolveDependencies();

        if (_agentComponent == null || _physicsComponent == null)
        {
            return;
        }

        SteeringCommand command = _agentComponent.CurrentCommand;
        Vector3 desiredVelocity = command.OutputMode switch
        {
            SteeringOutputMode.DesiredVelocity => command.DesiredVelocity.Truncate(_agentComponent.Settings.MaxSpeed),
            _ => ComputeVelocityFromForce(command.LinearForce, elapsedTime),
        };

        desiredVelocity.Z = 0.0f;
        _physicsComponent.Velocity = desiredVelocity;

        if (!AutoOrient)
        {
            return;
        }

        ApplyOrientation(command.DesiredFacing, desiredVelocity, elapsedTime);
    }

    public override EntityComponent Clone()
    {
        return new SteeringPhysicsBridgeComponent
        {
            AutoOrient = AutoOrient,
            MinimumFacingSpeed = MinimumFacingSpeed,
        };
    }

    private Vector3 ComputeVelocityFromForce(Vector3 force, float elapsedTime)
    {
        if (_agentComponent == null || _physicsComponent == null)
        {
            return Vector3.Zero;
        }

        float mass = MathF.Max(_agentComponent.Settings.Mass, 0.0001f);
        Vector3 deltaVelocity = force / mass * elapsedTime;
        Vector3 velocity = (_physicsComponent.Velocity + deltaVelocity).Truncate(_agentComponent.Settings.MaxSpeed);
        velocity.Z = 0.0f;
        return velocity;
    }

    private void ApplyOrientation(Vector3 desiredFacing, Vector3 desiredVelocity, float elapsedTime)
    {
        if (_sceneComponent == null || _agentComponent == null)
        {
            return;
        }

        Vector2 desiredPlanarDirection = new(desiredFacing.X, desiredFacing.Y);
        if (desiredPlanarDirection.LengthSquared() <= MinimumFacingSpeed * MinimumFacingSpeed)
        {
            desiredPlanarDirection = new Vector2(desiredVelocity.X, desiredVelocity.Y);
        }

        if (desiredPlanarDirection.LengthSquared() <= MinimumFacingSpeed * MinimumFacingSpeed)
        {
            return;
        }

        desiredPlanarDirection.Normalize();

        Vector3 currentForward = _sceneComponent.WorldMatrixNoScale.Right;
        Vector2 currentPlanarDirection = new(currentForward.X, currentForward.Y);

        if (currentPlanarDirection.LengthSquared() <= float.Epsilon)
        {
            currentPlanarDirection = desiredPlanarDirection;
        }
        else
        {
            currentPlanarDirection.Normalize();
        }

        float currentAngle = MathF.Atan2(currentPlanarDirection.Y, currentPlanarDirection.X);
        float desiredAngle = MathF.Atan2(desiredPlanarDirection.Y, desiredPlanarDirection.X);
        float angleDelta = WrapAngle(desiredAngle - currentAngle);
        float maxStep = MathF.Max(0.0f, _agentComponent.Settings.MaxTurnRate) * elapsedTime;
        float appliedDelta = Math.Clamp(angleDelta, -maxStep, maxStep);

        _sceneComponent.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, currentAngle + appliedDelta);
    }

    private void ResolveDependencies()
    {
        if (Owner == null)
        {
            _agentComponent = null;
            _physicsComponent = null;
            _sceneComponent = null;
            return;
        }

        _sceneComponent ??= Owner.RootComponent;
        _agentComponent ??= Owner.GetComponent<SteeringAgentComponent>();

        _physicsComponent ??= Owner.GetComponent<PhysicsBaseComponent>();
    }

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= MathF.Tau;
        }

        while (angle < -MathF.PI)
        {
            angle += MathF.Tau;
        }

        return angle;
    }
}