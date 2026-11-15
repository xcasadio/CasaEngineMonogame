using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Result of importing a 3-D file as a <see cref="Rendering.Models.StaticModel"/>:
/// the model plus the per-material metadata gathered at import time.
/// </summary>
public sealed class StaticModelImportResult
{
    public StaticModelImportResult(Rendering.Models.StaticModel model, IReadOnlyList<StaticModelImportedMaterial> materials)
    {
        Model = model;
        Materials = materials;
    }

    public Rendering.Models.StaticModel Model { get; }

    public IReadOnlyList<StaticModelImportedMaterial> Materials { get; }
}

/// <summary>
/// Per-material metadata captured while importing a 3-D model. Used by the editor to
/// author engine materials and to link imported textures.
/// </summary>
public sealed class StaticModelImportedMaterial
{
    public int MaterialIndex { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DiffuseTextureFilePath { get; set; }

    public string NormalTextureFilePath { get; set; }

    public string ReflectionTextureFilePath { get; set; }

    public string EffectFilePath { get; set; }

    public int LegacyTechniqueIndex { get; set; } = -1;

    public LegacyMaterialSurfaceIntent SurfaceIntent { get; set; } = LegacyMaterialSurfaceIntent.OpaqueLit;

    public bool UsesReflection { get; set; }

    public bool AlphaCutoutHint { get; set; }

    public bool BrightAmbientHint { get; set; }

    public Vector3 AmbientColor { get; set; } = Vector3.Zero;

    public Color DiffuseColor { get; set; } = Color.White;

    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;

    public Vector3 SpecularColor { get; set; } = new(0.5f);

    public float SpecularPower { get; set; } = 16.0f;

    // glTF PBR channels (ADR-0111). Additive: the legacy fields above keep their meaning.

    /// <summary>glTF <c>metallicFactor</c> (1 when the material has no metallic-roughness channel, 0 for an unlit one).</summary>
    public float MetallicFactor { get; set; } = 1.0f;

    /// <summary>glTF <c>roughnessFactor</c> (1 when the material has no metallic-roughness channel).</summary>
    public float RoughnessFactor { get; set; } = 1.0f;

    /// <summary>Full path of the external metallic-roughness image (green = roughness, blue = metallic), or null.</summary>
    public string MetallicRoughnessTextureFilePath { get; set; }

    /// <summary>Full path of the external occlusion image (red channel), or null.</summary>
    public string OcclusionTextureFilePath { get; set; }

    /// <summary>Full path of the external emissive image, or null.</summary>
    public string EmissiveTextureFilePath { get; set; }

    /// <summary>glTF <c>normalTexture.scale</c> (default 1).</summary>
    public float NormalScale { get; set; } = 1.0f;

    /// <summary>glTF <c>occlusionTexture.strength</c> (default 1).</summary>
    public float OcclusionStrength { get; set; } = 1.0f;

    /// <summary>glTF <c>alphaMode</c> (default opaque).</summary>
    public StaticModelAlphaMode AlphaMode { get; set; } = StaticModelAlphaMode.Opaque;

    /// <summary>glTF <c>alphaCutoff</c> (default 0.5); only meaningful when <see cref="AlphaMode"/> is <see cref="StaticModelAlphaMode.Mask"/>.</summary>
    public float AlphaCutoff { get; set; } = 0.5f;

    /// <summary>glTF <c>doubleSided</c> (default false).</summary>
    public bool DoubleSided { get; set; }
}

/// <summary>glTF <c>alphaMode</c> of an imported material.</summary>
public enum StaticModelAlphaMode
{
    Opaque = 0,
    Mask = 1,
    Blend = 2,
}
