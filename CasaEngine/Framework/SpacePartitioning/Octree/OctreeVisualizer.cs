using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SpacePartitioning.Octree;

public static class OctreeVisualizer
{
    public static void DisplayBoundingBoxes<T>(Octree<T> octree, Line3dRendererComponent line3dRendererComponent)
    {
        foreach (var boundingBox in GetBoundingBoxes(octree))
        {
            DisplayBoundingBox(boundingBox, line3dRendererComponent);
        }
    }

    private static List<BoundingBox> GetBoundingBoxes<T>(Octree<T> octree)
    {
        List<BoundingBox> boundingBoxes = new List<BoundingBox>();
        AddBoundingBoxes(octree.CurrentRoot, boundingBoxes);

        return boundingBoxes;
    }

    private static void AddBoundingBoxes<T>(OctreeNode<T> node, List<BoundingBox> boundingBoxes)
    {
        boundingBoxes.Add(node.Bounds);

        foreach (var child in node.Children)
        {
            AddBoundingBoxes(child, boundingBoxes);
        }
    }

    private static void DisplayBoundingBox(BoundingBox boundingBox, Line3dRendererComponent line3dRendererComponent)
    {
        var color = Color.Green;

        //face X, min Z
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z),
        new Vector3(boundingBox.Max.X, boundingBox.Min.Y, boundingBox.Min.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z),
            new Vector3(boundingBox.Min.X, boundingBox.Max.Y, boundingBox.Min.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Max.X, boundingBox.Min.Y, boundingBox.Min.Z),
            new Vector3(boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Min.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Min.X, boundingBox.Max.Y, boundingBox.Min.Z),
            new Vector3(boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Min.Z),
            color);

        //face X, max Z
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Max.Z),
            new Vector3(boundingBox.Max.X, boundingBox.Min.Y, boundingBox.Max.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Max.Z),
            new Vector3(boundingBox.Min.X, boundingBox.Max.Y, boundingBox.Max.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Max.X, boundingBox.Min.Y, boundingBox.Max.Z),
            new Vector3(boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Min.X, boundingBox.Max.Y, boundingBox.Max.Z),
            new Vector3(boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z),
            color);

        //face Z
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z),
            new Vector3(boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Max.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Max.X, boundingBox.Min.Y, boundingBox.Min.Z),
            new Vector3(boundingBox.Max.X, boundingBox.Min.Y, boundingBox.Max.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Min.X, boundingBox.Max.Y, boundingBox.Min.Z),
            new Vector3(boundingBox.Min.X, boundingBox.Max.Y, boundingBox.Max.Z),
            color);
        line3dRendererComponent.AddLine(
            new Vector3(boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Min.Z),
            new Vector3(boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z),
            color);
    }
}