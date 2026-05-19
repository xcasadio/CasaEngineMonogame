using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Materials.Definitions;

internal static class BuiltInMaterialDefinitions
{
    public static IReadOnlyList<MaterialDefinition> CreateAll()
        => new MaterialDefinition[]
        {
            CreateLitDiffuseDefinition(),
            CreateUnlitTextureDefinition(),
        };

    private static MaterialDefinition CreateLitDiffuseDefinition()
        => new(
            id: "lit-diffuse",
            displayName: "Lit Diffuse",
            runtimeMaterialType: typeof(LitDiffuseMaterial),
            description: "Lambert + specular material with optional base color and normal map textures.",
            properties: new[]
            {
                new MaterialPropertyDefinition(
                    key: "base_color_texture",
                    displayName: "Base Color",
                    valueType: MaterialPropertyType.Texture,
                    group: MaterialPropertyGroup.Textures,
                    flags: MaterialPropertyFlags.AssetReference | MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsShaderCompilation,
                    defaultValue: Guid.Empty,
                    description: "Texture asset used as the base color input.",
                    legacyAliases: new[] { "BasColor_asset_id", "albedo_asset_id" },
                    assetKind: "texture"),
                new MaterialPropertyDefinition(
                    key: "normal_texture",
                    displayName: "Normal Map",
                    valueType: MaterialPropertyType.Texture,
                    group: MaterialPropertyGroup.Textures,
                    flags: MaterialPropertyFlags.AssetReference | MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsShaderCompilation,
                    defaultValue: Guid.Empty,
                    description: "Optional tangent-space normal map.",
                    legacyAliases: new[] { "normal_map_asset_id", "texture_normal_asset_id" },
                    assetKind: "texture"),
                new MaterialPropertyDefinition(
                    key: "reflection_texture",
                    displayName: "Reflection Cubemap",
                    valueType: MaterialPropertyType.Texture,
                    group: MaterialPropertyGroup.Textures,
                    flags: MaterialPropertyFlags.AssetReference | MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsShaderCompilation,
                    defaultValue: Guid.Empty,
                    description: "Optional legacy reflection cubemap sampled by reflective static materials.",
                    legacyAliases: new[] { "texture_reflection_asset_id" },
                    assetKind: "dds"),
                new MaterialPropertyDefinition(
                    key: "diffuse_color",
                    displayName: "Diffuse Color",
                    valueType: MaterialPropertyType.Color,
                    group: MaterialPropertyGroup.Surface,
                    flags: MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsTransparency,
                    defaultValue: Color.White,
                    description: "Base diffuse tint multiplied with the material lighting response.",
                    editorControlHint: "ColorPicker"),
                new MaterialPropertyDefinition(
                    key: "alpha_cutoff",
                    displayName: "Alpha Cutoff",
                    valueType: MaterialPropertyType.Float,
                    group: MaterialPropertyGroup.Rendering,
                    defaultValue: 0.5f,
                    description: "Cutout threshold used when rendering in the alpha-test queue.",
                    minValue: 0.0f,
                    maxValue: 1.0f,
                    step: 0.01f),
                new MaterialPropertyDefinition(
                    key: "ambient_color",
                    displayName: "Ambient Color",
                    valueType: MaterialPropertyType.Vector3,
                    group: MaterialPropertyGroup.Lighting,
                    flags: MaterialPropertyFlags.SupportsOverrides,
                    defaultValue: Vector3.Zero,
                    description: "Per-material ambient term kept for legacy imported materials.",
                    editorControlHint: "ColorPicker"),
                new MaterialPropertyDefinition(
                    key: "emissive_color",
                    displayName: "Emissive Color",
                    valueType: MaterialPropertyType.Vector3,
                    group: MaterialPropertyGroup.Lighting,
                    flags: MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsShaderCompilation,
                    defaultValue: Vector3.Zero,
                    description: "Self-illuminated color added on top of direct lighting.",
                    editorControlHint: "ColorPicker"),
                new MaterialPropertyDefinition(
                    key: "specular_color",
                    displayName: "Specular Color",
                    valueType: MaterialPropertyType.Vector3,
                    group: MaterialPropertyGroup.Lighting,
                    flags: MaterialPropertyFlags.SupportsOverrides,
                    defaultValue: new Vector3(0.5f),
                    description: "Specular highlight tint.",
                    editorControlHint: "ColorPicker"),
                new MaterialPropertyDefinition(
                    key: "specular_power",
                    displayName: "Specular Power",
                    valueType: MaterialPropertyType.Float,
                    group: MaterialPropertyGroup.Lighting,
                    flags: MaterialPropertyFlags.SupportsOverrides,
                    defaultValue: 16.0f,
                    description: "Controls the sharpness of the specular highlight.",
                    minValue: 0.0f,
                    maxValue: 128.0f,
                    step: 1.0f),
            });

    private static MaterialDefinition CreateUnlitTextureDefinition()
        => new(
            id: "unlit-texture",
            displayName: "Unlit Texture",
            runtimeMaterialType: typeof(UnlitTextureMaterial),
            description: "Simple textured material with tint and opacity, without lighting.",
            properties: new[]
            {
                new MaterialPropertyDefinition(
                    key: "base_color_texture",
                    displayName: "Base Color",
                    valueType: MaterialPropertyType.Texture,
                    group: MaterialPropertyGroup.Textures,
                    flags: MaterialPropertyFlags.AssetReference | MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsShaderCompilation,
                    defaultValue: Guid.Empty,
                    description: "Texture displayed by the unlit shader.",
                    legacyAliases: new[] { "BasColor_asset_id" },
                    assetKind: "texture"),
                new MaterialPropertyDefinition(
                    key: "tint_color",
                    displayName: "Tint Color",
                    valueType: MaterialPropertyType.Color,
                    group: MaterialPropertyGroup.Surface,
                    flags: MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsTransparency,
                    defaultValue: Color.White,
                    description: "Multiplicative tint applied on top of the base texture.",
                    editorControlHint: "ColorPicker"),
                new MaterialPropertyDefinition(
                    key: "alpha",
                    displayName: "Alpha",
                    valueType: MaterialPropertyType.Float,
                    group: MaterialPropertyGroup.Rendering,
                    flags: MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsTransparency,
                    defaultValue: 1.0f,
                    description: "Overall opacity from 0 (transparent) to 1 (opaque).",
                    minValue: 0.0f,
                    maxValue: 1.0f,
                    step: 0.01f),
                new MaterialPropertyDefinition(
                    key: "alpha_cutoff",
                    displayName: "Alpha Cutoff",
                    valueType: MaterialPropertyType.Float,
                    group: MaterialPropertyGroup.Rendering,
                    defaultValue: 0.5f,
                    description: "Cutout threshold used when rendering in the alpha-test queue.",
                    minValue: 0.0f,
                    maxValue: 1.0f,
                    step: 0.01f),
            });

}