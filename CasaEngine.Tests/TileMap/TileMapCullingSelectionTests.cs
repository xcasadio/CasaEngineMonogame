using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// Covers the draw/culling path selection of <see cref="TileMapComponent"/>: an axis-aligned world
/// matrix keeps the plane based visible tile range, a rotated one falls back to per chunk frustum culling.
/// A full <see cref="TileMapComponent"/> cannot be instantiated without a GraphicsDevice and a loaded
/// world, so the selection predicate and the rotated chunk bounds are tested directly.
/// </summary>
public class TileMapCullingSelectionTests
{
    [Fact]
    public void IsAxisAlignedWorldMatrix_AcceptsScaleAndTranslationOnly()
    {
        Assert.True(TileMapComponent.IsAxisAlignedWorldMatrix(Matrix.Identity));
        Assert.True(TileMapComponent.IsAxisAlignedWorldMatrix(
            Matrix.CreateScale(2f, 3f, 1f) * Matrix.CreateTranslation(120f, -40f, 5f)));
    }

    [Fact]
    public void IsAxisAlignedWorldMatrix_RejectsRotations()
    {
        Assert.False(TileMapComponent.IsAxisAlignedWorldMatrix(Matrix.CreateRotationX(-MathHelper.PiOver2)));
        Assert.False(TileMapComponent.IsAxisAlignedWorldMatrix(Matrix.CreateRotationZ(MathHelper.PiOver4)));
        Assert.False(TileMapComponent.IsAxisAlignedWorldMatrix(
            Matrix.CreateRotationY(MathHelper.Pi) * Matrix.CreateTranslation(10f, 0f, 0f)));
    }

    [Fact]
    public void IsAxisAlignedWorldMatrix_RejectsSmallRotationsOnSmallScaleMaps()
    {
        // A 3 degrees rotation on a map scaled down to 0.001 leaves off-diagonal terms around 5e-5:
        // an absolute epsilon would accept it and the rotation would be dropped from the render.
        var world = Matrix.CreateRotationZ(MathHelper.ToRadians(3f)) * Matrix.CreateScale(0.001f);

        Assert.False(TileMapComponent.IsAxisAlignedWorldMatrix(world));
    }

    [Fact]
    public void IsAxisAlignedWorldMatrix_ToleratesFloatingPointNoise()
    {
        var world = Matrix.CreateScale(4f);
        world.M12 = 1e-6f;
        world.M31 = -1e-6f;

        Assert.True(TileMapComponent.IsAxisAlignedWorldMatrix(world));
    }

    [Fact]
    public void UpdateWorldBounds_WithWorldMatrix_MatchesAxisAlignedOverloadWhenIdentity()
    {
        var chunk = new TileMapChunk(0, new Point(1, 0), new Rectangle(2, 0, 2, 3));
        var world = Matrix.CreateTranslation(10f, 20f, 0f);

        chunk.UpdateWorldBounds(0f, 0f, 16f, 8f, 0.5f, 0.5f, in world);
        var transformedBounds = chunk.WorldBounds;

        chunk.UpdateWorldBounds(10f, 20f, 16f, 8f, 0.5f, 0.5f);

        Assert.Equal(chunk.WorldBounds.Min.X, transformedBounds.Min.X, 3);
        Assert.Equal(chunk.WorldBounds.Min.Y, transformedBounds.Min.Y, 3);
        Assert.Equal(chunk.WorldBounds.Max.X, transformedBounds.Max.X, 3);
        Assert.Equal(chunk.WorldBounds.Max.Y, transformedBounds.Max.Y, 3);
    }

    [Fact]
    public void UpdateWorldBounds_WithWorldMatrix_ProducesVerticalBoundsForGroundRotation()
    {
        var chunk = new TileMapChunk(0, Point.Zero, new Rectangle(0, 0, 2, 2));
        var world = Matrix.CreateRotationX(-MathHelper.PiOver2);

        chunk.UpdateWorldBounds(0f, 0f, 16f, 16f, 0f, 0f, in world);

        // A -90 degrees rotation around X maps the local -Y map axis onto the world +Z axis:
        // the chunk becomes a flat ground quad, its world Y extent collapses to zero.
        Assert.Equal(0f, chunk.WorldBounds.Min.X, 3);
        Assert.Equal(32f, chunk.WorldBounds.Max.X, 3);
        Assert.Equal(0f, chunk.WorldBounds.Min.Y, 3);
        Assert.Equal(0f, chunk.WorldBounds.Max.Y, 3);
        Assert.Equal(0f, chunk.WorldBounds.Min.Z, 3);
        Assert.Equal(32f, chunk.WorldBounds.Max.Z, 3);
    }

    [Fact]
    public void RotatedChunkBounds_AreCulledByViewFrustum()
    {
        var view = Matrix.CreateLookAt(new Vector3(0f, 40f, 0f), Vector3.Zero, Vector3.Forward);
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 1f, 1f, 200f);
        var frustum = new BoundingFrustum(view * projection);
        var world = Matrix.CreateRotationX(-MathHelper.PiOver2);

        var visibleChunk = new TileMapChunk(0, Point.Zero, new Rectangle(0, 0, 2, 2));
        visibleChunk.UpdateWorldBounds(-16f, 16f, 16f, 16f, 0f, 0f, in world);
        Assert.True(frustum.Intersects(visibleChunk.WorldBounds));

        var farChunk = new TileMapChunk(0, new Point(40, 0), new Rectangle(40, 0, 2, 2));
        farChunk.UpdateWorldBounds(0f, 0f, 16f, 16f, 0f, 0f, in world);
        Assert.False(frustum.Intersects(farChunk.WorldBounds));
    }
}
