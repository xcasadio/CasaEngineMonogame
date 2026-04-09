using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

internal static class SteeringNeighborhoodFrameIndex
{
    private const float DefaultCellSize = 96.0f;

    private sealed class WorldIndex
    {
        public int BuiltUpdateSequence = -1;
        public readonly Dictionary<long, List<SteeringNeighborSnapshot>> Cells = [];
    }

    private static readonly ConditionalWeakTable<World.World, WorldIndex> WorldIndexes = new();

    public static void PrepareForWorldUpdate(World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Rebuild(world, WorldIndexes.GetOrCreateValue(world));
    }

    public static void Query(World.World world, Entity owner, Vector3 origin, float radius, List<SteeringNeighborSnapshot> results, out int candidateCount, out int hitCount)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(results);

        WorldIndex index = WorldIndexes.GetOrCreateValue(world);
        if (index.BuiltUpdateSequence != world.UpdateSequence)
        {
            Rebuild(world, index);
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
                if (!index.Cells.TryGetValue(CombineCellKey(cellX, cellY), out List<SteeringNeighborSnapshot>? cellSnapshots))
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

    private static void Rebuild(World.World world, WorldIndex index)
    {
        index.Cells.Clear();

        for (int entityIndex = 0; entityIndex < world.Entities.Count; entityIndex++)
        {
            Entity entity = world.Entities[entityIndex];
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
            if (!index.Cells.TryGetValue(cellKey, out List<SteeringNeighborSnapshot>? cellSnapshots))
            {
                cellSnapshots = [];
                index.Cells.Add(cellKey, cellSnapshots);
            }

            cellSnapshots.Add(snapshot);
        }

        index.BuiltUpdateSequence = world.UpdateSequence;
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