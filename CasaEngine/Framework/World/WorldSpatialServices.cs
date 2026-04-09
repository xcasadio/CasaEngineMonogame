using System;
using CasaEngine.Framework.AI.Navigation;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.World;

public sealed class WorldSpatialServices
{
    public WorldSpatialServices(IWorldSpatialIndex3D worldIndex, ISteeringSpatialIndex2D steeringIndex)
    {
        WorldIndex = worldIndex ?? throw new ArgumentNullException(nameof(worldIndex));
        SteeringIndex = steeringIndex ?? throw new ArgumentNullException(nameof(steeringIndex));
    }

    public IWorldSpatialIndex3D WorldIndex { get; }

    public ISteeringSpatialIndex2D SteeringIndex { get; }

    public static WorldSpatialServices CreateDefault(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return new WorldSpatialServices(
            new OctreeWorldSpatialIndex(new BoundingBox(Vector3.One * -100000, Vector3.One * 100000), 64),
            new UniformGridSteeringSpatialIndex(world));
    }
}