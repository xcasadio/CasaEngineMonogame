namespace CasaEngine.Framework.Rendering.CellularLayers;

/// <summary>
/// One layer's per-tick cadence state, as of the most recent <see cref="CellularLayerService.Advance"/>
/// call - read-only snapshot returned by <see cref="CellularLayerService.TryGetLayerState"/>. Mirrors
/// the original's own per-layer accumulators, computed once per layer per tick before its cell loop
/// (Alundra's <c>GraphicManager.cs:988-997</c>).
/// </summary>
public readonly struct CellularLayerState
{
    public CellularLayerState(int animFrameTimer, int animFrameCounter, int phase, byte waveTick)
    {
        AnimFrameTimer = animFrameTimer;
        AnimFrameCounter = animFrameCounter;
        Phase = phase;
        WaveTick = waveTick;
    }

    /// <summary>Ticks accumulated since the V-animation counter last advanced.</summary>
    public int AnimFrameTimer { get; }

    /// <summary>Current V-animation frame index, wrapped modulo the layer's <c>AnimNum</c>.</summary>
    public int AnimFrameCounter { get; }

    /// <summary><c>(AnimFrameCounter &lt;&lt; 8) / AnimNum</c> - added to every cell's <c>V0</c> before
    /// the <c>&amp; 0xFF</c> wrap to get the sampled source row (docs/plan-e9d-mode-cellulaire.md
    /// §1.5 bis). <see cref="CellularLayerService.ComputePhase"/> is the pure function this is computed
    /// from.</summary>
    public int Phase { get; }

    /// <summary>Byte counter advanced every tick, wrapping mod 256 - the original's own <c>WaveTick</c>,
    /// read by every <see cref="CellularCellType.WaveX"/> cell in this layer.</summary>
    public byte WaveTick { get; }
}
