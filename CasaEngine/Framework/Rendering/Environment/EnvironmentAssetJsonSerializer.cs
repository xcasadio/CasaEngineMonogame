using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Environment;

public static class EnvironmentAssetJsonSerializer
{
    public static void Save(EnvironmentAsset environmentAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(environmentAsset);
        ArgumentNullException.ThrowIfNull(node);

        node["id"] = environmentAsset.Id.ToString();
        node["name"] = environmentAsset.Name;
        node["type"] = environmentAsset.Type.ToString();
        node["panorama_asset_id"] = environmentAsset.PanoramaAssetId.ToString();
        node["panorama_cubemap_size"] = environmentAsset.PanoramaCubemapSize;
        node["background_cubemap_asset_id"] = environmentAsset.BackgroundCubemapAssetId.ToString();
        node["specular_cubemap_asset_id"] = environmentAsset.SpecularCubemapAssetId.ToString();
        node["ambient_color"] = SaveVector3(environmentAsset.AmbientColor);
        node["ambient_intensity"] = environmentAsset.AmbientIntensity;
        node["specular_intensity"] = environmentAsset.SpecularIntensity;
    }

    public static void Load(EnvironmentAsset environmentAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(environmentAsset);
        ArgumentNullException.ThrowIfNull(node);

        environmentAsset.Type = node["type"] is { } typeToken
            ? typeToken.GetEnum<EnvironmentType>()
            : EnvironmentType.Cubemap;
        environmentAsset.PanoramaAssetId = node["panorama_asset_id"]?.GetGuid() ?? Guid.Empty;
        environmentAsset.PanoramaCubemapSize = PanoramaEnvironmentGenerator.NormalizeCubemapSize(
            node["panorama_cubemap_size"]?.GetInt32() ?? PanoramaEnvironmentGenerator.DefaultCubemapSize);
        environmentAsset.BackgroundCubemapAssetId = node["background_cubemap_asset_id"]?.GetGuid() ?? Guid.Empty;
        environmentAsset.SpecularCubemapAssetId = node["specular_cubemap_asset_id"]?.GetGuid() ?? Guid.Empty;
        environmentAsset.AmbientColor = node["ambient_color"] is { } ambientColorToken
            ? ambientColorToken.GetVector3()
            : new Vector3(0.05f, 0.05f, 0.05f);
        environmentAsset.AmbientIntensity = node["ambient_intensity"]?.GetSingle() ?? 1.0f;
        environmentAsset.SpecularIntensity = node["specular_intensity"]?.GetSingle() ?? 1.0f;
    }

    private static JObject SaveVector3(Vector3 value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
        };
    }
}