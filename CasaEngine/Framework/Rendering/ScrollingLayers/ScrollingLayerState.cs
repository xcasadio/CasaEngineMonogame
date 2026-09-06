namespace CasaEngine.Framework.Rendering.ScrollingLayers;

/// <summary>
/// One layer's per-tick runtime state, as of the most recent <see cref="ScrollingLayerService.Advance"/>
/// call - read-only snapshot returned by <see cref="ScrollingLayerService.TryGetLayerState"/>. Mirrors
/// the original engine's own per-layer accumulators (<c>AnimFrameTimer</c>/<c>AnimFrameCounter</c>/
/// <c>TimerX</c>/<c>TimerY</c> - GraphicManager.cs:868-925) one for one.
/// </summary>
public readonly struct ScrollingLayerState
{
    public ScrollingLayerState(int animFrameTimer, int animFrameCounter, int autoScrollOffsetX, int autoScrollOffsetY,
        int timerX, int timerY, int layerOffsetX, int layerOffsetY)
    {
        AnimFrameTimer = animFrameTimer;
        AnimFrameCounter = animFrameCounter;
        AutoScrollOffsetX = autoScrollOffsetX;
        AutoScrollOffsetY = autoScrollOffsetY;
        TimerX = timerX;
        TimerY = timerY;
        LayerOffsetX = layerOffsetX;
        LayerOffsetY = layerOffsetY;
    }

    /// <summary>Ticks accumulated since the V-animation counter last advanced.</summary>
    public int AnimFrameTimer { get; }

    /// <summary>Index of the frame currently shown (into the layer's <c>FrameTextureAssetIds</c>/
    /// resolved textures - always a valid index into the definition's own array).</summary>
    public int AnimFrameCounter { get; }

    /// <summary>Raw (unwrapped) auto-scroll accumulator on X - <c>OffsetX</c> in the original.</summary>
    public int AutoScrollOffsetX { get; }

    /// <summary>Raw (unwrapped) auto-scroll accumulator on Y - <c>OffsetY</c> in the original.</summary>
    public int AutoScrollOffsetY { get; }

    /// <summary>Ticks accumulated since the last extra auto-scroll pixel on X.</summary>
    public int TimerX { get; }

    /// <summary>Ticks accumulated since the last extra auto-scroll pixel on Y.</summary>
    public int TimerY { get; }

    /// <summary>
    /// The canvas-space offset visible at the view's top-left corner - camera parallax (from the
    /// last pushed scroll) plus <see cref="AutoScrollOffsetX"/>, wrapped into <c>[0, CanvasWidth)</c>.
    /// Recomputed by every <see cref="ScrollingLayerService.Advance"/> call, including one that
    /// consumes zero ticks (a new pushed scroll must show up immediately, exactly like the original
    /// recomputing every frame regardless of tick count).
    /// </summary>
    public int LayerOffsetX { get; }

    /// <summary>Same as <see cref="LayerOffsetX"/>, Y axis, wrapped into <c>[0, CanvasHeight)</c>.</summary>
    public int LayerOffsetY { get; }
}
