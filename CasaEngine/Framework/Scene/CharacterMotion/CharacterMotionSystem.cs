using CasaEngine.Core.Time;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using GameWorld = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Framework.Scene.CharacterMotion;

public sealed class CharacterMotionSystem : IWorldSystem, ICharacterMotionService
{
    private const float CompletionEpsilon = 0.0001f;

    private readonly GameWorld _world;
    private readonly HashSet<Entity> _registeredEntities = [];
    private readonly List<CharacterControllerComponent> _controllers = [];
    private readonly List<NavigationAgentComponent> _navigationAgents = [];
    private readonly List<SteeringAgentComponent> _steeringAgents = [];
    private readonly List<SteeringPhysicsBridgeComponent> _steeringPhysicsBridges = [];
    private readonly List<CharacterControllerMoveToDriverComponent> _legacyMoveToDrivers = [];
    private readonly List<CharacterControllerNavigationDriverComponent> _navigationDrivers = [];
    private readonly List<CharacterControllerSteeringBridgeComponent> _steeringBridges = [];
    private readonly List<CharacterControllerRootMotionBridgeComponent> _rootMotionBridges = [];
    private readonly List<CharacterControllerAnimationBridgeComponent> _animationBridges = [];
    private readonly List<CharacterMoveToRequest> _moveToRequests = [];
    private readonly Dictionary<CharacterControllerComponent, CharacterMotionCommand> _commands = [];

    public CharacterMotionSystem(GameWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _world.EntityAdded += OnEntityAdded;
        _world.EntityRemoved += OnEntityRemoved;
        _world.EntitiesClear += OnEntitiesClear;
    }

    public ICharacterMotionHandle MoveTo(Entity entity, Vector3 destination, CharacterMoveToOptions options, object owner = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        options.Validate();

        CharacterControllerComponent controller = entity.GetComponent<CharacterControllerComponent>()
            ?? throw new InvalidOperationException($"MoveTo target entity '{entity.Name}' has no CharacterControllerComponent.");

        if (entity.RootComponent == null)
        {
            throw new InvalidOperationException($"MoveTo target entity '{entity.Name}' has no root component.");
        }

        var request = new CharacterMoveToRequest(this, owner, entity, controller, destination, options);
        _moveToRequests.Add(request);
        request.Begin();
        RegisterEntity(entity);
        RegisterComponent(controller);
        return request;
    }

    public void Cancel(ICharacterMotionHandle handle)
    {
        if (handle is CharacterMoveToRequest request)
        {
            request.Cancel();
        }
    }

    public void CancelOwner(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        for (int index = 0; index < _moveToRequests.Count; index++)
        {
            CharacterMoveToRequest request = _moveToRequests[index];
            if (ReferenceEquals(request.Owner, owner))
            {
                request.Cancel();
            }
        }
    }

    public bool HasRequestsFor(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        for (int index = 0; index < _moveToRequests.Count; index++)
        {
            CharacterMoveToRequest request = _moveToRequests[index];
            if (request.IsActive && ReferenceEquals(request.Owner, owner))
            {
                return true;
            }
        }

        return false;
    }

    public void Update(FrameTime frameTime)
    {
        RefreshEntityRegistrations();

        float elapsedTime = frameTime.DeltaTime;
        if (elapsedTime <= 0f)
        {
            return;
        }

        _commands.Clear();

        UpdateNavigationAgents(elapsedTime);
        UpdateSteeringAgents(elapsedTime);
        UpdateSteeringPhysicsBridges(elapsedTime);
        UpdateManagedMoveToRequests(elapsedTime);
        UpdateLegacyMoveToDrivers(elapsedTime);
        UpdateNavigationDrivers(elapsedTime);
        UpdateSteeringBridges(elapsedTime);
        UpdateRootMotionBridges(elapsedTime);
        ApplyCommands();
        UpdateControllers(elapsedTime);
        UpdateAnimationBridges(elapsedTime);
        RemoveCompletedMoveToRequests();
    }

    public void Clear()
    {
        for (int index = 0; index < _moveToRequests.Count; index++)
        {
            _moveToRequests[index].Cancel();
        }

        foreach (Entity entity in _registeredEntities)
        {
            entity.ComponentAdded -= OnEntityComponentAdded;
            entity.ComponentRemoved -= OnEntityComponentRemoved;
        }

        _registeredEntities.Clear();
        _controllers.Clear();
        _navigationAgents.Clear();
        _steeringAgents.Clear();
        _steeringPhysicsBridges.Clear();
        _legacyMoveToDrivers.Clear();
        _navigationDrivers.Clear();
        _steeringBridges.Clear();
        _rootMotionBridges.Clear();
        _animationBridges.Clear();
        _moveToRequests.Clear();
        _commands.Clear();
    }

    private void Submit(CharacterMotionCommand command)
    {
        if (_commands.TryGetValue(command.Controller, out CharacterMotionCommand existing)
            && existing.Authority >= command.Authority)
        {
            return;
        }

        _commands[command.Controller] = command;
    }

    private void UpdateNavigationAgents(float elapsedTime)
    {
        for (int index = 0; index < _navigationAgents.Count; index++)
        {
            _navigationAgents[index].Update(elapsedTime);
        }
    }

    private void UpdateSteeringAgents(float elapsedTime)
    {
        for (int index = 0; index < _steeringAgents.Count; index++)
        {
            _steeringAgents[index].Update(elapsedTime);
        }
    }

    private void UpdateSteeringPhysicsBridges(float elapsedTime)
    {
        for (int index = 0; index < _steeringPhysicsBridges.Count; index++)
        {
            _steeringPhysicsBridges[index].Update(elapsedTime);
        }
    }

    private void UpdateManagedMoveToRequests(float elapsedTime)
    {
        for (int index = 0; index < _moveToRequests.Count; index++)
        {
            CharacterMoveToRequest request = _moveToRequests[index];
            if (request.TryBuildCommand(elapsedTime, out CharacterMotionCommand command))
            {
                Submit(command);
            }
        }
    }

    private void UpdateLegacyMoveToDrivers(float elapsedTime)
    {
        for (int index = 0; index < _legacyMoveToDrivers.Count; index++)
        {
            if (_legacyMoveToDrivers[index].TryBuildMotionCommand(elapsedTime, out CharacterMotionCommand command))
            {
                Submit(command);
            }
        }
    }

    private void UpdateNavigationDrivers(float elapsedTime)
    {
        for (int index = 0; index < _navigationDrivers.Count; index++)
        {
            if (_navigationDrivers[index].TryBuildMotionCommand(elapsedTime, out CharacterMotionCommand command))
            {
                Submit(command);
            }
        }
    }

    private void UpdateSteeringBridges(float elapsedTime)
    {
        for (int index = 0; index < _steeringBridges.Count; index++)
        {
            if (_steeringBridges[index].TryBuildMotionCommand(elapsedTime, out CharacterMotionCommand command))
            {
                Submit(command);
            }
        }
    }

    private void UpdateRootMotionBridges(float elapsedTime)
    {
        for (int index = 0; index < _rootMotionBridges.Count; index++)
        {
            _rootMotionBridges[index].Update(elapsedTime);
        }
    }

    private void ApplyCommands()
    {
        foreach (KeyValuePair<CharacterControllerComponent, CharacterMotionCommand> pair in _commands)
        {
            pair.Value.Apply();
        }
    }

    private void UpdateControllers(float elapsedTime)
    {
        for (int index = 0; index < _controllers.Count; index++)
        {
            _controllers[index].Update(elapsedTime);
        }
    }

    private void UpdateAnimationBridges(float elapsedTime)
    {
        for (int index = 0; index < _animationBridges.Count; index++)
        {
            _animationBridges[index].Update(elapsedTime);
        }
    }

    private void RemoveCompletedMoveToRequests()
    {
        for (int index = _moveToRequests.Count - 1; index >= 0; index--)
        {
            if (!_moveToRequests[index].IsActive)
            {
                _moveToRequests.RemoveAt(index);
            }
        }
    }

    private void RefreshEntityRegistrations()
    {
        for (int index = 0; index < _world.Entities.Count; index++)
        {
            RegisterEntity(_world.Entities[index]);
        }
    }

    private void RegisterEntity(Entity entity)
    {
        if (!_registeredEntities.Add(entity))
        {
            return;
        }

        entity.ComponentAdded += OnEntityComponentAdded;
        entity.ComponentRemoved += OnEntityComponentRemoved;

        if (entity.RootComponent != null)
        {
            RegisterComponent(entity.RootComponent);
        }

        foreach (EntityComponent component in entity.Components)
        {
            RegisterComponent(component);
        }

        foreach (Entity child in entity.Children)
        {
            RegisterEntity(child);
        }
    }

    private void UnregisterEntity(Entity entity)
    {
        if (!_registeredEntities.Remove(entity))
        {
            return;
        }

        entity.ComponentAdded -= OnEntityComponentAdded;
        entity.ComponentRemoved -= OnEntityComponentRemoved;

        if (entity.RootComponent != null)
        {
            UnregisterComponent(entity.RootComponent);
        }

        foreach (EntityComponent component in entity.Components)
        {
            UnregisterComponent(component);
        }

        foreach (Entity child in entity.Children)
        {
            UnregisterEntity(child);
        }
    }

    private void RegisterComponent(EntityComponent component)
    {
        switch (component)
        {
            case CharacterControllerComponent controller:
                AddUnique(_controllers, controller);
                break;
            case NavigationAgentComponent navigationAgent:
                AddUnique(_navigationAgents, navigationAgent);
                break;
            case SteeringAgentComponent steeringAgent:
                AddUnique(_steeringAgents, steeringAgent);
                break;
            case SteeringPhysicsBridgeComponent steeringPhysicsBridge:
                AddUnique(_steeringPhysicsBridges, steeringPhysicsBridge);
                break;
            case CharacterControllerMoveToDriverComponent moveToDriver:
                AddUnique(_legacyMoveToDrivers, moveToDriver);
                break;
            case CharacterControllerNavigationDriverComponent navigationDriver:
                AddUnique(_navigationDrivers, navigationDriver);
                break;
            case CharacterControllerSteeringBridgeComponent steeringBridge:
                AddUnique(_steeringBridges, steeringBridge);
                break;
            case CharacterControllerRootMotionBridgeComponent rootMotionBridge:
                AddUnique(_rootMotionBridges, rootMotionBridge);
                break;
            case CharacterControllerAnimationBridgeComponent animationBridge:
                AddUnique(_animationBridges, animationBridge);
                break;
        }
    }

    private void UnregisterComponent(EntityComponent component)
    {
        switch (component)
        {
            case CharacterControllerComponent controller:
                _controllers.Remove(controller);
                break;
            case NavigationAgentComponent navigationAgent:
                _navigationAgents.Remove(navigationAgent);
                break;
            case SteeringAgentComponent steeringAgent:
                _steeringAgents.Remove(steeringAgent);
                break;
            case SteeringPhysicsBridgeComponent steeringPhysicsBridge:
                _steeringPhysicsBridges.Remove(steeringPhysicsBridge);
                break;
            case CharacterControllerMoveToDriverComponent moveToDriver:
                _legacyMoveToDrivers.Remove(moveToDriver);
                break;
            case CharacterControllerNavigationDriverComponent navigationDriver:
                _navigationDrivers.Remove(navigationDriver);
                break;
            case CharacterControllerSteeringBridgeComponent steeringBridge:
                _steeringBridges.Remove(steeringBridge);
                break;
            case CharacterControllerRootMotionBridgeComponent rootMotionBridge:
                _rootMotionBridges.Remove(rootMotionBridge);
                break;
            case CharacterControllerAnimationBridgeComponent animationBridge:
                _animationBridges.Remove(animationBridge);
                break;
        }
    }

    private void OnEntityAdded(object sender, Entity entity)
    {
        RegisterEntity(entity);
    }

    private void OnEntityRemoved(object sender, Entity entity)
    {
        UnregisterEntity(entity);
    }

    private void OnEntitiesClear(object sender, EventArgs e)
    {
        Clear();
    }

    private void OnEntityComponentAdded(object sender, EntityComponent component)
    {
        RegisterComponent(component);
    }

    private void OnEntityComponentRemoved(object sender, EntityComponent component)
    {
        UnregisterComponent(component);
    }

    private static void AddUnique<T>(List<T> components, T component)
        where T : class
    {
        for (int index = 0; index < components.Count; index++)
        {
            if (ReferenceEquals(components[index], component))
            {
                return;
            }
        }

        components.Add(component);
    }

    private sealed class CharacterMoveToRequest : ICharacterMotionHandle
    {
        private readonly CharacterMotionSystem _system;
        private readonly Entity _entity;
        private readonly CharacterControllerComponent _controller;
        private readonly CharacterMoveToOptions _options;
        private CharacterControlMode _previousControlMode;
        private float _elapsedSeconds;
        private bool _hasPreviousControlMode;

        public CharacterMoveToRequest(
            CharacterMotionSystem system,
            object owner,
            Entity entity,
            CharacterControllerComponent controller,
            Vector3 destination,
            CharacterMoveToOptions options)
        {
            _system = system;
            Owner = owner;
            _entity = entity;
            _controller = controller;
            Destination = destination;
            _options = options;
        }

        public object Owner { get; }

        public Vector3 Destination { get; }

        public bool IsActive { get; private set; }

        public bool HasCompleted { get; private set; }

        public bool HasReachedDestination { get; private set; }

        public bool HasTimedOut { get; private set; }

        public void Begin()
        {
            _elapsedSeconds = 0f;
            HasCompleted = false;
            HasReachedDestination = false;
            HasTimedOut = false;
            IsActive = true;
            _previousControlMode = _controller.ControlMode;
            _hasPreviousControlMode = true;
            _controller.SetControlMode(_options.ControlMode);
        }

        public void Cancel()
        {
            Complete(reachedDestination: false, timedOut: false);
        }

        public bool TryBuildCommand(float elapsedTime, out CharacterMotionCommand command)
        {
            command = default;
            if (!IsActive)
            {
                return false;
            }

            if (_controller.Owner?.RootComponent == null)
            {
                Complete(reachedDestination: false, timedOut: false);
                return false;
            }

            if (_options.TimeoutSeconds > 0f)
            {
                _elapsedSeconds += Math.Max(0f, elapsedTime);
                if (_elapsedSeconds >= _options.TimeoutSeconds)
                {
                    Complete(reachedDestination: false, timedOut: true);
                    return false;
                }
            }

            Vector3 position = _entity.RootComponent!.Position;
            Vector3 toDestination = Destination - position;
            Vector2 intent = new(toDestination.X, -toDestination.Z);
            float stoppingDistance = Math.Max(_options.StoppingDistance, CompletionEpsilon);

            if (intent.LengthSquared() <= stoppingDistance * stoppingDistance)
            {
                Complete(reachedDestination: true, timedOut: false);
                return false;
            }

            if (intent.LengthSquared() > 1f)
            {
                intent.Normalize();
            }

            command = new CharacterMotionCommand(
                _controller,
                intent,
                _options.ControlMode,
                CharacterMotionAuthority.Cutscene);
            return true;
        }

        private void Complete(bool reachedDestination, bool timedOut)
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            HasCompleted = true;
            HasReachedDestination = reachedDestination;
            HasTimedOut = timedOut;
            _controller.Stop();

            if (_hasPreviousControlMode)
            {
                _controller.SetControlMode(_previousControlMode);
            }

            _hasPreviousControlMode = false;
        }
    }
}