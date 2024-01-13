using CasaEngine.Framework.SpacePartitioning.Octree;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace CasaEngine.Tests;

public class OctreeTests
{
    [Test]
    public void Should()
    {
        var octree = new Octree<DummyObject>(new BoundingBox(-Vector3.One * 150, Vector3.One * 150), 16);
        octree.Clear();

        for (int i = 0; i < 100; i++)
        {
            octree.AddItem(new BoundingBox(-Vector3.One + new Vector3(i), Vector3.One + new Vector3(i)), new DummyObject());
        }
    }
}

public class DummyObject
{
}