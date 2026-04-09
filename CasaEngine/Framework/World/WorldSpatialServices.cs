using System;
using CasaEngine.Framework.AI.Navigation;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.World;

public sealed class WorldSpatialServices
{
    public WorldSpatialServices(IWorldSpatialIndex3D worldIndex, ISteeringSpatialIndex2D steeringIndex, ISteeringNeighborhoodService2D neighborhoodService)
    {
        WorldIndex = worldIndex ?? throw new ArgumentNullException(nameof(worldIndex));
        SteeringIndex = steeringIndex ?? throw new ArgumentNullException(nameof(steeringIndex));
        NeighborhoodService = neighborhoodService ?? throw new ArgumentNullException(nameof(neighborhoodService));
    }

    public IWorldSpatialIndex3D WorldIndex { get; }

    public ISteeringSpatialIndex2D SteeringIndex { get; }

    public ISteeringNeighborhoodService2D NeighborhoodService { get; }

    public static WorldSpatialServices CreateDefault(World world, float steeringCellSize = UniformGridSteeringSpatialIndex.DefaultCellSize, float? steeringNeighborhoodCellSize = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        float resolvedNeighborhoodCellSize = steeringNeighborhoodCellSize ?? steeringCellSize;

        return new WorldSpatialServices(
            new OctreeWorldSpatialIndex(new BoundingBox(Vector3.One * -100000, Vector3.One * 100000), 64),
            new UniformGridSteeringSpatialIndex(world, steeringCellSize),
            new UniformGridSteeringNeighborhoodService(world, resolvedNeighborhoodCellSize));
    }
}