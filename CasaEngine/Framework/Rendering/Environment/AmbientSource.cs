namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Selects where the ambient term of lit materials comes from.
/// </summary>
public enum AmbientSource
{
    /// <summary>
    /// The ambient light is sampled from the specular environment cube (times the environment ambient colour) when the
    /// world has one, and is the flat ambient colour otherwise. This is the historical behaviour.
    /// </summary>
    Auto,

    /// <summary>
    /// The ambient light is always the flat <c>AmbientColor * AmbientIntensity</c>, even when a reflection cube is bound.
    /// The cube keeps driving the reflections.
    /// </summary>
    Flat,
}
