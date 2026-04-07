using Microsoft.Xna.Framework;
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

    public Vector3 AmbientColor { get; set; } = new(0.2f, 0.2f, 0.2f);

    public float AmbientIntensity { get; set; } = 1.0f;

    public float SpecularIntensity { get; set; } = 1.0f;

    public bool IsDirty { get; private set; }

    public int Version => _version;

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
    }
}