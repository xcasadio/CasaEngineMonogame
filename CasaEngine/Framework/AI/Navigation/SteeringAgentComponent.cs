
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class SteeringAgentComponent : EntityComponent
{
    private readonly List<SteeringBehaviorRuntime> _behaviors = [];
    private readonly List<Entity> _neighborCache = [];
    private readonly List<Entity> _nearbyEntities = [];
    private PhysicsBaseComponent? _physicsComponent;
    private SceneComponent? _sceneComponent;
    private bool _neighborCacheValid;
    private float _neighborCacheRadius;
    private Vector3 _neighborCacheOrigin;

    public SteeringAgentSettings Settings { get; } = new();

    public IReadOnlyList<SteeringBehaviorRuntime> Behaviors => _behaviors;

    public Vector3? TargetPosition { get; set; }

    public SteeringAgentKinematics Kinematics { get; private set; }

    public SteeringCommand CurrentCommand { get; private set; }

    public Vector3 LastTotalForce { get; private set; }

    public Vector3 LastDesiredVelocity { get; private set; }

    public Vector3 LastDesiredFacing { get; private set; } = Vector3.Forward;

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ResolveDependencies();
        InvalidateNeighborCache();
        RefreshKinematics();
        CurrentCommand = SteeringCommand.None(Settings.OutputMode);
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveDependencies();
        InvalidateNeighborCache();
        RefreshKinematics();
    }

    public override void Update(float elapsedTime)
    {
        InvalidateNeighborCache();
        RefreshKinematics();
        CurrentCommand = CalculateCommand(elapsedTime);
    }

    public void RegisterBehavior(SteeringBehaviorRuntime behavior)
    {
        for (int index = 0; index < _behaviors.Count; index++)
        {
            if (_behaviors[index].Name == behavior.Name)
            {
                _behaviors[index] = behavior;
                return;
            }
        }

        _behaviors.Add(behavior);
    }

    public bool RemoveBehavior(string name)
    {
        for (int index = 0; index < _behaviors.Count; index++)
        {
            if (_behaviors[index].Name == name)
            {
                _behaviors.RemoveAt(index);
                return true;
            }
        }

        return false;
    }

    public bool SetBehaviorEnabled(string name, bool enabled)
    {
        SteeringBehaviorRuntime? behavior = GetBehavior(name);

        if (behavior == null)
        {
            return false;
        }

        behavior.IsEnabled = enabled;
        return true;
    }

    public bool SetBehaviorWeight(string name, float weight)
    {
        SteeringBehaviorRuntime? behavior = GetBehavior(name);

        if (behavior == null)
        {
            return false;
        }

        behavior.Weight = weight;
        return true;
    }

    public SteeringBehaviorRuntime? GetBehavior(string name)
    {
        for (int index = 0; index < _behaviors.Count; index++)
        {
            if (_behaviors[index].Name == name)
            {
                return _behaviors[index];
            }
        }

        return null;
    }

    public Entity? FindEntity(string entityName)
    {
        if (Owner?.World == null || string.IsNullOrWhiteSpace(entityName))
        {
            return null;
        }

        for (int index = 0; index < Owner.World.Entities.Count; index++)
        {
            Entity entity = Owner.World.Entities[index];
            if (string.Equals(entity.Name, entityName, StringComparison.OrdinalIgnoreCase))
            {
                return entity;
            }
        }

        return null;
    }

    public IReadOnlyList<Entity> FindNeighborEntities(float radius)
    {
        if (Owner?.World == null || Owner == null)
        {
            return Array.Empty<Entity>();
        }

        if (_neighborCacheValid
            && Math.Abs(_neighborCacheRadius - radius) <= 0.01f
            && Vector3.DistanceSquared(_neighborCacheOrigin, Kinematics.Position) <= 0.0001f)
        {
            return _neighborCache;
        }

        _neighborCache.Clear();
        _nearbyEntities.Clear();
        BoundingBox bounds = CreateQueryBounds(Kinematics.Position, radius);
        Owner.World.QueryEntities(bounds, _nearbyEntities, static entity => entity.GetComponent<SteeringAgentComponent>() != null);
        float radiusSquared = radius * radius;
        for (int index = 0; index < _nearbyEntities.Count; index++)
        {
            Entity entity = _nearbyEntities[index];
            if (ReferenceEquals(entity, Owner) || entity.GetComponent<SteeringAgentComponent>() == null)
            {
                continue;
            }

            if (!TryGetEntityMotion(entity, out Vector3 position, out _, out _))
            {
                continue;
            }

            if (Vector3.DistanceSquared(Kinematics.Position, position) <= radiusSquared)
            {
                _neighborCache.Add(entity);
            }
        }

        _neighborCacheValid = true;
        _neighborCacheRadius = radius;
        _neighborCacheOrigin = Kinematics.Position;
        return _neighborCache;
    }

    public IEnumerable<Entity> FindEntitiesByPredicate(Func<Entity, bool> predicate)
    {
        if (Owner?.World == null)
        {
            yield break;
        }

        for (int index = 0; index < Owner.World.Entities.Count; index++)
        {
            Entity entity = Owner.World.Entities[index];
            if (predicate(entity))
            {
                yield return entity;
            }
        }
    }

    public bool TryGetEntityMotion(string entityName, out Vector3 position, out Vector3 velocity, out Vector3 forward)
    {
        return TryGetEntityMotion(FindEntity(entityName), out position, out velocity, out forward);
    }

    public bool TryGetEntityMotion(Entity? entity, out Vector3 position, out Vector3 velocity, out Vector3 forward)
    {
        position = Vector3.Zero;
        velocity = Vector3.Zero;
        forward = Vector3.Right;

        if (entity == null)
        {
            return false;
        }

        SceneComponent? sceneComponent = entity.RootComponent;
        PhysicsBaseComponent? physicsComponent = entity.GetComponent<PhysicsBaseComponent>();

        position = sceneComponent?.Position ?? Vector3.Zero;
        velocity = physicsComponent?.Velocity ?? Vector3.Zero;

        if (velocity.LengthSquared() > float.Epsilon)
        {
            forward = velocity;
        }
        else if (sceneComponent != null)
        {
            forward = sceneComponent.WorldMatrixNoScale.Right;
        }

        if (forward.LengthSquared() <= float.Epsilon)
        {
            forward = Vector3.Right;
        }
        else
        {
            forward.Normalize();
        }

        return true;
    }

    public bool TryGetCollisionRadius(Entity entity, out float radius)
    {
        radius = 0.0f;
        CircleCollisionComponent? circle = entity.GetComponent<CircleCollisionComponent>();
        if (circle != null)
        {
            radius = circle.Circle.Radius;
            return true;
        }

        return false;
    }

    public bool TryGetWallSegment(Entity entity, out Vector2 start, out Vector2 end)
    {
        start = Vector2.Zero;
        end = Vector2.Zero;

        Type entityType = entity.GetType();
        var startProperty = entityType.GetProperty("Start");
        var endProperty = entityType.GetProperty("End");

        if (startProperty?.PropertyType == typeof(Vector2)
            && endProperty?.PropertyType == typeof(Vector2))
        {
            start = (Vector2)startProperty.GetValue(entity)!;
            end = (Vector2)endProperty.GetValue(entity)!;
            return true;
        }

        return false;
    }

    public bool TryGetWallNormal(Entity entity, out Vector2 normal)
    {
        normal = Vector2.Zero;

        Type entityType = entity.GetType();
        var normalProperty = entityType.GetProperty("Normal");
        if (normalProperty?.PropertyType == typeof(Vector2))
        {
            normal = (Vector2)normalProperty.GetValue(entity)!;
            if (normal.LengthSquared() > float.Epsilon)
            {
                normal.Normalize();
                return true;
            }
        }

        return false;
    }

    public void RefreshKinematics()
    {
        ResolveDependencies();

        Vector3 position = _sceneComponent?.Position ?? Vector3.Zero;
        Vector3 velocity = _physicsComponent?.Velocity ?? Vector3.Zero;
        Vector3 forward = ResolveForward();
        Vector3 right = ResolveRight(forward);

        Kinematics = new SteeringAgentKinematics(
            position,
            velocity,
            forward,
            right,
            Settings.Mass,
            Settings.MaxSpeed,
            Settings.MaxForce,
            Settings.MaxTurnRate);
    }

    public SteeringCommand CalculateCommand(float elapsedTime)
    {
        Vector3 totalForce = Settings.UsePrioritizedAccumulation
            ? CalculatePrioritizedForce(elapsedTime)
            : CalculateWeightedForce(elapsedTime);

        totalForce = totalForce.Truncate(Settings.MaxForce);

        Vector3 desiredVelocity = totalForce;
        if (Settings.Mass > 0.0f)
        {
            desiredVelocity = (Kinematics.Velocity + totalForce / Settings.Mass).Truncate(Settings.MaxSpeed);
        }
        else
        {
            desiredVelocity = desiredVelocity.Truncate(Settings.MaxSpeed);
        }

        Vector3 desiredFacing = desiredVelocity.LengthSquared() > float.Epsilon
            ? Vector3.Normalize(desiredVelocity)
            : Kinematics.Forward;

        LastTotalForce = totalForce;
        LastDesiredVelocity = desiredVelocity;
        LastDesiredFacing = desiredFacing;

        return new SteeringCommand(totalForce, desiredVelocity, desiredFacing, Settings.OutputMode);
    }

    private Vector3 CalculateWeightedForce(float elapsedTime)
    {
        Vector3 totalForce = Vector3.Zero;

        for (int index = 0; index < _behaviors.Count; index++)
        {
            totalForce += _behaviors[index].Evaluate(Kinematics, this, elapsedTime);
        }

        return totalForce;
    }

    private Vector3 CalculatePrioritizedForce(float elapsedTime)
    {
        Vector3 totalForce = Vector3.Zero;

        for (int index = 0; index < _behaviors.Count; index++)
        {
            Vector3 behaviorForce = _behaviors[index].Evaluate(Kinematics, this, elapsedTime);
            if (!TryAccumulateForce(ref totalForce, behaviorForce, Settings.MaxForce))
            {
                break;
            }
        }

        return totalForce;
    }

    private static bool TryAccumulateForce(ref Vector3 runningTotal, Vector3 forceToAdd, float maxForce)
    {
        float magnitudeSoFar = runningTotal.Length();
        float magnitudeRemaining = maxForce - magnitudeSoFar;
        if (magnitudeRemaining <= 0.0f)
        {
            return false;
        }

        float magnitudeToAdd = forceToAdd.Length();
        if (magnitudeToAdd <= magnitudeRemaining)
        {
            runningTotal += forceToAdd;
            return true;
        }

        if (magnitudeToAdd <= float.Epsilon)
        {
            return true;
        }

        runningTotal += Vector3.Normalize(forceToAdd) * magnitudeRemaining;
        return false;
    }

    public override EntityComponent Clone()
    {
        SteeringAgentComponent clone = new();
        clone.Settings.Mass = Settings.Mass;
        clone.Settings.MaxSpeed = Settings.MaxSpeed;
        clone.Settings.MaxForce = Settings.MaxForce;
        clone.Settings.MaxTurnRate = Settings.MaxTurnRate;
        clone.Settings.OutputMode = Settings.OutputMode;
        clone.Settings.UsePrioritizedAccumulation = Settings.UsePrioritizedAccumulation;
        clone.TargetPosition = TargetPosition;

        for (int index = 0; index < _behaviors.Count; index++)
        {
            clone.RegisterBehavior(_behaviors[index].Clone());
        }

        return clone;
    }

    private void ResolveDependencies()
    {
        if (Owner == null)
        {
            _physicsComponent = null;
            _sceneComponent = null;
            return;
        }

        _sceneComponent ??= Owner.RootComponent;

        _physicsComponent ??= Owner.GetComponent<PhysicsBaseComponent>();
    }

    private void InvalidateNeighborCache()
    {
        _neighborCache.Clear();
        _nearbyEntities.Clear();
        _neighborCacheValid = false;
        _neighborCacheRadius = 0.0f;
        _neighborCacheOrigin = Vector3.Zero;
    }

    private static BoundingBox CreateQueryBounds(Vector3 origin, float radius)
    {
        Vector3 extent = new(radius, radius, Math.Max(1.0f, radius));
        return new BoundingBox(origin - extent, origin + extent);
    }

    private Vector3 ResolveForward()
    {
        if (_physicsComponent?.Velocity.LengthSquared() > float.Epsilon)
        {
            return Vector3.Normalize(_physicsComponent.Velocity);
        }

        if (_sceneComponent != null)
        {
            Vector3 forward = _sceneComponent.WorldMatrixNoScale.Right;
            if (forward.LengthSquared() > float.Epsilon)
            {
                return Vector3.Normalize(forward);
            }
        }

        return Vector3.Right;
    }

    private static Vector3 ResolveRight(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.UnitZ, forward);

        if (right.LengthSquared() <= float.Epsilon)
        {
            return Vector3.Right;
        }

        return Vector3.Normalize(right);
    }
}