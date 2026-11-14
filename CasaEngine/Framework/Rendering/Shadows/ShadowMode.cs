namespace CasaEngine.Framework.Rendering.Shadows;

/// <summary>
/// Selects how the shadow factor of the shadow-casting directional light is applied to a lit surface.
/// </summary>
public enum ShadowMode
{
    /// <summary>
    /// The shadow factor scales the direct diffuse and specular contribution of the shadowed light. The ambient term, the
    /// emissive colour and the reflections are not shadowed. This is the historical behaviour.
    /// </summary>
    OccludeDirectLight,

    /// <summary>
    /// The light is not occluded; the final colour of the pixel, excluding the emissive colour (lit colour, specular and
    /// reflection), is multiplied by <c>lerp(1, ShadowColor, 1 - shadowFactor)</c>.
    /// </summary>
    MultiplyLitColor,
}
