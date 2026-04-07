namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Identifies the source model that produces a scene environment.
/// </summary>
public enum EnvironmentType
{
    None,
    Cubemap,
    PanoramaHdr,
    Procedural,
    PhysicalAtmosphere,
}