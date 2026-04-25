namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Lightweight artist-friendly parameters for a simple gradient sky.
/// </summary>
public sealed class ProceduralSkySettings
{
    public Color ZenithColor { get; set; } = new(24, 52, 110);

    public Color HorizonColor { get; set; } = new(216, 191, 153);

    public Color GroundColor { get; set; } = new(64, 52, 40);

    public float SkyExponent { get; set; } = 0.75f;

    public float GroundExponent { get; set; } = 1.25f;

    public int CubemapSize { get; set; } = ProceduralSkyEnvironmentGenerator.DefaultCubemapSize;

    public ProceduralSkySettings Clone()
    {
        return new ProceduralSkySettings
        {
            ZenithColor = ZenithColor,
            HorizonColor = HorizonColor,
            GroundColor = GroundColor,
            SkyExponent = SkyExponent,
            GroundExponent = GroundExponent,
            CubemapSize = CubemapSize,
        };
    }
}