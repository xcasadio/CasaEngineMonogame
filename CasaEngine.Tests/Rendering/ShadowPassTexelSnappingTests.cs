using CasaEngine.Framework.Rendering.Draw;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ShadowPassTexelSnappingTests
{
    private const int Resolution = 4096;
    private const float MaxDistance = 150.0f;
    private const double Tolerance = 1e-3;

    private static readonly Vector3 LightDirection = new(-0.5265408f, -0.5735765f, -0.6275069f);
    private static readonly Vector3 CameraPosition = new(123.4f, 5.6f, -78.9f);

    private static readonly Vector3[] WorldPoints =
    {
        new(120.0f, 0.0f, -80.0f),
        new(131.7f, 2.5f, -64.2f),
        new(98.1f, -1.0f, -101.3f),
    };

    [Theory]
    [InlineData(0.01f, 0.0f, 0.0f)]
    [InlineData(0.02f, 0.013f, -0.005f)]
    [InlineData(0.37f, 0.0f, 0.11f)]
    [InlineData(-3.3f, 1.7f, 12.9f)]
    public void CameraMove_ShiftsTheShadowMapByWholeTexels(float dx, float dy, float dz)
    {
        Matrix before = ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition, LightDirection, MaxDistance, Resolution);
        Matrix after = ShadowPass.BuildDirectionalShadowViewProjection(CameraPosition + new Vector3(dx, dy, dz), LightDirection, MaxDistance, Resolution);

        foreach (Vector3 point in WorldPoints)
        {
            (double beforeX, double beforeY) = ToTexels(point, before);
            (double afterX, double afterY) = ToTexels(point, after);

            AssertWholeNumber(afterX - beforeX);
            AssertWholeNumber(afterY - beforeY);
        }
    }

    [Theory]
    [InlineData(0.0f, 0.0f, 0.0f)]
    [InlineData(0.031f, 0.0f, 0.0f)]
    [InlineData(-3.3f, 1.7f, 12.9f)]
    public void Camera_StaysWithinHalfATexelOfTheMapCentre(float dx, float dy, float dz)
    {
        Vector3 camera = CameraPosition + new Vector3(dx, dy, dz);
        Matrix viewProjection = ShadowPass.BuildDirectionalShadowViewProjection(camera, LightDirection, MaxDistance, Resolution);

        (double x, double y) = ToTexels(camera, viewProjection);

        Assert.InRange(Math.Abs(x - Resolution / 2.0), 0.0, 0.5 + Tolerance);
        Assert.InRange(Math.Abs(y - Resolution / 2.0), 0.0, 0.5 + Tolerance);
    }

    private static (double X, double Y) ToTexels(Vector3 point, Matrix m)
    {
        double clipX = point.X * (double)m.M11 + point.Y * (double)m.M21 + point.Z * (double)m.M31 + m.M41;
        double clipY = point.X * (double)m.M12 + point.Y * (double)m.M22 + point.Z * (double)m.M32 + m.M42;
        double clipW = point.X * (double)m.M14 + point.Y * (double)m.M24 + point.Z * (double)m.M34 + m.M44;
        return ((clipX / clipW * 0.5 + 0.5) * Resolution, (clipY / clipW * 0.5 + 0.5) * Resolution);
    }

    private static void AssertWholeNumber(double texels)
    {
        Assert.InRange(Math.Abs(texels - Math.Round(texels)), 0.0, Tolerance);
    }
}
