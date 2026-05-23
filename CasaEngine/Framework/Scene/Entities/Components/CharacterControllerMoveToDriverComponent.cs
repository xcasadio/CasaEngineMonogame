using System.ComponentModel;
using CasaEngine.Framework.Scene.CharacterMotion;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

[Browsable(false)]
[DisplayName("Character controller move-to driver")]
public sealed class CharacterControllerMoveToDriverComponent : EntityComponent, IWorldSystemDrivenComponent
{
    private const float CompletionEpsilon = 0.0001f;
    private CharacterControllerComponent _controller;
    private CharacterControlMode _previousControlMode;
    private bool _hasPreviousControlMode;
    private float _elapsedSeconds;
    private CharacterControlMode _controlMode = CharacterControlMode.Cutscene;

    public Vector3 Destination { get; private set; }

    public float StoppingDistance { get; private set; } = 0.1f;

    public float TimeoutSeconds { get; private set; }

    public bool IsMoving { get; private set; }

    public bool HasReachedDestination { get; private set; }

    public bool HasTimedOut { get; private set; }

    public CharacterControllerComponent Controller => _controller;

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ResolveController();
    }

    public override void InitializeWithWorld(Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveController();
    }

    public void MoveTo(
        Vector3 destination,
        float stoppingDistance = 0.1f,
        float timeoutSeconds = 0f,
        CharacterControlMode controlMode = CharacterControlMode.Cutscene)
    {
        if (stoppingDistance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(stoppingDistance));
        }

        if (timeoutSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        }

        ResolveController();
        if (_controller == null)
        {
            throw new InvalidOperationException("Move-to driver requires a CharacterControllerComponent on the owner entity.");
        }

        if (_controller.Owner?.RootComponent == null)
        {
            throw new InvalidOperationException("Move-to driver requires the controlled entity to have a root component.");
        }

        Destination = destination;
        StoppingDistance = stoppingDistance;
        TimeoutSeconds = timeoutSeconds;
        _elapsedSeconds = 0f;
        HasReachedDestination = false;
        HasTimedOut = false;
        IsMoving = true;
        _previousControlMode = _controller.ControlMode;
        _hasPreviousControlMode = true;
        _controlMode = controlMode;
        _controller.SetControlMode(controlMode);
    }

    public void Cancel()
    {
        Complete(reachedDestination: false, timedOut: false);
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

        if (!IsMoving)
        {
            return false;
        }

        ResolveController();
        if (_controller?.Owner?.RootComponent == null)
        {
            Complete(reachedDestination: false, timedOut: false);
            return false;
        }

        if (TimeoutSeconds > 0f)
        {
            _elapsedSeconds += Math.Max(0f, elapsedTime);
            if (_elapsedSeconds >= TimeoutSeconds)
            {
                Complete(reachedDestination: false, timedOut: true);
                return false;
            }
        }

        var position = _controller.Owner.RootComponent.Position;
        var toDestination = Destination - position;
        var planarDelta = new Vector2(toDestination.X, -toDestination.Z);
        var stoppingDistance = Math.Max(StoppingDistance, CompletionEpsilon);

        if (planarDelta.LengthSquared() <= stoppingDistance * stoppingDistance)
        {
            Complete(reachedDestination: true, timedOut: false);
            return false;
        }

        if (planarDelta.LengthSquared() > 1f)
        {
            planarDelta.Normalize();
        }

        command = new CharacterMotionCommand(
            _controller,
            planarDelta,
            _controlMode,
            CharacterMotionAuthority.Cutscene);
        return true;
    }

    public override CharacterControllerMoveToDriverComponent Clone()
    {
        return new CharacterControllerMoveToDriverComponent();
    }

    private void Complete(bool reachedDestination, bool timedOut)
    {
        IsMoving = false;
        HasReachedDestination = reachedDestination;
        HasTimedOut = timedOut;

        if (_controller != null)
        {
            _controller.Stop();

            if (_hasPreviousControlMode)
            {
                _controller.SetControlMode(_previousControlMode);
            }
        }

        _hasPreviousControlMode = false;
    }

    private void ResolveController()
    {
        _controller = Owner?.GetComponent<CharacterControllerComponent>();
    }
}