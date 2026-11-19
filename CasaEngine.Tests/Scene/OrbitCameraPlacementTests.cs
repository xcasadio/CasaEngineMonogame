using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// Pins the contract of <see cref="OrbitCameraComponent.SetCamera"/>: after the call the camera must
/// stand at the requested position and look at the requested target.
/// </summary>
public class OrbitCameraPlacementTests
{
    private const float Tolerance = 1e-3f;

    // The components are exercised without a world: OnScreenResized sets the viewport size and the
    // FarPlane / NearPlane setters fill the depth range InitializeWithWorld would normally provide.
    // NearPlane is clamped against MaxDepth, so FarPlane has to be assigned first.
    private static PerspectiveCameraComponent CreateCamera()
    {
        var camera = new PerspectiveCameraComponent();
        camera.OnScreenResized(1920, 1080);
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        return camera;
    }

    private static (PerspectiveCameraComponent Camera, OrbitCameraComponent Orbit) CreateOrbitCamera()
    {
        var camera = CreateCamera();
        var orbit = new OrbitCameraComponent();
        var entity = new Entity { RootComponent = camera };
        entity.AddComponent(orbit);
        return (camera, orbit);
    }

    public static TheoryData<float, float, float, float, float, float> CameraPlacements => new()
    {
        // Camera straight in front of the origin: the most common demo setup, and the case
        // Math.Sign(zAxis.X) == 0 used to mirror in Z.
        { 0f, 12f, 15f, 0f, 0f, 0f },
        // Non zero target, still on the YZ plane (EnvironmentShowcaseDemo).
        { 0f, 4.5f, 15.5f, 0f, 1.2f, 0f },
        // Off axis camera with a non zero target (TileMap3dDemo framing).
        { 34.686f, 42.36f, 50.701f, 0f, 8.8f, 0f },
        // Target away from the origin on X (SplitScreenDemo).
        { -10f, 8f, 18f, -10f, 1.4f, 0f },
        // Camera behind the target on -Z: the case the previous implementation got right.
        { 6f, 3f, -12f, 0f, 0f, 0f },
    };

    [Theory]
    [MemberData(nameof(CameraPlacements))]
    public void SetCamera_PlacesTheCameraAtThePositionLookingAtTheTarget(
        float positionX, float positionY, float positionZ,
        float targetX, float targetY, float targetZ)
    {
        var position = new Vector3(positionX, positionY, positionZ);
        var target = new Vector3(targetX, targetY, targetZ);

        var (camera, orbit) = CreateOrbitCamera();
        orbit.SetCamera(position, target);

        var view = camera.ViewMatrix;

        Assert.Equal(target.X, orbit.Target.X, Tolerance);
        Assert.Equal(target.Y, orbit.Target.Y, Tolerance);
        Assert.Equal(target.Z, orbit.Target.Z, Tolerance);

        Assert.Equal(position.X, camera.LocalPosition.X, Tolerance);
        Assert.Equal(position.Y, camera.LocalPosition.Y, Tolerance);
        Assert.Equal(position.Z, camera.LocalPosition.Z, Tolerance);

        // In view space the target sits straight ahead, at the requested distance down -Z.
        var targetInViewSpace = Vector3.Transform(target, view);
        Assert.Equal(0f, targetInViewSpace.X, Tolerance);
        Assert.Equal(0f, targetInViewSpace.Y, Tolerance);
        Assert.Equal(-(position - target).Length(), targetInViewSpace.Z, Tolerance);
    }

    [Theory]
    [MemberData(nameof(CameraPlacements))]
    public void SetCamera_MatchesCreateLookAt(
        float positionX, float positionY, float positionZ,
        float targetX, float targetY, float targetZ)
    {
        var position = new Vector3(positionX, positionY, positionZ);
        var target = new Vector3(targetX, targetY, targetZ);

        var (camera, orbit) = CreateOrbitCamera();
        orbit.SetCamera(position, target);

        // The orbit has no roll, so its up vector stays in the vertical plane holding the view
        // direction: the resulting basis is the one CreateLookAt builds from the world up.
        var expected = Matrix.CreateLookAt(position, target, Vector3.Up);
        var actual = camera.ViewMatrix;

        Assert.Equal(expected.M11, actual.M11, Tolerance);
        Assert.Equal(expected.M12, actual.M12, Tolerance);
        Assert.Equal(expected.M13, actual.M13, Tolerance);
        Assert.Equal(expected.M21, actual.M21, Tolerance);
        Assert.Equal(expected.M22, actual.M22, Tolerance);
        Assert.Equal(expected.M23, actual.M23, Tolerance);
        Assert.Equal(expected.M31, actual.M31, Tolerance);
        Assert.Equal(expected.M32, actual.M32, Tolerance);
        Assert.Equal(expected.M33, actual.M33, Tolerance);
        Assert.Equal(expected.M41, actual.M41, Tolerance);
        Assert.Equal(expected.M42, actual.M42, Tolerance);
        Assert.Equal(expected.M43, actual.M43, Tolerance);
    }

    [Fact]
    public void SetCamera_StoresAPositiveDistanceLikeTheEditorAndTheDistanceSetter()
    {
        var (_, orbit) = CreateOrbitCamera();
        orbit.SetCamera(new Vector3(0f, 12f, 15f), Vector3.Zero);

        // The Distance setter and EditorViewportCameraController both work with a
        // positive distance (camera at Target - Direction * Distance). A negative value here would
        // mirror the orbit / move controls until the next Distance assignment flips the camera.
        Assert.True(orbit.Distance > 0f);
        Assert.Equal(new Vector3(0f, 12f, 15f).Length(), orbit.Distance, Tolerance);
    }

    [Fact]
    public void SetCamera_ThenZoomingWithTheDistanceSetter_KeepsTheCameraOnTheSameSide()
    {
        var (camera, orbit) = CreateOrbitCamera();
        orbit.SetCamera(new Vector3(0f, 1f, 2.4f), new Vector3(0f, 0.9f, 0f));
        _ = camera.ViewMatrix;
        Assert.True(camera.LocalPosition.Z > 0f);

        orbit.Distance /= 1.1f;
        _ = camera.ViewMatrix;

        Assert.True(camera.LocalPosition.Z > 0f);
        Assert.True(camera.LocalPosition.Z < 2.4f);
    }

    [Fact]
    public void SetCamera_GivesTheCameraTheViewOfSetPositionAndTarget()
    {
        var position = new Vector3(0f, 8.5f, 38f);
        var target = new Vector3(0f, 2f, 0f);

        var (orbited, orbit) = CreateOrbitCamera();
        orbit.SetCamera(position, target);

        var plain = CreateCamera();
        plain.SetPositionAndTarget(position, target);

        Assert.Equal(target.Y, orbit.Target.Y, Tolerance);
        Assert.Equal(position.Z, orbited.LocalPosition.Z, Tolerance);

        var expected = plain.ViewMatrix;
        var actual = orbited.ViewMatrix;
        Assert.Equal(expected.M11, actual.M11, Tolerance);
        Assert.Equal(expected.M12, actual.M12, Tolerance);
        Assert.Equal(expected.M13, actual.M13, Tolerance);
        Assert.Equal(expected.M21, actual.M21, Tolerance);
        Assert.Equal(expected.M22, actual.M22, Tolerance);
        Assert.Equal(expected.M23, actual.M23, Tolerance);
        Assert.Equal(expected.M31, actual.M31, Tolerance);
        Assert.Equal(expected.M32, actual.M32, Tolerance);
        Assert.Equal(expected.M33, actual.M33, Tolerance);
        Assert.Equal(expected.M41, actual.M41, Tolerance);
        Assert.Equal(expected.M42, actual.M42, Tolerance);
        Assert.Equal(expected.M43, actual.M43, Tolerance);
    }
}
