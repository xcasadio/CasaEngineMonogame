using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.CellularLayers;

/// <summary>
/// Static description of one cellular backdrop layer (docs/engine/cellular-layers.md): V-animation
/// cadence, the six <c>WaveX</c> parameters, its cells and the per-<c>PalDex</c> tile sheet textures
/// baked by the converter (D2). Pushed once per world load via <see cref="CellularLayerService.SetLayers"/>;
/// the policy that builds these (which layers exist, blend/tint per <c>Ground</c>) lives in the
/// consuming game DLL, not here - same split as <see cref="CellularLayerService"/>'s sibling
/// <see cref="ScrollingLayers.ScrollingLayerService"/> (plan-e9d-mode-cellulaire.md D3).
/// </summary>
public struct CellularLayerDefinition
{
    /// <summary>Tie-breaker carried into the render sort key's stable id slot, and identifies the
    /// layer in logging - has no other meaning to this mechanism.</summary>
    public int LayerId;

    /// <summary>Ticks the V-animation counter holds a frame before advancing (0 = advances every tick).</summary>
    public int AnimTimer;

    /// <summary>Number of V-animation phases: <c>phase = (AnimFrameCounter &lt;&lt; 8) / AnimNum</c>,
    /// applied to every cell's sampled V as <c>(V0 + phase) &amp; 0xFF</c> (docs/plan-e9d-mode-cellulaire.md
    /// §1.5 bis) - NOT a frame-texture count like the sibling mechanism's <c>FrameTextureAssetIds</c>.</summary>
    public int AnimNum;

    /// <summary>
    /// The original's own front/back bucket (<c>GraphicManager.cs:825-826</c>). Decision D6: the
    /// component derives the render pass from this flag - <c>true</c> -&gt; <see cref="RenderPass2D.Effects"/>,
    /// <c>false</c> -&gt; <see cref="RenderPass2D.Background"/> - the same routing
    /// <c>AlundraBackdropStage.BuildDefinitions</c> already applies for the sibling mechanism. All 92
    /// shipped cellular layers measure <c>Ground = true</c>.
    /// </summary>
    public bool Ground;

    public SpriteBlendMode Blend;

    public Color Tint;

    /// <summary><c>WaveX</c>'s first term: <c>WaveLut[(Y0 * AWaveY) &amp; 0xFF] * WaveLut[(WaveTick * AWavePhase) &amp; 0xFF] * AWaveAmp</c>.</summary>
    public int AWaveY;

    public int AWavePhase;

    public int AWaveAmp;

    /// <summary><c>WaveX</c>'s second term: <c>WaveLut[(Y0 * BWaveY + WaveTick * BWavePhase) &amp; 0xFF] * BWaveWeight</c>.</summary>
    public int BWaveY;

    public int BWavePhase;

    public int BWaveWeight;

    /// <summary>Up to 200 cells (<see cref="CellularLayerService.CellMax"/>), in original draw order.
    /// A <see cref="CellularCellType.WaveX"/> cell met while the map's <c>WaveLut</c> is empty is
    /// skipped on its own; every other cell in the layer still advances and draws. The original
    /// writes that guard as a <c>break</c> inside a <c>switch</c> case, which leaves the switch and
    /// not the cell loop (GraphicManager.cs:1190-1193).</summary>
    public CellularCellDefinition[] Cells;

    /// <summary>One baked 256x256 tile-sheet texture id per <c>PalDex</c> actually used by this
    /// layer's cells (D2) - index directly by <see cref="CellularCellDefinition.PalDex"/>. An
    /// empty/nil id (<see cref="System.Guid.Empty"/>) resolves to a null sheet texture, same fallback
    /// convention as the sibling mechanism's frame ids.</summary>
    public System.Guid[] SheetTextureAssetIds;

    public int SortingLayer;

    public int OrderInLayer;
}
