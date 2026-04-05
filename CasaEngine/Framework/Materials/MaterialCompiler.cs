using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials;

public sealed class MaterialCompiler
{
    public CompiledMaterial Compile(MaterialAsset materialAsset, AssetContentManager assetContentManager)
        => CompileBoth(materialAsset, assetContentManager).CompiledMaterial;

    public MaterialBase CompileRuntimeMaterial(MaterialAsset materialAsset, AssetContentManager assetContentManager)
        => CompileBoth(materialAsset, assetContentManager).RuntimeMaterial;

    internal (CompiledMaterial CompiledMaterial, MaterialBase RuntimeMaterial) CompileBoth(
        MaterialAsset materialAsset,
        AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(assetContentManager);

        var definition = materialAsset.GetRequiredDefinition();
        var effectiveValues = BuildEffectiveValues(materialAsset, definition, assetContentManager);
        var resolvedTextures = BuildResolvedTextures(definition, effectiveValues, assetContentManager);
        var runtimeMaterial = CreateRuntimeMaterial(materialAsset, definition, effectiveValues, resolvedTextures);

        var compiledMaterial = new CompiledMaterial(
            definitionId: definition.Id,
            effectiveShader: EffectiveShaderResolver.Resolve(runtimeMaterial),
            properties: BuildCompiledProperties(definition, effectiveValues),
            textures: resolvedTextures,
            sourceAssetId: materialAsset.Id,
            name: materialAsset.Name,
            features: RenderFeatureResolver.ResolveMaterialFeatures(runtimeMaterial),
            blendState: runtimeMaterial.BlendState,
            depthStencilState: runtimeMaterial.DepthStencilState,
            rasterizerState: runtimeMaterial.RasterizerState,
            samplerState: runtimeMaterial.SamplerState,
            isTransparent: runtimeMaterial.IsTransparent,
            queue: runtimeMaterial.Queue,
            castShadows: runtimeMaterial.CastShadows,
            receiveShadows: runtimeMaterial.ReceiveShadows);

        return (compiledMaterial, runtimeMaterial);
    }

    private static IReadOnlyDictionary<string, MaterialValue> BuildEffectiveValues(
        MaterialAsset materialAsset,
        MaterialDefinition definition,
        AssetContentManager assetContentManager)
    {
        var values = new Dictionary<string, MaterialValue>(StringComparer.OrdinalIgnoreCase);
        var resolvedParents = new Dictionary<Guid, MaterialAsset?>();
        var authoringMaterialCache = assetContentManager.RuntimeContext?.MaterialAuthoringCache;

        MaterialAsset? ResolveParent(Guid assetId)
        {
            if (resolvedParents.TryGetValue(assetId, out var cachedMaterial))
            {
                return cachedMaterial;
            }

            try
            {
                cachedMaterial = authoringMaterialCache != null
                    ? authoringMaterialCache.GetOrLoad(assetId, assetContentManager)
                    : assetContentManager.Load<MaterialAsset>(assetId, cache: false);
            }
            catch
            {
                cachedMaterial = null;
            }

            resolvedParents[assetId] = cachedMaterial;
            return cachedMaterial;
        }

        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            var value = materialAsset.GetPropertyValueOrDefault(propertyDefinition.Key, ResolveParent);
            if (value is null)
            {
                throw new InvalidOperationException(
                    $"Material asset '{materialAsset.Name}' is missing the required property '{propertyDefinition.Key}'.");
            }

            values.Add(propertyDefinition.Key, value);
        }

        return values;
    }

    private static Dictionary<string, Texture2D?> BuildResolvedTextures(
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        AssetContentManager assetContentManager)
    {
        var textures = new Dictionary<string, Texture2D?>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            if (propertyDefinition.ValueType != MaterialPropertyType.Texture)
            {
                continue;
            }

            var textureAssetId = GetTextureId(effectiveValues[propertyDefinition.Key], propertyDefinition.Key);
            textures.Add(propertyDefinition.Key, ResolveTextureResource(textureAssetId, assetContentManager));
        }

        return textures;
    }

    private static IEnumerable<KeyValuePair<string, MaterialValue>> BuildCompiledProperties(
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues)
    {
        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            if (propertyDefinition.ValueType == MaterialPropertyType.Texture)
            {
                continue;
            }

            yield return new KeyValuePair<string, MaterialValue>(propertyDefinition.Key, effectiveValues[propertyDefinition.Key]);
        }
    }

    private static MaterialBase CreateRuntimeMaterial(
        MaterialAsset materialAsset,
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures)
        => definition.Id switch
        {
            "lit-diffuse" => CreateLitDiffuseMaterial(materialAsset, effectiveValues, resolvedTextures),
            "unlit-texture" => CreateUnlitTextureMaterial(materialAsset, effectiveValues, resolvedTextures),
            "legacy-multi-texture" => CreateLegacyMultiTextureMaterial(materialAsset, effectiveValues, resolvedTextures),
            _ => throw new NotSupportedException(
                $"Material compiler does not support material definition '{definition.Id}' yet."),
        };

    private static LitDiffuseMaterial CreateLitDiffuseMaterial(
        MaterialAsset materialAsset,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures)
    {
        var material = new LitDiffuseMaterial();
        ApplyCommonSettings(materialAsset, material, MaterialDefinitionRegistry.GetRequiredById("lit-diffuse"), effectiveValues);

        material.BasColorAssetId = GetTextureId(effectiveValues["base_color_texture"], "base_color_texture");
        material.BasColor = resolvedTextures["base_color_texture"];
        material.NormalMapAssetId = GetTextureId(effectiveValues["normal_texture"], "normal_texture");
        material.NormalMap = resolvedTextures["normal_texture"];
        material.DiffuseColor = GetColor(effectiveValues["diffuse_color"], "diffuse_color");
        material.AlphaCutoff = GetFloat(effectiveValues["alpha_cutoff"], "alpha_cutoff");
        material.EmissiveColor = GetVector3(effectiveValues["emissive_color"], "emissive_color");
        material.SpecularColor = GetVector3(effectiveValues["specular_color"], "specular_color");
        material.SpecularPower = GetFloat(effectiveValues["specular_power"], "specular_power");

        return material;
    }

    private static UnlitTextureMaterial CreateUnlitTextureMaterial(
        MaterialAsset materialAsset,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures)
    {
        var material = new UnlitTextureMaterial();
        ApplyCommonSettings(materialAsset, material, MaterialDefinitionRegistry.GetRequiredById("unlit-texture"), effectiveValues);

        material.BasColorAssetId = GetTextureId(effectiveValues["base_color_texture"], "base_color_texture");
        material.BasColor = resolvedTextures["base_color_texture"];
        material.Tint = GetColor(effectiveValues["tint_color"], "tint_color");
        material.Alpha = GetFloat(effectiveValues["alpha"], "alpha");
        material.AlphaCutoff = GetFloat(effectiveValues["alpha_cutoff"], "alpha_cutoff");

        return material;
    }

    private static Material CreateLegacyMultiTextureMaterial(
        MaterialAsset materialAsset,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures)
    {
        var material = new Material();
        ApplyCommonSettings(materialAsset, material, MaterialDefinitionRegistry.GetRequiredById("legacy-multi-texture"), effectiveValues);

        material.TextureBaseColorAssetId = GetTextureId(effectiveValues["base_color_texture"], "base_color_texture");
        material.TextureBaseColor = WrapTexture(resolvedTextures["base_color_texture"]);
        material.TextureOpacityAssetId = GetTextureId(effectiveValues["opacity_texture"], "opacity_texture");
        material.TextureOpacityColor = WrapTexture(resolvedTextures["opacity_texture"]);
        material.TextureNormalAssetId = GetTextureId(effectiveValues["normal_texture"], "normal_texture");
        material.TextureNormal = WrapTexture(resolvedTextures["normal_texture"]);
        material.TextureSpecularAssetId = GetTextureId(effectiveValues["specular_texture"], "specular_texture");
        material.TextureSpecular = WrapTexture(resolvedTextures["specular_texture"]);
        material.TextureRoughnessAssetId = GetTextureId(effectiveValues["roughness_texture"], "roughness_texture");
        material.TextureRoughness = WrapTexture(resolvedTextures["roughness_texture"]);
        material.TextureTangentAssetId = GetTextureId(effectiveValues["tangent_texture"], "tangent_texture");
        material.TextureTangent = WrapTexture(resolvedTextures["tangent_texture"]);
        material.TextureHeightAssetId = GetTextureId(effectiveValues["height_texture"], "height_texture");
        material.TextureHeight = WrapTexture(resolvedTextures["height_texture"]);
        material.TextureReflectionAssetId = GetTextureId(effectiveValues["reflection_texture"], "reflection_texture");
        material.TextureReflection = WrapTexture(resolvedTextures["reflection_texture"]);

        return material;
    }

    private static void ApplyCommonSettings(
        MaterialAsset materialAsset,
        MaterialBase material,
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues)
    {
        var resolvedRenderState = MaterialRenderStateResolver.Resolve(materialAsset, definition, effectiveValues);

        material.Id = materialAsset.Id;
        material.Name = materialAsset.Name;
        material.ShaderAssetId = materialAsset.ShaderAssetId;
        material.IsTransparent = resolvedRenderState.IsTransparent;
        material.Queue = resolvedRenderState.Queue;
        material.CastShadows = materialAsset.CastShadows;
        material.ReceiveShadows = materialAsset.ReceiveShadows;
        material.SetBlendStateByName(resolvedRenderState.BlendStateName);
        material.SetDepthStateByName(resolvedRenderState.DepthStencilStateName);
        material.SetRasterizerStateByName(materialAsset.RasterizerStateName);
        material.SetSamplerStateByName(materialAsset.SamplerStateName);
    }

    private static Texture2D? ResolveTextureResource(Guid textureAssetId, AssetContentManager assetContentManager)
    {
        if (textureAssetId == Guid.Empty)
        {
            return null;
        }

        try
        {
            var texture = assetContentManager.Load<Assets.Textures.Texture>(textureAssetId);
            texture.Load(assetContentManager);
            return texture.Resource;
        }
        catch
        {
            return null;
        }
    }

    private static Assets.Textures.Texture? WrapTexture(Texture2D? textureResource)
        => textureResource is null ? null : new Assets.Textures.Texture(textureResource);

    private static Guid GetTextureId(MaterialValue value, string propertyKey)
    {
        if (value.TryGetTextureId(out var textureAssetId))
        {
            return textureAssetId;
        }

        throw new InvalidOperationException(
            $"Material value for '{propertyKey}' is not a texture asset id.");
    }

    private static Color GetColor(MaterialValue value, string propertyKey)
    {
        if (value.TryGetColor(out var color))
        {
            return color;
        }

        throw new InvalidOperationException(
            $"Material value for '{propertyKey}' is not a color.");
    }

    private static Vector3 GetVector3(MaterialValue value, string propertyKey)
    {
        if (value.TryGetVector3(out var vector))
        {
            return vector;
        }

        throw new InvalidOperationException(
            $"Material value for '{propertyKey}' is not a Vector3.");
    }

    private static float GetFloat(MaterialValue value, string propertyKey)
    {
        if (value.TryGetFloat(out var floatValue))
        {
            return floatValue;
        }

        throw new InvalidOperationException(
            $"Material value for '{propertyKey}' is not a float.");
    }
}