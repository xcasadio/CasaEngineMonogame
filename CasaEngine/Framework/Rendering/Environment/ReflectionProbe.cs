using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Runtime definition of a local reflection probe.
/// V1 keeps the model intentionally small so probes can be selected without baking or editor authoring.
/// </summary>
public sealed class ReflectionProbe
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public Vector3 Position { get; set; } = Vector3.Zero;

    public float InfluenceRadius { get; set; } = 10.0f;

    public float BlendDistance { get; set; } = 2.0f;

    public Guid EnvironmentAssetId { get; set; } = Guid.Empty;

    public Guid SpecularCubemapAssetId { get; set; } = Guid.Empty;

    /// <summary>
    /// Cube supplied at run time (not serialized). When set it is used before <see cref="SpecularCubemapAssetId"/> and <see cref="EnvironmentAssetId"/>,
    /// like <c>WorldEnvironmentSettings.SpecularEnvironmentCubemap</c> for the global environment (ADR-0110, section Reflection probes).
    /// </summary>
    public Microsoft.Xna.Framework.Graphics.TextureCube SpecularCubemap { get; set; }
}