using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// Covers the world view bounds used by the axis-aligned draw path of <see cref="TileMapComponent"/>
/// to compute the visible tile range.
///
/// The bounds used to be built from the four viewport corner rays intersected with the tile map plane.
/// That quad only contains the visible part of the plane when the camera looks down at it: as soon as
/// the camera pitches towards the horizon the corner rays either miss the plane in front of the camera
/// or hit it arbitrarily far away, and the far half of the map ends up culled while it is on screen.
/// </summary>
public class TileMapVisibleTileRangeTests
{
    private const float PlaneZ = 0f;
    private static readonly Vector3 MapCenter = new(600f, -480f, PlaneZ);

    [Fact]
    public void FrustumSlabBounds_TopDownCamera_CoverEveryVisiblePlanePoint()
    {
        var (view, projection) = CreateCamera(pitchDegrees: 89f, distance: 900f);

        AssertBoundsCoverVisiblePlane(view, projection);
    }

    [Fact]
    public void FrustumSlabBounds_CameraPitchedBelowCornerElevation_CoverEveryVisiblePlanePoint()
    {
        // 12 degrees is below the corner ray elevation of a 45 degrees / 16:9 frustum (~18.4 degrees):
        // every corner ray points above the horizon and the old plane intersection ran backwards.
        var (view, projection) = CreateCamera(pitchDegrees: 12f, distance: 800f);

        AssertBoundsCoverVisiblePlane(view, projection);
    }

    [Fact]
    public void FrustumSlabBounds_CameraPitchedBelowTopEdgeElevation_CoverEveryVisiblePlanePoint()
    {
        // 20 degrees is above the corner ray elevation but below the top edge elevation (22.5 degrees):
        // the four corner rays still hit the plane, yet the visible region reaches past their quad.
        var (view, projection) = CreateCamera(pitchDegrees: 20f, distance: 800f);

        AssertBoundsCoverVisiblePlane(view, projection);
    }

    [Fact]
    public void CornerRayBounds_CameraPitchedTowardsHorizon_MissVisiblePlanePoints()
    {
        // Regression witness: the previous implementation is reproduced here and must fail where the
        // frustum slab bounds succeed, otherwise the tests above would prove nothing.
        var (view, projection) = CreateCamera(pitchDegrees: 12f, distance: 800f);
        var viewProjection = view * projection;

        Assert.True(TryGetCornerRayBounds(view, projection, out var minX, out var maxX, out var minY, out var maxY));

        var missedVisiblePoint = false;

        foreach (var point in EnumeratePlaneSamples())
        {
            if (!IsVisible(point, viewProjection))
            {
                continue;
            }

            if (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY)
            {
                missedVisiblePoint = true;
                break;
            }
        }

        Assert.True(missedVisiblePoint);
    }

    [Fact]
    public void FrustumSlabBounds_PlaneBehindTheCamera_ReportNoIntersection()
    {
        // Camera below the tile map plane looking away from it: the slab is outside the frustum.
        var view = Matrix.CreateLookAt(new Vector3(0f, 0f, -500f), new Vector3(0f, 0f, -1500f), Vector3.Up);
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 16f / 9f, 1f, 3000f);
        var corners = GetFrustumCorners(view * projection);

        Assert.False(TileMapComponent.TryGetFrustumSlabBounds(corners, PlaneZ, PlaneZ, out _, out _, out _, out _));
    }

    [Fact]
    public void FrustumSlabBounds_SlabIsUsedWhenLayersAreOffsetInZ()
    {
        var (view, projection) = CreateCamera(pitchDegrees: 45f, distance: 900f);
        var corners = GetFrustumCorners(view * projection);

        Assert.True(TileMapComponent.TryGetFrustumSlabBounds(corners, 0f, 0f, out var planeMinX, out var planeMaxX, out var planeMinY, out var planeMaxY));
        Assert.True(TileMapComponent.TryGetFrustumSlabBounds(corners, 0f, 32f, out var slabMinX, out var slabMaxX, out var slabMinY, out var slabMaxY));

        // A thicker slab can only widen the covered extent, never shrink it.
        Assert.True(slabMinX <= planeMinX);
        Assert.True(slabMaxX >= planeMaxX);
        Assert.True(slabMinY <= planeMinY);
        Assert.True(slabMaxY >= planeMaxY);
    }

    [Fact]
    public void FrustumSlabBounds_InvertedSlabBoundsAreAccepted()
    {
        var (view, projection) = CreateCamera(pitchDegrees: 45f, distance: 900f);
        var corners = GetFrustumCorners(view * projection);

        Assert.True(TileMapComponent.TryGetFrustumSlabBounds(corners, 0f, 32f, out var minX, out var maxX, out var minY, out var maxY));
        Assert.True(TileMapComponent.TryGetFrustumSlabBounds(corners, 32f, 0f, out var invertedMinX, out var invertedMaxX, out var invertedMinY, out var invertedMaxY));

        Assert.Equal(minX, invertedMinX, 3);
        Assert.Equal(maxX, invertedMaxX, 3);
        Assert.Equal(minY, invertedMinY, 3);
        Assert.Equal(maxY, invertedMaxY, 3);
    }

    private static void AssertBoundsCoverVisiblePlane(Matrix view, Matrix projection)
    {
        var viewProjection = view * projection;
        var corners = GetFrustumCorners(viewProjection);

        Assert.True(TileMapComponent.TryGetFrustumSlabBounds(corners, PlaneZ, PlaneZ, out var minX, out var maxX, out var minY, out var maxY));

        var visibleSampleCount = 0;

        foreach (var point in EnumeratePlaneSamples())
        {
            if (!IsVisible(point, viewProjection))
            {
                continue;
            }

            visibleSampleCount++;

            Assert.InRange(point.X, minX, maxX);
            Assert.InRange(point.Y, minY, maxY);
        }

        Assert.True(visibleSampleCount > 0);
    }

    private static (Matrix View, Matrix Projection) CreateCamera(float pitchDegrees, float distance)
    {
        var pitch = MathHelper.ToRadians(pitchDegrees);
        var position = MapCenter + new Vector3(
            0f,
            -distance * MathF.Cos(pitch),
            distance * MathF.Sin(pitch));

        var view = Matrix.CreateLookAt(position, MapCenter, Vector3.UnitZ);
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 16f / 9f, 1f, 5000f);
        return (view, projection);
    }

    private static Vector3[] GetFrustumCorners(Matrix viewProjection)
    {
        var corners = new Vector3[BoundingFrustum.CornerCount];
        new BoundingFrustum(viewProjection).GetCorners(corners);
        return corners;
    }

    private static IEnumerable<Vector3> EnumeratePlaneSamples()
    {
        for (var x = -6000f; x <= 6000f; x += 60f)
        {
            for (var y = -6000f; y <= 6000f; y += 60f)
            {
                yield return new Vector3(x, y, PlaneZ);
            }
        }
    }

    private static bool IsVisible(Vector3 worldPoint, Matrix viewProjection)
    {
        var clip = Vector4.Transform(new Vector4(worldPoint, 1f), viewProjection);

        return clip.W > 0f
            && clip.X >= -clip.W && clip.X <= clip.W
            && clip.Y >= -clip.W && clip.Y <= clip.W
            && clip.Z >= 0f && clip.Z <= clip.W;
    }

    /// <summary>
    /// Previous implementation: the four viewport corner rays intersected with the tile map plane.
    /// </summary>
    private static bool TryGetCornerRayBounds(Matrix view, Matrix projection, out float minX, out float maxX, out float minY, out float maxY)
    {
        var viewport = new Viewport(0, 0, 1920, 1080) { MinDepth = 0f, MaxDepth = 1f };
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;
        maxY = float.MinValue;

        return IncludeViewportCorner(viewport, view, projection, viewport.X, viewport.Y, ref minX, ref maxX, ref minY, ref maxY)
            && IncludeViewportCorner(viewport, view, projection, viewport.X + viewport.Width, viewport.Y, ref minX, ref maxX, ref minY, ref maxY)
            && IncludeViewportCorner(viewport, view, projection, viewport.X, viewport.Y + viewport.Height, ref minX, ref maxX, ref minY, ref maxY)
            && IncludeViewportCorner(viewport, view, projection, viewport.X + viewport.Width, viewport.Y + viewport.Height, ref minX, ref maxX, ref minY, ref maxY);
    }

    private static bool IncludeViewportCorner(
        Viewport viewport,
        Matrix view,
        Matrix projection,
        float screenX,
        float screenY,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY)
    {
        var nearPoint = viewport.Unproject(new Vector3(screenX, screenY, 0f), projection, view, Matrix.Identity);
        var farPoint = viewport.Unproject(new Vector3(screenX, screenY, 1f), projection, view, Matrix.Identity);
        var direction = farPoint - nearPoint;

        if (Math.Abs(direction.Z) < 0.0001f)
        {
            return false;
        }

        var distance = (PlaneZ - nearPoint.Z) / direction.Z;
        var worldPoint = nearPoint + direction * distance;

        minX = Math.Min(minX, worldPoint.X);
        maxX = Math.Max(maxX, worldPoint.X);
        minY = Math.Min(minY, worldPoint.Y);
        maxY = Math.Max(maxY, worldPoint.Y);
        return true;
    }
}
