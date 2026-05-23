using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.CharacterMotion;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class CharacterControllerNavigationDriverComponent : EntityComponent, IWorldSystemDrivenComponent
{
    private const float CompletionEpsilon = 0.0001f;
    private readonly List<Vector3> _waypoints = [];
    private CharacterControllerComponent _controller;
    private Entity _targetEntity;
    private CharacterControlMode _previousControlMode;
    private bool _hasPreviousControlMode;
    private bool _hasTargetEntity;

    public bool AutoSetControlMode { get; set; } = true;

    public CharacterControlMode ControlMode { get; set; } = CharacterControlMode.AI;

    public bool RestoreControlModeOnComplete { get; set; } = true;

    public float StoppingDistance { get; private set; } = 0.1f;

    public bool IsMoving { get; private set; }

    public bool HasReachedDestination { get; private set; }

    public int CurrentWaypointIndex { get; private set; }

    public IReadOnlyList<Vector3> Waypoints => _waypoints;

    public Vector2 LastMoveIntent { get; private set; }

    public CharacterControllerComponent Controller => _controller;

    public Entity TargetEntity => _targetEntity;

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

    public void MoveTo(Vector3 destination, float stoppingDistance = 0.1f)
    {
        _waypoints.Clear();
        _waypoints.Add(destination);
        _hasTargetEntity = false;
        _targetEntity = null;
        BeginNavigation(stoppingDistance);
    }

    public void SetPath(IReadOnlyList<Vector3> waypoints, float stoppingDistance = 0.1f)
    {
        ArgumentNullException.ThrowIfNull(waypoints);

        _waypoints.Clear();
        for (int index = 0; index < waypoints.Count; index++)
        {
            _waypoints.Add(waypoints[index]);
        }

        _hasTargetEntity = false;
        _targetEntity = null;
        BeginNavigation(stoppingDistance);
    }

    public void FollowTarget(Entity targetEntity, float stoppingDistance = 0.1f)
    {
        ArgumentNullException.ThrowIfNull(targetEntity);

        _waypoints.Clear();
        _targetEntity = targetEntity;
        _hasTargetEntity = true;
        BeginNavigation(stoppingDistance);
    }

    public void Cancel()
    {
        Complete(reachedDestination: false);
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
            Complete(reachedDestination: false);
            return false;
        }

        Vector2 intent;
        while (true)
        {
            if (!TryGetCurrentTarget(out Vector3 targetPosition))
            {
                Complete(reachedDestination: false);
                return false;
            }

            Vector3 position = _controller.Owner.RootComponent.Position;
            Vector3 toTarget = targetPosition - position;
            intent = new Vector2(toTarget.X, -toTarget.Z);
            float stoppingDistance = Math.Max(StoppingDistance, CompletionEpsilon);

            if (intent.LengthSquared() > stoppingDistance * stoppingDistance)
            {
                break;
            }

            if (!_hasTargetEntity && CurrentWaypointIndex < _waypoints.Count - 1)
            {
                CurrentWaypointIndex++;
                continue;
            }

            Complete(reachedDestination: true);
            return false;
        }

        if (intent.LengthSquared() > 1f)
        {
            intent.Normalize();
        }

        LastMoveIntent = intent;
        command = new CharacterMotionCommand(
            _controller,
            intent,
            ControlMode,
            CharacterMotionAuthority.Navigation,
            AutoSetControlMode);
        return true;
    }

    public override EntityComponent Clone()
    {
        return new CharacterControllerNavigationDriverComponent
        {
            AutoSetControlMode = AutoSetControlMode,
            ControlMode = ControlMode,
            RestoreControlModeOnComplete = RestoreControlModeOnComplete,
        };
    }

    private void BeginNavigation(float stoppingDistance)
    {
        if (stoppingDistance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(stoppingDistance));
        }

        ResolveController();
        if (_controller == null)
        {
            throw new InvalidOperationException("Navigation driver requires a CharacterControllerComponent on the owner entity.");
        }

        if (!_hasTargetEntity && _waypoints.Count == 0)
        {
            Complete(reachedDestination: true);
            return;
        }

        StoppingDistance = stoppingDistance;
        CurrentWaypointIndex = 0;
        LastMoveIntent = Vector2.Zero;
        HasReachedDestination = false;
        IsMoving = true;
        _previousControlMode = _controller.ControlMode;
        _hasPreviousControlMode = true;

        if (AutoSetControlMode)
        {
            _controller.SetControlMode(ControlMode);
        }
    }

    private void Complete(bool reachedDestination)
    {
        IsMoving = false;
        HasReachedDestination = reachedDestination;
        LastMoveIntent = Vector2.Zero;

        if (_controller != null)
        {
            _controller.Stop();
            if (RestoreControlModeOnComplete && _hasPreviousControlMode)
            {
                _controller.SetControlMode(_previousControlMode);
            }
        }

        _hasPreviousControlMode = false;
    }

    private bool TryGetCurrentTarget(out Vector3 targetPosition)
    {
        targetPosition = Vector3.Zero;
        if (_hasTargetEntity)
        {
            if (_targetEntity?.RootComponent == null)
            {
                return false;
            }

            targetPosition = _targetEntity.RootComponent.Position;
            return true;
        }

        if (CurrentWaypointIndex < 0 || CurrentWaypointIndex >= _waypoints.Count)
        {
            return false;
        }

        targetPosition = _waypoints[CurrentWaypointIndex];
        return true;
    }

    private void ResolveController()
    {
        _controller = Owner?.GetComponent<CharacterControllerComponent>();
    }
}