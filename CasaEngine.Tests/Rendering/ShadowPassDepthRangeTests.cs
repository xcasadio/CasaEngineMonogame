using CasaEngine.Framework.Rendering.Draw;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ShadowPassDepthRangeTests
{
    private const int Resolution = 4096;
    private const float MaxDistance = 50.0f;
    private const double Tolerance = 1e-4;

    private static readonly Vector3 LightDirection = new(-0.5265408f, -0.5735765f, -0.6275069f);
    private static readonly Vector3 CameraPosition = new(123.4f, 5.6f, -78.9f);

    private static readonly Vector3[] WorldPoints =
    {
        new(120.0f, 0.0f, -80.0f),
        new(131.7f, 2.5f, -64.2f),
        new(98.1f, -1.0f, -101.3f),
        new(140.0f, 30.0f, -60.0f),
    };

    [Fact]
    public void ZeroDepthRange_GivesTheBitIdenticalMatrixOfTheFormerCode()
    {
        foreach (float maxDistance in new[] { 0.5f, 1.0f, 48.0f, 150.0f })
        {
            Matrix expected = FormerBuild(CameraPosition, LightDirection, maxDistance, Resolution);

            Matrix withOverload = ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition, LightDirection, maxDistance, 0.0f, Resolution);
            Matrix withLegacySignature = ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition, LightDirection, maxDistance, Resolution);

            AssertBitIdentical(expected, withOverload);
            AssertBitIdentical(expected, withLegacySignature);
        }
    }

    [Theory]
    [InlineData(60.0f)]
    [InlineData(99.0f)]
    [InlineData(140.0f)]
    public void PointUpstreamOfTheCameraBeyondTheMaxDistance_IsInsideTheDepthRangeOnlyWithADepthRange(float distanceUpstream)
    {
        const float depthRange = 150.0f;
        Vector3 direction = Vector3.Normalize(LightDirection);
        Vector3 point = CameraPosition - direction * distanceUpstream;

        double depthWithout = DepthOf(point, ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition, LightDirection, MaxDistance, Resolution));
        double depthWith = DepthOf(point, ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition, LightDirection, MaxDistance, depthRange, Resolution));

        // MaxDistance < distanceUpstream < depthRange: behind the light before, inside the map after.
        Assert.True(distanceUpstream > MaxDistance && distanceUpstream < depthRange);
        Assert.True(depthWithout < 0.0, $"expected the point to be clipped without a depth range, depth was {depthWithout}");
        Assert.InRange(depthWith, 1e-6, 1.0 - 1e-6);

        // The light sits depthRange upstream: the depth is linear in the distance from the light between 0.1 and 2 * depthRange.
        double expected = (depthRange - distanceUpstream - 0.1) / (2.0 * depthRange - 0.1);
        Assert.InRange(Math.Abs(depthWith - expected), 0.0, Tolerance);
    }

    [Fact]
    public void DepthRange_DoesNotChangeTheLateralCoordinatesNorTheTexelSnapping()
    {
        const float depthRange = 150.0f;
        Matrix without = ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition, LightDirection, MaxDistance, Resolution);
        Matrix with = ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition, LightDirection, MaxDistance, depthRange, Resolution);

        foreach (Vector3 point in WorldPoints)
        {
            (double withoutX, double withoutY) = ToTexels(point, without);
            (double withX, double withY) = ToTexels(point, with);

            Assert.InRange(Math.Abs(withX - withoutX), 0.0, 1e-2);
            Assert.InRange(Math.Abs(withY - withoutY), 0.0, 1e-2);
        }

        // The map still moves by whole texels when the camera moves, with a depth range.
        foreach (Vector3 move in new[] { new Vector3(0.01f, 0.0f, 0.0f), new Vector3(0.37f, 0.0f, 0.11f), new Vector3(-3.3f, 1.7f, 12.9f) })
        {
            Matrix after = ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition + move, LightDirection, MaxDistance, depthRange, Resolution);
            foreach (Vector3 point in WorldPoints)
            {
                (double beforeX, double beforeY) = ToTexels(point, with);
                (double afterX, double afterY) = ToTexels(point, after);

                Assert.InRange(Math.Abs(afterX - beforeX - Math.Round(afterX - beforeX)), 0.0, 1e-2);
                Assert.InRange(Math.Abs(afterY - beforeY - Math.Round(afterY - beforeY)), 0.0, 1e-2);
            }
        }

        // The camera stays within half a texel of the centre of the map.
        (double cameraX, double cameraY) = ToTexels(CameraPosition, with);
        Assert.InRange(Math.Abs(cameraX - Resolution / 2.0), 0.0, 0.5 + 1e-2);
        Assert.InRange(Math.Abs(cameraY - Resolution / 2.0), 0.0, 0.5 + 1e-2);
    }

    [Theory]
    [InlineData(0.0f, 48.0f, 48.0f)]
    [InlineData(-5.0f, 48.0f, 48.0f)]
    [InlineData(float.NaN, 48.0f, 48.0f)]
    [InlineData(120.0f, 48.0f, 120.0f)]
    [InlineData(0.25f, 48.0f, 1.0f)]
    public void ResolveDepthExtent_FollowsTheLateralExtentUnlessADepthRangeIsGiven(float depthRange, float lateralExtent, float expected)
    {
        Assert.Equal(expected, ShadowPass.ResolveDepthExtent(lateralExtent, depthRange));
    }

    // The code of BuildDirectionalShadowViewProjection before the depth range was introduced, kept here as the reference.
    private static Matrix FormerBuild(Vector3 cameraPosition, Vector3 lightDirection, float maxDistance, int resolution)
    {
        Vector3 direction = lightDirection;
        if (direction.LengthSquared() <= 0.0001f)
        {
            direction = Vector3.Normalize(new Vector3(-0.5f, -1.0f, -0.5f));
        }
        else
        {
            direction.Normalize();
        }

        float clampedDistance = MathF.Max(1.0f, maxDistance);
        Vector3 target = cameraPosition;
        Vector3 position = target - direction * clampedDistance;
        Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;

        Matrix view = Matrix.CreateLookAt(position, target, up);
        float texelSize = clampedDistance * 2.0f / resolution;
        view.M41 = MathF.Round(view.M41 / texelSize) * texelSize;
        view.M42 = MathF.Round(view.M42 / texelSize) * texelSize;

        Matrix projection = Matrix.CreateOrthographic(clampedDistance * 2.0f, clampedDistance * 2.0f, 0.1f, clampedDistance * 2.0f);
        return view * projection;
    }

    private static void AssertBitIdentical(Matrix expected, Matrix actual)
    {
        float[] expectedValues = ToArray(expected);
        float[] actualValues = ToArray(actual);
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedValues[i]), BitConverter.SingleToInt32Bits(actualValues[i]));
        }
    }

    private static float[] ToArray(Matrix m) => new[]
    {
        m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44,
    };

    private static double DepthOf(Vector3 point, Matrix m)
    {
        double clipZ = point.X * (double)m.M13 + point.Y * (double)m.M23 + point.Z * (double)m.M33 + m.M43;
        double clipW = point.X * (double)m.M14 + point.Y * (double)m.M24 + point.Z * (double)m.M34 + m.M44;
        return clipZ / clipW;
    }

    private static (double X, double Y) ToTexels(Vector3 point, Matrix m)
    {
        double clipX = point.X * (double)m.M11 + point.Y * (double)m.M21 + point.Z * (double)m.M31 + m.M41;
        double clipY = point.X * (double)m.M12 + point.Y * (double)m.M22 + point.Z * (double)m.M32 + m.M42;
        double clipW = point.X * (double)m.M14 + point.Y * (double)m.M24 + point.Z * (double)m.M34 + m.M44;
        return ((clipX / clipW * 0.5 + 0.5) * Resolution, (clipY / clipW * 0.5 + 0.5) * Resolution);
    }
}
