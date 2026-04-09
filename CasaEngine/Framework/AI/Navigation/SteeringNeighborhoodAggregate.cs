using System;
using System.Collections.Generic;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public readonly record struct SteeringNeighborhoodAggregateQuery(
    float Radius,
    uint InclusionMask = 0u,
    Entity? ExcludedEntity = null,
    bool CaptureDebugNeighbors = false);

public readonly record struct SteeringNeighborhoodAggregateResult(
    int CandidateScans,
    int WindowCellCount,
    int NonEmptyCellCount,
    int NeighborCount,
    double SeparationForceX,
    double SeparationForceY,
    double AverageHeadingX,
    double AverageHeadingY,
    double CenterX,
    double CenterY);

public sealed class SteeringNeighborhoodAggregateContext
{
    public static readonly SteeringNeighborhoodAggregateContext Empty = new(default, Array.Empty<Vector3>());

    public SteeringNeighborhoodAggregateContext(SteeringNeighborhoodAggregateResult result, IReadOnlyList<Vector3> debugNeighborPositions)
    {
        Result = result;
        DebugNeighborPositions = debugNeighborPositions ?? Array.Empty<Vector3>();
    }

    public SteeringNeighborhoodAggregateResult Result { get; }

    public IReadOnlyList<Vector3> DebugNeighborPositions { get; }
}