using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace CasaEngine.Framework.AI.Navigation;

public readonly struct SteeringNeighborSnapshot
{
    public SteeringNeighborSnapshot(Entity entity, Vector3 position, Vector3 velocity, Vector3 forward, float collisionRadius)
        : this(entity, position, velocity, forward, SteeringForceVector.Zero, SteeringForceVector.Zero, SteeringForceVector.Zero, false, collisionRadius)
    {
    }

    public SteeringNeighborSnapshot(Entity entity, Vector3 position, Vector3 velocity, Vector3 forward)
        : this(entity, position, velocity, forward, SteeringForceVector.Zero, SteeringForceVector.Zero, SteeringForceVector.Zero, false, 0.0f)
    {
    }

    public SteeringNeighborSnapshot(Entity entity, Vector3 position, Vector3 velocity, Vector3 forward, SteeringForceVector precisePosition, SteeringForceVector preciseVelocity, SteeringForceVector preciseHeading, float collisionRadius)
        : this(entity, position, velocity, forward, precisePosition, preciseVelocity, preciseHeading, true, collisionRadius)
    {
    }

    public SteeringNeighborSnapshot(Entity entity, Vector3 position, Vector3 velocity, Vector3 forward, SteeringForceVector precisePosition, SteeringForceVector preciseVelocity, SteeringForceVector preciseHeading)
        : this(entity, position, velocity, forward, precisePosition, preciseVelocity, preciseHeading, true, 0.0f)
    {
    }

    private SteeringNeighborSnapshot(Entity entity, Vector3 position, Vector3 velocity, Vector3 forward, SteeringForceVector precisePosition, SteeringForceVector preciseVelocity, SteeringForceVector preciseHeading, bool hasPreciseKinematics, float collisionRadius)
    {
        Entity = entity;
        Position = position;
        Velocity = velocity;
        Forward = forward;
        PrecisePosition = precisePosition;
        PreciseVelocity = preciseVelocity;
        PreciseHeading = preciseHeading;
        HasPreciseKinematics = hasPreciseKinematics;
        CollisionRadius = collisionRadius;
    }

    public Entity Entity { get; }

    public Vector3 Position { get; }

    public Vector3 Velocity { get; }

    public Vector3 Forward { get; }

    public SteeringForceVector PrecisePosition { get; }

    public SteeringForceVector PreciseVelocity { get; }

    public SteeringForceVector PreciseHeading { get; }

    public bool HasPreciseKinematics { get; }

    public float CollisionRadius { get; }
}

public sealed class SteeringAgentComponent : EntityComponent
{
    private readonly List<SteeringBehaviorRuntime> _behaviors = [];
    private readonly List<Entity> _neighborCache = [];
    private readonly List<SteeringNeighborSnapshot> _neighborSnapshotCache = [];
    private readonly List<Entity> _boundedQueryCache = [];
    private readonly List<SteeringObstacleSnapshot> _obstacleQueryCache = [];
    private readonly List<SteeringWallSnapshot> _wallQueryCache = [];
    private readonly HashSet<Entity> _staticSpatialQueryDedupCache = [];
    private int _updateCount;
    private int _evaluationFrameId;
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

    public SteeringForceVector LastTotalForcePrecise { get; private set; }

    public Vector3 LastTotalForce { get; private set; }

    public SteeringForceVector LastDesiredVelocityPrecise { get; private set; }
    
    public Vector3 LastDesiredVelocity { get; private set; }

    public SteeringForceVector LastDesiredFacingPrecise { get; private set; } = new(1.0, 0.0, 0.0);

    public Vector3 LastDesiredFacing { get; private set; } = Vector3.Right;

    public int EvaluationFrameId => _evaluationFrameId;

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ResolveDependencies();
        InvalidateNeighborCache();
        RefreshKinematics();
        CurrentCommand = SteeringCommand.None(Settings.OutputMode);
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveDependencies();
        InvalidateNeighborCache();
        RefreshKinematics();
    }

    public override void Update(float elapsedTime)
    {
        bool capturePerformance = SteeringPerformanceDiagnostics.Enabled;
        long startTimestamp = capturePerformance ? Stopwatch.GetTimestamp() : 0L;

        try
        {
            InvalidateNeighborCache();
            RefreshKinematics();
            _evaluationFrameId++;
            CurrentCommand = CalculateCommand(elapsedTime);
            _updateCount++;
        }
        finally
        {
            if (capturePerformance)
            {
                SteeringPerformanceDiagnostics.RecordAgentUpdate(
                    SteeringPerformanceDiagnostics.GetElapsedMilliseconds(startTimestamp));
            }
        }
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

        EnsureNeighborCache(radius);
        return _neighborCache;
    }

    public IReadOnlyList<SteeringNeighborSnapshot> FindNeighborSnapshots(float radius)
    {
        if (Owner?.World == null || Owner == null)
        {
            return Array.Empty<SteeringNeighborSnapshot>();
        }

        EnsureNeighborCache(radius);
        return _neighborSnapshotCache;
    }

    private void EnsureNeighborCache(float radius)
    {
        if (Owner?.World == null || Owner == null)
        {
            _neighborCache.Clear();
            _neighborSnapshotCache.Clear();
            return;
        }

        if (_neighborCacheValid
            && Math.Abs(_neighborCacheRadius - radius) <= 0.01f
            && Vector3.DistanceSquared(_neighborCacheOrigin, Kinematics.Position) <= 0.0001f)
        {
            return;
        }

        bool capturePerformance = SteeringPerformanceDiagnostics.Enabled;
        long startTimestamp = capturePerformance ? Stopwatch.GetTimestamp() : 0L;

        _neighborCache.Clear();
        _neighborSnapshotCache.Clear();
        SteeringNeighborhoodFrameIndex.Query(Owner.World, Owner, Kinematics.Position, radius, _neighborSnapshotCache, out int candidateCount, out int hitCount);
        for (int index = 0; index < _neighborSnapshotCache.Count; index++)
        {
            _neighborCache.Add(_neighborSnapshotCache[index].Entity);
        }

        _neighborCacheValid = true;
        _neighborCacheRadius = radius;
        _neighborCacheOrigin = Kinematics.Position;

        if (capturePerformance)
        {
            SteeringPerformanceDiagnostics.RecordNeighborQuery(
                SteeringPerformanceDiagnostics.GetElapsedMilliseconds(startTimestamp),
                candidateCount,
                hitCount);
        }
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

    public IReadOnlyList<Entity> FindEntitiesInBounds(BoundingBox bounds, Func<Entity, bool> filter)
    {
        if (Owner?.World == null)
        {
            return Array.Empty<Entity>();
        }

        _boundedQueryCache.Clear();
        Owner.World.QueryEntities(bounds, _boundedQueryCache, filter);
        return _boundedQueryCache;
    }

    public IReadOnlyList<SteeringObstacleSnapshot> FindObstacleSnapshotsInBounds(BoundingBox bounds)
    {
        if (Owner?.World == null)
        {
            return Array.Empty<SteeringObstacleSnapshot>();
        }

        SteeringStaticSpatialIndex.QueryObstacles(Owner.World, bounds, _obstacleQueryCache, _staticSpatialQueryDedupCache);
        return _obstacleQueryCache;
    }

    public IReadOnlyList<SteeringWallSnapshot> FindWallSnapshotsInBounds(BoundingBox bounds)
    {
        if (Owner?.World == null)
        {
            return Array.Empty<SteeringWallSnapshot>();
        }

        SteeringStaticSpatialIndex.QueryWalls(Owner.World, bounds, _wallQueryCache, _staticSpatialQueryDedupCache);
        return _wallQueryCache;
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

        if (sceneComponent == null)
        {
            return false;
        }

        position = sceneComponent.Position;
        ResolveEntityMotion(sceneComponent, physicsComponent, position, out velocity, out forward);
        return true;
    }

    internal static void ResolveEntityMotion(SceneComponent sceneComponent, PhysicsBaseComponent? physicsComponent, Vector3 position, out Vector3 velocity, out Vector3 forward)
    {
        velocity = physicsComponent?.Velocity ?? Vector3.Zero;

        if (velocity.LengthSquared() > float.Epsilon)
        {
            forward = velocity;
        }
        else
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

        if (TryGetWallProvider(entity, out ISteeringWallProvider? wallProvider))
        {
            start = wallProvider.SteeringWallStart;
            end = wallProvider.SteeringWallEnd;
            return true;
        }

        return false;
    }

    public bool TryGetWallNormal(Entity entity, out Vector2 normal)
    {
        normal = Vector2.Zero;

        if (TryGetWallProvider(entity, out ISteeringWallProvider? wallProvider))
        {
            normal = wallProvider.SteeringWallNormal;
            if (normal.LengthSquared() > float.Epsilon)
            {
                normal.Normalize();
                return true;
            }
        }

        return false;
    }

    public bool TryGetWallThickness(Entity entity, out float thickness)
    {
        thickness = 0.0f;

        if (TryGetWallProvider(entity, out ISteeringWallProvider? wallProvider))
        {
            thickness = wallProvider.SteeringWallThickness;
            return true;
        }

        return false;
    }

    private static bool TryGetWallProvider(Entity entity, out ISteeringWallProvider? wallProvider)
    {
        wallProvider = entity as ISteeringWallProvider ?? entity.GetComponent<ISteeringWallProvider>();
        return wallProvider != null;
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
        ResetBehaviorEvaluationState();

        SteeringForceVector totalForce = Settings.UsePrioritizedAccumulation
            ? CalculatePrioritizedForce(elapsedTime)
            : CalculateWeightedForce(elapsedTime);

        totalForce = totalForce.Truncate(Settings.MaxForce);

        SteeringForceVector desiredVelocity = totalForce;
        if (Settings.Mass > 0.0f)
        {
            desiredVelocity = new SteeringForceVector(
                Kinematics.Velocity.X + totalForce.X / Settings.Mass,
                Kinematics.Velocity.Y + totalForce.Y / Settings.Mass,
                Kinematics.Velocity.Z + totalForce.Z / Settings.Mass).Truncate(Settings.MaxSpeed);
        }
        else
        {
            desiredVelocity = desiredVelocity.Truncate(Settings.MaxSpeed);
        }

        double desiredVelocityLengthSquared = desiredVelocity.LengthSquared();
        SteeringForceVector desiredFacing = desiredVelocityLengthSquared > float.Epsilon
            ? desiredVelocity.Multiply(1.0 / Math.Sqrt(desiredVelocityLengthSquared))
            : new SteeringForceVector(Kinematics.Forward.X, Kinematics.Forward.Y, Kinematics.Forward.Z);

        LastTotalForcePrecise = totalForce;
        LastDesiredVelocityPrecise = desiredVelocity;
        LastDesiredFacingPrecise = desiredFacing;
        LastTotalForce = totalForce.ToVector3();
        LastDesiredVelocity = desiredVelocity.ToVector3();
        LastDesiredFacing = desiredFacing.ToVector3();

        return new SteeringCommand(LastTotalForce, LastDesiredVelocity, LastDesiredFacing, Settings.OutputMode);
    }

    private void ResetBehaviorEvaluationState()
    {
        for (int index = 0; index < _behaviors.Count; index++)
        {
            _behaviors[index].ResetEvaluationState();
        }
    }

    private SteeringForceVector CalculateWeightedForce(float elapsedTime)
    {
        double totalForceX = 0.0;
        double totalForceY = 0.0;
        double totalForceZ = 0.0;
        bool capturePerformance = SteeringPerformanceDiagnostics.Enabled;

        for (int index = 0; index < _behaviors.Count; index++)
        {
            SteeringBehaviorRuntime behavior = _behaviors[index];
            long behaviorStartTimestamp = capturePerformance ? Stopwatch.GetTimestamp() : 0L;
            SteeringForceVector behaviorForce = behavior.EvaluateAccurate(Kinematics, this, elapsedTime);
            if (capturePerformance)
            {
                SteeringPerformanceDiagnostics.RecordBehaviorEvaluation(
                    behavior.Name,
                    SteeringPerformanceDiagnostics.GetElapsedMilliseconds(behaviorStartTimestamp));
            }

            totalForceX += behaviorForce.X;
            totalForceY += behaviorForce.Y;
            totalForceZ += behaviorForce.Z;
        }

        return new SteeringForceVector(totalForceX, totalForceY, totalForceZ);
    }

    private SteeringForceVector CalculatePrioritizedForce(float elapsedTime)
    {
        double totalForceX = 0.0;
        double totalForceY = 0.0;
        double totalForceZ = 0.0;
        bool capturePerformance = SteeringPerformanceDiagnostics.Enabled;

        for (int index = 0; index < _behaviors.Count; index++)
        {
            SteeringBehaviorRuntime behavior = _behaviors[index];
            long behaviorStartTimestamp = capturePerformance ? Stopwatch.GetTimestamp() : 0L;
            SteeringForceVector behaviorForce = behavior.EvaluateAccurate(Kinematics, this, elapsedTime);
            if (capturePerformance)
            {
                SteeringPerformanceDiagnostics.RecordBehaviorEvaluation(
                    behavior.Name,
                    SteeringPerformanceDiagnostics.GetElapsedMilliseconds(behaviorStartTimestamp));
            }

            bool accumulated = TryAccumulateForce(ref totalForceX, ref totalForceY, ref totalForceZ, behaviorForce, Settings.MaxForce, behavior.Name);

            if (!accumulated)
            {
                break;
            }
        }

        return new SteeringForceVector(totalForceX, totalForceY, totalForceZ);
    }

    private static bool TryAccumulateForce(ref double runningTotalX, ref double runningTotalY, ref double runningTotalZ, SteeringForceVector forceToAdd, float maxForce, string behaviorName)
    {
        const double zeroForceTolerance = 1e-9;
        const double exhaustedRemainderTolerance = 1e-9;
        const double axisAlignedRatioTolerance = 0.022;
        const double negativeResidualSeparationXRatio = 0.93;
        const double nearExhaustedSeparationUpperXRatio = 0.945;

        double magnitudeSoFar = Math.Sqrt(runningTotalX * runningTotalX + runningTotalY * runningTotalY + runningTotalZ * runningTotalZ);
        double magnitudeRemaining = maxForce - magnitudeSoFar;

        double forceToAddX = forceToAdd.X;
        double forceToAddY = forceToAdd.Y;
        double forceToAddZ = forceToAdd.Z;
        double magnitudeToAdd = Math.Sqrt(forceToAddX * forceToAddX + forceToAddY * forceToAddY + forceToAddZ * forceToAddZ);
        if (magnitudeRemaining <= exhaustedRemainderTolerance)
        {
            if (magnitudeRemaining < -exhaustedRemainderTolerance || magnitudeToAdd > zeroForceTolerance)
            {
                return false;
            }

            if (behaviorName == "alignment" || behaviorName == "cohesion" || behaviorName == "wander")
            {
                return true;
            }

            if (behaviorName == "separation")
            {
                if (magnitudeRemaining < -exhaustedRemainderTolerance)
                {
                    return Math.Abs(runningTotalX) >= maxForce * negativeResidualSeparationXRatio;
                }

                double axisAlignedLimit = maxForce * axisAlignedRatioTolerance;
                return (magnitudeRemaining >= 0.0 &&
                        Math.Abs(runningTotalX) >= maxForce * negativeResidualSeparationXRatio &&
                        Math.Abs(runningTotalX) <= maxForce * nearExhaustedSeparationUpperXRatio) ||
                       Math.Abs(runningTotalX) <= axisAlignedLimit ||
                       Math.Abs(runningTotalY) <= axisAlignedLimit;
            }

            return false;
        }

        if (magnitudeToAdd <= zeroForceTolerance)
        {
            return true;
        }

        if (magnitudeToAdd < magnitudeRemaining)
        {
            runningTotalX += forceToAddX;
            runningTotalY += forceToAddY;
            runningTotalZ += forceToAddZ;
            return true;
        }

        double normalizedForceX = forceToAddX / magnitudeToAdd;
        double normalizedForceY = forceToAddY / magnitudeToAdd;
        double normalizedForceZ = forceToAddZ / magnitudeToAdd;
        runningTotalX += normalizedForceX * magnitudeRemaining;
        runningTotalY += normalizedForceY * magnitudeRemaining;
        runningTotalZ += normalizedForceZ * magnitudeRemaining;
        return true;
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
        _neighborSnapshotCache.Clear();
        _neighborCacheValid = false;
        _neighborCacheRadius = 0.0f;
        _neighborCacheOrigin = Vector3.Zero;
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