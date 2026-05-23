using CasaEngine.Framework.Application.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class NavigationDebugDraw2D
{
    private readonly Renderer2DComponent _renderer;

    public NavigationDebugDraw2D(Renderer2DComponent renderer, int maxPrimitiveCount = 2048)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        Budget = new NavigationDebugDrawBudget(maxPrimitiveCount);
    }

    public NavigationDebugDrawBudget Budget { get; }

    public void ResetFrameBudget()
    {
        Budget.Reset();
    }

    public int DrawGrid(NavigationGrid2D grid, Rectangle visibleCells, NavigationQuery query, Color walkableColor, Color blockedColor, float zOrder = 0f)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(query);

        Rectangle clippedCells = ClipVisibleCells(grid, visibleCells);
        int drawnPrimitiveCount = 0;
        for (int y = clippedCells.Top; y < clippedCells.Bottom; y++)
        {
            for (int x = clippedCells.Left; x < clippedCells.Right; x++)
            {
                if (!Budget.TryConsume())
                {
                    return drawnPrimitiveCount;
                }

                NavigationGridCell cell = grid.GetCell(x, y);
                Color color = query.CanEnter(cell) ? walkableColor : blockedColor;
                _renderer.DrawRectangle(
                    grid.Origin.X + x * grid.CellSize,
                    grid.Origin.Z + y * grid.CellSize,
                    grid.CellSize,
                    grid.CellSize,
                    color,
                    zOrder);
                drawnPrimitiveCount++;
            }
        }

        return drawnPrimitiveCount;
    }

    public int DrawPath(NavigationPath path, Color color, float zOrder = 0f)
    {
        ArgumentNullException.ThrowIfNull(path);

        int drawnPrimitiveCount = 0;
        for (int pointIndex = 1; pointIndex < path.Points.Count; pointIndex++)
        {
            if (!Budget.TryConsume())
            {
                return drawnPrimitiveCount;
            }

            Vector2 start = ToVector2(path.Points[pointIndex - 1]);
            Vector2 end = ToVector2(path.Points[pointIndex]);
            _renderer.DrawLine(start, end, color, zOrder);
            drawnPrimitiveCount++;
        }

        return drawnPrimitiveCount;
    }

    public static Rectangle ClipVisibleCells(NavigationGrid2D grid, Rectangle visibleCells)
    {
        ArgumentNullException.ThrowIfNull(grid);

        int left = Math.Clamp(visibleCells.Left, 0, grid.Width);
        int top = Math.Clamp(visibleCells.Top, 0, grid.Height);
        int right = Math.Clamp(visibleCells.Right, left, grid.Width);
        int bottom = Math.Clamp(visibleCells.Bottom, top, grid.Height);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    public static int CountVisibleCells(NavigationGrid2D grid, Rectangle visibleCells)
    {
        Rectangle clippedCells = ClipVisibleCells(grid, visibleCells);
        return clippedCells.Width * clippedCells.Height;
    }

    private static Vector2 ToVector2(Vector3 point)
    {
        return new Vector2(point.X, point.Z);
    }
}