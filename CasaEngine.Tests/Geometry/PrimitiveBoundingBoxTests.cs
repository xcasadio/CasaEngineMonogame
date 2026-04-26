using CasaEngine.Framework.Rendering.Geometry;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Geometry;

public class PrimitiveBoundingBoxTests
{
    [Fact]
    public void BoxBoundingBox_IsCenteredOnOrigin()
    {
        var box = new Box { Size = new Vector3(1f, 1f, 1f) };

        Assert.Equal(new Vector3(-0.5f, -0.5f, -0.5f), box.BoundingBox.Min);
        Assert.Equal(new Vector3(0.5f, 0.5f, 0.5f), box.BoundingBox.Max);
    }

    [Fact]
    public void SphereBoundingBox_IsCenteredOnOrigin()
    {
        var sphere = new Sphere { Radius = 2f };

        Assert.Equal(new Vector3(-2f, -2f, -2f), sphere.BoundingBox.Min);
        Assert.Equal(new Vector3(2f, 2f, 2f), sphere.BoundingBox.Max);
    }

    [Fact]
    public void CylinderBoundingBox_IsCenteredOnOrigin()
    {
        var cylinder = new Cylinder { Length = 4f, Radius = 1.5f };

        Assert.Equal(new Vector3(-2f, -1.5f, -1.5f), cylinder.BoundingBox.Min);
        Assert.Equal(new Vector3(2f, 1.5f, 1.5f), cylinder.BoundingBox.Max);
    }

    [Fact]
    public void BoxBoundingBox_TransformKeepsDimensionsAfterTranslation()
    {
        var box = new Box { Size = Vector3.One };
        var translatedBounds = box.BoundingBox.Transform(Matrix.CreateTranslation(10f, 0f, 0f));

        Assert.Equal(Vector3.One, translatedBounds.Max - translatedBounds.Min);
        Assert.Equal(new Vector3(9.5f, -0.5f, -0.5f), translatedBounds.Min);
        Assert.Equal(new Vector3(10.5f, 0.5f, 0.5f), translatedBounds.Max);
    }
}