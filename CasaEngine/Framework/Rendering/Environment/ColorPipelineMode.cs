namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Selects how a world's colours are produced and presented.
/// </summary>
public enum ColorPipelineMode
{
    /// <summary>
    /// The historical pipeline: colours are lit and written in display space straight into the view's target. Every
    /// existing image is unchanged in this mode.
    /// </summary>
    Legacy,

    /// <summary>
    /// Colours are decoded to linear light, lit and accumulated in a high-dynamic-range scene target, then tonemapped
    /// into the view's destination before the user interface is composed.
    /// </summary>
    LinearHdr,
}
