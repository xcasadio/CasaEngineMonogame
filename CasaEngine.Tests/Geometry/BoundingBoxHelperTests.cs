using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Geometry;

public class BoundingBoxHelperTests
{
    [Fact]
    public void Transform_ContainsAllRotatedCorners()
    {
        var localBounds = new BoundingBox(-Vector3.One * 0.5f, Vector3.One * 0.5f);
        var transform = Matrix.CreateRotationY(MathHelper.PiOver4)
            * Matrix.CreateRotationX(MathHelper.PiOver4 * 0.5f)
            * Matrix.CreateTranslation(3.0f, -2.0f, -5.0f);

        var transformedBounds = localBounds.Transform(transform);

        for (uint cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            var transformedCorner = Vector3.Transform(localBounds.Corner(cornerIndex), transform);

            Assert.InRange(transformedCorner.X, transformedBounds.Min.X, transformedBounds.Max.X);
            Assert.InRange(transformedCorner.Y, transformedBounds.Min.Y, transformedBounds.Max.Y);
            Assert.InRange(transformedCorner.Z, transformedBounds.Min.Z, transformedBounds.Max.Z);
        }
    }

    [Fact]
    public void Transform_ExpandsRotatedBoxForFrustumEdgeCases()
    {
        var localBounds = new BoundingBox(-Vector3.One * 0.5f, Vector3.One * 0.5f);
        var transform = Matrix.CreateRotationY(MathHelper.PiOver4)
            * Matrix.CreateTranslation(2.75f, 0.0f, -5.0f);

        var accurateBounds = localBounds.Transform(transform);

        var legacyMin = Vector3.Transform(localBounds.Min, transform);
        var legacyMax = Vector3.Transform(localBounds.Max, transform);
        var legacyBounds = new BoundingBox(Vector3.Min(legacyMin, legacyMax), Vector3.Max(legacyMin, legacyMax));

        var view = Matrix.CreateLookAt(Vector3.Zero, Vector3.Forward, Vector3.Up);
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 1.0f, 0.1f, 100.0f);
        var frustum = new BoundingFrustum(view * projection);

        Assert.NotEqual(ContainmentType.Disjoint, accurateBounds.ContainsPrecise(frustum));
        Assert.True(legacyBounds.GetDimensions().Z < 0.001f);
        Assert.True(accurateBounds.GetDimensions().Z > 1.4f);
        Assert.True(accurateBounds.Min.Z < legacyBounds.Min.Z);
        Assert.True(accurateBounds.Max.Z > legacyBounds.Max.Z);
    }
}
