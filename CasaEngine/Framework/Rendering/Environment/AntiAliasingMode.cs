namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Selects the screen-space anti-aliasing applied after the tonemapping in <see cref="ColorPipelineMode.LinearHdr"/>
/// (ADR-0112). Ignored in <see cref="ColorPipelineMode.Legacy"/>.
/// </summary>
public enum AntiAliasingMode
{
    /// <summary>No anti-aliasing pass.</summary>
    None,

    /// <summary>Fast approximate anti-aliasing: a post-process filter on the tonemapped scene colour.</summary>
    Fxaa,
}
