using CasaEngine.Core.Math.Geometry;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// A 3D camera whose FarPlane is +infinity (the editor cameras): its projection, frustum culling, picking ray and sky
/// direction stay exact, and the picking ray keeps its precision with the camera far from the origin.
/// </summary>
public class InfiniteFarPlaneTests
{
    private const float NearPlane = 1f;
    private const float FiniteFarPlane = 1000f;
    private static readonly Vector3 Target = new(100f, 20f, -50f);
    private static readonly Viewport DepthZeroToOneViewport = new(0, 0, 1920, 1080);

    // Camera at Distance from Target, yaw pi/4 and pitch -pi/6 like the editor camera, built without a world as in
    // OrbitCameraPlacementTests: FarPlane first, then NearPlane (clamped against it).
    private static PerspectiveCameraComponent CreateCamera(float distance, float farPlane)
    {
        var camera = new PerspectiveCameraComponent();
        camera.OnScreenResized(1920, 1080);
        camera.FarPlane = farPlane;
        camera.NearPlane = NearPlane;

        float pitch = -MathHelper.Pi / 6f;
        float yaw = MathHelper.PiOver4;
        var offset = new Vector3(MathF.Cos(pitch) * MathF.Sin(yaw), -MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));
        camera.SetPositionAndTarget(Target + offset * distance, Target);
        return camera;
    }

    private static Vector3 GetEye(Matrix view) => Matrix.Invert(view).Translation;

    private static Vector3 GetForward(Matrix view) => Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, Matrix.Invert(view)));

    public static TheoryData<float, float> DistancesAndFarPlanes => new()
    {
        { 10f, FiniteFarPlane },
        { 10f, float.PositiveInfinity },
        { 3000f, FiniteFarPlane },
        { 3000f, float.PositiveInfinity },
        { 100000f, FiniteFarPlane },
        { 100000f, float.PositiveInfinity },
    };

    [Fact]
    public void Projection_WithInfiniteFarPlane_IsTheInfinitePerspective()
    {
        var camera = CreateCamera(10f, float.PositiveInfinity);

        Matrix projection = camera.ProjectionMatrix;

        Assert.Equal(float.PositiveInfinity, camera.FarPlane);
        Assert.Equal(float.PositiveInfinity, camera.ViewDistance);
        Assert.Equal(-1f, projection.M33);
        Assert.Equal(-1f, projection.M34);
        Assert.Equal(-NearPlane, projection.M43);
        Assert.Equal(0f, projection.M44);
        Assert.True(float.IsFinite(projection.M11) && float.IsFinite(projection.M22));
    }

    [Fact]
    public void FrustumCulling_WithInfiniteFarPlane_KeepsDistantBoxesAndRejectsBoxesBehind()
    {
        var infinite = CreateCamera(10f, float.PositiveInfinity);
        var finite = CreateCamera(10f, FiniteFarPlane);
        Matrix view = infinite.ViewMatrix;
        Vector3 eye = GetEye(view);
        Vector3 forward = GetForward(view);
        var extent = new Vector3(5f);
        var ahead = new BoundingBox(eye + forward * 5000f - extent, eye + forward * 5000f + extent);
        var behind = new BoundingBox(eye - forward * 50f - extent, eye - forward * 50f + extent);

        var infiniteFrustum = new BoundingFrustum(view * infinite.ProjectionMatrix);
        var finiteFrustum = new BoundingFrustum(view * finite.ProjectionMatrix);

        Assert.NotEqual(ContainmentType.Disjoint, infiniteFrustum.Contains(ahead));
        Assert.Equal(ContainmentType.Disjoint, finiteFrustum.Contains(ahead));
        Assert.Equal(ContainmentType.Disjoint, infiniteFrustum.Contains(behind));
        Assert.Equal(ContainmentType.Disjoint, finiteFrustum.Contains(behind));
    }

    [Theory]
    [MemberData(nameof(DistancesAndFarPlanes))]
    public void PickingRay_AtTheViewportCentre_FollowsTheCameraAxis(float distance, float farPlane)
    {
        var camera = CreateCamera(distance, farPlane);
        Matrix view = camera.ViewMatrix;

        Ray ray = RayHelper.CalculateRayFromScreenCoordinate(new Vector2(960f, 540f), camera.ProjectionMatrix, view, camera.Viewport);

        Assert.True(float.IsFinite(ray.Direction.X) && float.IsFinite(ray.Direction.Y) && float.IsFinite(ray.Direction.Z));
        Assert.True(Vector3.Distance(ray.Direction, GetForward(view)) < 1e-4f, $"direction {ray.Direction}, axis {GetForward(view)}");
        Assert.True(Vector3.Distance(ray.Position, GetEye(view)) < 1.5f * NearPlane + distance * 1e-6f, "origin on the near plane");
    }

    [Theory]
    [MemberData(nameof(DistancesAndFarPlanes))]
    public void PickingRay_ThroughTheProjectedPixelOfAPoint_PassesThroughThatPoint(float distance, float farPlane)
    {
        var camera = CreateCamera(distance, farPlane);
        Matrix view = camera.ViewMatrix;
        Matrix projection = camera.ProjectionMatrix;
        Vector3 point = Target + new Vector3(30f, 5f, 10f) * MathF.Max(1f, distance / 100f);
        // The pixel of the point; a 0..1 depth viewport, since x and y do not depend on the depth range.
        Vector3 pixel = DepthZeroToOneViewport.Project(point, projection, view, Matrix.Identity);

        Ray ray = RayHelper.CalculateRayFromScreenCoordinate(new Vector2(pixel.X, pixel.Y), projection, view, camera.Viewport);

        float pointDistance = Vector3.Distance(GetEye(view), point);
        float along = Vector3.Dot(point - ray.Position, ray.Direction);
        float miss = Vector3.Distance(ray.Position + ray.Direction * along, point);
        Assert.True(miss < 1e-3f * pointDistance, $"miss {miss} at {pointDistance}");
    }

    [Fact]
    public void PickingRay_OrthographicCamera_FollowsTheAxisFromThePixel()
    {
        var camera = new Camera2dComponent();
        camera.OnScreenResized(1920, 1080);
        camera.FarPlane = FiniteFarPlane;
        camera.NearPlane = NearPlane;
        Matrix view = camera.ViewMatrix;
        Matrix projection = camera.ProjectionMatrix;

        Ray first = RayHelper.CalculateRayFromScreenCoordinate(new Vector2(100f, 200f), projection, view, camera.Viewport);
        Ray second = RayHelper.CalculateRayFromScreenCoordinate(new Vector2(1800f, 900f), projection, view, camera.Viewport);

        Vector3 forward = GetForward(view);
        Assert.True(Vector3.Distance(first.Direction, forward) < 1e-5f);
        Assert.True(Vector3.Distance(second.Direction, forward) < 1e-5f);
        // One pixel is 1 / Zoom world unit across the orthographic view.
        float expected = new Vector2(1700f, 700f).Length() / camera.Zoom;
        Assert.Equal(expected, Vector3.Distance(first.Position, second.Position), 1e-1f);
    }

    [Theory]
    [InlineData(FiniteFarPlane)]
    [InlineData(float.PositiveInfinity)]
    public void SkyDirection_FromTheFarClipPlane_IsFiniteAndMatchesTheFiniteFormula(float farPlane)
    {
        // SkyCubemap.fx: normalize(worldPosition.xyz - EyePosition * worldPosition.w), worldPosition being clip depth 1
        // through the inverse view projection.
        var camera = CreateCamera(3000f, farPlane);
        Matrix view = camera.ViewMatrix;
        Matrix inverseViewProjection = Matrix.Invert(view * camera.ProjectionMatrix);
        Vector3 eye = GetEye(view);
        Vector2[] clipPoints = [Vector2.Zero, new(-1f, -1f), new(1f, -1f), new(-1f, 1f), new(1f, 1f)];

        foreach (Vector2 clip in clipPoints)
        {
            Vector4 world = Vector4.Transform(new Vector4(clip.X, clip.Y, 1f, 1f), inverseViewProjection);
            Vector3 direction = Vector3.Normalize(new Vector3(world.X, world.Y, world.Z) - eye * world.W);

            Assert.True(float.IsFinite(direction.X) && float.IsFinite(direction.Y) && float.IsFinite(direction.Z), $"clip {clip}");
            if (float.IsFinite(farPlane))
            {
                Vector3 previous = Vector3.Normalize(new Vector3(world.X, world.Y, world.Z) / world.W - eye);
                Assert.True(Vector3.Distance(direction, previous) < 1e-4f, $"clip {clip}: {direction} / {previous}");
            }
        }

        // The float inverse of the view projection limits the precision as much as before the change: with the camera
        // 3000 units from the origin, the centre direction is about 1e-4 from the axis, with either formula.
        Vector4 centre = Vector4.Transform(new Vector4(0f, 0f, 1f, 1f), inverseViewProjection);
        Vector3 centreDirection = Vector3.Normalize(new Vector3(centre.X, centre.Y, centre.Z) - eye * centre.W);
        Assert.True(Vector3.Distance(centreDirection, GetForward(view)) < 1e-3f);
    }
}
