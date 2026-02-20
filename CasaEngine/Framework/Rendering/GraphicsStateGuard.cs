using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// RAII guard that captures the current <see cref="GraphicsDevice"/> render state
/// on construction and restores it on <see cref="Dispose"/>.
///
/// Usage:
/// <code>
/// using var guard = new GraphicsStateGuard(graphicsDevice);
/// // ... render a view, modify states freely ...
/// // Dispose() restores the captured state automatically.
/// </code>
///
/// Use this to prevent one view's render pass from corrupting the state
/// expected by subsequent views or game components.
/// </summary>
public readonly struct GraphicsStateGuard : IDisposable
{
    private readonly GraphicsDevice       _gd;
    private readonly GraphicsStateSnapshot _snapshot;

    /// <summary>
    /// Captures the current render state of <paramref name="graphicsDevice"/>.
    /// </summary>
    public GraphicsStateGuard(GraphicsDevice graphicsDevice)
    {
        _gd       = graphicsDevice;
        _snapshot = GraphicsStateSnapshot.Capture(graphicsDevice);
    }

    /// <summary>
    /// Restores the captured render state. Called automatically at the end of a
    /// <c>using</c> block.
    /// </summary>
    public void Dispose() => _snapshot.Restore(_gd);
}
