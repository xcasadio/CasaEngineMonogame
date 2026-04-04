using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Materials;

public static class MaterialDefinitionRegistry
{
    private static readonly MaterialDefinition[] Definitions =
    {
        CreateLitDiffuseDefinition(),
        CreateUnlitTextureDefinition(),
        CreateLegacyMultiTextureDefinition(),
    };

    private static readonly Dictionary<string, MaterialDefinition> DefinitionsById = BuildIdLookup(Definitions);
    private static readonly Dictionary<Type, MaterialDefinition> DefinitionsByRuntimeType = BuildRuntimeTypeLookup(Definitions);
    private static readonly Dictionary<string, MaterialDefinition> DefinitionsByLegacyType = BuildLegacyTypeLookup(Definitions);

    public static IReadOnlyList<MaterialDefinition> All => Definitions;

    public static bool TryGetById(string id, out MaterialDefinition definition)
        => DefinitionsById.TryGetValue(id, out definition!);

    public static bool TryGetByRuntimeType(Type runtimeMaterialType, out MaterialDefinition definition)
        => DefinitionsByRuntimeType.TryGetValue(runtimeMaterialType, out definition!);

    public static bool TryGetByLegacyTypeName(string typeName, out MaterialDefinition definition)
        => DefinitionsByLegacyType.TryGetValue(typeName, out definition!);

    public static MaterialDefinition GetRequiredById(string id)
    {
        if (TryGetById(id, out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"Unknown material definition '{id}'.");
    }

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
                    key: "diffuse_color",
                    displayName: "Diffuse Color",
                    valueType: MaterialPropertyType.Color,
                    group: MaterialPropertyGroup.Surface,
                    flags: MaterialPropertyFlags.SupportsOverrides,
                    defaultValue: Color.White,
                    description: "Base diffuse tint multiplied with the material lighting response."),
                new MaterialPropertyDefinition(
                    key: "emissive_color",
                    displayName: "Emissive Color",
                    valueType: MaterialPropertyType.Vector3,
                    group: MaterialPropertyGroup.Lighting,
                    flags: MaterialPropertyFlags.SupportsOverrides | MaterialPropertyFlags.AffectsShaderCompilation,
                    defaultValue: Vector3.Zero,
                    description: "Self-illuminated color added on top of direct lighting."),
                new MaterialPropertyDefinition(
                    key: "specular_color",
                    displayName: "Specular Color",
                    valueType: MaterialPropertyType.Vector3,
                    group: MaterialPropertyGroup.Lighting,
                    flags: MaterialPropertyFlags.SupportsOverrides,
                    defaultValue: new Vector3(0.5f),
                    description: "Specular highlight tint."),
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
                    flags: MaterialPropertyFlags.SupportsOverrides,
                    defaultValue: Color.White,
                    description: "Multiplicative tint applied on top of the base texture."),
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
            });

    private static MaterialDefinition CreateLegacyMultiTextureDefinition()
        => new(
            id: "legacy-multi-texture",
            displayName: "Legacy Multi Texture",
            runtimeMaterialType: typeof(Material),
            description: "Legacy runtime material exposing the multi-texture slots already present in the engine.",
            properties: new[]
            {
                CreateLegacyTextureProperty("base_color_texture", "Base Color", "texture_base_color_asset_id", affectsShaderCompilation: true),
                CreateLegacyTextureProperty("opacity_texture", "Opacity", "texture_opacity_asset_id"),
                CreateLegacyTextureProperty("normal_texture", "Normal", "texture_normal_asset_id", affectsShaderCompilation: true),
                CreateLegacyTextureProperty("specular_texture", "Specular", "texture_specular_asset_id"),
                CreateLegacyTextureProperty("roughness_texture", "Roughness", "texture_roughness_asset_id"),
                CreateLegacyTextureProperty("tangent_texture", "Tangent", "texture_tangent_asset_id"),
                CreateLegacyTextureProperty("height_texture", "Height", "texture_height_asset_id"),
                CreateLegacyTextureProperty("reflection_texture", "Reflection", "texture_reflection_asset_id"),
            });

    private static MaterialPropertyDefinition CreateLegacyTextureProperty(
        string key,
        string displayName,
        string legacyAlias,
        bool affectsShaderCompilation = false)
    {
        var flags = MaterialPropertyFlags.AssetReference | MaterialPropertyFlags.SupportsOverrides;
        if (affectsShaderCompilation)
        {
            flags |= MaterialPropertyFlags.AffectsShaderCompilation;
        }

        return new MaterialPropertyDefinition(
            key: key,
            displayName: displayName,
            valueType: MaterialPropertyType.Texture,
            group: MaterialPropertyGroup.Textures,
            flags: flags,
            defaultValue: Guid.Empty,
            description: $"Legacy texture slot '{displayName}'.",
            legacyAliases: new[] { legacyAlias },
            assetKind: "texture");
    }

    private static Dictionary<string, MaterialDefinition> BuildIdLookup(IEnumerable<MaterialDefinition> definitions)
    {
        var lookup = new Dictionary<string, MaterialDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (!lookup.TryAdd(definition.Id, definition))
            {
                throw new InvalidOperationException($"Duplicate material definition id '{definition.Id}'.");
            }
        }

        return lookup;
    }

    private static Dictionary<Type, MaterialDefinition> BuildRuntimeTypeLookup(IEnumerable<MaterialDefinition> definitions)
    {
        var lookup = new Dictionary<Type, MaterialDefinition>();

        foreach (var definition in definitions)
        {
            if (!lookup.TryAdd(definition.RuntimeMaterialType, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate runtime material type '{definition.RuntimeMaterialType.FullName}' in material definition registry.");
            }
        }

        return lookup;
    }

    private static Dictionary<string, MaterialDefinition> BuildLegacyTypeLookup(IEnumerable<MaterialDefinition> definitions)
    {
        var lookup = new Dictionary<string, MaterialDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            foreach (var legacyTypeName in definition.LegacyTypeNames)
            {
                if (!lookup.TryAdd(legacyTypeName, definition))
                {
                    throw new InvalidOperationException(
                        $"Duplicate legacy material type name '{legacyTypeName}' in material definition registry.");
                }
            }
        }

        return lookup;
    }
}