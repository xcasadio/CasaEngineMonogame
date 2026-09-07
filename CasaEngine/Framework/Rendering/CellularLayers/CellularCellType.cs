namespace CasaEngine.Framework.Rendering.CellularLayers;

/// <summary>
/// The original engine's per-cell behaviour tag (Alundra's <c>ScrollScreen.cs:369-375</c>
/// <c>CellType</c>) - deliberately non-contiguous, there is no value 3
/// (docs/plan-e9d-mode-cellulaire.md §1.2).
/// </summary>
public enum CellularCellType
{
    /// <summary>Drift + period step, camera parallax, wraps on both axes within the 320x240 screen.</summary>
    Normal = 0,

    /// <summary>
    /// Empty case in the original (<c>GraphicManager.cs:1104-1105</c>) - draws nothing, advances no
    /// state. Zero occurrences across the shipped corpus (docs/plan-e9d-mode-cellulaire.md D5, mirrors
    /// how D-E9b-14 froze the sibling mechanism's own unreachable opcode).
    /// </summary>
    ScriptTrack = 1,

    /// <summary>Same drift and X wrap as <see cref="Normal"/>, but respawns at a random X at the top
    /// instead of wrapping on Y.</summary>
    FallRespawn = 2,

    /// <summary>Stateless: X comes from two <c>WaveLut</c> samples, ignores camera parallax, never
    /// moves in Y.</summary>
    WaveX = 4
}
