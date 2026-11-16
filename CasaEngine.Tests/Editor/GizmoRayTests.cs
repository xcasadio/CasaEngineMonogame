using CasaEngine.Framework.Scene.Entities.Components;
using GizmoTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The gizmo ray with the viewport the editor gives the gizmo (depth range 0..1) and an editor camera without a far
/// plane: finite, along the camera axis, and through the point of its pixel, near and far from the origin.
/// </summary>
public sealed class GizmoRayTests
{
    private static readonly Vector3 Target = new(100f, 20f, -50f);
    private static readonly Viewport GizmoViewport = new(0, 0, 1920, 1080);

    public static TheoryData<float, float> DistancesAndFarPlanes => new()
    {
        { 10f, 1000f },
        { 10f, float.PositiveInfinity },
        { 3000f, float.PositiveInfinity },
        { 100000f, 1000f },
        { 100000f, float.PositiveInfinity },
    };

    private static ArcBallCameraComponent CreateCamera(float distance, float farPlane)
    {
        var camera = new ArcBallCameraComponent();
        camera.OnScreenResized(1920, 1080);
        camera.FarPlane = farPlane;
        camera.NearPlane = 1f;

        float pitch = -MathHelper.Pi / 6f;
        float yaw = MathHelper.PiOver4;
        var offset = new Vector3(MathF.Cos(pitch) * MathF.Sin(yaw), -MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));
        camera.SetCamera(Target + offset * distance, Target, Vector3.Up);
        return camera;
    }

    [Theory]
    [MemberData(nameof(DistancesAndFarPlanes))]
    public void FromScreen_AtTheViewportCentre_FollowsTheCameraAxis(float distance, float farPlane)
    {
        var camera = CreateCamera(distance, farPlane);
        Matrix view = camera.ViewMatrix;
        Vector3 forward = Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, Matrix.Invert(view)));

        Ray ray = GizmoRay.FromScreen(new Vector2(960f, 540f), GizmoViewport, view, camera.ProjectionMatrix);

        Assert.True(float.IsFinite(ray.Direction.X) && float.IsFinite(ray.Direction.Y) && float.IsFinite(ray.Direction.Z));
        Assert.True(Vector3.Distance(ray.Direction, forward) < 1e-4f, $"direction {ray.Direction}, axis {forward}");
    }

    [Theory]
    [MemberData(nameof(DistancesAndFarPlanes))]
    public void FromScreen_ThroughTheProjectedPixelOfAPoint_PassesThroughThatPoint(float distance, float farPlane)
    {
        var camera = CreateCamera(distance, farPlane);
        Matrix view = camera.ViewMatrix;
        Matrix projection = camera.ProjectionMatrix;
        Vector3 point = Target + new Vector3(30f, 5f, 10f) * MathF.Max(1f, distance / 100f);
        Vector3 pixel = GizmoViewport.Project(point, projection, view, Matrix.Identity);

        Ray ray = GizmoRay.FromScreen(new Vector2(pixel.X, pixel.Y), GizmoViewport, view, projection);

        float pointDistance = Vector3.Distance(Matrix.Invert(view).Translation, point);
        float along = Vector3.Dot(point - ray.Position, ray.Direction);
        float miss = Vector3.Distance(ray.Position + ray.Direction * along, point);
        Assert.True(miss < 1e-3f * pointDistance, $"miss {miss} at {pointDistance}");
    }
}
