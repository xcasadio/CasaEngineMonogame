using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class GridPathfinder2D
{
    private const int NoNode = -1;
    private const float DiagonalCost = 1.41421356f;

    public static GridPathfinder2D Shared { get; } = new();

    public bool TryFindPath(NavigationGrid2D grid, Vector3 start, Vector3 goal, NavigationQuery query, out NavigationPath path)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(query);

        path = null;
        if (!grid.TryGetCellFromWorld(start, out Point startCell)
            || !grid.TryGetCellFromWorld(goal, out Point goalCell))
        {
            return false;
        }

        return TryFindPath(grid, startCell, goalCell, query, out path);
    }

    public bool TryFindPath(NavigationGrid2D grid, Point start, Point goal, NavigationQuery query, out NavigationPath path)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(query);

        path = null;
        if (!grid.IsInside(start.X, start.Y)
            || !grid.IsInside(goal.X, goal.Y)
            || !grid.IsCellWalkable(start.X, start.Y, query)
            || !grid.IsCellWalkable(goal.X, goal.Y, query))
        {
            return false;
        }

        int nodeCount = grid.Width * grid.Height;
        int[] cameFrom = new int[nodeCount];
        float[] costs = new float[nodeCount];
        bool[] closed = new bool[nodeCount];
        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            cameFrom[nodeIndex] = NoNode;
            costs[nodeIndex] = float.PositiveInfinity;
        }

        int startIndex = grid.GetCellIndex(start.X, start.Y);
        int goalIndex = grid.GetCellIndex(goal.X, goal.Y);
        costs[startIndex] = 0f;

        var open = new PriorityQueue<int, float>();
        open.Enqueue(startIndex, EstimateCost(start, goal, query.AllowDiagonalMovement));

        while (open.Count > 0)
        {
            int currentIndex = open.Dequeue();
            if (closed[currentIndex])
            {
                continue;
            }

            if (currentIndex == goalIndex)
            {
                path = BuildPath(grid, cameFrom, startIndex, goalIndex);
                return true;
            }

            closed[currentIndex] = true;
            Point current = ToPoint(currentIndex, grid.Width);
            VisitNeighbors(grid, current, goal, query, currentIndex, cameFrom, costs, closed, open);
        }

        return false;
    }

    private static void VisitNeighbors(
        NavigationGrid2D grid,
        Point current,
        Point goal,
        NavigationQuery query,
        int currentIndex,
        int[] cameFrom,
        float[] costs,
        bool[] closed,
        PriorityQueue<int, float> open)
    {
        int neighborCount = query.AllowDiagonalMovement ? 8 : 4;
        for (int neighborOffsetIndex = 0; neighborOffsetIndex < neighborCount; neighborOffsetIndex++)
        {
            Point offset = GetNeighborOffset(neighborOffsetIndex);
            int neighborX = current.X + offset.X;
            int neighborY = current.Y + offset.Y;
            if (!CanVisitNeighbor(grid, current, neighborX, neighborY, offset, query))
            {
                continue;
            }

            int neighborIndex = grid.GetCellIndex(neighborX, neighborY);
            if (closed[neighborIndex])
            {
                continue;
            }

            float stepCost = grid.GetCell(neighborX, neighborY).Cost * GetMovementCost(offset);
            float newCost = costs[currentIndex] + stepCost;
            if (newCost >= costs[neighborIndex])
            {
                continue;
            }

            cameFrom[neighborIndex] = currentIndex;
            costs[neighborIndex] = newCost;
            var neighbor = new Point(neighborX, neighborY);
            float priority = newCost + EstimateCost(neighbor, goal, query.AllowDiagonalMovement);
            open.Enqueue(neighborIndex, priority);
        }
    }

    private static bool CanVisitNeighbor(NavigationGrid2D grid, Point current, int neighborX, int neighborY, Point offset, NavigationQuery query)
    {
        if (!grid.IsInside(neighborX, neighborY) || !grid.IsCellWalkable(neighborX, neighborY, query))
        {
            return false;
        }

        if (offset.X == 0 || offset.Y == 0 || !query.PreventDiagonalCornerCutting)
        {
            return true;
        }

        return grid.IsCellWalkable(current.X + offset.X, current.Y, query)
            && grid.IsCellWalkable(current.X, current.Y + offset.Y, query);
    }

    private static NavigationPath BuildPath(NavigationGrid2D grid, int[] cameFrom, int startIndex, int goalIndex)
    {
        var reversedCells = new List<int>();
        int currentIndex = goalIndex;
        while (currentIndex != NoNode)
        {
            reversedCells.Add(currentIndex);
            if (currentIndex == startIndex)
            {
                break;
            }

            currentIndex = cameFrom[currentIndex];
        }

        var path = new NavigationPath();
        for (int index = reversedCells.Count - 1; index >= 0; index--)
        {
            Point cell = ToPoint(reversedCells[index], grid.Width);
            path.AddPoint(grid.GetWorldPosition(cell.X, cell.Y));
        }

        return path;
    }

    private static float EstimateCost(Point start, Point goal, bool allowDiagonalMovement)
    {
        int deltaX = Math.Abs(goal.X - start.X);
        int deltaY = Math.Abs(goal.Y - start.Y);
        if (!allowDiagonalMovement)
        {
            return deltaX + deltaY;
        }

        int diagonalSteps = Math.Min(deltaX, deltaY);
        int straightSteps = Math.Max(deltaX, deltaY) - diagonalSteps;
        return diagonalSteps * DiagonalCost + straightSteps;
    }

    private static float GetMovementCost(Point offset)
    {
        return offset.X != 0 && offset.Y != 0 ? DiagonalCost : 1f;
    }

    private static Point GetNeighborOffset(int neighborOffsetIndex)
    {
        return neighborOffsetIndex switch
        {
            0 => new Point(0, -1),
            1 => new Point(1, 0),
            2 => new Point(0, 1),
            3 => new Point(-1, 0),
            4 => new Point(-1, -1),
            5 => new Point(1, -1),
            6 => new Point(1, 1),
            _ => new Point(-1, 1),
        };
    }

    private static Point ToPoint(int nodeIndex, int width)
    {
        return new Point(nodeIndex % width, nodeIndex / width);
    }
}