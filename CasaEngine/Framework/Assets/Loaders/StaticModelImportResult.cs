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
}
