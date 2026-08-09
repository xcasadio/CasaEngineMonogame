using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Scene;

public sealed class Camera2dComponentTests
{
    private const float NearPlane = 1.0f;
    private const float FarPlane = 1000.0f;

    private static Camera2dComponent CreateCamera(int width = 1920, int height = 1080)
    {
        var camera = new Camera2dComponent();
        camera.OnScreenResized(width, height);
        camera.FarPlane = FarPlane;
        camera.NearPlane = NearPlane;
        return camera;
    }

    [Fact]
    public void ProjectionMatrix_UsesViewportSizeDividedByZoom()
    {
        var camera = CreateCamera();

        Assert.Equal(Matrix.CreateOrthographic(1920f, 1080f, NearPlane, FarPlane), camera.ProjectionMatrix);

        camera.Zoom = 4f;

        Assert.Equal(Matrix.CreateOrthographic(480f, 270f, NearPlane, FarPlane), camera.ProjectionMatrix);
    }

    [Fact]
    public void Zoom_RejectsZeroAndNegativeValues()
    {
        var camera = CreateCamera();

        camera.Zoom = 0f;
        Assert.Equal(Camera2dComponent.MinimumZoom, camera.Zoom);

        camera.Zoom = -3f;
        Assert.Equal(Camera2dComponent.MinimumZoom, camera.Zoom);

        camera.Zoom = 2f;
        Assert.Equal(2f, camera.Zoom);
    }

    [Fact]
    public void OnScreenResized_UpdatesProjectionToNewViewportSize()
    {
        var camera = CreateCamera();
        _ = camera.ProjectionMatrix;

        camera.OnScreenResized(800, 600);

        Assert.Equal(Matrix.CreateOrthographic(800f, 600f, NearPlane, FarPlane), camera.ProjectionMatrix);
    }

    [Fact]
    public void PixelSnap_SnapsViewMatrixToTexelGridWithoutChangingTarget()
    {
        var camera = CreateCamera();
        camera.Zoom = 2f;
        camera.PixelSnap = true;
        camera.Target = new Vector3(10.3f, -20.4f, 0f);

        var snappedTarget = new Vector3(10.5f, -20.5f, 0f);
        var expected = Matrix.CreateLookAt(
            new Vector3(snappedTarget.X, snappedTarget.Y, snappedTarget.Z + 500f),
            snappedTarget,
            Vector3.Up);

        Assert.Equal(expected, camera.ViewMatrix);
        Assert.Equal(new Vector3(10.3f, -20.4f, 0f), camera.Target);
    }

    [Fact]
    public void PixelSnap_KeepsViewMatrixStableInsideTheSameTexelCell()
    {
        var camera = CreateCamera();
        camera.PixelSnap = true;
        camera.Target = new Vector3(10.1f, 5.1f, 0f);
        var viewMatrix = camera.ViewMatrix;

        camera.Target = new Vector3(10.2f, 5.2f, 0f);
        Assert.Equal(viewMatrix, camera.ViewMatrix);

        camera.Target = new Vector3(10.9f, 5.1f, 0f);
        Assert.NotEqual(viewMatrix, camera.ViewMatrix);
    }

    [Fact]
    public void PixelSnap_Disabled_KeepsExactTargetInViewMatrix()
    {
        var camera = CreateCamera();
        camera.Target = new Vector3(10.3f, -20.4f, 0f);

        var expected = Matrix.CreateLookAt(
            new Vector3(10.3f, -20.4f, 500f),
            new Vector3(10.3f, -20.4f, 0f),
            Vector3.Up);

        Assert.Equal(expected, camera.ViewMatrix);
    }

    [Fact]
    public void Clone_CopiesTargetZoomAndPixelSnap()
    {
        var camera = CreateCamera();
        camera.Target = new Vector3(3f, 4f, 5f);
        camera.Zoom = 3f;
        camera.PixelSnap = true;

        var clone = camera.Clone();

        Assert.Equal(camera.Target, clone.Target);
        Assert.Equal(camera.Zoom, clone.Zoom);
        Assert.Equal(camera.PixelSnap, clone.PixelSnap);
    }

    [Fact]
    public void SetPositionAndTarget_MovesTargetOnly()
    {
        var camera = CreateCamera();

        camera.SetPositionAndTarget(new Vector3(100f, 100f, 100f), new Vector3(12f, 34f, 0f));

        Assert.Equal(new Vector3(12f, 34f, 0f), camera.Target);
    }

    // Equivalence with the perspective trick this component replaced (the former
    // Camera3dIn2dAxisComponent, since removed): a camera pushed back to
    // z = (height / 2) / tan(fov / 2) frames the target plane exactly like an orthographic camera of
    // the viewport size. The reference is rebuilt here from that formula, with the field of view
    // Camera3dComponent.ComputeFieldOfView derives from the aspect ratio.
    [Fact]
    public void TargetPlanePointsProjectLikeThePerspectiveTrickAtZoomOne()
    {
        const int width = 1920;
        const int height = 1080;
        var target = new Vector3(640f, 360f, 0f);

        var camera = CreateCamera(width, height);
        camera.Target = target;

        var aspectRatio = width / (float)height;
        var fieldOfView = MathHelper.PiOver4 * 1.777777777f / aspectRatio;
        var distance = height * 0.5f / MathF.Tan(fieldOfView * 0.5f);
        var referenceView = Matrix.CreateLookAt(
            new Vector3(target.X, target.Y, target.Z + distance),
            target,
            Vector3.Up);
        var referenceProjection = Matrix.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, NearPlane, FarPlane);

        var orthographic = camera.ViewMatrix * camera.ProjectionMatrix;
        var perspective = referenceView * referenceProjection;

        foreach (var offset in new[]
                 {
                     Vector3.Zero,
                     new Vector3(400f, 0f, 0f),
                     new Vector3(-400f, 0f, 0f),
                     new Vector3(0f, 300f, 0f),
                     new Vector3(0f, -300f, 0f),
                     new Vector3(-960f, 540f, 0f),
                 })
        {
            var point = target + offset;
            var orthographicNdc = ProjectToNdc(point, orthographic);
            var perspectiveNdc = ProjectToNdc(point, perspective);

            Assert.Equal(perspectiveNdc.X, orthographicNdc.X, 3);
            Assert.Equal(perspectiveNdc.Y, orthographicNdc.Y, 3);
        }
    }

    private static Vector2 ProjectToNdc(Vector3 point, Matrix viewProjection)
    {
        var clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
        return new Vector2(clip.X / clip.W, clip.Y / clip.W);
    }
}
