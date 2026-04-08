using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Environment;

public sealed class EnvironmentAsset : ObjectBase
{
    public EnvironmentType Type { get; set; } = EnvironmentType.Cubemap;

    public Guid PanoramaAssetId { get; set; } = Guid.Empty;

    public int PanoramaCubemapSize { get; set; } = PanoramaEnvironmentGenerator.DefaultCubemapSize;

    public Guid BackgroundCubemapAssetId { get; set; } = Guid.Empty;

    public Guid SpecularCubemapAssetId { get; set; } = Guid.Empty;

    public Vector3 AmbientColor { get; set; } = new(0.05f, 0.05f, 0.05f);

    public float AmbientIntensity { get; set; } = 1.0f;

    public float SpecularIntensity { get; set; } = 1.0f;

    public override void Load(JObject element)
    {
        base.Load(element);
        EnvironmentAssetJsonSerializer.Load(this, element);
    }
}