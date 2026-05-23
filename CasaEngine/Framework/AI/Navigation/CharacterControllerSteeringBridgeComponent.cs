using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class CharacterControllerSteeringBridgeComponent : EntityComponent
{
    private SteeringAgentComponent _agentComponent;
    private CharacterControllerComponent _controller;

    public bool AutoSetControlMode { get; set; } = true;

    public CharacterControlMode ControlMode { get; set; } = CharacterControlMode.AI;

    public bool SteeringUsesXYPlane { get; set; } = true;

    public bool StopWhenIdle { get; set; } = true;

    public float MinimumCommandSpeed { get; set; } = 0.001f;

    public Vector2 LastMoveIntent { get; private set; }

    public override int UpdateOrder => (int)EntityComponentUpdateOrder.BeforeDefault;

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

        ResolveDependencies();
        if (_agentComponent == null || _controller == null)
        {
            LastMoveIntent = Vector2.Zero;
            return;
        }

        if (AutoSetControlMode && _controller.ControlMode != ControlMode)
        {
            _controller.SetControlMode(ControlMode);
        }

        ApplyCommand(_agentComponent.CurrentCommand);
    }

    public void ApplyCommand(SteeringCommand command)
    {
        if (_controller == null)
        {
            ResolveDependencies();
        }

        if (_controller == null)
        {
            LastMoveIntent = Vector2.Zero;
            return;
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
                _controller.SetMoveIntent(Vector2.Zero);
            }

            return;
        }

        if (intent.LengthSquared() > 1f)
        {
            intent.Normalize();
        }

        LastMoveIntent = intent;
        _controller.SetMoveIntent(intent);
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