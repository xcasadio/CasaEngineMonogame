using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Debug;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Spatial.Octree;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.World;

public sealed class OctreeWorldSpatialIndex : IWorldSpatialIndex3D
{
    private readonly Octree<Entity> _octree;

    public OctreeWorldSpatialIndex(BoundingBox bounds, int maxChildren)
    {
        _octree = new Octree<Entity>(bounds, maxChildren);
    }

    public void Clear()
    {
        _octree.Clear();
    }

    public void Add(Entity entity, BoundingBox bounds)
    {
        _octree.AddItem(bounds, entity);
    }

    public bool Remove(Entity entity)
    {
        return _octree.RemoveItem(entity);
    }

    public bool Move(Entity entity, BoundingBox newBounds)
    {
        return _octree.MoveItem(entity, newBounds);
    }

    public void ApplyPendingMoves()
    {
        _octree.ApplyPendingMoves();
    }

    public void Query(BoundingFrustum frustum, List<Entity> results, Func<Entity, bool> filter = null)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (filter == null)
        {
            _octree.GetContainedObjects(frustum, results);
            return;
        }

        _octree.GetContainedObjects(frustum, results, filter);
    }

    public void Query(BoundingBox bounds, List<Entity> results, Func<Entity, bool> filter = null)
    {
        ArgumentNullException.ThrowIfNull(results);
        _octree.GetContainedObjects(bounds, results, filter);
    }

    public void DebugDraw(Line3dRendererComponent line3dRendererComponent)
    {
        ArgumentNullException.ThrowIfNull(line3dRendererComponent);
        OctreeVisualizer.DisplayBoundingBoxes(_octree, line3dRendererComponent);
    }
}