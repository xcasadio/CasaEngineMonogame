using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public readonly struct SteeringObstacleSnapshot
{
    public SteeringObstacleSnapshot(Entity entity, Vector2 position, float radius)
    {
        Entity = entity;
        Position = position;
        Radius = radius;
    }

    public Entity Entity { get; }

    public Vector2 Position { get; }

    public float Radius { get; }
}

public readonly struct SteeringWallSnapshot
{
    public SteeringWallSnapshot(Entity entity, Vector2 start, Vector2 end, Vector2 normal, float thickness)
    {
        Entity = entity;
        Start = start;
        End = end;
        Normal = normal;
        Thickness = thickness;
    }

    public Entity Entity { get; }

    public Vector2 Start { get; }

    public Vector2 End { get; }

    public Vector2 Normal { get; }

    public float Thickness { get; }
}

internal static class SteeringStaticSpatialIndex
{
    private const float DefaultCellSize = 96.0f;

    private readonly record struct ObstacleProviderRegistration(Entity Owner, ISteeringObstacleProvider Provider);

    private readonly record struct WallProviderRegistration(Entity Owner, ISteeringWallProvider Provider);

    private sealed class WorldIndex
    {
        public bool IsInitialized;
        public bool IsDirty = true;
        public readonly Dictionary<long, List<SteeringObstacleSnapshot>> Obstacles = [];
        public readonly Dictionary<long, List<SteeringWallSnapshot>> Walls = [];
        public readonly List<ObstacleProviderRegistration> ObstacleProviders = [];
        public readonly List<WallProviderRegistration> WallProviders = [];
    }

    private static readonly ConditionalWeakTable<World.World, WorldIndex> WorldIndexes = new();

    public static void QueryObstacles(World.World world, BoundingBox bounds, List<SteeringObstacleSnapshot> results, HashSet<Entity> deduplicationSet)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(deduplicationSet);

        WorldIndex index = GetOrBuildIndex(world);
        Query(index.Obstacles, bounds, results, deduplicationSet, static snapshot => snapshot.Entity, static (list, snapshot) => list.Add(snapshot));
    }

    public static void QueryWalls(World.World world, BoundingBox bounds, List<SteeringWallSnapshot> results, HashSet<Entity> deduplicationSet)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(deduplicationSet);

        WorldIndex index = GetOrBuildIndex(world);
        Query(index.Walls, bounds, results, deduplicationSet, static snapshot => snapshot.Entity, static (list, snapshot) => list.Add(snapshot));
    }

    private static WorldIndex GetOrBuildIndex(World.World world)
    {
        WorldIndex index = WorldIndexes.GetOrCreateValue(world);
        EnsureInitialized(world, index);

        if (!index.IsDirty)
        {
            return index;
        }

        index.Obstacles.Clear();
        index.Walls.Clear();

        for (int registrationIndex = 0; registrationIndex < index.ObstacleProviders.Count; registrationIndex++)
        {
            ObstacleProviderRegistration registration = index.ObstacleProviders[registrationIndex];
            SteeringObstacleSnapshot snapshot = new(
                registration.Owner,
                registration.Provider.SteeringObstaclePosition,
                registration.Provider.SteeringObstacleRadius);
            Add(index.Obstacles, CreateBounds(snapshot.Position, snapshot.Radius), snapshot);
        }

        for (int registrationIndex = 0; registrationIndex < index.WallProviders.Count; registrationIndex++)
        {
            WallProviderRegistration registration = index.WallProviders[registrationIndex];
            SteeringWallSnapshot snapshot = new(
                registration.Owner,
                registration.Provider.SteeringWallStart,
                registration.Provider.SteeringWallEnd,
                registration.Provider.SteeringWallNormal,
                registration.Provider.SteeringWallThickness);
            Add(index.Walls, CreateBounds(snapshot.Start, snapshot.End, snapshot.Thickness * 0.5f), snapshot);
        }

        index.IsDirty = false;
        return index;
    }

    private static void EnsureInitialized(World.World world, WorldIndex index)
    {
        if (index.IsInitialized)
        {
            return;
        }

        index.IsInitialized = true;

        world.EntityAdded += (_, entity) => RegisterEntity(index, entity);
        world.EntityRemoved += (_, entity) => UnregisterEntity(index, entity);

        for (int entityIndex = 0; entityIndex < world.Entities.Count; entityIndex++)
        {
            RegisterEntity(index, world.Entities[entityIndex]);
        }
    }

    private static void RegisterEntity(WorldIndex index, Entity entity)
    {
        entity.ComponentAdded += OnEntityComponentAdded;
        entity.ComponentRemoved += OnEntityComponentRemoved;

        RegisterProvider(index, entity, entity);

        if (entity.RootComponent != null)
        {
            RegisterProvider(index, entity, entity.RootComponent);
        }

        foreach (EntityComponent component in entity.Components)
        {
            RegisterProvider(index, entity, component);
        }
    }

    private static void UnregisterEntity(WorldIndex index, Entity entity)
    {
        entity.ComponentAdded -= OnEntityComponentAdded;
        entity.ComponentRemoved -= OnEntityComponentRemoved;

        RemoveRegistrations(index.ObstacleProviders, entity);
        RemoveRegistrations(index.WallProviders, entity);
        index.IsDirty = true;
    }

    private static void OnEntityComponentAdded(object? sender, EntityComponent component)
    {
        if (sender is not Entity entity
            || entity.World == null)
        {
            return;
        }

        WorldIndex index = WorldIndexes.GetOrCreateValue(entity.World);
        RegisterProvider(index, entity, component);
    }

    private static void OnEntityComponentRemoved(object? sender, EntityComponent component)
    {
        if (sender is not Entity entity
            || entity.World == null)
        {
            return;
        }

        WorldIndex index = WorldIndexes.GetOrCreateValue(entity.World);
        UnregisterProvider(index, component);
    }

    private static void RegisterProvider(WorldIndex index, Entity owner, object providerHost)
    {
        if (providerHost is ISteeringObstacleProvider obstacleProvider)
        {
            RegisterProvider(index.ObstacleProviders, new ObstacleProviderRegistration(owner, obstacleProvider), static registration => registration.Provider, ref index.IsDirty);
        }

        if (providerHost is ISteeringWallProvider wallProvider)
        {
            RegisterProvider(index.WallProviders, new WallProviderRegistration(owner, wallProvider), static registration => registration.Provider, ref index.IsDirty);
        }
    }

    private static void UnregisterProvider(WorldIndex index, object providerHost)
    {
        RemoveRegistration(index.ObstacleProviders, providerHost, static registration => registration.Provider, ref index.IsDirty);
        RemoveRegistration(index.WallProviders, providerHost, static registration => registration.Provider, ref index.IsDirty);
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