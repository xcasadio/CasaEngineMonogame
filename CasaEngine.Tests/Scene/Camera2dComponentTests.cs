using CasaEngine.Framework.Scene.Entities;
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

    //  Centres the camera on a point of the scene.
    private static void Place(Camera2dComponent camera, Vector3 point)
    {
        camera.CentreOn(point);
    }

    //  The view the camera gave before ADR-0150 for a target: it stood 500 units in front of the target, which was
    //  first rounded to the 1 / zoom grid in x and y when the pixel snap was on.
    private static Matrix FormulaView(Vector3 target, float zoom, bool pixelSnap)
    {
        if (pixelSnap)
        {
            float step = 1.0f / zoom;
            target.X = MathF.Round(target.X / step) * step;
            target.Y = MathF.Round(target.Y / step) * step;
        }

        return Matrix.CreateLookAt(new Vector3(target.X, target.Y, target.Z + 500f), target, Vector3.Up);
    }

    [Fact]
    public void OverAGridOfPointsZoomsAndSnaps_TheView_IsTheOneOfTheFormerTarget()
    {
        Vector3[] points = [Vector3.Zero, new Vector3(10.3f, -20.4f, 0f), new Vector3(640f, 360f, 0f), new Vector3(-123.45f, 67.89f, 12f), new Vector3(0.49f, 0.51f, -30f)];
        float[] zooms = [0.5f, 1f, 2f, 3f, 4f];

        foreach (Vector3 point in points)
        {
            foreach (float zoom in zooms)
            {
                foreach (bool pixelSnap in new[] { false, true })
                {
                    var camera = CreateCamera();
                    camera.Zoom = zoom;
                    camera.PixelSnap = pixelSnap;

                    Place(camera, point);

                    Matrix expected = FormulaView(point, zoom, pixelSnap);
                    float[] expectedTerms = Matrix.ToFloatArray(expected);
                    float[] actualTerms = Matrix.ToFloatArray(camera.ViewMatrix);
                    for (int index = 0; index < expectedTerms.Length; index++)
                    {
                        Assert.True(MathF.Abs(expectedTerms[index] - actualTerms[index]) <= 1e-4f,
                            $"term {index}: expected {expectedTerms[index]}, actual {actualTerms[index]} (point {point}, zoom {zoom}, snap {pixelSnap})");
                    }
                }
            }
        }
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
    public void PixelSnap_SnapsViewMatrixToTexelGridWithoutChangingTheTransform()
    {
        var camera = CreateCamera();
        camera.Zoom = 2f;
        camera.PixelSnap = true;
        camera.CentreOn(new Vector3(10.3f, -20.4f, 0f));

        var snappedTarget = new Vector3(10.5f, -20.5f, 0f);
        var expected = Matrix.CreateLookAt(
            new Vector3(snappedTarget.X, snappedTarget.Y, snappedTarget.Z + 500f),
            snappedTarget,
            Vector3.Up);

        AssertMatrixEqual(expected, camera.ViewMatrix);
        Assert.Equal(new Vector3(10.3f, -20.4f, 500f), camera.LocalPosition);
    }

    [Fact]
    public void PixelSnap_KeepsViewMatrixStableInsideTheSameTexelCell()
    {
        var camera = CreateCamera();
        camera.PixelSnap = true;
        camera.CentreOn(new Vector3(10.1f, 5.1f, 0f));
        var viewMatrix = camera.ViewMatrix;

        camera.CentreOn(new Vector3(10.2f, 5.2f, 0f));
        Assert.Equal(viewMatrix, camera.ViewMatrix);

        camera.CentreOn(new Vector3(10.9f, 5.1f, 0f));
        Assert.NotEqual(viewMatrix, camera.ViewMatrix);
    }

    [Fact]
    public void PixelSnap_Disabled_KeepsExactPointInViewMatrix()
    {
        var camera = CreateCamera();
        camera.CentreOn(new Vector3(10.3f, -20.4f, 0f));

        var expected = Matrix.CreateLookAt(
            new Vector3(10.3f, -20.4f, 500f),
            new Vector3(10.3f, -20.4f, 0f),
            Vector3.Up);

        AssertMatrixEqual(expected, camera.ViewMatrix);
    }

    [Fact]
    public void Clone_CopiesTheTransformZoomAndPixelSnap()
    {
        var camera = CreateCamera();
        camera.CentreOn(new Vector3(3f, 4f, 5f));
        camera.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Backward, 0.3f);
        camera.Zoom = 3f;
        camera.PixelSnap = true;

        var clone = camera.Clone();

        Assert.Equal(new Vector3(3f, 4f, 505f), clone.LocalPosition);
        Assert.Equal(camera.LocalOrientation, clone.LocalOrientation);
        AssertMatrixEqual(camera.ViewMatrix, clone.ViewMatrix);
        Assert.Equal(camera.Zoom, clone.Zoom);
        Assert.Equal(camera.PixelSnap, clone.PixelSnap);
    }

    [Fact]
    public void SetPositionAndTarget_CentresTheCameraOnTheTarget_WhateverThePosition()
    {
        var camera = CreateCamera();

        camera.SetPositionAndTarget(new Vector3(100f, 100f, 100f), new Vector3(12f, 34f, 0f));

        Assert.Equal(new Vector3(12f, 34f, 500f), camera.LocalPosition);
    }

    [Fact]
    public void ANewCamera_StandsTheDefaultDistanceInFrontOfTheOrigin()
    {
        var camera = CreateCamera();

        Assert.Equal(new Vector3(0f, 0f, Camera2dComponent.DefaultDistance), camera.LocalPosition);
        AssertMatrixEqual(Matrix.CreateLookAt(new Vector3(0f, 0f, 500f), Vector3.Zero, Vector3.Up), camera.ViewMatrix);
    }

    [Fact]
    public void CentreOn_PutsThePointAtTheCentreOfThePicture_AndKeepsTheOrientation()
    {
        var camera = CreateCamera();
        Quaternion roll = Quaternion.CreateFromAxisAngle(Vector3.Backward, 0.4f);
        camera.LocalOrientation = roll;

        camera.CentreOn(new Vector3(40f, -30f, 7f));

        Assert.Equal(roll, camera.LocalOrientation);
        Vector3 seen = Vector3.Transform(new Vector3(40f, -30f, 7f), camera.ViewMatrix);
        Assert.Equal(0f, seen.X, 3);
        Assert.Equal(0f, seen.Y, 3);
        Assert.Equal(-Camera2dComponent.DefaultDistance, seen.Z, 3);
    }

    [Fact]
    public void AMoveOfTheTransform_IsSeenAtTheNextReadOfTheView_WithoutUpdate()
    {
        var camera = CreateCamera();
        Matrix before = camera.ViewMatrix;

        camera.LocalPosition = new Vector3(25f, 0f, 500f);

        Assert.NotEqual(before, camera.ViewMatrix);
        AssertMatrixEqual(Matrix.CreateLookAt(new Vector3(25f, 0f, 500f), new Vector3(25f, 0f, 0f), Vector3.Up), camera.ViewMatrix);
    }

    [Fact]
    public void ARolledCamera_TurnsThePicture()
    {
        var camera = CreateCamera();
        // A quarter turn about the view axis: the world +X axis is seen along the screen -Y axis.
        camera.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Backward, MathHelper.PiOver2);

        Vector3 seen = Vector3.Transform(new Vector3(100f, 0f, 0f), camera.ViewMatrix);

        Assert.Equal(0f, seen.X, 3);
        Assert.Equal(-100f, seen.Y, 3);
        AssertMatrixEqual(Matrix.Invert(camera.WorldMatrixNoScale), camera.ViewMatrix);
    }

    [Fact]
    public void AMoveOfTheParentEntity_MovesTheView()
    {
        var parentRoot = new TransformComponent();
        var parent = new Entity { RootComponent = parentRoot };
        var camera = CreateCamera();
        var child = new Entity { RootComponent = camera };
        parent.AddChild(child);
        Matrix before = camera.ViewMatrix;

        parentRoot.LocalPosition = new Vector3(60f, -40f, 0f);

        Assert.NotEqual(before, camera.ViewMatrix);
        AssertMatrixEqual(Matrix.CreateLookAt(new Vector3(60f, -40f, 500f), new Vector3(60f, -40f, 0f), Vector3.Up), camera.ViewMatrix);
    }

    [Fact]
    public void PixelSnap_RoundsTheWorldTranslation_OfAParentedCamera()
    {
        var parentRoot = new TransformComponent { LocalPosition = new Vector3(10.3f, -20.4f, 0f) };
        var parent = new Entity { RootComponent = parentRoot };
        var camera = CreateCamera();
        camera.PixelSnap = true;
        var child = new Entity { RootComponent = camera };
        parent.AddChild(child);

        AssertMatrixEqual(Matrix.CreateLookAt(new Vector3(10f, -20f, 500f), new Vector3(10f, -20f, 0f), Vector3.Up), camera.ViewMatrix);
        Assert.Equal(new Vector3(0f, 0f, 500f), camera.LocalPosition);
    }

    private static void AssertMatrixEqual(Matrix expected, Matrix actual)
    {
        float[] expectedTerms = Matrix.ToFloatArray(expected);
        float[] actualTerms = Matrix.ToFloatArray(actual);
        for (int index = 0; index < expectedTerms.Length; index++)
        {
            Assert.True(MathF.Abs(expectedTerms[index] - actualTerms[index]) <= 1e-4f,
                $"term {index}: expected {expectedTerms[index]}, actual {actualTerms[index]}");
        }
    }

    // Equivalence with the perspective trick this component replaced (the former
    // Camera3dIn2dAxisComponent, since removed): a camera pushed back to
    // z = (height / 2) / tan(fov / 2) frames the target plane exactly like an orthographic camera of
    // the viewport size. The reference is rebuilt here from that formula, with the field of view
    // the 3d camera derived from the aspect ratio before ADR-0095 (pi/4 at 16:9, the value the 3d camera now keeps).
    [Fact]
    public void TargetPlanePointsProjectLikeThePerspectiveTrickAtZoomOne()
    {
        const int width = 1920;
        const int height = 1080;
        var target = new Vector3(640f, 360f, 0f);

        var camera = CreateCamera(width, height);
        camera.CentreOn(target);

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
