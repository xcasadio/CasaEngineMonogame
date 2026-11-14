namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Selects the operator that maps the high-dynamic-range scene colour to the display range in
/// <see cref="ColorPipelineMode.LinearHdr"/>.
/// </summary>
public enum TonemapOperator
{
    /// <summary>
    /// Narkowicz's fit of the ACES filmic curve, clamped to [0, 1].
    /// </summary>
    AcesApprox,

    /// <summary>
    /// The simple Reinhard operator, <c>c / (1 + c)</c>.
    /// </summary>
    Reinhard,
}
