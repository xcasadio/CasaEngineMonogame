using System.Linq;
using System.Reflection;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The perspective camera posed by the transform of its component (ADR-0144).
/// </summary>
public sealed class PerspectiveCameraComponentTests
{
    private const float ViewTolerance = 1e-5f;

    private static PerspectiveCameraComponent CreateCamera(int width = 320, int height = 240)
    {
        var camera = new PerspectiveCameraComponent();
        // Without a world the viewport is empty: the far plane first, since the near plane is clamped on it.
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        camera.OnScreenResized(width, height);
        return camera;
    }

    private static void AssertMatrixEqual(Matrix expected, Matrix actual, float tolerance)
    {
        Assert.Equal(expected.M11, actual.M11, tolerance);
        Assert.Equal(expected.M12, actual.M12, tolerance);
        Assert.Equal(expected.M13, actual.M13, tolerance);
        Assert.Equal(expected.M14, actual.M14, tolerance);
        Assert.Equal(expected.M21, actual.M21, tolerance);
        Assert.Equal(expected.M22, actual.M22, tolerance);
        Assert.Equal(expected.M23, actual.M23, tolerance);
        Assert.Equal(expected.M24, actual.M24, tolerance);
        Assert.Equal(expected.M31, actual.M31, tolerance);
        Assert.Equal(expected.M32, actual.M32, tolerance);
        Assert.Equal(expected.M33, actual.M33, tolerance);
        Assert.Equal(expected.M34, actual.M34, tolerance);
        Assert.Equal(expected.M41, actual.M41, tolerance);
        Assert.Equal(expected.M42, actual.M42, tolerance);
        Assert.Equal(expected.M43, actual.M43, tolerance);
        Assert.Equal(expected.M44, actual.M44, tolerance);
    }

    [Fact]
    public void TheView_IsTheInverseOfTheWorldTransform_RollIncluded()
    {
        var camera = CreateCamera();
        camera.LocalPosition = new Vector3(3f, -2f, 7f);
        camera.LocalOrientation = Quaternion.CreateFromYawPitchRoll(0.7f, -0.4f, 0.3f);

        var expected = Matrix.Invert(
            Matrix.CreateFromQuaternion(camera.LocalOrientation) * Matrix.CreateTranslation(camera.LocalPosition));

        AssertMatrixEqual(Matrix.Invert(camera.WorldMatrixNoScale), camera.ViewMatrix, ViewTolerance);
        AssertMatrixEqual(expected, camera.ViewMatrix, ViewTolerance);
    }

    [Fact]
    public void ARolledCamera_SeesItsLocalUpAsTheScreenUp()
    {
        var camera = CreateCamera();
        camera.LocalOrientation = Quaternion.CreateFromYawPitchRoll(0f, 0f, MathHelper.PiOver2);

        // A quarter turn about the viewing axis: the world +X axis becomes the camera's -Y.
        var seen = Vector3.TransformNormal(Vector3.UnitX, camera.ViewMatrix);

        Assert.Equal(0f, seen.X, ViewTolerance);
        Assert.Equal(-1f, seen.Y, ViewTolerance);
        Assert.Equal(0f, seen.Z, ViewTolerance);
    }

    [Fact]
    public void AChangeOfPositionOrOrientation_IsSeenWithoutUpdate()
    {
        var camera = CreateCamera();
        var first = camera.ViewMatrix;

        camera.LocalPosition = new Vector3(10f, 20f, 30f);
        var moved = camera.ViewMatrix;
        camera.LocalOrientation = Quaternion.CreateFromYawPitchRoll(1f, 0.2f, -0.5f);
        var turned = camera.ViewMatrix;

        Assert.NotEqual(first, moved);
        Assert.NotEqual(moved, turned);
        AssertMatrixEqual(Matrix.Invert(camera.WorldMatrixNoScale), turned, ViewTolerance);
    }

    [Fact]
    public void AMoveOfTheParentEntity_IsSeenWithoutUpdate()
    {
        var parent = new Entity { RootComponent = new TransformComponent() };
        var camera = CreateCamera();
        camera.LocalPosition = new Vector3(0f, 1f, 5f);
        var child = new Entity { RootComponent = camera };
        parent.AddChild(child);
        var first = camera.ViewMatrix;

        parent.RootComponent.LocalPosition = new Vector3(100f, 0f, 0f);
        parent.RootComponent.LocalOrientation = Quaternion.CreateFromYawPitchRoll(0.5f, 0f, 0f);

        var expectedWorld = Matrix.CreateTranslation(camera.LocalPosition)
                            * Matrix.CreateFromQuaternion(parent.RootComponent.LocalOrientation)
                            * Matrix.CreateTranslation(parent.RootComponent.LocalPosition);
        Assert.NotEqual(first, camera.ViewMatrix);
        AssertMatrixEqual(Matrix.Invert(expectedWorld), camera.ViewMatrix, ViewTolerance);
    }

    [Fact]
    public void AScaledComponent_GivesTheSameViewAsAnUnscaledOne()
    {
        var plain = CreateCamera();
        var scaled = CreateCamera();
        foreach (var camera in new[] { plain, scaled })
        {
            camera.LocalPosition = new Vector3(4f, 5f, 6f);
            camera.LocalOrientation = Quaternion.CreateFromYawPitchRoll(0.3f, 0.2f, 0.1f);
        }

        scaled.LocalScale = new Vector3(2f, 3f, 4f);

        AssertMatrixEqual(plain.ViewMatrix, scaled.ViewMatrix, ViewTolerance);
    }

    [Fact]
    public void SetPositionAndTarget_EqualsALookAt_ForACameraWithNoParent()
    {
        var camera = CreateCamera();
        var position = new Vector3(12f, 7f, -3f);
        var target = new Vector3(-4f, 1f, 9f);

        camera.SetPositionAndTarget(position, target);

        AssertMatrixEqual(Matrix.CreateLookAt(position, target, Vector3.Up), camera.ViewMatrix, ViewTolerance);
        Assert.Equal(position, camera.LocalPosition);
    }

    [Fact]
    public void SetPositionAndTarget_RefusesATargetItCannotAimAt()
    {
        var camera = CreateCamera();

        Assert.Throws<ArgumentException>(() => camera.SetPositionAndTarget(Vector3.One, Vector3.One));
        Assert.Throws<ArgumentException>(() => camera.SetPositionAndTarget(Vector3.Zero, new Vector3(0f, 5f, 0f)));
    }

    [Fact]
    public void Clone_GivesACameraOfTheSameClass_FieldOfViewAndTransform()
    {
        var camera = CreateCamera();
        camera.FieldOfView = 0.6f;
        camera.LocalPosition = new Vector3(1f, 2f, 3f);
        camera.LocalOrientation = Quaternion.CreateFromYawPitchRoll(0.1f, 0.2f, 0.3f);

        PerspectiveCameraComponent clone = camera.Clone();

        Assert.NotSame(camera, clone);
        Assert.Equal(0.6f, clone.FieldOfView, ViewTolerance);
        Assert.Equal(camera.LocalPosition, clone.LocalPosition);
        Assert.Equal(camera.LocalOrientation, clone.LocalOrientation);
        AssertMatrixEqual(camera.ViewMatrix, clone.ViewMatrix, ViewTolerance);
    }

    [Fact]
    public void ACamera_LoadsByItsTypeName_AndRoundTripsThroughTheEditorWriter()
    {
        var camera = CreateCamera();
        camera.FieldOfView = 0.5655f;
        camera.LocalPosition = new Vector3(-9f, 8f, 7f);
        camera.LocalOrientation = Quaternion.CreateFromYawPitchRoll(0.9f, -0.3f, 0.25f);
        var entity = new Entity { RootComponent = camera };

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);

        Assert.Equal(nameof(PerspectiveCameraComponent), (string)node["root_component"]!["type"]!);

        var loaded = ElementFactory.LoadComponent<SceneComponent>((JObject)node["root_component"]!);

        var loadedCamera = Assert.IsType<PerspectiveCameraComponent>(loaded);
        Assert.Equal(0.5655f, loadedCamera.FieldOfView, ViewTolerance);
        Assert.Equal(camera.LocalPosition, loadedCamera.LocalPosition);
        Assert.Equal(camera.LocalOrientation, loadedCamera.LocalOrientation);
        Assert.Equal(320, loadedCamera.Viewport.Width);
        Assert.Equal(240, loadedCamera.Viewport.Height);
        AssertMatrixEqual(camera.ViewMatrix, loadedCamera.ViewMatrix, ViewTolerance);
    }

    [Theory]
    [InlineData(typeof(Camera2dComponent))]
    [InlineData(typeof(ArcBallCameraComponent))]
    [InlineData(typeof(CameraLookAtComponent))]
    [InlineData(typeof(CameraTargeted2dComponent))]
    public void TheOtherCameras_DoNotOverrideTheViewRefreshHook(Type cameraType)
    {
        var method = cameraType.GetMethod("IsViewOutOfDate", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.Equal(typeof(CameraComponent), method!.DeclaringType);
    }

    [Fact]
    public void ThePerspectiveCamera_OverridesTheViewRefreshHook()
    {
        var method = typeof(PerspectiveCameraComponent).GetMethod("IsViewOutOfDate", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Equal(typeof(PerspectiveCameraComponent), method!.DeclaringType);
    }
}
