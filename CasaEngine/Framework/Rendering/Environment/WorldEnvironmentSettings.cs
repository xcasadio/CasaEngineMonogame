using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Mutable world-scoped environment settings used as the source of truth for a scene.
/// </summary>
public sealed class WorldEnvironmentSettings
{
    private int _version;

    public EnvironmentType Type { get; set; } = EnvironmentType.None;

    public EnvironmentBackgroundMode BackgroundMode { get; set; } = EnvironmentBackgroundMode.LegacyClearColor;

    public Color BackgroundColor { get; set; } = Color.CornflowerBlue;

    public Guid EnvironmentAssetId { get; set; } = Guid.Empty;

    public Guid BackgroundCubemapAssetId { get; set; } = Guid.Empty;

    public XnaTextureCube? BackgroundCubemap { get; set; }

    public XnaTextureCube? SpecularEnvironmentCubemap { get; set; }

    public Vector3 AmbientColor { get; set; } = new(0.05f, 0.05f, 0.05f);

    public float AmbientIntensity { get; set; } = 1.0f;

    public float SpecularIntensity { get; set; } = 1.0f;

    public bool IsDirty { get; private set; }

    public int Version => _version;

    public void ResetToDefaults()
    {
        Type = EnvironmentType.None;
        BackgroundMode = EnvironmentBackgroundMode.LegacyClearColor;
        BackgroundColor = Color.CornflowerBlue;
        EnvironmentAssetId = Guid.Empty;
        BackgroundCubemapAssetId = Guid.Empty;
        BackgroundCubemap = null;
        SpecularEnvironmentCubemap = null;
        AmbientColor = new Vector3(0.05f, 0.05f, 0.05f);
        AmbientIntensity = 1.0f;
        SpecularIntensity = 1.0f;
        IsDirty = false;
    }

    public void Load(JObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        ResetToDefaults();

        if (element.TryGetValue("type", StringComparison.OrdinalIgnoreCase, out var typeNode))
        {
            Type = typeNode.GetEnum<EnvironmentType>();
        }

        if (element.TryGetValue("background_mode", StringComparison.OrdinalIgnoreCase, out var backgroundModeNode))
        {
            BackgroundMode = backgroundModeNode.GetEnum<EnvironmentBackgroundMode>();
        }

        if (element.TryGetValue("background_color", StringComparison.OrdinalIgnoreCase, out var backgroundColorNode))
        {
            BackgroundColor = backgroundColorNode.GetColor();
        }

        if (element.TryGetValue("environment_asset_id", StringComparison.OrdinalIgnoreCase, out var environmentAssetIdNode))
        {
            EnvironmentAssetId = environmentAssetIdNode.GetGuid();
        }

        if (element.TryGetValue("background_cubemap_asset_id", StringComparison.OrdinalIgnoreCase, out var backgroundCubemapAssetIdNode))
        {
            BackgroundCubemapAssetId = backgroundCubemapAssetIdNode.GetGuid();
        }

        if (element.TryGetValue("ambient_color", StringComparison.OrdinalIgnoreCase, out var ambientColorNode))
        {
            AmbientColor = ambientColorNode.GetVector3();
        }

        if (element.TryGetValue("ambient_intensity", StringComparison.OrdinalIgnoreCase, out var ambientIntensityNode))
        {
            AmbientIntensity = ambientIntensityNode.GetSingle();
        }

        if (element.TryGetValue("specular_intensity", StringComparison.OrdinalIgnoreCase, out var specularIntensityNode))
        {
            SpecularIntensity = specularIntensityNode.GetSingle();
        }
    }

    public void MarkDirty()
    {
        IsDirty = true;
        _version++;
    }

    public void MarkClean()
    {
        IsDirty = false;
    }

    public WorldEnvironmentSettings Clone()
    {
        return new WorldEnvironmentSettings
        {
            Type = Type,
            BackgroundMode = BackgroundMode,
            BackgroundColor = BackgroundColor,
            EnvironmentAssetId = EnvironmentAssetId,
            BackgroundCubemapAssetId = BackgroundCubemapAssetId,
            BackgroundCubemap = BackgroundCubemap,
            SpecularEnvironmentCubemap = SpecularEnvironmentCubemap,
            AmbientColor = AmbientColor,
            AmbientIntensity = AmbientIntensity,
            SpecularIntensity = SpecularIntensity,
        };
    }

    public void CopyFrom(WorldEnvironmentSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Type = other.Type;
        BackgroundMode = other.BackgroundMode;
        BackgroundColor = other.BackgroundColor;
        EnvironmentAssetId = other.EnvironmentAssetId;
        BackgroundCubemapAssetId = other.BackgroundCubemapAssetId;
        BackgroundCubemap = other.BackgroundCubemap;
        SpecularEnvironmentCubemap = other.SpecularEnvironmentCubemap;
        AmbientColor = other.AmbientColor;
        AmbientIntensity = other.AmbientIntensity;
        SpecularIntensity = other.SpecularIntensity;
        if (other.IsDirty)
        {
            MarkDirty();
        }
        else
        {
            MarkClean();
        }
    }
}