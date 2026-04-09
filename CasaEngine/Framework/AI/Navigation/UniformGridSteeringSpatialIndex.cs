using System;
using System.Collections.Generic;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using GameWorld = CasaEngine.Framework.World.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class UniformGridSteeringSpatialIndex : ISteeringSpatialIndex2D
{
    private const float DefaultCellSize = 96.0f;

    private readonly record struct ObstacleProviderRegistration(Entity Owner, ISteeringObstacleProvider Provider);

    private readonly record struct WallProviderRegistration(Entity Owner, ISteeringWallProvider Provider);

    private readonly GameWorld _world;
    private readonly Dictionary<long, List<SteeringNeighborSnapshot>> _neighborCells = [];
    private readonly Dictionary<long, List<SteeringObstacleSnapshot>> _obstacleCells = [];
    private readonly Dictionary<long, List<SteeringWallSnapshot>> _wallCells = [];
    private readonly List<ObstacleProviderRegistration> _obstacleProviders = [];
    private readonly List<WallProviderRegistration> _wallProviders = [];

    private bool _staticIndexDirty = true;
    private int _builtNeighborUpdateSequence = -1;

    public UniformGridSteeringSpatialIndex(GameWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));

        _world.EntityAdded += OnEntityAdded;
        _world.EntityRemoved += OnEntityRemoved;
        _world.EntitiesClear += OnEntitiesClear;
        _world.EntitiesCleared += OnEntitiesCleared;

        for (int entityIndex = 0; entityIndex < _world.Entities.Count; entityIndex++)
        {
            RegisterEntity(_world.Entities[entityIndex]);
        }
    }

    public void PrepareForWorldUpdate()
    {
        RebuildNeighborIndex();
    }

    public void QueryNeighbors(Entity owner, Vector3 origin, float radius, List<SteeringNeighborSnapshot> results, out int candidateCount, out int hitCount)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(results);

        if (_builtNeighborUpdateSequence != _world.UpdateSequence)
        {
            RebuildNeighborIndex();
        }

        results.Clear();
        candidateCount = 0;
        hitCount = 0;

        float minX = origin.X - radius;
        float maxX = origin.X + radius;
        float minY = origin.Y - radius;
        float maxY = origin.Y + radius;
        int minCellX = ToCell(minX);
        int maxCellX = ToCell(maxX);
        int minCellY = ToCell(minY);
        int maxCellY = ToCell(maxY);

        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                if (!_neighborCells.TryGetValue(CombineCellKey(cellX, cellY), out List<SteeringNeighborSnapshot>? cellSnapshots))
                {
                    continue;
                }

                for (int snapshotIndex = 0; snapshotIndex < cellSnapshots.Count; snapshotIndex++)
                {
                    SteeringNeighborSnapshot snapshot = cellSnapshots[snapshotIndex];
                    if (ReferenceEquals(snapshot.Entity, owner))
                    {
                        continue;
                    }

                    candidateCount++;

                    float dx = snapshot.Position.X - origin.X;
                    float dy = snapshot.Position.Y - origin.Y;
                    float effectiveRadius = radius + snapshot.CollisionRadius;
                    if (dx * dx + dy * dy >= effectiveRadius * effectiveRadius)
                    {
                        continue;
                    }

                    results.Add(snapshot);
                    hitCount++;
                }
            }
        }
    }

    public void QueryObstacles(BoundingBox bounds, List<SteeringObstacleSnapshot> results, HashSet<Entity> deduplicationSet)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(deduplicationSet);
        RebuildStaticIndexIfNeeded();
        Query(_obstacleCells, bounds, results, deduplicationSet, static snapshot => snapshot.Entity, static (list, snapshot) => list.Add(snapshot));
    }

    public void QueryWalls(BoundingBox bounds, List<SteeringWallSnapshot> results, HashSet<Entity> deduplicationSet)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(deduplicationSet);
        RebuildStaticIndexIfNeeded();
        Query(_wallCells, bounds, results, deduplicationSet, static snapshot => snapshot.Entity, static (list, snapshot) => list.Add(snapshot));
    }

    private void RebuildNeighborIndex()
    {
        _neighborCells.Clear();

        for (int entityIndex = 0; entityIndex < _world.Entities.Count; entityIndex++)
        {
            Entity entity = _world.Entities[entityIndex];
            if (entity.GetComponent<SteeringAgentComponent>() == null)
            {
                continue;
            }

            SceneComponent? sceneComponent = entity.RootComponent;
            if (sceneComponent == null)
            {
                continue;
            }

            Vector3 position = sceneComponent.Position;
            Vector3 velocity;
            Vector3 forward;
            SteeringForceVector precisePosition = SteeringForceVector.Zero;
            SteeringForceVector preciseVelocity = SteeringForceVector.Zero;
            SteeringForceVector preciseHeading = SteeringForceVector.Zero;
            bool hasPreciseKinematics = false;

            if (entity.GetComponent<ISteeringPreciseKinematicsProvider>() is ISteeringPreciseKinematicsProvider preciseKinematicsProvider)
            {
                precisePosition = preciseKinematicsProvider.SteeringPrecisePosition;
                preciseVelocity = preciseKinematicsProvider.SteeringPreciseVelocity;
                preciseHeading = preciseKinematicsProvider.SteeringPreciseHeading;
                position = precisePosition.ToVector3();
                velocity = preciseVelocity.ToVector3();
                forward = preciseHeading.ToVector3();
                hasPreciseKinematics = true;
            }
            else
            {
                PhysicsBaseComponent? physicsComponent = entity.GetComponent<PhysicsBaseComponent>();
                SteeringAgentComponent.ResolveEntityMotion(sceneComponent, physicsComponent, position, out velocity, out forward);
            }

            float collisionRadius = 0.0f;
            if (entity.GetComponent<CircleCollisionComponent>() is CircleCollisionComponent circleCollisionComponent)
            {
                collisionRadius = circleCollisionComponent.Circle.Radius;
            }

            SteeringNeighborSnapshot snapshot = hasPreciseKinematics
                ? new SteeringNeighborSnapshot(entity, position, velocity, forward, precisePosition, preciseVelocity, preciseHeading, collisionRadius)
                : new SteeringNeighborSnapshot(entity, position, velocity, forward, collisionRadius);

            int cellX = ToCell(position.X);
            int cellY = ToCell(position.Y);
            long cellKey = CombineCellKey(cellX, cellY);
            if (!_neighborCells.TryGetValue(cellKey, out List<SteeringNeighborSnapshot>? cellSnapshots))
            {
                cellSnapshots = [];
                _neighborCells.Add(cellKey, cellSnapshots);
            }

            cellSnapshots.Add(snapshot);
        }

        _builtNeighborUpdateSequence = _world.UpdateSequence;
    }

    private void RebuildStaticIndexIfNeeded()
    {
        if (!_staticIndexDirty)
        {
            return;
        }

        _obstacleCells.Clear();
        _wallCells.Clear();

        for (int registrationIndex = 0; registrationIndex < _obstacleProviders.Count; registrationIndex++)
        {
            ObstacleProviderRegistration registration = _obstacleProviders[registrationIndex];
            SteeringObstacleSnapshot snapshot = new(
                registration.Owner,
                registration.Provider.SteeringObstaclePosition,
                registration.Provider.SteeringObstacleRadius);
            Add(_obstacleCells, CreateBounds(snapshot.Position, snapshot.Radius), snapshot);
        }

        for (int registrationIndex = 0; registrationIndex < _wallProviders.Count; registrationIndex++)
        {
            WallProviderRegistration registration = _wallProviders[registrationIndex];
            SteeringWallSnapshot snapshot = new(
                registration.Owner,
                registration.Provider.SteeringWallStart,
                registration.Provider.SteeringWallEnd,
                registration.Provider.SteeringWallNormal,
                registration.Provider.SteeringWallThickness);
            Add(_wallCells, CreateBounds(snapshot.Start, snapshot.End, snapshot.Thickness * 0.5f), snapshot);
        }

        _staticIndexDirty = false;
    }

    private void RegisterEntity(Entity entity)
    {
        entity.ComponentAdded += OnEntityComponentAdded;
        entity.ComponentRemoved += OnEntityComponentRemoved;

        RegisterProvider(entity, entity);

        if (entity.RootComponent != null)
        {
            RegisterProvider(entity, entity.RootComponent);
        }

        foreach (EntityComponent component in entity.Components)
        {
            RegisterProvider(entity, component);
        }
    }

    private void UnregisterEntity(Entity entity)
    {
        entity.ComponentAdded -= OnEntityComponentAdded;
        entity.ComponentRemoved -= OnEntityComponentRemoved;

        RemoveRegistrations(_obstacleProviders, entity);
        RemoveRegistrations(_wallProviders, entity);
        _staticIndexDirty = true;
    }

    private void RegisterProvider(Entity owner, object providerHost)
    {
        if (providerHost is ISteeringObstacleProvider obstacleProvider)
        {
            RegisterProvider(_obstacleProviders, new ObstacleProviderRegistration(owner, obstacleProvider), static registration => registration.Provider, ref _staticIndexDirty);
        }

        if (providerHost is ISteeringWallProvider wallProvider)
        {
            RegisterProvider(_wallProviders, new WallProviderRegistration(owner, wallProvider), static registration => registration.Provider, ref _staticIndexDirty);
        }
    }

    private void UnregisterProvider(object providerHost)
    {
        RemoveRegistration(_obstacleProviders, providerHost, static registration => registration.Provider, ref _staticIndexDirty);
        RemoveRegistration(_wallProviders, providerHost, static registration => registration.Provider, ref _staticIndexDirty);
    }

    private void OnEntityAdded(object? sender, Entity entity)
    {
        RegisterEntity(entity);
    }

    private void OnEntityRemoved(object? sender, Entity entity)
    {
        UnregisterEntity(entity);
    }

    private void OnEntitiesClear(object? sender, EventArgs e)
    {
        for (int entityIndex = 0; entityIndex < _world.Entities.Count; entityIndex++)
        {
            Entity entity = _world.Entities[entityIndex];
            entity.ComponentAdded -= OnEntityComponentAdded;
            entity.ComponentRemoved -= OnEntityComponentRemoved;
        }

        _neighborCells.Clear();
        _obstacleCells.Clear();
        _wallCells.Clear();
        _obstacleProviders.Clear();
        _wallProviders.Clear();
        _staticIndexDirty = true;
        _builtNeighborUpdateSequence = -1;
    }

    private void OnEntitiesCleared(object? sender, EventArgs e)
    {
        _neighborCells.Clear();
        _obstacleCells.Clear();
        _wallCells.Clear();
        _builtNeighborUpdateSequence = -1;
    }

    private void OnEntityComponentAdded(object? sender, EntityComponent component)
    {
        if (sender is not Entity entity)
        {
            return;
        }

        RegisterProvider(entity, component);
    }

    private void OnEntityComponentRemoved(object? sender, EntityComponent component)
    {
        UnregisterProvider(component);
    }

    private static void RegisterProvider<TRegistration, TProvider>(List<TRegistration> registrations, TRegistration registration, Func<TRegistration, TProvider> providerSelector, ref bool isDirty)
        where TProvider : class
    {
        TProvider provider = providerSelector(registration);
        for (int index = 0; index < registrations.Count; index++)
        {
            if (ReferenceEquals(providerSelector(registrations[index]), provider))
            {
                return;
            }
        }

        registrations.Add(registration);
        isDirty = true;
    }

    private static void RemoveRegistration<TRegistration, TProvider>(List<TRegistration> registrations, object providerHost, Func<TRegistration, TProvider> providerSelector, ref bool isDirty)
        where TProvider : class
    {
        for (int index = registrations.Count - 1; index >= 0; index--)
        {
            if (!ReferenceEquals(providerSelector(registrations[index]), providerHost))
            {
                continue;
            }

            registrations.RemoveAt(index);
            isDirty = true;
        }
    }

    private static void RemoveRegistrations(List<ObstacleProviderRegistration> registrations, Entity owner)
    {
        for (int index = registrations.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(registrations[index].Owner, owner))
            {
                registrations.RemoveAt(index);
            }
        }
    }

    private static void RemoveRegistrations(List<WallProviderRegistration> registrations, Entity owner)
    {
        for (int index = registrations.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(registrations[index].Owner, owner))
            {
                registrations.RemoveAt(index);
            }
        }
    }

    private static void Query<T>(Dictionary<long, List<T>> cells, BoundingBox bounds, List<T> results, HashSet<Entity> deduplicationSet, Func<T, Entity> entitySelector, Action<List<T>, T> addResult)
    {
        results.Clear();
        deduplicationSet.Clear();

        int minCellX = ToCell(bounds.Min.X);
        int maxCellX = ToCell(bounds.Max.X);
        int minCellY = ToCell(bounds.Min.Y);
        int maxCellY = ToCell(bounds.Max.Y);

        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                if (!cells.TryGetValue(CombineCellKey(cellX, cellY), out List<T>? cellItems))
                {
                    continue;
                }

                for (int index = 0; index < cellItems.Count; index++)
                {
                    T item = cellItems[index];
                    if (!deduplicationSet.Add(entitySelector(item)))
                    {
                        continue;
                    }

                    addResult(results, item);
                }
            }
        }
    }

    private static void Add(Dictionary<long, List<SteeringObstacleSnapshot>> cells, BoundingBox bounds, SteeringObstacleSnapshot snapshot)
    {
        int minCellX = ToCell(bounds.Min.X);
        int maxCellX = ToCell(bounds.Max.X);
        int minCellY = ToCell(bounds.Min.Y);
        int maxCellY = ToCell(bounds.Max.Y);

        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                long key = CombineCellKey(cellX, cellY);
                if (!cells.TryGetValue(key, out List<SteeringObstacleSnapshot>? cellItems))
                {
                    cellItems = [];
                    cells.Add(key, cellItems);
                }

                cellItems.Add(snapshot);
            }
        }
    }

    private static void Add(Dictionary<long, List<SteeringWallSnapshot>> cells, BoundingBox bounds, SteeringWallSnapshot snapshot)
    {
        int minCellX = ToCell(bounds.Min.X);
        int maxCellX = ToCell(bounds.Max.X);
        int minCellY = ToCell(bounds.Min.Y);
        int maxCellY = ToCell(bounds.Max.Y);

        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                long key = CombineCellKey(cellX, cellY);
                if (!cells.TryGetValue(key, out List<SteeringWallSnapshot>? cellItems))
                {
                    cellItems = [];
                    cells.Add(key, cellItems);
                }

                cellItems.Add(snapshot);
            }
        }
    }

    private static BoundingBox CreateBounds(Vector2 position, float radius)
    {
        Vector3 extent = new(radius, radius, Math.Max(1.0f, radius));
        Vector3 center = new(position, 0.0f);
        return new BoundingBox(center - extent, center + extent);
    }

    private static BoundingBox CreateBounds(Vector2 start, Vector2 end, float padding)
    {
        float minX = MathF.Min(start.X, end.X) - padding;
        float minY = MathF.Min(start.Y, end.Y) - padding;
        float maxX = MathF.Max(start.X, end.X) + padding;
        float maxY = MathF.Max(start.Y, end.Y) + padding;
        return new BoundingBox(new Vector3(minX, minY, -padding), new Vector3(maxX, maxY, padding));
    }

    private static int ToCell(float coordinate)
    {
        return (int)MathF.Floor(coordinate / DefaultCellSize);
    }

    private static long CombineCellKey(int cellX, int cellY)
    {
        return ((long)cellX << 32) | (uint)cellY;
    }
}