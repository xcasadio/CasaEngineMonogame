namespace CasaEngine.Framework.Rendering.ScrollingLayers;

/// <summary>
/// The fixed sizes a <see cref="ScrollingLayerService"/> instance tiles/covers against: the wrapping
/// canvas every layer's texture is assumed to be (Alundra: 640x480), and the view size in world units
/// the covering quads must fill (Alundra: 320x240, the original's own framebuffer - see
/// docs/engine/scrolling-layers.md). Pushed once per world load alongside the layer/tint definitions.
/// </summary>
public readonly struct ScrollingLayerConfiguration
{
    public ScrollingLayerConfiguration(int canvasWidth, int canvasHeight, int viewWidth, int viewHeight)
    {
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
    }

    public int CanvasWidth { get; }

    public int CanvasHeight { get; }

    public int ViewWidth { get; }

    public int ViewHeight { get; }
}
