using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

/// <summary>
/// <see cref="ICollisionField"/> backed by a regular grid of cells over the world XZ plane, each
/// cell carrying a ground height, a walkability flag and an optional surface tag.
/// </summary>
/// <remarks>
/// All the data is supplied by the caller: the engine derives nothing from tile assets, which carry
/// no per-cell ground height. Cells are stored row by row along Z, so the cell (x, z) lives at
/// index z * <see cref="Width"/> + x. Heights are relative to <see cref="Origin"/>.Y, so the world
/// Y reported by a sample is Origin.Y + height of the cell.
/// Extension points deliberately left out of this version: per-cell slope normals (every sample
/// reports <see cref="Vector3.Up"/>) and an upward tolerance in the ground interval (ground strictly
/// above the sample position is never reported).
/// </remarks>
public sealed class HeightGridCollisionField : ICollisionField
{
    private readonly float[] _heights;
    private readonly bool[] _walkable;
    private readonly string[] _surfaceTags;

    /// <param name="origin">World position of the lower corner of the cell (0, 0); its Y is the height base.</param>
    /// <param name="cellSize">Size of a cell on both horizontal axes; must be strictly positive.</param>
    /// <param name="width">Number of cells along X; must be strictly positive.</param>
    /// <param name="depth">Number of cells along Z; must be strictly positive.</param>
    /// <param name="heights">Height of every cell, relative to <paramref name="origin"/>.Y; width * depth entries.</param>
    /// <param name="walkable">Walkability of every cell, or null when every cell is walkable.</param>
    /// <param name="surfaceTags">Surface tag of every cell, or null when the grid carries no tag.</param>
    public HeightGridCollisionField(
        Vector3 origin,
        float cellSize,
        int width,
        int depth,
        float[] heights,
        bool[] walkable = null,
        string[] surfaceTags = null)
    {
        ArgumentNullException.ThrowIfNull(heights);

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
        _heights = heights;
        _walkable = walkable;
        _surfaceTags = surfaceTags;
    }

    /// <summary>World position of the lower corner of the cell (0, 0).</summary>
    public Vector3 Origin { get; }

    /// <summary>Size of a cell on both horizontal axes.</summary>
    public float CellSize { get; }

    /// <summary>Number of cells along X.</summary>
    public int Width { get; }

    /// <summary>Number of cells along Z.</summary>
    public int Depth { get; }

    /// <summary>
    /// Samples the cell containing <paramref name="worldPosition"/>. A position outside the grid has
    /// no ground. Inside it, with delta = worldPosition.Y - ground height of the cell, ground is
    /// reported when 0 &lt;= delta &lt;= <paramref name="maxDropDistance"/>: a position sitting
    /// exactly on the surface is on ground, a position below it is not.
    /// </summary>
    public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
    {
        sample = default;

        var cellX = (int)MathF.Floor((worldPosition.X - Origin.X) / CellSize);

        if (cellX < 0 || cellX >= Width)
        {
            return false;
        }

        var cellZ = (int)MathF.Floor((worldPosition.Z - Origin.Z) / CellSize);

        if (cellZ < 0 || cellZ >= Depth)
        {
            return false;
        }

        var cellIndex = cellZ * Width + cellX;
        var groundHeight = Origin.Y + _heights[cellIndex];
        var delta = worldPosition.Y - groundHeight;

        if (delta < 0f || delta > maxDropDistance)
        {
            return false;
        }

        var isWalkable = _walkable == null || _walkable[cellIndex];
        var surfaceTag = _surfaceTags?[cellIndex];

        sample = new GroundSample(groundHeight, Vector3.Up, isWalkable, surfaceTag);
        return true;
    }
}
