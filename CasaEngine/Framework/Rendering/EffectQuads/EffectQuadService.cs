namespace CasaEngine.Framework.Rendering.EffectQuads;

/// <summary>
/// Engine-side holder of the effect quads of the game (docs/engine/effect-quads.md, ADR-0071): the texture asset ids of the sheets the
/// quads sample from, and the last pushed frame of <see cref="EffectQuad"/>s. Deliberately free of any GPU type -
/// <see cref="Application.Components.EffectQuadComponent"/> resolves the sheets and submits the quads, the same house split as
/// <see cref="CellularLayers.CellularLayerService"/>.
///
/// The game DLL pushes from <c>GameManager.UpdateWorld</c>: <see cref="SetSheets"/> when a bank is loaded, <see cref="SetFrame"/> every
/// drawn frame, <see cref="Clear"/> when the bank goes away. The pushed frame stays until the next <see cref="SetFrame"/> or
/// <see cref="Clear"/>, so a frame in which the DLL pushes nothing draws the previous one again (the DLL pushes an empty frame to draw
/// nothing).
/// </summary>
public sealed class EffectQuadService
{
    private System.Guid[] _sheetIds = System.Array.Empty<System.Guid>();
    private EffectQuad[] _quads = System.Array.Empty<EffectQuad>();
    private int _quadCount;

    /// <summary>Strictly increases on every <see cref="SetSheets"/> and <see cref="Clear"/> call, starting at 0 - never reset.
    /// <see cref="Application.Components.EffectQuadComponent"/> re-resolves its textures whenever this changes.</summary>
    public int SheetsVersion { get; private set; }

    /// <summary>Number of sheets given to <see cref="SetSheets"/>, absent ones included.</summary>
    public int SheetCount => _sheetIds.Length;

    /// <summary>Number of quads of the pushed frame.</summary>
    public int QuadCount => _quadCount;

    /// <summary>The texture asset id of the sheet <paramref name="index"/>; <see cref="System.Guid.Empty"/> for an absent sheet.</summary>
    public System.Guid GetSheetId(int index)
    {
        return _sheetIds[index];
    }

    /// <summary>The quad <paramref name="index"/> of the pushed frame.</summary>
    public EffectQuad GetQuad(int index)
    {
        if ((uint)index >= (uint)_quadCount)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index));
        }

        return _quads[index];
    }

    /// <summary>
    /// Replaces the sheets (copied): the texture asset id of each sheet, <see cref="System.Guid.Empty"/> for a sheet the producer does
    /// not have. Raises <see cref="SheetsVersion"/> even when the ids are the same, the caller decides when its sheets change.
    /// </summary>
    public void SetSheets(System.ReadOnlySpan<System.Guid> sheetTextureAssetIds)
    {
        _sheetIds = sheetTextureAssetIds.ToArray();
        SheetsVersion++;
    }

    /// <summary>Replaces the pushed frame (copied). It stays until the next call or <see cref="Clear"/>.</summary>
    public void SetFrame(System.ReadOnlySpan<EffectQuad> quads)
    {
        if (_quads.Length < quads.Length)
        {
            _quads = new EffectQuad[quads.Length];
        }

        quads.CopyTo(_quads);
        _quadCount = quads.Length;
    }

    /// <summary>Empties the sheets and the pushed frame and raises <see cref="SheetsVersion"/>.</summary>
    public void Clear()
    {
        _sheetIds = System.Array.Empty<System.Guid>();
        _quadCount = 0;
        SheetsVersion++;
    }
}
