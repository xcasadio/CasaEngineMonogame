using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.CharacterMotion;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class CharacterControllerSteeringBridgeComponent : EntityComponent, IWorldSystemDrivenComponent
{
    private SteeringAgentComponent _agentComponent;
    private CharacterControllerComponent _controller;

    public bool AutoSetControlMode { get; set; } = true;

    public CharacterControlMode ControlMode { get; set; } = CharacterControlMode.AI;

    public bool SteeringUsesXYPlane { get; set; } = true;

    public bool StopWhenIdle { get; set; } = true;

    public float MinimumCommandSpeed { get; set; } = 0.001f;

    public Vector2 LastMoveIntent { get; private set; }

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ResolveDependencies();
    }

    public override void InitializeWithWorld(Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveDependencies();
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (TryBuildMotionCommand(elapsedTime, out CharacterMotionCommand command))
        {
            command.Apply();
        }
    }

    public bool TryBuildMotionCommand(float elapsedTime, out CharacterMotionCommand command)
    {
        command = default;

        ResolveDependencies();
        if (_agentComponent == null || _controller == null)
        {
            LastMoveIntent = Vector2.Zero;
            return false;
        }

        return TryBuildCommand(_agentComponent.CurrentCommand, out command);
    }

    public void ApplyCommand(SteeringCommand command)
    {
        if (TryBuildCommand(command, out CharacterMotionCommand motionCommand))
        {
            motionCommand.Apply();
        }
    }

    public bool TryBuildCommand(SteeringCommand command, out CharacterMotionCommand motionCommand)
    {
        motionCommand = default;

        if (_controller == null)
        {
            ResolveDependencies();
        }

        if (_controller == null)
        {
            LastMoveIntent = Vector2.Zero;
            return false;
        }

        Vector3 desiredVelocity = command.DesiredVelocity;
        Vector2 intent = SteeringUsesXYPlane
            ? new Vector2(desiredVelocity.X, -desiredVelocity.Y)
            : new Vector2(desiredVelocity.X, -desiredVelocity.Z);

        if (intent.LengthSquared() <= MinimumCommandSpeed * MinimumCommandSpeed)
        {
            LastMoveIntent = Vector2.Zero;
            if (StopWhenIdle)
            {
                motionCommand = new CharacterMotionCommand(
                    _controller,
                    Vector2.Zero,
                    ControlMode,
                    CharacterMotionAuthority.Steering,
                    AutoSetControlMode);
                return true;
            }

            return false;
        }

        if (intent.LengthSquared() > 1f)
        {
            intent.Normalize();
        }

        LastMoveIntent = intent;
        motionCommand = new CharacterMotionCommand(
            _controller,
            intent,
            ControlMode,
            CharacterMotionAuthority.Steering,
            AutoSetControlMode);
        return true;
    }

    public override EntityComponent Clone()
    {
        return new CharacterControllerSteeringBridgeComponent
        {
            AutoSetControlMode = AutoSetControlMode,
            ControlMode = ControlMode,
            SteeringUsesXYPlane = SteeringUsesXYPlane,
            StopWhenIdle = StopWhenIdle,
            MinimumCommandSpeed = MinimumCommandSpeed,
        };
    }

    private void ResolveDependencies()
    {
        if (Owner == null)
        {
            _agentComponent = null;
            _controller = null;
            return;
        }

        _agentComponent = Owner.GetComponent<SteeringAgentComponent>();
        _controller = Owner.GetComponent<CharacterControllerComponent>();
    }
}