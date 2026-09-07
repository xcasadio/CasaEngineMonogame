namespace CasaEngine.Framework.Rendering.CellularLayers;

/// <summary>
/// The one tunable this mechanism needs beyond the layer/cell definitions themselves - unlike the
/// sibling scrolling-layer mechanism, the screen a cellular layer's cells are placed against is a
/// fixed original-engine constant (<see cref="CellularLayerService.ScreenWidth"/>/
/// <see cref="CellularLayerService.ScreenHeight"/>), never configurable. Pushed once per world load
/// alongside the layer definitions.
/// </summary>
public readonly struct CellularLayerConfiguration
{
    public CellularLayerConfiguration(float backgroundDepth = 1f)
    {
        BackgroundDepth = backgroundDepth;
    }

    /// <summary>
    /// How far a <see cref="CellularLayerDefinition.Ground"/> == false layer (rendered as
    /// <see cref="Depth.RenderPass2D.Background"/>) recedes behind the camera target along Z, same
    /// convention and rationale as <see cref="ScrollingLayers.ScrollingLayerConfiguration.BackgroundDepth"/>
    /// (plan-e9c-defauts-321.md D-E9c-5). No shipped cellular layer currently exercises this branch -
    /// all 92 measure <c>Ground = true</c> - but the policy exists for parity with the sibling
    /// mechanism should a future layer need it.
    /// </summary>
    public float BackgroundDepth { get; }
}
