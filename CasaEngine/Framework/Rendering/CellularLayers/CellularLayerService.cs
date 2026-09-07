using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.CellularLayers;

/// <summary>
/// Engine-side mechanism behind the cellular mode of Alundra's backdrop layers
/// (docs/engine/cellular-layers.md, docs/plan-e9d-mode-cellulaire.md): per-tick V-animation cadence,
/// per-cell drift/period/parallax/wrap and the stateless <c>WaveX</c> formula, for a set of
/// <see cref="CellularLayerDefinition"/>s sharing a per-map <c>WaveLut</c>. Deliberately free of any
/// GPU type - <see cref="Application.Components.CellularLayerComponent"/> is the thin component that
/// resolves the baked tile-sheet textures and submits the cells, the same house pattern as the sibling
/// <see cref="ScrollingLayers.ScrollingLayerService"/> (plan-e9d-mode-cellulaire.md D3 - a parallel
/// pair, not an extension: a cellular layer needs per-sprite state, an arbitrary source rectangle and
/// its own parallax per cell, none of which the sibling's one-accumulator-per-layer model can carry).
///
/// This service knows nothing about the PSX fidelity rules beyond what is transcribed here - the
/// per-frame call that drives it (<c>GraphicManager.cs:988-1225</c>) is reproduced one tick at a time,
/// exactly like the sibling mechanism's own <c>RenderLayerToBuffer</c> port: no closed-form shortcut,
/// no floating-point clock, a nominal one-tick frame reproduces the original bit for bit and a
/// catch-up frame follows the same logic tick the rest of the frame used.
///
/// <see cref="CellularCellType.FallRespawn"/> shares the original's own global random stream
/// (<c>Random.cs:5,14</c>, D7) - this service therefore owns no generator of its own. <see cref="Advance"/>
/// takes the next raw 32-bit value through an injected delegate, so the consuming DLL can wire it to
/// that shared stream and tests can pin an exact sequence.
/// </summary>
public sealed class CellularLayerService
{
    /// <summary>The original engine's fixed framebuffer size a cell's screen-space position wraps
    /// against (<c>GraphicManager.cs:816-817</c>) - never configurable, unlike the sibling mechanism's
    /// canvas/view.</summary>
    public const int ScreenWidth = 320;

    public const int ScreenHeight = 240;

    /// <summary>Hard cap on cells per layer (<c>ScrollScreen.cs:228</c>) - every shipped layer's cell
    /// count is <c>Divisions</c>, between 15 and 120, so this cap is never hit by the current corpus
    /// but is still enforced defensively.</summary>
    public const int CellMax = 200;

    private struct CellRuntime
    {
        /// <summary>Persistent drift position - only meaningful for <see cref="CellularCellType.Normal"/>
        /// and <see cref="CellularCellType.FallRespawn"/> cells.</summary>
        public int PosX;

        public int PosY;

        public int TickX;

        public int TickY;

        /// <summary>Last resolved screen-space draw position, for every cell type (recomputed every
        /// tick this cell is reached; a <see cref="CellularCellType.WaveX"/> cell has no persistent
        /// <see cref="PosX"/>/<see cref="PosY"/>, only this).</summary>
        public int DrawX;

        public int DrawY;

        public bool ShouldDraw;
    }

    private struct LayerRuntime
    {
        public CellularLayerDefinition Definition;
        public int AnimFrameTimer;
        public int AnimFrameCounter;
        public byte WaveTick;
        public CellRuntime[] Cells;
    }

    private LayerRuntime[] _layers = System.Array.Empty<LayerRuntime>();
    private int[] _waveLut = System.Array.Empty<int>();
    private CellularLayerConfiguration _configuration;

    public CellularLayerConfiguration Configuration => _configuration;

    public int LayerCount => _layers.Length;

    /// <summary>The map's <c>WaveLut</c> (<c>int32[256]</c> in the original), read by every
    /// <see cref="CellularCellType.WaveX"/> cell. Empty until <see cref="SetWaveLut"/> is called.</summary>
    public System.ReadOnlySpan<int> WaveLut => _waveLut;

    /// <summary>Strictly increases on every <see cref="SetLayers"/> and <see cref="Clear"/> call,
    /// starting at 0 - never reset. <see cref="Application.Components.CellularLayerComponent"/>
    /// re-resolves its textures whenever this changes.</summary>
    public int LayersVersion { get; private set; }

    /// <summary>The raw camera scroll last received via <see cref="SetFrame"/> - <c>cameraX</c>/
    /// <c>cameraY</c> in the transcribed formulas, used for each cell's own <c>baseX</c>/<c>baseY</c>
    /// parallax term.</summary>
    public int LastPushedCameraX { get; private set; }

    public int LastPushedCameraY { get; private set; }

    /// <summary>Ticks armed by the last <see cref="SetFrame"/>, not yet consumed by <see cref="Advance"/>.</summary>
    public int PendingTicks { get; private set; }

    /// <summary>The camera target last pushed via <see cref="SetFrame"/> - used only at submission time
    /// to place a cell's screen-space position in world space; this service never reads it itself.</summary>
    public Vector3 CameraTarget { get; private set; }

    /// <summary>Number of <see cref="SetFrame"/> calls received since the last <see cref="Clear"/>.</summary>
    public int FramesPushed { get; private set; }

    /// <summary>True between a <see cref="SetFrame"/> call and the <see cref="Advance"/> that consumes it.</summary>
    public bool HasPendingFrame { get; private set; }

    public void SetConfiguration(CellularLayerConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Replaces every layer, resetting all per-layer/per-cell runtime state (each cell reseeds
    /// <c>posX</c>/<c>posY</c> at its own <c>X0</c>/<c>Y0</c> - docs/plan-e9d-mode-cellulaire.md §1.5
    /// ter "Amorçage"). Called once per world load, never per frame.
    /// </summary>
    public void SetLayers(System.ReadOnlySpan<CellularLayerDefinition> layers)
    {
        var newLayers = new LayerRuntime[layers.Length];
        for (var i = 0; i < layers.Length; i++)
        {
            var definition = layers[i];
            var cellCount = definition.Cells?.Length ?? 0;
            var cells = new CellRuntime[cellCount];
            for (var c = 0; c < cellCount; c++)
            {
                cells[c] = new CellRuntime
                {
                    PosX = definition.Cells[c].X0,
                    PosY = definition.Cells[c].Y0,
                };
            }

            newLayers[i] = new LayerRuntime { Definition = definition, Cells = cells };
        }

        _layers = newLayers;
        LayersVersion++;
    }

    /// <summary>Pushes the per-map <c>WaveLut</c> (a copy is kept). An empty span is a valid push - it
    /// reproduces the original's own defensive early exit on every <see cref="CellularCellType.WaveX"/>
    /// cell (docs/plan-e9d-mode-cellulaire.md §1.5 ter).</summary>
    public void SetWaveLut(System.ReadOnlySpan<int> waveLut)
    {
        _waveLut = waveLut.ToArray();
    }

    /// <summary>Removes every layer and the <c>WaveLut</c>, and resets the push contract. Strictly
    /// increases <see cref="LayersVersion"/> - never resets it to 0.</summary>
    public void Clear()
    {
        _layers = System.Array.Empty<LayerRuntime>();
        _waveLut = System.Array.Empty<int>();
        FramesPushed = 0;
        PendingTicks = 0;
        HasPendingFrame = false;
        LastPushedCameraX = 0;
        LastPushedCameraY = 0;
        CameraTarget = Vector3.Zero;
        LayersVersion++;
    }

    /// <summary>
    /// Arms one frame's worth of ticks: the raw camera scroll used by every cell's own parallax term,
    /// the camera target used to place cells in world space at submission time, and how many logic
    /// ticks this frame represents. A second call before the next <see cref="Advance"/> OVERWRITES the
    /// pending frame - the last push before an <see cref="Advance"/> is the one consumed, never a
    /// cumulative one (same contract as the sibling mechanism's own <c>SetFrame</c>).
    /// </summary>
    public void SetFrame(int cameraX, int cameraY, int ticks, Vector3 cameraTarget)
    {
        LastPushedCameraX = cameraX;
        LastPushedCameraY = cameraY;
        PendingTicks = ticks;
        CameraTarget = cameraTarget;
        HasPendingFrame = true;
        FramesPushed++;
    }

    /// <summary>
    /// Consumes the pending frame: for each of <see cref="PendingTicks"/> ticks, for every layer, in
    /// the original's own per-frame order - cadence first (<c>AnimFrameTimer</c>/<c>AnimFrameCounter</c>/
    /// <c>WaveTick</c>), then every cell in definition order. <paramref name="nextRandomUInt32"/> is
    /// called only when a <see cref="CellularCellType.FallRespawn"/> cell actually respawns this tick
    /// (D7 - the shared global stream, never this service's own generator).
    /// </summary>
    public void Advance(System.Func<uint> nextRandomUInt32)
    {
        var ticks = PendingTicks;

        for (var tick = 0; tick < ticks; tick++)
        {
            for (var i = 0; i < _layers.Length; i++)
            {
                AdvanceLayerOneTick(ref _layers[i], _waveLut, LastPushedCameraX, LastPushedCameraY, nextRandomUInt32);
            }
        }

        PendingTicks = 0;
        HasPendingFrame = false;
    }

    private static void AdvanceLayerOneTick(ref LayerRuntime layer, int[] waveLut, int cameraX, int cameraY, System.Func<uint> nextRandomUInt32)
    {
        ref readonly var definition = ref layer.Definition;

        // Cadence, once per layer per tick, before the cell loop - GraphicManager.cs:988-997.
        layer.AnimFrameTimer++;
        if (layer.AnimFrameTimer > definition.AnimTimer)
        {
            layer.AnimFrameCounter++;
            if (layer.AnimFrameCounter >= definition.AnimNum)
            {
                layer.AnimFrameCounter = 0;
            }

            layer.AnimFrameTimer = 0;
        }

        layer.WaveTick = unchecked((byte)(layer.WaveTick + 1));

        var cellCount = System.Math.Min(layer.Cells.Length, CellMax);

        for (var c = 0; c < cellCount; c++)
        {
            ref readonly var cellDefinition = ref definition.Cells[c];
            ref var cellRuntime = ref layer.Cells[c];

            switch (cellDefinition.Type)
            {
                case CellularCellType.Normal:
                    AdvanceNormalCell(ref cellRuntime, in cellDefinition, cameraX, cameraY);
                    break;

                case CellularCellType.ScriptTrack:
                    // Empty case in the original - draws nothing, advances no state (D5).
                    cellRuntime.ShouldDraw = false;
                    break;

                case CellularCellType.FallRespawn:
                    AdvanceFallRespawnCell(ref cellRuntime, in cellDefinition, cameraX, cameraY, nextRandomUInt32);
                    break;

                case CellularCellType.WaveX:
                    if (waveLut.Length == 0)
                    {
                        // The original's guard is `break` inside a `switch` case, so it leaves the
                        // switch and the cell loop CONTINUES with the next cell - it is a per-cell
                        // skip, not an exit from the layer. A layer mixing WaveX with Normal or
                        // FallRespawn cells still advances and draws those when the lookup table is
                        // absent (GraphicManager.cs:1190-1193).
                        cellRuntime.ShouldDraw = false;
                        break;
                    }

                    AdvanceWaveXCell(ref cellRuntime, in cellDefinition, in definition, waveLut, layer.WaveTick);
                    break;
            }
        }
    }

    private static void AdvanceNormalCell(ref CellRuntime cell, in CellularCellDefinition definition, int cameraX, int cameraY)
    {
        cell.PosX += definition.DX;
        cell.PosY += definition.DY;

        if (definition.PeriodX != 0)
        {
            var stepX = ComputePeriodStepOr(definition.DX, definition.PeriodX);
            if (++cell.TickX >= System.Math.Abs(definition.PeriodX))
            {
                cell.PosX += stepX;
                cell.TickX = 0;
            }
        }

        if (definition.PeriodY != 0)
        {
            var stepY = ComputePeriodStepOr(definition.DY, definition.PeriodY);
            if (++cell.TickY >= System.Math.Abs(definition.PeriodY))
            {
                cell.PosY += stepY;
                cell.TickY = 0;
            }
        }

        var baseX = ComputeCameraBase(cameraX, definition.CamXNum, definition.CamXDen);
        var baseY = ComputeCameraBase(cameraY, definition.CamYNum, definition.CamYDen);

        var sx = cell.PosX - baseX;
        var minX = definition.U0 - definition.U1;
        if (sx < minX)
        {
            cell.PosX += ScreenWidth - minX;
            sx = cell.PosX - baseX;
        }
        else if (sx > ScreenWidth - 1)
        {
            cell.PosX += -ScreenWidth + minX;
            sx = cell.PosX - baseX;
        }

        var sy = cell.PosY - baseY;
        var minY = definition.V0 - definition.V1;
        if (sy < minY)
        {
            cell.PosY += ScreenHeight - minY;
            sy = cell.PosY - baseY;
        }
        else if (sy > ScreenHeight - 1)
        {
            cell.PosY += -ScreenHeight + minY;
            sy = cell.PosY - baseY;
        }

        cell.DrawX = sx;
        cell.DrawY = sy;
        cell.ShouldDraw = true;
    }

    private static void AdvanceFallRespawnCell(ref CellRuntime cell, in CellularCellDefinition definition, int cameraX, int cameraY, System.Func<uint> nextRandomUInt32)
    {
        cell.PosX += definition.DX;
        cell.PosY += definition.DY;

        // Transcribed faithfully as the original writes it: the step is computed unconditionally,
        // ahead of the period check, unlike Normal's own shape where the step is computed only inside
        // the `PeriodX != 0` guard. Both shapes are behaviourally equivalent when the period is 0 (the
        // step is simply never applied), but the spec calls for the shape itself to be preserved.
        var stepX = ComputePeriodStepOr(definition.DX, definition.PeriodX);
        if (System.Math.Abs(definition.PeriodX) > 0 && ++cell.TickX >= System.Math.Abs(definition.PeriodX))
        {
            cell.PosX += stepX;
            cell.TickX = 0;
        }

        var stepY = ComputePeriodStepOr(definition.DY, definition.PeriodY);
        if (System.Math.Abs(definition.PeriodY) > 0 && ++cell.TickY >= System.Math.Abs(definition.PeriodY))
        {
            cell.PosY += stepY;
            cell.TickY = 0;
        }

        var baseX = ComputeCameraBase(cameraX, definition.CamXNum, definition.CamXDen);
        var baseY = ComputeCameraBase(cameraY, definition.CamYNum, definition.CamYDen);

        var sx = cell.PosX - baseX;
        var minX = definition.U0 - definition.U1;
        if (sx < minX)
        {
            cell.PosX += ScreenWidth - minX;
            sx = cell.PosX - baseX;
        }
        else if (sx > ScreenWidth - 1)
        {
            cell.PosX += -ScreenWidth + minX;
            sx = cell.PosX - baseX;
        }

        // No Y wrap for FallRespawn - a fall past the bottom respawns at a random X at the top instead.
        var sy = cell.PosY - baseY;
        if (sy > ScreenHeight - 1)
        {
            cell.PosX = (int)(((ulong)nextRandomUInt32() * (ulong)ScreenWidth) >> 32);
            cell.PosY += -ScreenHeight + (definition.V0 - definition.V1);
            sx = cell.PosX - baseX;
            sy = cell.PosY - baseY;
        }

        cell.DrawX = sx;
        cell.DrawY = sy;
        cell.ShouldDraw = true;
    }

    private static void AdvanceWaveXCell(ref CellRuntime cell, in CellularCellDefinition cellDefinition, in CellularLayerDefinition layerDefinition, int[] waveLut, byte waveTick)
    {
        // Stateless and camera-parallax-free (docs/plan-e9d-mode-cellulaire.md §1.5 ter) - no posX/posY
        // to persist, unlike Normal/FallRespawn. The wave parameters are layer-scoped (AWave*/BWave*),
        // the sample origin is the cell's own Y0.
        ComputeWaveDraw(
            waveLut, cellDefinition.X0, cellDefinition.Y0, waveTick,
            layerDefinition.AWaveY, layerDefinition.AWavePhase, layerDefinition.AWaveAmp,
            layerDefinition.BWaveY, layerDefinition.BWavePhase, layerDefinition.BWaveWeight,
            out var x, out var y);

        cell.DrawX = x;
        cell.DrawY = y;
        cell.ShouldDraw = true;
    }

    private static int ComputeCameraBase(int camera, int factorNum, int factorDenom)
    {
        return factorDenom != 0 ? camera * factorNum / factorDenom : 0;
    }

    // ---- Pure functions (docs/engine/cellular-layers.md) ------------------------------------------

    /// <summary>
    /// The period step direction as an OR of signs, recomputed every tick -
    /// <c>(delta &lt; 0 || period &lt; 0) ? -1 : 1</c>. Deliberately NOT the XOR-of-signs-computed-once
    /// rule the sibling scrolling-layer mechanism uses (docs/plan-e9d-mode-cellulaire.md §1.4) - the
    /// two diverge only when both <paramref name="delta"/> and <paramref name="period"/> are negative
    /// (OR gives -1, XOR would give +1).
    /// </summary>
    public static int ComputePeriodStepOr(int delta, int period)
    {
        return delta < 0 || period < 0 ? -1 : 1;
    }

    /// <summary><c>(animFrameCounter &lt;&lt; 8) / animNum</c>, truncated integer division. A
    /// non-positive <paramref name="animNum"/> is defensive-only (never measured in the shipped
    /// corpus) and yields phase 0 rather than dividing by zero.</summary>
    public static int ComputePhase(int animFrameCounter, int animNum)
    {
        return animNum <= 0 ? 0 : (animFrameCounter << 8) / animNum;
    }

    /// <summary>The sampled source row: <c>(v0 + phase) &amp; 0xFF</c> - wraps modulo 256
    /// (docs/plan-e9d-mode-cellulaire.md §1.5 bis).</summary>
    public static int ComputeSourceV(int v0, int phase)
    {
        return (v0 + phase) & 0xFF;
    }

    /// <summary>
    /// <c>CellularCellType.WaveX</c>'s full formula (docs/plan-e9d-mode-cellulaire.md §1.5 ter) - pure
    /// and stateless: two <paramref name="waveLut"/> samples combined into <paramref name="x"/>,
    /// <paramref name="y"/> is always <paramref name="y0"/> unchanged. The two <c>&lt; 0 -&gt; += 0x7F</c>
    /// fixups are the PSX signed-division rounding correction (an arithmetic right shift of a negative
    /// rounds toward negative infinity; the fixup makes it truncate toward zero instead) and must not
    /// be simplified away. <paramref name="waveLut"/> must be non-empty - the empty-lut early exit is
    /// the caller's (<see cref="Advance"/>'s) responsibility, since in the original it skips the rest
    /// of the cell loop, not just this formula.
    /// </summary>
    public static void ComputeWaveDraw(
        System.ReadOnlySpan<int> waveLut, int x0, int y0, byte waveTick,
        int aWaveY, int aWavePhase, int aWaveAmp, int bWaveY, int bWavePhase, int bWaveWeight,
        out int x, out int y)
    {
        var idxA1 = (y0 * aWaveY) & 0xFF;
        var idxA2 = (waveTick * aWavePhase) & 0xFF;
        var aW = waveLut[idxA1] * waveLut[idxA2] * aWaveAmp;
        if (aW < 0)
        {
            aW += 0x7F;
        }

        var idxB = (y0 * bWaveY + waveTick * bWavePhase) & 0xFF;
        var bW = waveLut[idxB] * bWaveWeight;

        var tSum = (aW >> 7) + bW;
        if (tSum < 0)
        {
            tSum += 0x7F;
        }

        x = x0 + (tSum >> 7) - 8;
        y = y0;
    }

    public bool TryGetLayerState(int layerIndex, out CellularLayerState state)
    {
        if ((uint)layerIndex >= (uint)_layers.Length)
        {
            state = default;
            return false;
        }

        ref readonly var layer = ref _layers[layerIndex];
        var phase = ComputePhase(layer.AnimFrameCounter, layer.Definition.AnimNum);
        state = new CellularLayerState(layer.AnimFrameTimer, layer.AnimFrameCounter, phase, layer.WaveTick);
        return true;
    }

    public bool TryGetCellState(int layerIndex, int cellIndex, out CellularCellState state)
    {
        if ((uint)layerIndex >= (uint)_layers.Length)
        {
            state = default;
            return false;
        }

        ref readonly var layer = ref _layers[layerIndex];
        if ((uint)cellIndex >= (uint)layer.Cells.Length)
        {
            state = default;
            return false;
        }

        ref readonly var cell = ref layer.Cells[cellIndex];
        state = new CellularCellState(cell.ShouldDraw, cell.DrawX, cell.DrawY);
        return true;
    }

    /// <summary>The layer definition pushed by <see cref="SetLayers"/> at <paramref name="index"/>.</summary>
    public CellularLayerDefinition GetLayerDefinition(int index)
    {
        return _layers[index].Definition;
    }

    /// <summary>Number of cells in the layer pushed by <see cref="SetLayers"/> at <paramref name="index"/>.</summary>
    public int GetCellCount(int layerIndex)
    {
        return _layers[layerIndex].Cells.Length;
    }

    /// <summary>
    /// Zeroes every layer's and every cell's per-tick runtime state, reseeding each cell's
    /// <c>posX</c>/<c>posY</c> at its own <c>X0</c>/<c>Y0</c> (same reseed <see cref="SetLayers"/>
    /// performs), while keeping every <see cref="CellularLayerDefinition"/>, this service's
    /// <see cref="LayersVersion"/>, the pushed <c>WaveLut</c> and the push contract untouched - same
    /// role as the sibling mechanism's own <c>ResetLayerRuntimeState</c>, called explicitly by
    /// <see cref="Application.Components.CellularLayerComponent"/> whenever it re-resolves.
    /// </summary>
    public void ResetLayerRuntimeState()
    {
        for (var i = 0; i < _layers.Length; i++)
        {
            ref var layer = ref _layers[i];
            layer.AnimFrameTimer = 0;
            layer.AnimFrameCounter = 0;
            layer.WaveTick = 0;

            for (var c = 0; c < layer.Cells.Length; c++)
            {
                layer.Cells[c] = new CellRuntime
                {
                    PosX = layer.Definition.Cells[c].X0,
                    PosY = layer.Definition.Cells[c].Y0,
                };
            }
        }
    }
}
