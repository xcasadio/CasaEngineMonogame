using CasaEngine.Framework.Application.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class NavigationDebugDraw3D
{
    private readonly Line3dRendererComponent _renderer;

    public NavigationDebugDraw3D(Line3dRendererComponent renderer, int maxPrimitiveCount = 2048)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        Budget = new NavigationDebugDrawBudget(maxPrimitiveCount);
    }

    public NavigationDebugDrawBudget Budget { get; }

    public void ResetFrameBudget()
    {
        Budget.Reset();
    }

    public int DrawPath(NavigationPath path, Color color)
    {
        ArgumentNullException.ThrowIfNull(path);

        int drawnPrimitiveCount = 0;
        for (int pointIndex = 1; pointIndex < path.Points.Count; pointIndex++)
        {
            if (!Budget.TryConsume())
            {
                return drawnPrimitiveCount;
            }

            _renderer.AddLine(path.Points[pointIndex - 1], path.Points[pointIndex], color);
            drawnPrimitiveCount++;
        }

        return drawnPrimitiveCount;
    }

    public bool DrawLink(Vector3 start, Vector3 end, Color color)
    {
        if (!Budget.TryConsume())
        {
            return false;
        }

        _renderer.AddLine(start, end, color);
        return true;
    }
}