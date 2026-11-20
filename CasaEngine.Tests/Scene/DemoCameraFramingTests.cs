using CasaEngine.Demos;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// <see cref="DemoCameraFraming.Frame"/>, the method the demos frame their camera with: a framing asked of a camera
/// posed by an orbit goes through the orbit, so that it survives the next write of the orbit (the first mouse gesture).
/// </summary>
public sealed class DemoCameraFramingTests
{
    private const float Tolerance = 1e-4f;

    private static PerspectiveCameraComponent CreateCamera()
    {
        var camera = new PerspectiveCameraComponent();
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        camera.OnScreenResized(1920, 1080);
        return camera;
    }

    private static void AssertMatrixEqual(Matrix expected, Matrix actual)
    {
        float[] expectedTerms = Matrix.ToFloatArray(expected);
        float[] actualTerms = Matrix.ToFloatArray(actual);
        for (int index = 0; index < expectedTerms.Length; index++)
        {
            Assert.True(MathF.Abs(expectedTerms[index] - actualTerms[index]) <= Tolerance,
                $"term {index}: expected {expectedTerms[index]}, actual {actualTerms[index]}");
        }
    }

    [Fact]
    public void AFramingOfAnOrbitedCamera_SurvivesAWriteOfTheOrbit()
    {
        var camera = CreateCamera();
        var orbit = new OrbitCameraComponent();
        var entity = new Entity { RootComponent = camera };
        entity.AddComponent(orbit);
        var position = new Vector3(0f, 5.487f, 12.687f);
        var target = new Vector3(0f, 1.2f, 0f);

        DemoCameraFraming.Frame(camera, position, target);
        Matrix framed = camera.ViewMatrix;
        // What the first mouse gesture does: it writes the state of the orbit back.
        orbit.Distance = orbit.Distance;
        orbit.Yaw = orbit.Yaw;

        AssertMatrixEqual(Matrix.CreateLookAt(position, target, Vector3.Up), framed);
        AssertMatrixEqual(framed, camera.ViewMatrix);
        Assert.Equal(target, orbit.Target);
    }

    [Fact]
    public void AFramingOfACameraWithNoOrbit_PosesTheCameraDirectly()
    {
        var camera = CreateCamera();
        _ = new Entity { RootComponent = camera };
        var position = new Vector3(0f, 7f, 12f);
        var target = new Vector3(0f, 0.5f, 0f);

        DemoCameraFraming.Frame(camera, position, target);

        AssertMatrixEqual(Matrix.CreateLookAt(position, target, Vector3.Up), camera.ViewMatrix);
    }

    [Fact]
    public void AFramingOfACameraWithNoEntity_PosesTheCameraDirectly()
    {
        var camera = CreateCamera();

        DemoCameraFraming.Frame(camera, new Vector3(3f, 2f, 9f), Vector3.Zero);

        Assert.Equal(new Vector3(3f, 2f, 9f), camera.LocalPosition);
    }
}
