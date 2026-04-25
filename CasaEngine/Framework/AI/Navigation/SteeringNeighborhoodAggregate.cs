using CasaEngine.Framework.Scene.Entities;
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
    public static readonly SteeringNeighborhoodAggregateContext Empty = new(default, Array.Empty<Vector3>(), false);

    public SteeringNeighborhoodAggregateContext(SteeringNeighborhoodAggregateResult result, IReadOnlyList<Vector3> debugNeighborPositions, bool wasComputedThisRequest)
    {
        Result = result;
        DebugNeighborPositions = debugNeighborPositions ?? Array.Empty<Vector3>();
        WasComputedThisRequest = wasComputedThisRequest;
    }

    public SteeringNeighborhoodAggregateResult Result { get; }

    public IReadOnlyList<Vector3> DebugNeighborPositions { get; }

    public bool WasComputedThisRequest { get; }
}