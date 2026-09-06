namespace CasaEngine.Framework.Rendering.ScrollingLayers;

/// <summary>
/// The fixed sizes a <see cref="ScrollingLayerService"/> instance tiles/covers against: the wrapping
/// canvas every layer's texture is assumed to be (Alundra: 640x480), and the view size in world units
/// the covering quads must fill (Alundra: 320x240, the original's own framebuffer - see
/// docs/engine/scrolling-layers.md). Pushed once per world load alongside the layer/tint definitions.
/// </summary>
public readonly struct ScrollingLayerConfiguration
{
    public ScrollingLayerConfiguration(int canvasWidth, int canvasHeight, int viewWidth, int viewHeight, float backgroundDepth = 1f)
    {
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
        BackgroundDepth = backgroundDepth;
    }

    public int CanvasWidth { get; }

    public int CanvasHeight { get; }

    public int ViewWidth { get; }

    public int ViewHeight { get; }

    /// <summary>
    /// How far a <see cref="RenderPass2D.Background"/> layer recedes behind the camera target along Z
    /// (plan-e9c-defauts-321.md, D-E9c-5): submitted at <c>cameraTarget.Z - BackgroundDepth</c> instead
    /// of at the camera depth every other pass and the tint use. Default 1 (Alundra's camera target Z
    /// is 0, so a background layer lands at z = -1). In this engine a smaller world Z is FARTHER from
    /// the camera, so a receded background layer's depth write is correctly rejected wherever a
    /// same-frame static-batch tile (world Z 0) already wrote its own - reproducing the original's own
    /// policy of placing Ground=false layers behind every floor, wall and entity
    /// (GraphicManager.cs:825-826), which the previous z = 0 for every quad (D-E9b-4, revised here)
    /// under-fixed: a Background layer submitted at the same depth as the camera's own tiles used to
    /// win draw-order ties against the immediate static tile batch when flushed afterward, overpainting
    /// tiles at world Z 0 (measured on map 321's topmost bone row).
    /// </summary>
    public float BackgroundDepth { get; }
}
