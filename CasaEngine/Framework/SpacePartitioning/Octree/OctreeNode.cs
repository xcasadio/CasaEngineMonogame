using System.Diagnostics;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SpacePartitioning.Octree;

[DebuggerDisplay("{DebuggerDisplayString,nq}")]
public class OctreeNode<T>
{
    private const int NumChildNodes = 8;
    private readonly List<OctreeItem<T>> _items = new();
    private readonly OctreeNodeCache _nodeCache;

    public BoundingBox Bounds { get; set; }
    public int MaxChildren { get; private set; }
    public OctreeNode<T>? Parent { get; private set; }
    public OctreeNode<T>[] Children { get; private set; } = Array.Empty<OctreeNode<T>>();

    public static OctreeNode<T> CreateNewTree(ref BoundingBox bounds, int maxChildren)
    {
        return new OctreeNode<T>(ref bounds, maxChildren, new OctreeNodeCache(maxChildren), null);
    }

    public OctreeNode<T> AddItem(BoundingBox itemBounds, T item)
    {
        OctreeItem<T> ignored;
        return AddItem(itemBounds, item, out ignored);
    }

    public OctreeNode<T> AddItem(ref BoundingBox itemBounds, T item)
    {
        return AddItem(ref itemBounds, item, out _);
    }

    public OctreeNode<T> AddItem(BoundingBox itemBounds, T item, out OctreeItem<T> itemContainer)
    {
        return AddItem(ref itemBounds, item, out itemContainer);
    }

    public OctreeNode<T> AddItem(ref BoundingBox itemBounds, T item, out OctreeItem<T> octreeItem)
    {
        if (Parent != null)
        {
            throw new Exception("Can only add items to the root Octree node.");
        }

        octreeItem = _nodeCache.GetOctreeItem(ref itemBounds, item);
        return CoreAddRootItem(octreeItem);
    }

    private OctreeNode<T> CoreAddRootItem(OctreeItem<T> octreeItem)
    {
        var root = this;
        var result = CoreAddItem(octreeItem);
        if (!result)
        {
            root = ResizeAndAdd(octreeItem);
        }

        return root;
    }

    /// <summary>
    /// Move a contained OctreeItem. If the root OctreeNode needs to be resized, the new root node is returned.
    /// </summary>
    public OctreeNode<T> MoveContainedItem(OctreeItem<T> item, BoundingBox newBounds)
    {
        OctreeNode<T> newRoot = null;

        var container = item.Container;
        if (!container._items.Contains(item))
        {
            throw new Exception("Can't move item " + item + ", its container does not contain it.");
        }

        item.Bounds = newBounds;
        if (container.Bounds.Contains(item.Bounds) == ContainmentType.Contains)
        {
            // Item did not leave the node.
            newRoot = null;

            // It may have moved into the bounds of a child node.
            for (var i = 0; i < Children.Length; i++)
            {
                if (Children[i].CoreAddItem(item))
                {
                    _items.Remove(item);
                    break;
                }
            }
        }
        else
        {
            container._items.Remove(item);
            item.Container = null;

            var node = container;
            while (node.Parent != null && !node.CoreAddItem(item))
            {
                node = node.Parent;
            }

            if (item.Container == null)
            {
                // This should only occur if the item has moved beyond the root node's bounds.
                // We need to resize the root tree.
                Debug.Assert(node == GetRootNode());
                newRoot = node.CoreAddRootItem(item);
            }

            container.Parent.ConsiderConsolidation();
        }

        return newRoot;
    }

    /// <summary>
    /// Mark an item as having moved, but do not alter the octree structure. Call <see cref="Octree{T}.ApplyPendingMoves"/> to update the octree structure.
    /// </summary>
    public void MarkItemAsMoved(OctreeItem<T> octreeItem, BoundingBox newBounds)
    {
        if (!_items.Contains(octreeItem))
        {
            throw new Exception("Cannot mark item as moved which doesn't belong to this OctreeNode.");
        }
        if (newBounds.ContainsNaN())
        {
            throw new Exception("Invalid bounds: " + newBounds);
        }

        octreeItem.HasPendingMove = true;
        octreeItem.Bounds = newBounds;
    }

    private OctreeNode<T> GetRootNode()
    {
        if (Parent == null)
        {
            return this;
        }

        var root = Parent;
        while (root.Parent != null)
        {
            root = root.Parent;
        }

        return root;
    }

    public void RemoveItem(OctreeItem<T> octreeItem)
    {
        var container = octreeItem.Container;
        if (!container._items.Remove(octreeItem))
        {
            throw new Exception("Item isn't contained in its container.");
        }

        container.Parent?.ConsiderConsolidation();

        _nodeCache.AddOctreeItem(octreeItem);
    }

    private void ConsiderConsolidation()
    {
        if (Children.Length > 0 && GetItemCount() < MaxChildren)
        {
            ConsolidateChildren();
            Parent?.ConsiderConsolidation();
        }
    }

    private void ConsolidateChildren()
    {
        for (var i = 0; i < Children.Length; i++)
        {
            var child = Children[i];
            child.ConsolidateChildren();

            foreach (var childItem in child._items)
            {
                _items.Add(childItem);
                childItem.Container = this;
            }
        }

        RecycleChildren();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetContainedObjects(BoundingFrustum frustum, List<T> results)
    {
        Debug.Assert(results != null);
        CoreGetContainedObjects(ref frustum, results, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetContainedObjects(ref BoundingFrustum frustum, List<T> results)
    {
        Debug.Assert(results != null);
        CoreGetContainedObjects(ref frustum, results, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetContainedObjects(ref BoundingFrustum frustum, List<T> results, Func<T, bool> filter)
    {
        Debug.Assert(results != null);
        CoreGetContainedObjects(ref frustum, results, filter);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetAllContainedObjects(List<T> results) => GetAllContainedObjects(results, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetAllContainedObjects(List<T> results, Func<T, bool> filter)
    {
        Debug.Assert(results != null);
        CoreGetAllContainedObjects(results, filter);
    }

    public int RayCast(Ray ray, List<RayCastHit<T>> hits, RayCastFilter<T> filter)
    {
        if (ray.Intersects(Bounds) == null)
        {
            return 0;
        }

        var numHits = 0;

        foreach (var item in _items)
        {
            if (ray.Intersects(item.Bounds) != null)
            {
                numHits += filter(ray, item.Item, hits);
            }
        }

        for (var i = 0; i < Children.Length; i++)
        {
            numHits += Children[i].RayCast(ray, hits, filter);
        }

        return numHits;
    }

    public BoundingBox GetPreciseBounds()
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        return CoreGetPreciseBounds(ref min, ref max);
    }

    private BoundingBox CoreGetPreciseBounds(ref Vector3 min, ref Vector3 max)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            min = Vector3.Min(min, item.Bounds.Min);
            max = Vector3.Max(max, item.Bounds.Max);
        }

        for (var i = 0; i < Children.Length; i++)
        {
            Children[i].CoreGetPreciseBounds(ref min, ref max);
        }

        return new BoundingBox(min, max);
    }

    public int GetItemCount()
    {
        var count = _items.Count;
        for (var i = 0; i < Children.Length; i++)
        {
            count += Children[i].GetItemCount();
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CoreGetContainedObjects(ref BoundingFrustum frustum, List<T> results, Func<T, bool> filter)
    {
        //TODO : check if IsVisible is true
        var ct = frustum.Contains(Bounds);
        if (ct == ContainmentType.Contains)
        {
            CoreGetAllContainedObjects(results, filter);
        }
        else if (ct == ContainmentType.Intersects)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var octreeItem = _items[i];
                if (frustum.Contains(octreeItem.Bounds) != ContainmentType.Disjoint)
                {
                    if (filter == null || filter(octreeItem.Item))
                    {
                        results.Add(octreeItem.Item);
                    }
                }
            }
            for (var i = 0; i < Children.Length; i++)
            {
                Children[i].CoreGetContainedObjects(ref frustum, results, filter);
            }
        }
    }

    public IEnumerable<OctreeItem<T>> GetAllOctreeItems()
    {
        foreach (var item in _items)
        {
            yield return item;
        }
        foreach (var child in Children)
        {
            foreach (var childItem in child.GetAllOctreeItems())
            {
                yield return childItem;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CoreGetAllContainedObjects(List<T> results, Func<T, bool> filter)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var octreeItem = _items[i];
            if (filter == null || filter(octreeItem.Item))
            {
                results.Add(octreeItem.Item);
            }
        }
        for (var i = 0; i < Children.Length; i++)
        {
            Children[i].CoreGetAllContainedObjects(results, filter);
        }
    }

    public void Clear()
    {
        if (Parent != null)
        {
            throw new Exception("Can only clear the root OctreeNode.");
        }

        RecycleNode(true, true);
    }

    private void RecycleNode(bool recycleChildren = true, bool isRootNode = false)
    {
        if (recycleChildren)
        {
            RecycleChildren();
        }

        _items.Clear();

        if (!isRootNode)
        {
            _nodeCache.AddNode(this);
        }
    }

    private void RecycleChildren()
    {
        if (Children.Length == 0)
        {
            return;
        }

        for (var i = 0; i < Children.Length; i++)
        {
            Children[i].RecycleNode();
        }

        _nodeCache.AddAndClearChildrenArray(Children);
        Children = Array.Empty<OctreeNode<T>>();
    }

    private bool CoreAddItem(OctreeItem<T> item)
    {
        if (Bounds.Contains(item.Bounds) != ContainmentType.Contains)
        {
            return false;
        }

        if (_items.Count >= MaxChildren && Children.Length == 0)
        {
            var newNode = SplitChildren(ref item.Bounds, null);
            if (newNode != null)
            {
                var succeeded = newNode.CoreAddItem(item);
                Debug.Assert(succeeded, "Octree node returned from SplitChildren must fit the item given to it.");
                return true;
            }
        }
        else if (Children.Length > 0)
        {
            for (var i = 0; i < Children.Length; i++)
            {
                if (Children[i].CoreAddItem(item))
                {
                    return true;
                }
            }
        }

        // Couldn't fit in any children.
#if DEBUG
        foreach (var child in Children)
        {
            Debug.Assert(child.Bounds.Contains(item.Bounds) != ContainmentType.Contains);
        }
#endif

        _items.Add(item);
        item.Container = this;

        return true;
    }

    // Splits the node into 8 children
    private OctreeNode<T> SplitChildren(ref BoundingBox itemBounds, OctreeNode<T> existingChild)
    {
        Debug.Assert(Children.Length == 0, "Children must be empty before SplitChildren is called.");

        OctreeNode<T> childBigEnough = null;
        Children = _nodeCache.GetChildrenArray();
        var boundingBox = Bounds;
        var center = boundingBox.GetCenter();
        var dimensions = boundingBox.GetDimensions();

        var quaterDimensions = dimensions * 0.25f;

        var i = 0;
        for (var x = -1f; x <= 1f; x += 2f)
        {
            for (var y = -1f; y <= 1f; y += 2f)
            {
                for (var z = -1f; z <= 1f; z += 2f)
                {
                    var childCenter = center + (quaterDimensions * new Vector3(x, y, z));
                    var min = childCenter - quaterDimensions;
                    var max = childCenter + quaterDimensions;
                    var childBounds = new BoundingBox(min, max);
                    OctreeNode<T> newChild;

                    if (existingChild != null && existingChild.Bounds == childBounds)
                    {
                        newChild = existingChild;
                    }
                    else
                    {
                        newChild = _nodeCache.GetNode(ref childBounds);
                    }

                    if (childBounds.Contains(itemBounds) == ContainmentType.Contains)
                    {
                        Debug.Assert(childBigEnough == null);
                        childBigEnough = newChild;
                    }

                    newChild.Parent = this;
                    Children[i] = newChild;
                    i++;
                }
            }
        }

        PushItemsToChildren();
#if DEBUG
        for (var g = 0; g < Children.Length; g++)
        {
            Debug.Assert(Children[g] != null);
        }
#endif
        return childBigEnough;
    }

    private void PushItemsToChildren()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            for (var c = 0; c < Children.Length; c++)
            {
                if (Children[c].CoreAddItem(item))
                {
                    _items.Remove(item);
                    i--;
                    break;
                }
            }
        }

#if DEBUG
        for (var i = 0; i < _items.Count; i++)
        {
            for (var c = 0; c < Children.Length; c++)
            {
                Debug.Assert(Children[c].Bounds.Contains(_items[i].Bounds) != ContainmentType.Contains);
            }
        }
#endif
    }

    private OctreeNode<T> ResizeAndAdd(OctreeItem<T> octreeItem)
    {
        var oldRoot = this;
        var boundingBox = Bounds;
        var oldRootCenter = boundingBox.GetCenter();
        var oldRootHalfExtents = boundingBox.GetDimensions() * 0.5f;

        var expandDirection = Vector3.Normalize(octreeItem.Bounds.GetCenter() - oldRootCenter);
        var newCenter = oldRootCenter;
        if (expandDirection.X >= 0) // oldRoot = Left
        {
            newCenter.X += oldRootHalfExtents.X;
        }
        else
        {
            newCenter.X -= oldRootHalfExtents.X;
        }

        if (expandDirection.Y >= 0) // oldRoot = Bottom
        {
            newCenter.Y += oldRootHalfExtents.Y;
        }
        else
        {
            newCenter.Y -= oldRootHalfExtents.Y;
        }

        if (expandDirection.Z >= 0) // oldRoot = Far
        {
            newCenter.Z += oldRootHalfExtents.Z;
        }
        else
        {
            newCenter.Z -= oldRootHalfExtents.Z;
        }

        var newRootBounds = new BoundingBox(newCenter - oldRootHalfExtents * 2f, newCenter + oldRootHalfExtents * 2f);
        var newRoot = _nodeCache.GetNode(ref newRootBounds);
        var fittingNode = newRoot.SplitChildren(ref octreeItem.Bounds, oldRoot);
        if (fittingNode != null)
        {
            var succeeded = fittingNode.CoreAddItem(octreeItem);
            Debug.Assert(succeeded, "Octree node returned from SplitChildren must fit the item given to it.");
            return newRoot;
        }

        return newRoot.CoreAddRootItem(octreeItem);
    }

    public OctreeNode(BoundingBox box, int maxChildren)
        : this(ref box, maxChildren, new OctreeNodeCache(maxChildren), null)
    {
    }

    private OctreeNode(ref BoundingBox bounds, int maxChildren, OctreeNodeCache nodeCache, OctreeNode<T> parent)
    {
        Bounds = bounds;
        MaxChildren = maxChildren;
        _nodeCache = nodeCache;
        Parent = parent;
    }

    private void Reset(ref BoundingBox newBounds)
    {
        Bounds = newBounds;

        _items.Clear();
        Parent = null;

        if (Children.Length != 0)
        {
            _nodeCache.AddAndClearChildrenArray(Children);
            Children = Array.Empty<OctreeNode<T>>();
        }
    }

    /// <summary>
    /// Attempts to find an OctreeNode for the given item, in this OctreeNode and its children.
    /// </summary>
    /// <param name="item">The item to find.</param>
    /// <param name="octreeItem">The contained OctreeItem.</param>
    /// <returns>true if the item was contained in the Octree; false otherwise.</returns>
    internal bool TryGetContainedOctreeItem(T item, out OctreeItem<T> octreeItem)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var containedItem = _items[i];
            if (containedItem.Item.Equals(item))
            {
                octreeItem = containedItem;
                return true;
            }
        }

        for (var i = 0; i < Children.Length; i++)
        {
            var child = Children[i];
            Debug.Assert(child != null, "node child cannot be null.");
            if (child.TryGetContainedOctreeItem(item, out octreeItem))
            {
                return true;
            }
        }

        octreeItem = null;
        return false;
    }

    /// <summary>
    /// Determines if there is only one child node in use. If so, recycles all other nodes and returns that one.
    /// If this is not true, the node is returned unchanged.
    /// </summary>
    internal OctreeNode<T> TryTrimChildren()
    {
        if (_items.Count == 0)
        {
            OctreeNode<T> loneChild = null;
            for (var i = 0; i < Children.Length; i++)
            {
                var child = Children[i];
                if (child.GetItemCount() != 0)
                {
                    if (loneChild != null)
                    {
                        return this;
                    }

                    loneChild = child;
                }
            }

            if (loneChild != null)
            {
                // Recycle excess
                for (var i = 0; i < Children.Length; i++)
                {
                    var child = Children[i];
                    if (child != loneChild)
                    {
                        child.RecycleNode();
                    }
                }

                RecycleNode(recycleChildren: false);

                // Return lone child in use
                loneChild.Parent = null;
                return loneChild;
            }
        }

        return this;
    }

    internal void CollectPendingMoves(List<OctreeItem<T>> pendingMoves)
    {
        for (var i = 0; i < Children.Length; i++)
        {
            Children[i].CollectPendingMoves(pendingMoves);
        }

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.HasPendingMove)
            {
                pendingMoves.Add(item);
            }
        }
    }

    private string DebuggerDisplayString => $"{Bounds.Min} - {Bounds.Max}, Items:{_items.Count}";

    private class OctreeNodeCache
    {
        private readonly Stack<OctreeNode<T>> _cachedNodes = new();
        private readonly Stack<OctreeNode<T>[]> _cachedChildren = new();
        private readonly Stack<OctreeItem<T>> _cachedItems = new();

        public int MaxChildren { get; private set; }

        public int MaxCachedItemCount { get; set; } = 100;

        public OctreeNodeCache(int maxChildren)
        {
            MaxChildren = maxChildren;
        }

        public void AddNode(OctreeNode<T> child)
        {
            Debug.Assert(!_cachedNodes.Contains(child));
            if (_cachedNodes.Count < MaxCachedItemCount)
            {
                for (var i = 0; i < child._items.Count; i++)
                {
                    var item = child._items[i];
                    item.Item = default(T);
                    item.Container = null;
                }
                child.Parent = null;
                child.Children = Array.Empty<OctreeNode<T>>();

                _cachedNodes.Push(child);
            }
        }

        public void AddOctreeItem(OctreeItem<T> octreeItem)
        {
            if (_cachedItems.Count < MaxCachedItemCount)
            {
                octreeItem.Item = default(T);
                octreeItem.Container = null;
                _cachedItems.Push(octreeItem);
            }
        }

        public OctreeNode<T> GetNode(ref BoundingBox bounds)
        {
            if (_cachedNodes.Count > 0)
            {
                var node = _cachedNodes.Pop();
                node.Reset(ref bounds);
                return node;
            }

            return CreateNewNode(ref bounds);
        }

        public void AddAndClearChildrenArray(OctreeNode<T>[] children)
        {
            if (_cachedChildren.Count < MaxCachedItemCount)
            {
                for (var i = 0; i < children.Length; i++)
                {
                    children[i] = null;
                }

                _cachedChildren.Push(children);
            }
        }

        public OctreeNode<T>[] GetChildrenArray()
        {
            if (_cachedChildren.Count > 0)
            {
                var children = _cachedChildren.Pop();
#if DEBUG
                Debug.Assert(children.Length == NumChildNodes);
                for (var i = 0; i < children.Length; i++)
                {
                    Debug.Assert(children[i] == null);
                }
#endif

                return children;
            }

            return new OctreeNode<T>[NumChildNodes];
        }

        public OctreeItem<T> GetOctreeItem(ref BoundingBox bounds, T item)
        {
            OctreeItem<T> octreeItem;
            if (_cachedItems.Count > 0)
            {
                octreeItem = _cachedItems.Pop();
                octreeItem.Bounds = bounds;
                octreeItem.Item = item;
                octreeItem.Container = null;
            }
            else
            {
                octreeItem = CreateNewItem(ref bounds, item);
            }

            return octreeItem;
        }

        private OctreeItem<T> CreateNewItem(ref BoundingBox bounds, T item) => new(ref bounds, item);

        private OctreeNode<T> CreateNewNode(ref BoundingBox bounds)
        {
            OctreeNode<T> node = new(ref bounds, MaxChildren, this, null);
            return node;
        }
    }
}