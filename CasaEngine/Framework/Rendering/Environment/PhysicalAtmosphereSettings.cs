using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Lightweight physically-inspired sky parameters that expose sun, atmosphere, and ground transition controls.
/// </summary>
public sealed class PhysicalAtmosphereSettings
{
    public Vector3 SunDirection { get; set; } = Vector3.Normalize(new Vector3(0.25f, 0.85f, -0.45f));

    public Color SpaceColor { get; set; } = new(8, 16, 34);

    public Color RayleighColor { get; set; } = new(86, 144, 255);

    public Color MieColor { get; set; } = new(255, 226, 194);

    public Color SunsetColor { get; set; } = new(255, 170, 112);

    public Color GroundColor { get; set; } = new(52, 46, 40);

    public float AtmosphereDensity { get; set; } = 1.0f;

    public float RayleighDensity { get; set; } = 1.0f;

    public float MieDensity { get; set; } = 0.35f;

    public float MieAnisotropy { get; set; } = 0.76f;

    public float SunIntensity { get; set; } = 4.0f;

    public float SunDiscSize { get; set; } = 0.02f;

    public float GroundFalloff { get; set; } = 1.15f;

    public float SpaceFalloff { get; set; } = 1.6f;

    public int CubemapSize { get; set; } = PhysicalAtmosphereEnvironmentGenerator.DefaultCubemapSize;

    public PhysicalAtmosphereSettings Clone()
    {
        return new PhysicalAtmosphereSettings
        {
            SunDirection = SunDirection,
            SpaceColor = SpaceColor,
            RayleighColor = RayleighColor,
            MieColor = MieColor,
            SunsetColor = SunsetColor,
            GroundColor = GroundColor,
            AtmosphereDensity = AtmosphereDensity,
            RayleighDensity = RayleighDensity,
            MieDensity = MieDensity,
            MieAnisotropy = MieAnisotropy,
            SunIntensity = SunIntensity,
            SunDiscSize = SunDiscSize,
            GroundFalloff = GroundFalloff,
            SpaceFalloff = SpaceFalloff,
            CubemapSize = CubemapSize,
        };
    }
}