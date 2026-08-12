using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Covers the second collider family: the <see cref="ICollisionField"/> contract, the height grid
/// implementation of it, and the per-world slot carrying a field.
/// </summary>
public class CollisionFieldTests
{
    /// <summary>
    /// Minimal hand-written field: a single square of ground at a fixed height, used to exercise the
    /// contract itself rather than any implementation detail of the height grid.
    /// </summary>
    private sealed class SquareCollisionField : ICollisionField
    {
        private readonly float _halfSize;
        private readonly float _groundHeight;
        private readonly bool _isWalkable;
        private readonly string _surfaceTag;

        public SquareCollisionField(float halfSize, float groundHeight, bool isWalkable, string surfaceTag)
        {
            _halfSize = halfSize;
            _groundHeight = groundHeight;
            _isWalkable = isWalkable;
            _surfaceTag = surfaceTag;
        }

        public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
        {
            sample = default;

            if (MathF.Abs(worldPosition.X) > _halfSize || MathF.Abs(worldPosition.Z) > _halfSize)
            {
                return false;
            }

            var delta = worldPosition.Y - _groundHeight;

            if (delta < 0f || delta > maxDropDistance)
            {
                return false;
            }

            sample = new GroundSample(_groundHeight, Vector3.Up, _isWalkable, _surfaceTag);
            return true;
        }
    }

    [Fact]
    public void Field_InsideItsExtent_ReportsGroundAndPropagatesTheSurfaceTag()
    {
        ICollisionField field = new SquareCollisionField(5f, 2f, isWalkable: true, surfaceTag: "grass");

        var found = field.TrySampleGround(new Vector3(1f, 3f, -4f), 2f, out var sample);

        Assert.True(found);
        Assert.True(sample.HasGround);
        Assert.Equal(2f, sample.GroundHeight, precision: 4);
        Assert.Equal(Vector3.Up, sample.Normal);
        Assert.True(sample.IsWalkable);
        Assert.Equal("grass", sample.SurfaceTag);
    }

    [Fact]
    public void Field_OutsideItsExtent_ReportsNoGround()
    {
        ICollisionField field = new SquareCollisionField(5f, 2f, isWalkable: true, surfaceTag: "grass");

        var found = field.TrySampleGround(new Vector3(9f, 3f, 0f), 2f, out var sample);

        Assert.False(found);
        Assert.False(sample.HasGround);
    }

    [Fact]
    public void Field_OnNonWalkableGround_StillReportsThePresenceOfGround()
    {
        ICollisionField field = new SquareCollisionField(5f, 2f, isWalkable: false, surfaceTag: "lava");

        var found = field.TrySampleGround(new Vector3(0f, 2f, 0f), 1f, out var sample);

        Assert.True(found);
        Assert.True(sample.HasGround);
        Assert.False(sample.IsWalkable);
        Assert.Equal("lava", sample.SurfaceTag);
    }

    [Fact]
    public void DefaultGroundSample_HasNoGround()
    {
        var sample = default(GroundSample);

        Assert.False(sample.HasGround);
        Assert.Null(sample.SurfaceTag);
    }

    private static HeightGridCollisionField CreateGrid(Vector3 origin)
    {
        // 3 cells along X, 2 along Z, stored row by row along Z.
        var heights = new[]
        {
            0f, 1f, 2f,
            3f, 4f, 5f
        };

        var walkable = new[]
        {
            true, true, true,
            true, false, true
        };

        var surfaceTags = new[]
        {
            "a", "b", "c",
            "d", "e", "f"
        };

        return new HeightGridCollisionField(origin, 2f, 3, 2, heights, walkable, surfaceTags);
    }

    [Theory]
    [InlineData(0.5f, 0.5f, 0f, "a")]
    [InlineData(2.5f, 0.5f, 1f, "b")]
    [InlineData(5.9f, 3.9f, 5f, "f")]
    [InlineData(0f, 2f, 3f, "d")]
    public void HeightGrid_MapsAPositionToItsCell(float x, float z, float expectedHeight, string expectedTag)
    {
        var field = CreateGrid(Vector3.Zero);

        var found = field.TrySampleGround(new Vector3(x, expectedHeight + 0.25f, z), 1f, out var sample);

        Assert.True(found);
        Assert.Equal(expectedHeight, sample.GroundHeight, precision: 4);
        Assert.Equal(expectedTag, sample.SurfaceTag);
    }

    [Theory]
    [InlineData(-0.1f, 0.5f)]
    [InlineData(6f, 0.5f)]
    [InlineData(0.5f, -0.1f)]
    [InlineData(0.5f, 4f)]
    public void HeightGrid_OutsideItsBounds_ReportsNoGround(float x, float z)
    {
        var field = CreateGrid(Vector3.Zero);

        var found = field.TrySampleGround(new Vector3(x, 0f, z), 100f, out var sample);

        Assert.False(found);
        Assert.False(sample.HasGround);
    }

    [Fact]
    public void HeightGrid_MapsNegativeWorldCoordinates_ByFlooring()
    {
        var field = CreateGrid(new Vector3(-6f, 0f, -4f));

        // Cell (0, 0) spans x in [-6, -4) and z in [-4, -2).
        var found = field.TrySampleGround(new Vector3(-5.5f, 0.5f, -3.5f), 1f, out var sample);

        Assert.True(found);
        Assert.Equal(0f, sample.GroundHeight, precision: 4);
        Assert.Equal("a", sample.SurfaceTag);

        // Cell (2, 1) is the last one: x in [-2, 0), z in [-2, 0).
        found = field.TrySampleGround(new Vector3(-0.5f, 5.5f, -1.5f), 1f, out sample);

        Assert.True(found);
        Assert.Equal(5f, sample.GroundHeight, precision: 4);
        Assert.Equal("f", sample.SurfaceTag);

        // One cell further along X is outside the grid.
        found = field.TrySampleGround(new Vector3(0.5f, 5.5f, -1.5f), 1f, out sample);

        Assert.False(found);
    }

    [Fact]
    public void HeightGrid_AddsTheOriginHeightToTheCellHeight()
    {
        var field = CreateGrid(new Vector3(0f, 10f, 0f));

        var found = field.TrySampleGround(new Vector3(2.5f, 11f, 0.5f), 1f, out var sample);

        Assert.True(found);
        Assert.Equal(11f, sample.GroundHeight, precision: 4);
    }

    [Fact]
    public void HeightGrid_ExactlyOnTheSurface_IsOnGround()
    {
        var field = CreateGrid(Vector3.Zero);

        var found = field.TrySampleGround(new Vector3(2.5f, 1f, 0.5f), 0.5f, out var sample);

        Assert.True(found);
        Assert.Equal(1f, sample.GroundHeight, precision: 4);
    }

    [Fact]
    public void HeightGrid_AtExactlyTheMaxDropDistance_IsOnGround()
    {
        var field = CreateGrid(Vector3.Zero);

        var found = field.TrySampleGround(new Vector3(2.5f, 1.5f, 0.5f), 0.5f, out var sample);

        Assert.True(found);
        Assert.Equal(1f, sample.GroundHeight, precision: 4);
    }

    [Fact]
    public void HeightGrid_BeyondTheMaxDropDistance_ReportsNoGround()
    {
        var field = CreateGrid(Vector3.Zero);

        var found = field.TrySampleGround(new Vector3(2.5f, 1.75f, 0.5f), 0.5f, out var sample);

        Assert.False(found);
        Assert.False(sample.HasGround);
    }

    [Fact]
    public void HeightGrid_WithGroundStrictlyAboveTheSample_ReportsNoGround()
    {
        var field = CreateGrid(Vector3.Zero);

        var found = field.TrySampleGround(new Vector3(2.5f, 0.5f, 0.5f), 100f, out var sample);

        Assert.False(found);
        Assert.False(sample.HasGround);
    }

    [Fact]
    public void HeightGrid_OnANonWalkableCellInRange_ReportsGroundThatIsNotWalkable()
    {
        var field = CreateGrid(Vector3.Zero);

        // Cell (1, 1): height 4, flagged non walkable.
        var found = field.TrySampleGround(new Vector3(2.5f, 4.25f, 2.5f), 1f, out var sample);

        Assert.True(found);
        Assert.True(sample.HasGround);
        Assert.False(sample.IsWalkable);
        Assert.Equal("e", sample.SurfaceTag);
    }

    [Fact]
    public void HeightGrid_WithoutOptionalData_IsWalkableAndUntagged()
    {
        var field = new HeightGridCollisionField(Vector3.Zero, 1f, 2, 1, new[] { 0f, 0f });

        var found = field.TrySampleGround(new Vector3(1.5f, 0f, 0.5f), 1f, out var sample);

        Assert.True(found);
        Assert.True(sample.IsWalkable);
        Assert.Null(sample.SurfaceTag);
    }

    [Theory]
    [InlineData(0f, 2, 2)]
    [InlineData(-1f, 2, 2)]
    [InlineData(1f, 0, 2)]
    [InlineData(1f, 2, 0)]
    public void HeightGrid_RejectsInvalidDimensions(float cellSize, int width, int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HeightGridCollisionField(Vector3.Zero, cellSize, width, depth, new[] { 0f, 0f, 0f, 0f }));
    }

    [Fact]
    public void HeightGrid_RejectsDataOfTheWrongLength()
    {
        Assert.Throws<ArgumentException>(
            () => new HeightGridCollisionField(Vector3.Zero, 1f, 2, 2, new[] { 0f, 0f }));

        Assert.Throws<ArgumentException>(
            () => new HeightGridCollisionField(Vector3.Zero, 1f, 2, 1, new[] { 0f, 0f }, new[] { true }));

        Assert.Throws<ArgumentException>(
            () => new HeightGridCollisionField(Vector3.Zero, 1f, 2, 1, new[] { 0f, 0f }, null, new[] { "a" }));
    }

    [Fact]
    public void World_HasNoCollisionFieldByDefaultAndAcceptsOne()
    {
        var world = new World();

        Assert.Null(world.CollisionField);

        var field = new HeightGridCollisionField(Vector3.Zero, 1f, 1, 1, new[] { 0f });
        world.CollisionField = field;

        Assert.Same(field, world.CollisionField);
    }

    [Fact]
    public void World_ClearEntities_KeepsTheCollisionField()
    {
        var world = new World();
        var field = new HeightGridCollisionField(Vector3.Zero, 1f, 1, 1, new[] { 0f });
        world.CollisionField = field;

        world.ClearEntities();

        Assert.Same(field, world.CollisionField);
    }

    [Fact]
    public void World_Clear_DropsTheCollisionField()
    {
        var world = new World();
        world.CollisionField = new HeightGridCollisionField(Vector3.Zero, 1f, 1, 1, new[] { 0f });

        world.Clear();

        Assert.Null(world.CollisionField);
    }
}
