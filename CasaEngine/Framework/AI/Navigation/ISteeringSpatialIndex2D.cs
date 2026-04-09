using System.Collections.Generic;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public interface ISteeringSpatialIndex2D
{
    void PrepareForWorldUpdate();

    void QueryNeighbors(Entity owner, Vector3 origin, float radius, List<SteeringNeighborSnapshot> results, out int candidateCount, out int hitCount, out int windowCellCount, out int nonEmptyCellCount);

    void QueryObstacles(BoundingBox bounds, List<SteeringObstacleSnapshot> results, HashSet<Entity> deduplicationSet);

    void QueryWalls(BoundingBox bounds, List<SteeringWallSnapshot> results, HashSet<Entity> deduplicationSet);
}