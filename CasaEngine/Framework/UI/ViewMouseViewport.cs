using CasaEngine.Framework.Rendering;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Adapts a view's viewport rectangle to MGUI's <see cref="IMouseViewport"/> interface,
/// allowing per-view mouse hit-testing with viewport-local coordinates.
///
/// Coordinates passed to <see cref="IsInside"/> are assumed to be viewport-local
/// (i.e. already offset by the view's screen-space origin, as produced by
/// <see cref="ViewManager.ScreenToView"/>).
/// </summary>
public sealed class ViewMouseViewport : IMouseViewport
{
    private readonly IRenderSurface _surface;

    public ViewMouseViewport(IRenderSurface surface)
    {
        _surface = surface;
    }

    /// <summary>
    /// Returns true when <paramref name="position"/> is inside the view bounds
    /// (0, 0, width, height) in viewport-local coordinates.
    /// </summary>
    public bool IsInside(Vector2 position)
    {
        var vp = _surface.ViewportRect;
        return position.X >= 0f && position.Y >= 0f
            && position.X <= vp.Width && position.Y <= vp.Height;
    }

    /// <summary>No additional offset — position is already viewport-local.</summary>
    public Vector2 GetOffset() => Vector2.Zero;
}
