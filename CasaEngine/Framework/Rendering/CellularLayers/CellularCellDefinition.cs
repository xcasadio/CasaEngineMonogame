namespace CasaEngine.Framework.Rendering.CellularLayers;

/// <summary>
/// Static description of one cell of a cellular backdrop layer (docs/engine/cellular-layers.md,
/// Alundra's <c>ScrollScreen.cs:487-533</c>) - a rectangle cut from the layer's own tile sheet, drawn
/// as its own sprite. Up to 200 of these per <see cref="CellularLayerDefinition"/>
/// (<see cref="CellularLayerService.CellMax"/>). The two dead bytes the original also carries
/// (<c>Unused0</c>/<c>Unused1</c>) are not represented - the converter already drops them
/// (docs/plan-e9d-mode-cellulaire.md §1.1).
/// </summary>
public struct CellularCellDefinition
{
    /// <summary>Which of the layer's <see cref="CellularLayerDefinition.SheetTextureAssetIds"/> this
    /// cell samples from.</summary>
    public int PalDex;

    /// <summary>Source rectangle in the 256x256 tile sheet, in bytes - inclusive on both ends
    /// (width = <c>U1 - U0 + 1</c>, height = <c>V1 - V0 + 1</c>).</summary>
    public int U0;

    public int V0;

    public int U1;

    public int V1;

    public CellularCellType Type;

    /// <summary>Nominal position (<c>Int16</c> range in the original) - also this cell's seeded
    /// starting <c>posX</c>/<c>posY</c> at load (docs/plan-e9d-mode-cellulaire.md §1.5 ter, "Amorçage").</summary>
    public int X0;

    public int Y0;

    /// <summary>Camera parallax factor on X, per cell: <c>cameraX * CamXNum / CamXDen</c>, truncated.
    /// A zero denominator disables this axis' parallax contribution.</summary>
    public int CamXNum;

    public int CamXDen;

    /// <summary>Camera parallax factor on Y, per cell, same rule as <see cref="CamXNum"/>/<see cref="CamXDen"/>.</summary>
    public int CamYNum;

    public int CamYDen;

    /// <summary>Drift added to <c>posX</c> every tick, plus one further step every <c>|PeriodX|</c>
    /// ticks - direction is an OR of signs recomputed every tick (<see cref="CellularLayerService.ComputePeriodStepOr"/>),
    /// NOT the XOR-computed-once-at-load rule the sibling scrolling-layer mechanism uses for its own
    /// auto-scroll (docs/plan-e9d-mode-cellulaire.md §1.4 - do not unify the two).</summary>
    public int DX;

    public int PeriodX;

    public int DY;

    public int PeriodY;
}
