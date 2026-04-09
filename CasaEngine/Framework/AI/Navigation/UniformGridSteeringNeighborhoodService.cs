using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using GameWorld = CasaEngine.Framework.World.World;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class UniformGridSteeringNeighborhoodService : ISteeringNeighborhoodService2D
{
    public const float DefaultCellSize = 96.0f;

    private sealed class AgentCacheBucket
    {
        public int EvaluationFrameId = -1;
        public int WorldUpdateSequence = -1;
        public float Radius;
        public uint InclusionMask;
        public Entity? ExcludedEntity;
        public bool CaptureDebugNeighbors;
        public SteeringNeighborhoodAggregateContext Context = SteeringNeighborhoodAggregateContext.Empty;
        public List<Vector3> DebugNeighborPositions = [];
    }

    private readonly GameWorld _world;
    private readonly float _cellSize;
    private readonly SteeringNeighborhoodFrame2D _frame = new();
    private readonly SteeringNeighborhoodCellGrid2D _cellGrid = new();
    private readonly ConditionalWeakTable<SteeringAgentComponent, AgentCacheBucket> _cache = new();
    private int _builtWorldUpdateSequence = -1;

    public UniformGridSteeringNeighborhoodService(GameWorld world, float cellSize = DefaultCellSize)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _cellSize = cellSize > 1.0f ? cellSize : DefaultCellSize;
    }

    public void PrepareForWorldUpdate()
    {
        EnsureFrameBuilt();
    }

    public SteeringNeighborhoodAggregateContext GetNeighborhoodAggregate(SteeringAgentComponent agent, in SteeringNeighborhoodAggregateQuery query)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (agent.Owner?.World == null || !ReferenceEquals(agent.Owner.World, _world))
        {
            return SteeringNeighborhoodAggregateContext.Empty;
        }

        EnsureFrameBuilt();

        AgentCacheBucket bucket = _cache.GetOrCreateValue(agent);
        if (bucket.EvaluationFrameId == agent.EvaluationFrameId
            && bucket.WorldUpdateSequence == _world.UpdateSequence
            && Math.Abs(bucket.Radius - query.Radius) <= 0.01f
            && bucket.InclusionMask == query.InclusionMask
            && ReferenceEquals(bucket.ExcludedEntity, query.ExcludedEntity)
            && bucket.CaptureDebugNeighbors == query.CaptureDebugNeighbors)
        {
            return bucket.Context;
        }

        if (!_frame.TryGetAgentIndex(agent, out int selfIndex))
        {
            return SteeringNeighborhoodAggregateContext.Empty;
        }

        List<Vector3>? debugNeighborPositions = query.CaptureDebugNeighbors
            ? bucket.DebugNeighborPositions
            : null;

        SteeringNeighborhoodAggregateResult result = ComputeNeighborhood(selfIndex, query, debugNeighborPositions);
        SteeringNeighborhoodAggregateContext context = new(
            result,
            debugNeighborPositions is null
                ? Array.Empty<Vector3>()
                : debugNeighborPositions);

        bucket.EvaluationFrameId = agent.EvaluationFrameId;
        bucket.WorldUpdateSequence = _world.UpdateSequence;
        bucket.Radius = query.Radius;
        bucket.InclusionMask = query.InclusionMask;
        bucket.ExcludedEntity = query.ExcludedEntity;
        bucket.CaptureDebugNeighbors = query.CaptureDebugNeighbors;
        bucket.Context = context;

        return context;
    }

    private void EnsureFrameBuilt()
    {
        if (_builtWorldUpdateSequence == _world.UpdateSequence)
        {
            return;
        }

        _frame.BeginBuild(_world.UpdateSequence, _world.Entities.Count);

        for (int entityIndex = 0; entityIndex < _world.Entities.Count; entityIndex++)
        {
            Entity entity = _world.Entities[entityIndex];
            SteeringAgentComponent? steeringAgent = entity.GetComponent<SteeringAgentComponent>();
            SceneComponent? sceneComponent = entity.RootComponent;
            if (steeringAgent == null || sceneComponent == null)
            {
                continue;
            }

            Vector3 position = sceneComponent.Position;
            Vector3 velocity;
            Vector3 forward;
            double positionX;
            double positionY;
            double velocityX;
            double velocityY;
            double headingX;
            double headingY;

            if (entity.GetComponent<ISteeringPreciseKinematicsProvider>() is ISteeringPreciseKinematicsProvider preciseKinematicsProvider)
            {
                SteeringForceVector precisePosition = preciseKinematicsProvider.SteeringPrecisePosition;
                SteeringForceVector preciseVelocity = preciseKinematicsProvider.SteeringPreciseVelocity;
                SteeringForceVector preciseHeading = preciseKinematicsProvider.SteeringPreciseHeading;
                position = precisePosition.ToVector3();
                positionX = precisePosition.X;
                positionY = precisePosition.Y;
                velocityX = preciseVelocity.X;
                velocityY = preciseVelocity.Y;
                headingX = preciseHeading.X;
                headingY = preciseHeading.Y;
            }
            else
            {
                PhysicsBaseComponent? physicsComponent = entity.GetComponent<PhysicsBaseComponent>();
                SteeringAgentComponent.ResolveEntityMotion(sceneComponent, physicsComponent, position, out velocity, out forward);
                positionX = position.X;
                positionY = position.Y;
                velocityX = velocity.X;
                velocityY = velocity.Y;
                headingX = forward.X;
                headingY = forward.Y;
            }

            float collisionRadius = 0.0f;
            if (entity.GetComponent<CircleCollisionComponent>() is CircleCollisionComponent circleCollisionComponent)
            {
                collisionRadius = circleCollisionComponent.Circle.Radius;
            }

            _frame.AddParticipant(
                steeringAgent,
                entity,
                uint.MaxValue,
                positionX,
                positionY,
                position.Z,
                velocityX,
                velocityY,
                headingX,
                headingY,
                collisionRadius);
        }

        _cellGrid.Build(_frame, _cellSize);
        _builtWorldUpdateSequence = _world.UpdateSequence;
    }

    private SteeringNeighborhoodAggregateResult ComputeNeighborhood(int selfIndex, in SteeringNeighborhoodAggregateQuery query, List<Vector3>? debugNeighborPositions)
    {
        if (debugNeighborPositions != null)
        {
            debugNeighborPositions.Clear();
        }

        double selfPositionX = _frame.PositionX[selfIndex];
        double selfPositionY = _frame.PositionY[selfIndex];
        int minCellX = _cellGrid.ToCell(selfPositionX - query.Radius);
        int maxCellX = _cellGrid.ToCell(selfPositionX + query.Radius);
        int minCellY = _cellGrid.ToCell(selfPositionY - query.Radius);
        int maxCellY = _cellGrid.ToCell(selfPositionY + query.Radius);

        int candidateScans = 0;
        int neighborCount = 0;
        int nonEmptyCellCount = 0;
        int windowCellCount = (maxCellX - minCellX + 1) * (maxCellY - minCellY + 1);
        double separationForceX = 0.0;
        double separationForceY = 0.0;
        double headingX = 0.0;
        double headingY = 0.0;
        double centerX = 0.0;
        double centerY = 0.0;

        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                if (!_cellGrid.TryGetCellRange(cellX, cellY, out int startIndex, out int count))
                {
                    continue;
                }

                nonEmptyCellCount++;

                int endIndex = startIndex + count;
                for (int packedIndex = startIndex; packedIndex < endIndex; packedIndex++)
                {
                    int otherIndex = _cellGrid.PackedParticipantIndices[packedIndex];
                    if (otherIndex == selfIndex)
                    {
                        continue;
                    }

                    if (query.InclusionMask != 0u
                        && (_frame.ParticipationMask[otherIndex] & query.InclusionMask) == 0u)
                    {
                        continue;
                    }

                    if (query.ExcludedEntity != null
                        && ReferenceEquals(_frame.Owners[otherIndex], query.ExcludedEntity))
                    {
                        continue;
                    }

                    candidateScans++;

                    double awayX = selfPositionX - _frame.PositionX[otherIndex];
                    double awayY = selfPositionY - _frame.PositionY[otherIndex];
                    double distanceSquared = awayX * awayX + awayY * awayY;
                    if (distanceSquared <= double.Epsilon)
                    {
                        continue;
                    }

                    double effectiveRadius = query.Radius + _frame.CollisionRadius[otherIndex];
                    if (distanceSquared >= effectiveRadius * effectiveRadius)
                    {
                        continue;
                    }

                    separationForceX += awayX / distanceSquared;
                    separationForceY += awayY / distanceSquared;
                    headingX += _frame.HeadingX[otherIndex];
                    headingY += _frame.HeadingY[otherIndex];
                    centerX += _frame.PositionX[otherIndex];
                    centerY += _frame.PositionY[otherIndex];
                    neighborCount++;

                    if (query.CaptureDebugNeighbors && debugNeighborPositions != null)
                    {
                        debugNeighborPositions.Add(new Vector3((float)_frame.PositionX[otherIndex], (float)_frame.PositionY[otherIndex], _frame.PositionZ[otherIndex]));
                    }
                }
            }
        }

        if (neighborCount > 0)
        {
            headingX /= neighborCount;
            headingY /= neighborCount;
            centerX /= neighborCount;
            centerY /= neighborCount;
        }

        return new SteeringNeighborhoodAggregateResult(
            candidateScans,
            windowCellCount,
            nonEmptyCellCount,
            neighborCount,
            separationForceX,
            separationForceY,
            headingX,
            headingY,
            centerX,
            centerY);
    }
}