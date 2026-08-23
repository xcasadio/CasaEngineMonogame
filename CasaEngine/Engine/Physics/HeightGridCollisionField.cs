using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

/// <summary>
/// <see cref="ICollisionField"/> backed by a regular grid of cells over the world XZ plane, each
/// cell carrying a ground height, a walkability flag and an optional surface tag.
/// </summary>
/// <remarks>
/// All the data is supplied by the caller: the engine derives nothing from tile assets, which carry
/// no per-cell ground height. Cells are stored row by row along the second horizontal axis (in
/// X-then-Y-then-Z order, skipping <see cref="Up"/>), so the cell at horizontal indices (a, b) lives
/// at index b * <see cref="Width"/> + a. For the default <c>up = Vector3.Up</c> the horizontal axes
/// are X and Z, matching the previous fixed-axis behaviour exactly (a = X, b = Z). Heights are
/// relative to the origin's coordinate along <see cref="Up"/>, so the ground coordinate reported by
/// a sample is that origin coordinate + height of the cell.
/// Extension points deliberately left out of this version: per-cell slope normals (every sample
/// reports <see cref="Up"/> as-is) and an upward tolerance in the ground interval (ground strictly
/// above the sample position is never reported).
/// </remarks>
public sealed class HeightGridCollisionField : ICollisionField
{
    private readonly float[] _heights;
    private readonly bool[] _walkable;
    private readonly string[] _surfaceTags;
    private readonly int _upAxis;
    private readonly int _horizontalAxisA;
    private readonly int _horizontalAxisB;

    /// <param name="origin">World position of the lower corner of the cell (0, 0); its coordinate along <paramref name="up"/> is the height base.</param>
    /// <param name="cellSize">Size of a cell on both horizontal axes; must be strictly positive.</param>
    /// <param name="width">Number of cells along the first horizontal axis; must be strictly positive.</param>
    /// <param name="depth">Number of cells along the second horizontal axis; must be strictly positive.</param>
    /// <param name="heights">Height of every cell, relative to the origin's coordinate along <paramref name="up"/>; width * depth entries.</param>
    /// <param name="walkable">Walkability of every cell, or null when every cell is walkable.</param>
    /// <param name="surfaceTags">Surface tag of every cell, or null when the grid carries no tag.</param>
    /// <param name="up">
    /// Elevation axis of the grid, matching the world's simulation space policy. Must be exactly one
    /// of <c>±Vector3.UnitX</c>, <c>±Vector3.UnitY</c> or <c>±Vector3.UnitZ</c>, or null to keep the
    /// field's original fixed-axis behaviour: <see cref="Vector3.Up"/> (Y up), horizontal axes X and Z.
    /// </param>
    public HeightGridCollisionField(
        Vector3 origin,
        float cellSize,
        int width,
        int depth,
        float[] heights,
        bool[] walkable = null,
        string[] surfaceTags = null,
        Vector3? up = null)
    {
        ArgumentNullException.ThrowIfNull(heights);

        var upValue = up ?? Vector3.Up;
        _upAxis = AxisIndexOf(upValue);

        if (cellSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "The cell size of a height grid must be strictly positive.");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The width of a height grid must be strictly positive.");
        }

        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The depth of a height grid must be strictly positive.");
        }

        var cellCount = width * depth;

        if (heights.Length != cellCount)
        {
            throw new ArgumentException($"A height grid of {width}x{depth} needs {cellCount} heights, got {heights.Length}.", nameof(heights));
        }

        if (walkable != null && walkable.Length != cellCount)
        {
            throw new ArgumentException($"A height grid of {width}x{depth} needs {cellCount} walkability flags, got {walkable.Length}.", nameof(walkable));
        }

        if (surfaceTags != null && surfaceTags.Length != cellCount)
        {
            throw new ArgumentException($"A height grid of {width}x{depth} needs {cellCount} surface tags, got {surfaceTags.Length}.", nameof(surfaceTags));
        }

        Origin = origin;
        CellSize = cellSize;
        Width = width;
        Depth = depth;
        Up = upValue;
        _heights = heights;
        _walkable = walkable;
        _surfaceTags = surfaceTags;

        // The two horizontal axes are the remaining ones in X -> Y -> Z order.
        Span<int> horizontal = stackalloc int[2];
        var count = 0;
        for (var axis = 0; axis < 3; axis++)
        {
            if (axis != _upAxis)
            {
                horizontal[count++] = axis;
            }
        }

        _horizontalAxisA = horizontal[0];
        _horizontalAxisB = horizontal[1];
    }

    /// <summary>World position of the lower corner of the cell (0, 0).</summary>
    public Vector3 Origin { get; }

    /// <summary>Size of a cell on both horizontal axes.</summary>
    public float CellSize { get; }

    /// <summary>Number of cells along the first horizontal axis.</summary>
    public int Width { get; }

    /// <summary>Number of cells along the second horizontal axis.</summary>
    public int Depth { get; }

    /// <summary>Elevation axis of the grid, matching the world's simulation space policy.</summary>
    public Vector3 Up { get; }

    /// <summary>
    /// Samples the cell containing <paramref name="worldPosition"/>. A position outside the grid has
    /// no ground. Inside it, with delta = (worldPosition's coordinate along <see cref="Up"/>) - ground
    /// height of the cell, ground is reported when 0 &lt;= delta &lt;= <paramref name="maxDropDistance"/>:
    /// a position sitting exactly on the surface is on ground, a position below it is not.
    /// </summary>
    public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
    {
        sample = default;

        var cellA = (int)MathF.Floor((Component(worldPosition, _horizontalAxisA) - Component(Origin, _horizontalAxisA)) / CellSize);

        if (cellA < 0 || cellA >= Width)
        {
            return false;
        }

        var cellB = (int)MathF.Floor((Component(worldPosition, _horizontalAxisB) - Component(Origin, _horizontalAxisB)) / CellSize);

        if (cellB < 0 || cellB >= Depth)
        {
            return false;
        }

        var cellIndex = cellB * Width + cellA;
        var groundHeight = Component(Origin, _upAxis) + _heights[cellIndex];
        var delta = Component(worldPosition, _upAxis) - groundHeight;

        if (delta < 0f || delta > maxDropDistance)
        {
            return false;
        }

        var isWalkable = _walkable == null || _walkable[cellIndex];
        var surfaceTag = _surfaceTags?[cellIndex];

        sample = new GroundSample(groundHeight, Up, isWalkable, surfaceTag);
        return true;
    }

    private static float Component(in Vector3 v, int axis) => axis switch
    {
        0 => v.X,
        1 => v.Y,
        _ => v.Z,
    };

    private static int AxisIndexOf(Vector3 up)
    {
        if (up == Vector3.UnitX || up == -Vector3.UnitX)
        {
            return 0;
        }

        if (up == Vector3.UnitY || up == -Vector3.UnitY)
        {
            return 1;
        }

        if (up == Vector3.UnitZ || up == -Vector3.UnitZ)
        {
            return 2;
        }

        throw new ArgumentException($"The up axis of a height grid must be exactly one of +/-UnitX, +/-UnitY or +/-UnitZ, got {up}.", nameof(up));
    }
}
