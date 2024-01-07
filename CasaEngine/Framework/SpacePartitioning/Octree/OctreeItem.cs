using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SpacePartitioning.Octree;

public class OctreeItem<T>
{
    //The node this item directly resides in
    public OctreeNode<T> Container;
    public BoundingBox Bounds;
    public T Item;

    public bool HasPendingMove { get; set; }

    public OctreeItem(ref BoundingBox bounds, T item)
    {
        Bounds = bounds;
        Item = item;
    }

    public override string ToString()
    {
        return $"{Bounds}, {Item}";
    }
}