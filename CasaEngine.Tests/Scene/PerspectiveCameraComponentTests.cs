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
/// The perspective camera posed by the transform of its component (ADR-0147).
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

    [Fact]
    public void TheEditorWriter_WritesTheKeysOfTheCamera_InTheSameOrderAsBeforeTheFusion()
    {
        // The 3d camera class merged its abstract base (ADR-0149): the keys written for it and their order must not move.
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(new Entity { RootComponent = CreateCamera() }, node);

        string[] keys = ((JObject)node["root_component"]!).Properties().Select(property => property.Name).ToArray();

        Assert.Equal(ExpectedCameraKeys, keys);
    }

    private static readonly string[] ExpectedCameraKeys =
        ["id", "name", "type", "local_transform", "children_component", "view_distance", "viewport", "fieldOfView", "picture_offset"];

    [Fact]
    public void TheCamera2d_OverridesTheViewRefreshHook_SinceItIsPosedByItsTransform()
    {
        // ADR-0150. Before it, the 2d camera placed itself from a target and left the hook alone.
        var method = typeof(Camera2dComponent).GetMethod("IsViewOutOfDate", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Equal(typeof(Camera2dComponent), method!.DeclaringType);
    }

    [Fact]
    public void ThePerspectiveCamera_OverridesTheViewRefreshHook()
    {
        var method = typeof(PerspectiveCameraComponent).GetMethod("IsViewOutOfDate", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Equal(typeof(PerspectiveCameraComponent), method!.DeclaringType);
    }
    // ---- Picture offset ------------------------------------------------------------------------------------

    private const float PixelTolerance = 0.01f;

    // Where a point given in view space lands on the screen, in pixels, y down.
    private static Vector2 ProjectToPixels(CameraComponent camera, Vector3 viewPoint)
    {
        var clip = Vector4.Transform(new Vector4(viewPoint, 1f), camera.ProjectionMatrix);
        float ndcX = clip.X / clip.W;
        float ndcY = clip.Y / clip.W;
        return new Vector2(
            (ndcX * 0.5f + 0.5f) * camera.Viewport.Width,
            (1f - (ndcY * 0.5f + 0.5f)) * camera.Viewport.Height);
    }

    // The point of view space that a centred camera draws at the given pixel, at the given depth.
    private static Vector3 ViewPointAtPixel(PerspectiveCameraComponent camera, float pixelX, float pixelY, float depth)
    {
        float halfHeight = depth * MathF.Tan(camera.FieldOfView * 0.5f);
        float halfWidth = halfHeight * camera.Viewport.AspectRatio;
        float ndcX = pixelX / camera.Viewport.Width * 2f - 1f;
        float ndcY = 1f - pixelY / camera.Viewport.Height * 2f;
        return new Vector3(ndcX * halfWidth, ndcY * halfHeight, -depth);
    }

    private static Matrix ExpectedOffCentreProjection(PerspectiveCameraComponent camera, Vector2 offset)
    {
        float near = camera.NearPlane;
        float halfHeight = near * MathF.Tan(camera.FieldOfView * 0.5f);
        float halfWidth = halfHeight * camera.Viewport.AspectRatio;
        float centreX = -offset.X * 2f * halfWidth;
        float centreY = offset.Y * 2f * halfHeight;
        return Matrix.CreatePerspectiveOffCenter(
            centreX - halfWidth, centreX + halfWidth, centreY - halfHeight, centreY + halfHeight, near, camera.FarPlane);
    }

    [Fact]
    public void ANewCamera_HasNoPictureOffset_AndTheCentredProjection()
    {
        var camera = CreateCamera();

        Assert.Equal(Vector2.Zero, camera.PictureOffset);
        AssertMatrixEqual(
            Matrix.CreatePerspectiveFieldOfView(camera.FieldOfView, 320f / 240f, 1f, 1000f),
            camera.ProjectionMatrix,
            1e-6f);
    }

    [Theory]
    [InlineData(2f)]
    [InlineData(50f)]
    [InlineData(900f)]
    public void AnOffset_MovesEveryDepthByTheSamePixels_RightAndUp(float depth)
    {
        var centred = CreateCamera();
        var shifted = CreateCamera();
        shifted.PictureOffset = new Vector2(0.25f, -0.1f);
        var point = ViewPointAtPixel(centred, 100f, 90f, depth);

        var before = ProjectToPixels(centred, point);
        var after = ProjectToPixels(shifted, point);

        Assert.Equal(100f, before.X, PixelTolerance);
        Assert.Equal(90f, before.Y, PixelTolerance);
        Assert.Equal(80f, after.X - before.X, PixelTolerance);
        Assert.Equal(-24f, after.Y - before.Y, PixelTolerance);
    }

    [Fact]
    public void AnOffsetOfNPixels_MovesThePictureByNPixels_OverTheWholeWidth()
    {
        var centred = CreateCamera();
        var shifted = CreateCamera();
        var point = ViewPointAtPixel(centred, 160f, 120f, 50f);
        var before = ProjectToPixels(centred, point);

        for (int pixels = -320; pixels <= 320; pixels++)
        {
            shifted.PictureOffset = new Vector2(pixels / 320f, 0f);

            var after = ProjectToPixels(shifted, point);

            Assert.True(MathF.Abs(after.X - before.X - pixels) < PixelTolerance,
                $"offset of {pixels} pixels moved the picture by {after.X - before.X}");
            Assert.True(MathF.Abs(after.Y - before.Y) < PixelTolerance);
        }
    }

    [Fact]
    public void TheFrustum_FollowsTheOffset()
    {
        var camera = CreateCamera();
        var broughtIn = ViewPointAtPixel(camera, -40f, 120f, 50f);
        var pushedOut = ViewPointAtPixel(camera, 300f, 120f, 50f);
        var centredFrustum = new BoundingFrustum(camera.ViewMatrix * camera.ProjectionMatrix);

        camera.PictureOffset = new Vector2(0.25f, 0f);
        var shiftedFrustum = new BoundingFrustum(camera.ViewMatrix * camera.ProjectionMatrix);

        Assert.Equal(ContainmentType.Disjoint, centredFrustum.Contains(broughtIn));
        Assert.Equal(ContainmentType.Contains, centredFrustum.Contains(pushedOut));
        Assert.Equal(ContainmentType.Contains, shiftedFrustum.Contains(broughtIn));
        Assert.Equal(ContainmentType.Disjoint, shiftedFrustum.Contains(pushedOut));
    }

    [Fact]
    public void AnOffsetChangedInGame_IsSeenAtTheNextRead()
    {
        var camera = CreateCamera();
        var first = camera.ProjectionMatrix;

        camera.PictureOffset = new Vector2(0.4f, 0.3f);
        var second = camera.ProjectionMatrix;

        Assert.NotEqual(first, second);
        AssertMatrixEqual(ExpectedOffCentreProjection(camera, new Vector2(0.4f, 0.3f)), second, 1e-6f);
    }

    [Fact]
    public void TheOffset_DoesNotMoveThePointOfView()
    {
        var camera = CreateCamera();
        camera.LocalPosition = new Vector3(1f, 2f, 3f);
        var view = camera.ViewMatrix;

        camera.PictureOffset = new Vector2(0.5f, -0.5f);

        Assert.Equal(view, camera.ViewMatrix);
    }

    [Fact]
    public void TheOffset_RoundTripsThroughTheEditorWriter_UnderItsKey()
    {
        var camera = CreateCamera();
        camera.PictureOffset = new Vector2(0.25f, -0.1f);
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(new Entity { RootComponent = camera }, node);
        var cameraNode = (JObject)node["root_component"]!;

        Assert.Equal(0.25f, (float)cameraNode["picture_offset"]!["x"]!, ViewTolerance);
        Assert.Equal(-0.1f, (float)cameraNode["picture_offset"]!["y"]!, ViewTolerance);

        var loaded = Assert.IsType<PerspectiveCameraComponent>(ElementFactory.LoadComponent<SceneComponent>(cameraNode));

        Assert.Equal(new Vector2(0.25f, -0.1f), loaded.PictureOffset);
    }

    [Fact]
    public void ACameraSavedWithoutTheOffsetKey_LoadsWithNoOffset()
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(new Entity { RootComponent = CreateCamera() }, node);
        var cameraNode = (JObject)node["root_component"]!;
        Assert.True(cameraNode.Remove("picture_offset"));

        var loaded = Assert.IsType<PerspectiveCameraComponent>(ElementFactory.LoadComponent<SceneComponent>(cameraNode));

        Assert.Equal(Vector2.Zero, loaded.PictureOffset);
    }

    [Fact]
    public void Clone_KeepsTheOffset()
    {
        var camera = CreateCamera();
        camera.PictureOffset = new Vector2(-0.3f, 0.2f);

        Assert.Equal(new Vector2(-0.3f, 0.2f), camera.Clone().PictureOffset);
    }
}
