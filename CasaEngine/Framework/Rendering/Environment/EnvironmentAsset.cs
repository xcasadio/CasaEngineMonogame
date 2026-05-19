using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Environment;

public sealed class EnvironmentAsset : ObjectBase
{
    private Vector3 _ambientColor = new(0.05f, 0.05f, 0.05f);
    private float _ambientIntensity = 1.0f;
    private float _specularIntensity = 1.0f;

    public EnvironmentType Type { get; set; } = EnvironmentType.Cubemap;

    public ProceduralSkySettings ProceduralSky { get; set; } = new();

    public PhysicalAtmosphereSettings PhysicalAtmosphere { get; set; } = new();

    public Guid PanoramaAssetId { get; set; } = Guid.Empty;

    public int PanoramaCubemapSize { get; set; } = PanoramaEnvironmentGenerator.DefaultCubemapSize;

    public Guid BackgroundCubemapAssetId { get; set; } = Guid.Empty;

    public Guid SpecularCubemapAssetId { get; set; } = Guid.Empty;

    public Vector3 AmbientColor
    {
        get => _ambientColor;
        set => _ambientColor = EnvironmentLightingSanitizer.NormalizeAmbientColor(value);
    }

    public float AmbientIntensity
    {
        get => _ambientIntensity;
        set => _ambientIntensity = EnvironmentLightingSanitizer.NormalizeIntensity(value);
    }

    public float SpecularIntensity
    {
        get => _specularIntensity;
        set => _specularIntensity = EnvironmentLightingSanitizer.NormalizeIntensity(value);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        EnvironmentAssetJsonSerializer.Load(this, element);
    }
}