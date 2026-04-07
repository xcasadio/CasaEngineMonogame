using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Materials.Compilation;

public sealed class MaterialCompiler
{
    public delegate MaterialBase RuntimeMaterialFactory(
        MaterialAsset materialAsset,
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures,
        AssetContentManager assetContentManager);

    private static readonly object RuntimeMaterialFactoryLock = new();
    private static readonly Dictionary<string, RuntimeMaterialFactory> RuntimeMaterialFactories =
        CreateRuntimeMaterialFactories();

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
        var runtimeMaterial = CreateRuntimeMaterial(materialAsset, definition, effectiveValues, resolvedTextures, assetContentManager);
        var compiledTextureBindings = BuildCompiledTextureBindings(definition, effectiveValues, resolvedTextures, runtimeMaterial);

        var compiledMaterial = new CompiledMaterial(
            definitionId: definition.Id,
            effectiveShader: EffectiveShaderResolver.Resolve(runtimeMaterial),
            properties: BuildCompiledProperties(definition, effectiveValues),
            textures: resolvedTextures,
            textureBindings: compiledTextureBindings,
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

    public static IDisposable RegisterRuntimeMaterialFactory(string definitionId, RuntimeMaterialFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentNullException.ThrowIfNull(factory);

        lock (RuntimeMaterialFactoryLock)
        {
            RuntimeMaterialFactories.TryGetValue(definitionId, out var previousFactory);
            RuntimeMaterialFactories[definitionId] = factory;

            return new ScopedRegistration(() =>
            {
                lock (RuntimeMaterialFactoryLock)
                {
                    if (previousFactory is null)
                    {
                        RuntimeMaterialFactories.Remove(definitionId);
                    }
                    else
                    {
                        RuntimeMaterialFactories[definitionId] = previousFactory;
                    }
                }
            });
        }
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

            if (IsTextureCubeProperty(propertyDefinition))
            {
                textures.Add(propertyDefinition.Key, null);
                continue;
            }

            var textureAssetId = GetTextureId(effectiveValues[propertyDefinition.Key], propertyDefinition.Key);
            textures.Add(propertyDefinition.Key, ResolveTextureResource(textureAssetId, assetContentManager));
        }

        return textures;
    }

    private static IEnumerable<KeyValuePair<string, CompiledMaterialTextureBinding>> BuildCompiledTextureBindings(
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures,
        MaterialBase runtimeMaterial)
    {
        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            if (propertyDefinition.ValueType != MaterialPropertyType.Texture)
            {
                continue;
            }

            var textureAssetId = GetTextureId(effectiveValues[propertyDefinition.Key], propertyDefinition.Key);
            if (IsTextureCubeProperty(propertyDefinition))
            {
                var reflectionCube = runtimeMaterial is LitDiffuseMaterial litMaterial
                    ? litMaterial.ReflectionCube
                    : null;
                yield return new KeyValuePair<string, CompiledMaterialTextureBinding>(
                    propertyDefinition.Key,
                    new CompiledMaterialTextureBinding(
                        textureAssetId,
                        CompiledMaterialTextureBindingKind.TextureCube,
                        textureCube: reflectionCube));
                continue;
            }

            resolvedTextures.TryGetValue(propertyDefinition.Key, out var texture);
            yield return new KeyValuePair<string, CompiledMaterialTextureBinding>(
                propertyDefinition.Key,
                new CompiledMaterialTextureBinding(
                    textureAssetId,
                    CompiledMaterialTextureBindingKind.Texture2D,
                    texture: texture));
        }
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
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures,
        AssetContentManager assetContentManager)
    {
        RuntimeMaterialFactory factory;
        lock (RuntimeMaterialFactoryLock)
        {
            if (!RuntimeMaterialFactories.TryGetValue(definition.Id, out factory!))
            {
                throw new NotSupportedException(
                    $"Material compiler does not support material definition '{definition.Id}' yet.");
            }
        }

        return factory(materialAsset, definition, effectiveValues, resolvedTextures, assetContentManager);
    }

    private static Dictionary<string, RuntimeMaterialFactory> CreateRuntimeMaterialFactories()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["lit-diffuse"] = CreateLitDiffuseMaterial,
            ["unlit-texture"] = CreateUnlitTextureMaterial,
        };

    private static MaterialBase CreateLitDiffuseMaterial(
        MaterialAsset materialAsset,
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures,
        AssetContentManager assetContentManager)
    {
        var material = new LitDiffuseMaterial();
        ApplyCommonSettings(materialAsset, material, definition, effectiveValues);

        material.BasColorAssetId = GetTextureId(effectiveValues["base_color_texture"], "base_color_texture");
        material.BasColor = resolvedTextures["base_color_texture"];
        material.NormalMapAssetId = GetTextureId(effectiveValues["normal_texture"], "normal_texture");
        material.NormalMap = resolvedTextures["normal_texture"];
        material.ReflectionCubeAssetId = GetTextureId(effectiveValues["reflection_texture"], "reflection_texture");
        material.ReflectionCube = ResolveTextureCubeResource(material.ReflectionCubeAssetId, assetContentManager);
        material.DiffuseColor = GetColor(effectiveValues["diffuse_color"], "diffuse_color");
        material.AlphaCutoff = GetFloat(effectiveValues["alpha_cutoff"], "alpha_cutoff");
        material.AmbientColor = GetVector3(effectiveValues["ambient_color"], "ambient_color");
        material.EmissiveColor = GetVector3(effectiveValues["emissive_color"], "emissive_color");
        material.SpecularColor = GetVector3(effectiveValues["specular_color"], "specular_color");
        material.SpecularPower = GetFloat(effectiveValues["specular_power"], "specular_power");

        return material;
    }

    private static UnlitTextureMaterial CreateUnlitTextureMaterial(
        MaterialAsset materialAsset,
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues,
        IReadOnlyDictionary<string, Texture2D?> resolvedTextures,
        AssetContentManager assetContentManager)
    {
        var material = new UnlitTextureMaterial();
        ApplyCommonSettings(materialAsset, material, definition, effectiveValues);

        material.BasColorAssetId = GetTextureId(effectiveValues["base_color_texture"], "base_color_texture");
        material.BasColor = resolvedTextures["base_color_texture"];
        material.Tint = GetColor(effectiveValues["tint_color"], "tint_color");
        material.Alpha = GetFloat(effectiveValues["alpha"], "alpha");
        material.AlphaCutoff = GetFloat(effectiveValues["alpha_cutoff"], "alpha_cutoff");

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

    private static XnaTextureCube? ResolveTextureCubeResource(Guid textureAssetId, AssetContentManager assetContentManager)
    {
        if (textureAssetId == Guid.Empty)
        {
            return null;
        }

        try
        {
            return assetContentManager.Load<XnaTextureCube>(textureAssetId);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTextureCubeProperty(MaterialPropertyDefinition propertyDefinition)
        => string.Equals(propertyDefinition.Key, "reflection_texture", StringComparison.OrdinalIgnoreCase)
           && string.Equals(propertyDefinition.AssetKind, "dds", StringComparison.OrdinalIgnoreCase);

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