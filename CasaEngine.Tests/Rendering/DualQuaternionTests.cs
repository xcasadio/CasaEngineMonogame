using CasaEngine.Core.Math;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class DualQuaternionTests
{
    [Fact]
    public void TryCreate_RoundTripsRigidTransform()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1f, 2f, 3f)), MathHelper.ToRadians(37f));
        var transform = Matrix.CreateFromQuaternion(rotation) * Matrix.CreateTranslation(new Vector3(2f, -3f, 4f));

        var success = DualQuaternion.TryCreate(transform, out var dualQuaternion);

        Assert.True(success);

        var roundTripped = dualQuaternion.ToMatrix();
        var point = new Vector3(0.25f, -0.5f, 1.3f);
        var expectedPoint = Vector3.Transform(point, transform);
        var actualPoint = Vector3.Transform(point, roundTripped);
        AssertVectorAlmostEqual(expectedPoint, actualPoint);
    }

    [Fact]
    public void TryCreate_ReturnsFalse_WhenTransformContainsScale()
    {
        var transform = Matrix.CreateScale(1.25f, 1f, 1f)
                        * Matrix.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(20f)))
                        * Matrix.CreateTranslation(new Vector3(1f, 2f, 3f));

        var success = DualQuaternion.TryCreate(transform, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryWriteSkinningPalette_ReturnsFalse_WhenAnyBoneUsesScale()
    {
        var sourcePalette = new[]
        {
            Matrix.Identity,
            Matrix.CreateScale(1.1f) * Matrix.CreateTranslation(new Vector3(0f, 1f, 0f)),
        };
        var destinationPalette = new Vector4[sourcePalette.Length * 2];

        var success = DualQuaternion.TryWriteSkinningPalette(sourcePalette, destinationPalette);

        Assert.False(success);
    }

    private static void AssertVectorAlmostEqual(Vector3 expected, Vector3 actual, float tolerance = 1e-4f)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0f, tolerance);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0f, tolerance);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0f, tolerance);
    }
}