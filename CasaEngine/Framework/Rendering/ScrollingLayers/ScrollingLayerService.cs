using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.ScrollingLayers;

/// <summary>
/// Engine-side mechanism behind scrolling background layers (docs/engine/scrolling-layers.md,
/// plan-e9b-backdrops-moteur.md): per-tick camera parallax, auto-scroll and V-animation cadence for a
/// set of <see cref="ScrollingLayerDefinition"/>s plus an optional full-viewport
/// <see cref="ScrollingTintDefinition"/>. Deliberately free of any GPU type (no <c>Texture2D</c>, no
/// <c>Game</c>) - <see cref="Application.Components.ScrollingLayerComponent"/> is the thin component
/// that resolves textures and submits the quads, exactly the house pattern used by
/// <see cref="ScreenEffects.ScreenEffectService"/>/<see cref="Application.Components.ScreenEffectComponent"/>
/// and <see cref="Audio.AudioService"/>.
///
/// This service knows nothing about the PSX fidelity rules (opcode-driven scroll activation, scripted
/// shake, tint walker...) - those stay in the game DLL, which computes the per-frame camera scroll and
/// pushes it here every frame via <see cref="SetFrame"/>. The single tick-based clock and its
/// per-layer accumulators (<see cref="Advance"/>) mirror the original engine's own
/// <c>RenderLayerToBuffer</c> (GraphicManager.cs:868-925) exactly, one tick at a time - there is no
/// closed-form shortcut and no floating-point clock: everything advances by whole logic ticks, so a
/// nominal one-tick frame reproduces the original bit for bit and a catch-up/saturated frame follows
/// the same logic tick the rest of the frame used.
/// </summary>
public sealed class ScrollingLayerService
{
    private struct LayerRuntime
    {
        public ScrollingLayerDefinition Definition;
        public int AnimFrameTimer;
        public int AnimFrameCounter;
        public int AutoScrollOffsetX;
        public int AutoScrollOffsetY;
        public int TimerX;
        public int TimerY;
        public int LayerOffsetX;
        public int LayerOffsetY;
    }

    private LayerRuntime[] _layers = System.Array.Empty<LayerRuntime>();
    private ScrollingTintDefinition? _tint;
    private ScrollingLayerConfiguration _configuration;

    /// <summary>Canvas/view sizes the current layers tile against.</summary>
    public ScrollingLayerConfiguration Configuration => _configuration;

    /// <summary>The overlay tint, if the current world has one.</summary>
    public ScrollingTintDefinition? Tint => _tint;

    public int LayerCount => _layers.Length;

    /// <summary>
    /// Strictly increases on every <see cref="SetLayers"/>, <see cref="SetTint"/> and <see cref="Clear"/>
    /// call, starting at 0 - never reset. <see cref="Application.Components.ScrollingLayerComponent"/>
    /// re-resolves its textures whenever this changes.
    /// </summary>
    public int LayersVersion { get; private set; }

    /// <summary>The scroll X last received via <see cref="SetFrame"/> (the original's own clamped,
    /// non-negative <c>g_cameraScrollingX</c> - see <c>AlundraCameraMath.ToOriginalScrollSpace</c>).</summary>
    public int LastPushedScrollX { get; private set; }

    public int LastPushedScrollY { get; private set; }

    /// <summary>Ticks armed by the last <see cref="SetFrame"/>, not yet consumed by <see cref="Advance"/>.</summary>
    public int PendingTicks { get; private set; }

    /// <summary>The camera target last pushed via <see cref="SetFrame"/> - used to place the covering
    /// quads in world space at submission time.</summary>
    public Vector3 CameraTarget { get; private set; }

    /// <summary>Number of <see cref="SetFrame"/> calls received since the last <see cref="Clear"/>.</summary>
    public int FramesPushed { get; private set; }

    /// <summary>True between a <see cref="SetFrame"/> call and the <see cref="Advance"/> that consumes it.</summary>
    public bool HasPendingFrame { get; private set; }

    public void SetConfiguration(ScrollingLayerConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Replaces every layer, resetting all per-layer runtime state to zero (a fresh accumulator/cadence
    /// start, matching the original's own per-map-instance reset - ScrollScreen.cs:86-95/:207-212).
    /// Called once per world load (D-E9b-9), never per frame - allocating a new array here is fine.
    /// </summary>
    public void SetLayers(System.ReadOnlySpan<ScrollingLayerDefinition> layers)
    {
        _layers = new LayerRuntime[layers.Length];
        for (var i = 0; i < layers.Length; i++)
        {
            _layers[i] = new LayerRuntime { Definition = layers[i] };
        }

        LayersVersion++;
    }

    public void SetTint(ScrollingTintDefinition? tint)
    {
        _tint = tint;
        LayersVersion++;
    }

    /// <summary>Removes every layer and tint and resets the push contract. Strictly increases
    /// <see cref="LayersVersion"/> - never resets it to 0.</summary>
    public void Clear()
    {
        _layers = System.Array.Empty<LayerRuntime>();
        _tint = null;
        FramesPushed = 0;
        PendingTicks = 0;
        HasPendingFrame = false;
        LastPushedScrollX = 0;
        LastPushedScrollY = 0;
        CameraTarget = Vector3.Zero;
        LayersVersion++;
    }

    /// <summary>
    /// Arms one frame's worth of ticks (D-E9b-3): the original's own clamped scroll, the camera target
    /// used to place quads, and how many logic ticks this frame represents. A second call before the
    /// next <see cref="Advance"/> OVERWRITES the pending frame - the last push before an
    /// <see cref="Advance"/> is the one consumed, never a cumulative one.
    /// </summary>
    public void SetFrame(int scrollX, int scrollY, int ticks, Vector3 cameraTarget)
    {
        LastPushedScrollX = scrollX;
        LastPushedScrollY = scrollY;
        PendingTicks = ticks;
        CameraTarget = cameraTarget;
        HasPendingFrame = true;
        FramesPushed++;
    }

    /// <summary>
    /// Consumes the pending frame: for each of <see cref="PendingTicks"/> ticks, for every layer, in
    /// the original's own order - V-animation cadence, then auto-scroll (D-E9b-3) - then recomputes
    /// every layer's final wrapped offset from <see cref="LastPushedScrollX"/>/<see cref="LastPushedScrollY"/>
    /// and its own accumulator. The offset recompute always runs, even for zero ticks, so a new pushed
    /// scroll is reflected immediately (exactly like the original, which recomputes every frame
    /// regardless of tick count).
    /// </summary>
    public void Advance()
    {
        var ticks = PendingTicks;

        for (var tick = 0; tick < ticks; tick++)
        {
            for (var i = 0; i < _layers.Length; i++)
            {
                AdvanceLayerOneTick(ref _layers[i]);
            }
        }

        for (var i = 0; i < _layers.Length; i++)
        {
            RecomputeLayerOffset(ref _layers[i]);
        }

        PendingTicks = 0;
        HasPendingFrame = false;
    }

    private static void AdvanceLayerOneTick(ref LayerRuntime layer)
    {
        ref readonly var definition = ref layer.Definition;

        // V-animation cadence: GraphicManager.cs:871-880.
        //
        // frameCount is the RAW definition length, not the resolved-after-fallback Frames.Length the
        // retired BackdropRenderer.AdvanceAnimation used to modulo against - a deliberate, documented
        // deviation (docs/engine/scrolling-layers.md §7). It is observationally equivalent: the
        // resolved length is always 0 (layer skipped), 1 (fallback - ScrollingLayerComponent.Submit
        // clamps the frame index, so frame 0 is drawn regardless of this counter's value, same as a
        // resolved modulo 1) or N (identical, no fallback). It is NOT numerically equivalent: on a
        // degraded (fallback) layer this counter's raw VALUE differs from what the old resolved-modulo
        // code would have produced. A tick-by-tick equivalence harness (S1) comparing this counter's
        // value directly, rather than the submitted texture/clamped frame index, will see a spurious
        // divergence on any degraded layer.
        layer.AnimFrameTimer++;
        if (layer.AnimFrameTimer > definition.AnimTimer)
        {
            layer.AnimFrameCounter++;
            var frameCount = definition.FrameTextureAssetIds?.Length ?? 1;
            if (layer.AnimFrameCounter >= frameCount)
            {
                layer.AnimFrameCounter = 0;
            }

            layer.AnimFrameTimer = 0;
        }

        // Auto-scroll accumulator: GraphicManager.cs:882-898 / direction rule ScrollScreen.cs:246-270.
        layer.TimerX++;
        layer.AutoScrollOffsetX += definition.ScrollXSpeed;
        if (definition.ScrollXPeriod != 0 && layer.TimerX >= System.Math.Abs(definition.ScrollXPeriod))
        {
            layer.AutoScrollOffsetX += ScrollDirection(definition.ScrollXSpeed, definition.ScrollXPeriod);
            layer.TimerX = 0;
        }

        layer.TimerY++;
        layer.AutoScrollOffsetY += definition.ScrollYSpeed;
        if (definition.ScrollYPeriod != 0 && layer.TimerY >= System.Math.Abs(definition.ScrollYPeriod))
        {
            layer.AutoScrollOffsetY += ScrollDirection(definition.ScrollYSpeed, definition.ScrollYPeriod);
            layer.TimerY = 0;
        }
    }

    private static int ScrollDirection(int speed, int period)
    {
        return (speed < 0) != (period < 0) ? -1 : 1;
    }

    private void RecomputeLayerOffset(ref LayerRuntime layer)
    {
        ref readonly var definition = ref layer.Definition;

        var parallaxX = ComputeParallaxOffset(LastPushedScrollX, definition.FactorXNum, definition.FactorXDenom);
        var parallaxY = ComputeParallaxOffset(LastPushedScrollY, definition.FactorYNum, definition.FactorYDenom);

        layer.LayerOffsetX = WrapOffset(parallaxX + layer.AutoScrollOffsetX, _configuration.CanvasWidth);
        layer.LayerOffsetY = WrapOffset(parallaxY + layer.AutoScrollOffsetY, _configuration.CanvasHeight);
    }

    public bool TryGetLayerState(int index, out ScrollingLayerState state)
    {
        if ((uint)index >= (uint)_layers.Length)
        {
            state = default;
            return false;
        }

        ref readonly var layer = ref _layers[index];
        state = new ScrollingLayerState(
            layer.AnimFrameTimer, layer.AnimFrameCounter,
            layer.AutoScrollOffsetX, layer.AutoScrollOffsetY,
            layer.TimerX, layer.TimerY,
            layer.LayerOffsetX, layer.LayerOffsetY);
        return true;
    }

    /// <summary>The layer definition pushed by <see cref="SetLayers"/> at <paramref name="index"/>.</summary>
    public ScrollingLayerDefinition GetLayerDefinition(int index)
    {
        return _layers[index].Definition;
    }

    /// <summary>
    /// Zeroes every layer's per-tick runtime state (V-animation timer/counter, auto-scroll
    /// accumulators/timers, wrapped offsets) while keeping each layer's <see cref="ScrollingLayerDefinition"/>
    /// and this service's <see cref="LayersVersion"/>/<see cref="Tint"/>/push contract untouched
    /// (D-E9b-7: <see cref="Application.Components.ScrollingLayerComponent.ResolveTextures"/> calls this
    /// explicitly whenever it re-resolves, i.e. on every <see cref="LayersVersion"/> change - so a bare
    /// <see cref="SetTint"/> resets the counters too, not just <see cref="SetLayers"/>). A no-op on
    /// <see cref="SetLayers"/>'s own version bump since that already starts every layer zeroed; not
    /// called by <see cref="SetLayers"/>/<see cref="SetTint"/>/<see cref="Clear"/> themselves so the
    /// production sequence <see cref="Clear"/> → <see cref="SetLayers"/> → <see cref="SetTint"/> keeps
    /// resetting exactly once, from the component's single post-Update re-resolve.
    /// </summary>
    public void ResetLayerRuntimeState()
    {
        for (var i = 0; i < _layers.Length; i++)
        {
            ref var layer = ref _layers[i];
            layer.AnimFrameTimer = 0;
            layer.AnimFrameCounter = 0;
            layer.AutoScrollOffsetX = 0;
            layer.AutoScrollOffsetY = 0;
            layer.TimerX = 0;
            layer.TimerY = 0;
            layer.LayerOffsetX = 0;
            layer.LayerOffsetY = 0;
        }
    }

    // ---- Pure functions (docs/engine/scrolling-layers.md - allocation-free port of BackdropOffsetMath) ----

    /// <summary>
    /// <c>scroll * factorNum / factorDenom</c>, truncated integer division - the original's own
    /// arithmetic, not a float division rounded afterwards (1/3 of scroll 5 is 1, not 1.667).
    /// <paramref name="scroll"/> is expected non-negative (the original's own clamped scroll space);
    /// a zero denominator disables this axis' parallax (contributes 0) instead of dividing by zero.
    /// </summary>
    public static int ComputeParallaxOffset(int scroll, int factorNum, int factorDenom)
    {
        return factorDenom == 0 ? 0 : scroll * factorNum / factorDenom;
    }

    /// <summary>Wraps <paramref name="value"/> into <c>[0, canvasSize)</c>.</summary>
    public static int WrapOffset(int value, int canvasSize)
    {
        var wrapped = value % canvasSize;
        return wrapped < 0 ? wrapped + canvasSize : wrapped;
    }

    /// <summary>
    /// The first (leftmost/topmost) tile-local origin, spaced <paramref name="tileSize"/> apart, whose
    /// <c>[origin, origin + tileSize)</c> interval covers screen position 0 - one axis of
    /// <see cref="ScrollingLayerComponent.Submit"/>'s covering-quad tiling, given that screen position
    /// 0 samples canvas coordinate <paramref name="offset"/>.
    /// </summary>
    public static int CoveringOriginStart(int offset, int tileSize)
    {
        return -WrapOffset(offset, tileSize);
    }

    /// <summary>
    /// How many <paramref name="tileSize"/>-spaced origins starting at <paramref name="start"/>
    /// (see <see cref="CoveringOriginStart"/>) are needed to fully cover <c>[0, viewportSize)</c> -
    /// the other half of the pair, replacing <c>BackdropOffsetMath.ComputeCoveringOrigins1D</c>'s
    /// allocated <c>List&lt;int&gt;</c> with a plain count a caller loops over (at most 2 per axis for
    /// this mechanism's 640x480 canvas against a 320x240 view).
    /// </summary>
    public static int CoveringOriginCount(int viewportSize, int start, int tileSize)
    {
        if (tileSize <= 0 || viewportSize <= 0)
        {
            return 0;
        }

        return ((viewportSize - start) + tileSize - 1) / tileSize;
    }
}
