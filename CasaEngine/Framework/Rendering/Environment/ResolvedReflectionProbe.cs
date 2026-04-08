using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Reflection probe chosen for a given view position with a normalized contribution weight.
/// </summary>
public readonly struct ResolvedReflectionProbe
{
    public Guid ProbeId { get; init; }

    public Guid EnvironmentAssetId { get; init; }

    public Guid SpecularCubemapAssetId { get; init; }

    public Vector3 Position { get; init; }

    public float InfluenceRadius { get; init; }

    public float Weight { get; init; }
}