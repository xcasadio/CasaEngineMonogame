using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SpacePartitioning.Octree;

/// <summary>
/// Maintains a reference to the current root node of a dynamic octree.
/// The root node may change as items are added and removed from contained nodes.
/// </summary>
/// <typeparam name="T">The type stored in the octree.</typeparam>
public class Octree<T>
{
    private OctreeNode<T> _currentRoot;
    private readonly List<OctreeItem<T>> _pendingMoveStage = new();

    public Octree(BoundingBox boundingBox, int maxChildren)
    {
        _currentRoot = new OctreeNode<T>(boundingBox, maxChildren);
    }

    // The current root node of the octree. This may change when items are added and removed.
    public OctreeNode<T> CurrentRoot => _currentRoot;

    public OctreeItem<T> AddItem(BoundingBox itemBounds, T item)
    {
        _currentRoot = _currentRoot.AddItem(ref itemBounds, item, out var ret);
        return ret;
    }

    public void GetContainedObjects(BoundingFrustum frustum, List<T> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        _currentRoot.GetContainedObjects(ref frustum, results);
    }

    public void GetContainedObjects(BoundingFrustum frustum, List<T> results, Func<T, bool> filter)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        _currentRoot.GetContainedObjects(ref frustum, results, filter);
    }

    public int RayCast(Ray ray, List<RayCastHit<T>> hits, RayCastFilter<T> filter)
    {
        if (hits == null)
        {
            throw new ArgumentNullException(nameof(hits));
        }

        return _currentRoot.RayCast(ray, hits, filter);
    }

    public void GetAllContainedObjects(List<T> results)
    {
        _currentRoot.GetAllContainedObjects(results);
    }

    public void GetAllContainedObjects(List<T> results, Func<T, bool> filter)
    {
        _currentRoot.GetAllContainedObjects(results, filter);
    }

    public bool RemoveItem(T item)
    {
        if (_currentRoot.TryGetContainedOctreeItem(item, out var octreeItem))
        {
            RemoveItem(octreeItem);
            return true;
        }

        return false;
    }

    public void RemoveItem(OctreeItem<T> octreeItem)
    {
        octreeItem.Container.RemoveItem(octreeItem);
        _currentRoot = _currentRoot.TryTrimChildren();
    }

    public bool MoveItem(T item, BoundingBox newBounds)
    {
        if (_currentRoot.TryGetContainedOctreeItem(item, out var octreeItem))
        {
            MoveItem(octreeItem, newBounds);
            return true;
        }

        return false;
    }

    public void MoveItem(OctreeItem<T> octreeItem, BoundingBox newBounds)
    {
        if (newBounds.ContainsNaN())
        {
            throw new Exception("Invalid bounds: " + newBounds);
        }

        var newRoot = octreeItem.Container.MoveContainedItem(octreeItem, newBounds);

        if (newRoot != null)
        {
            _currentRoot = newRoot;
        }

        _currentRoot = _currentRoot.TryTrimChildren();
    }

    public void Clear() => _currentRoot.Clear();

    //Apply pending moves. This may change the current root node.
    public void ApplyPendingMoves()
    {
        _pendingMoveStage.Clear();
        _currentRoot.CollectPendingMoves(_pendingMoveStage);
        foreach (var item in _pendingMoveStage)
        {
            MoveItem(item, item.Bounds);
            item.HasPendingMove = false;
        }
    }
}