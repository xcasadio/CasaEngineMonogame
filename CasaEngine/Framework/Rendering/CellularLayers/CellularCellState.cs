namespace CasaEngine.Framework.Rendering.CellularLayers;

/// <summary>
/// One cell's resolved draw position, as of the most recent <see cref="CellularLayerService.Advance"/>
/// call - read-only snapshot returned by <see cref="CellularLayerService.TryGetCellState"/>. Screen
/// space, 320x240, top-left origin - <see cref="Application.Components.CellularLayerComponent"/>
/// converts it to world space at submission time, the same split the sibling mechanism uses for its
/// own <c>LayerOffsetX</c>/<c>LayerOffsetY</c>.
/// </summary>
public readonly struct CellularCellState
{
    public CellularCellState(bool shouldDraw, int drawX, int drawY)
    {
        ShouldDraw = shouldDraw;
        DrawX = drawX;
        DrawY = drawY;
    }

    /// <summary>
    /// False for a <see cref="CellularCellType.ScriptTrack"/> cell (draws nothing, by design - D5) and
    /// for a <see cref="CellularCellType.WaveX"/> cell reached while the map's <c>WaveLut</c> is empty.
    /// True otherwise.
    /// </summary>
    public bool ShouldDraw { get; }

    /// <summary>Screen-space X (the original's own <c>sx</c>/<c>x</c>) at which this cell was last drawn.</summary>
    public int DrawX { get; }

    /// <summary>Screen-space Y (the original's own <c>sy</c>/<c>y</c>).</summary>
    public int DrawY { get; }
}
