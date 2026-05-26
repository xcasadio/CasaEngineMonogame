using CasaEngine.Framework.Rendering.Shaders;

namespace CasaEngine.Framework.Materials.Runtime;

/// <summary>
/// Builds runtime <see cref="MaterialPropertyBlock"/> values from authoring-time
/// <see cref="MaterialInstanceData"/> overrides.
///
/// Only overrides that are safe to apply without changing render states or shader
/// technique selection are emitted here. Compilation-affecting or render-state
/// changes remain the responsibility of the material asset/runtime material path.
/// </summary>
public static class MaterialInstancePropertyBlockMapper
{
    public delegate void OverrideMapper(
        MaterialPropertyBlock propertyBlock,
        MaterialAsset materialAsset,
        MaterialDefinition definition,
        MaterialInstanceData materialInstanceData,
        Func<Guid, MaterialAsset> parentResolver);

    private static readonly object OverrideMapperLock = new();
    private static readonly Dictionary<string, OverrideMapper> OverrideMappers = CreateOverrideMappers();

    public static MaterialPropertyBlock Create(
        MaterialAsset materialAsset,
        MaterialInstanceData materialInstanceData,
        Func<Guid, MaterialAsset> parentResolver = null)
    {
        var propertyBlock = new MaterialPropertyBlock();
        Apply(propertyBlock, materialAsset, materialInstanceData, parentResolver);
        return propertyBlock;
    }

    public static void Apply(
        MaterialPropertyBlock propertyBlock,
        MaterialAsset materialAsset,
        MaterialInstanceData materialInstanceData,
        Func<Guid, MaterialAsset> parentResolver = null)
    {
        ArgumentNullException.ThrowIfNull(propertyBlock);
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(materialInstanceData);

        propertyBlock.Clear();
        if (materialInstanceData.IsEmpty)
        {
            return;
        }

        var definition = materialAsset.GetRequiredDefinition();
        OverrideMapper mapper;
        lock (OverrideMapperLock)
        {
            if (!OverrideMappers.TryGetValue(definition.Id, out mapper!))
            {
                return;
            }
        }

        mapper(propertyBlock, materialAsset, definition, materialInstanceData, parentResolver);
    }

    public static IDisposable RegisterOverrideMapper(string definitionId, OverrideMapper mapper)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentNullException.ThrowIfNull(mapper);

        lock (OverrideMapperLock)
        {
            OverrideMappers.TryGetValue(definitionId, out var previousMapper);
            OverrideMappers[definitionId] = mapper;

            return new ScopedRegistration(() =>
            {
                lock (OverrideMapperLock)
                {
                    if (previousMapper is null)
                    {
                        OverrideMappers.Remove(definitionId);
                    }
                    else
                    {
                        OverrideMappers[definitionId] = previousMapper;
                    }
                }
            });
        }
    }

    private static Dictionary<string, OverrideMapper> CreateOverrideMappers()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["lit-diffuse"] = static (propertyBlock, materialAsset, definition, materialInstanceData, parentResolver)
                => ApplyLitDiffuseOverrides(propertyBlock, definition, materialInstanceData),
            ["unlit-texture"] = ApplyUnlitTextureOverrides,
        };

    private static void ApplyLitDiffuseOverrides(
        MaterialPropertyBlock propertyBlock,
        MaterialDefinition definition,
        MaterialInstanceData materialInstanceData)
    {
        if (TryGetOverrideValue(definition, materialInstanceData, "diffuse_color", out var diffuseColorOverride)
            && diffuseColorOverride.TryGetColor(out var diffuseColor))
        {
            propertyBlock.SetColor(ShaderParameterNames.DiffuseColor, diffuseColor);
        }

        if (TryGetOverrideValue(definition, materialInstanceData, "specular_color", out var specularColorOverride)
            && specularColorOverride.TryGetVector3(out var specularColor))
        {
            propertyBlock.SetVector3(ShaderParameterNames.SpecularColor, specularColor);
        }

        if (TryGetOverrideValue(definition, materialInstanceData, "specular_power", out var specularPowerOverride)
            && specularPowerOverride.TryGetFloat(out var specularPower))
        {
            propertyBlock.SetFloat(ShaderParameterNames.SpecularPower, specularPower);
        }
    }

    private static void ApplyUnlitTextureOverrides(
        MaterialPropertyBlock propertyBlock,
        MaterialAsset materialAsset,
        MaterialDefinition definition,
        MaterialInstanceData materialInstanceData,
        Func<Guid, MaterialAsset> parentResolver)
    {
        Color tintColorOverride = default;
        bool hasTintOverride = TryGetOverrideValue(definition, materialInstanceData, "tint_color", out var tintOverride)
            && tintOverride.TryGetColor(out tintColorOverride);

        float alphaValueOverride = 1.0f;
        bool canApplyAlpha = CanApplyUnlitAlphaOverride(materialAsset, parentResolver);
        bool hasAlphaOverride = canApplyAlpha
            && TryGetOverrideValue(definition, materialInstanceData, "alpha", out var alphaOverride)
            && alphaOverride.TryGetFloat(out alphaValueOverride);

        if (!hasTintOverride && !hasAlphaOverride)
        {
            return;
        }

        Color tintColor = hasTintOverride
            ? tintColorOverride
            : GetEffectiveColor(materialAsset, "tint_color", Color.White, parentResolver);
        float alpha = hasAlphaOverride
            ? alphaValueOverride
            : GetEffectiveFloat(materialAsset, "alpha", 1.0f, parentResolver);

        propertyBlock.SetVector4(ShaderParameterNames.TintColor, tintColor.ToVector4());
        propertyBlock.SetFloat(ShaderParameterNames.Alpha, alpha);
    }

    private static bool TryGetOverrideValue(
        MaterialDefinition definition,
        MaterialInstanceData materialInstanceData,
        string propertyKey,
        out MaterialValue value)
    {
        if (materialInstanceData.TryGetPropertyOverride(propertyKey, out value!))
        {
            return true;
        }

        var propertyDefinition = definition.GetRequiredProperty(propertyKey);
        for (int i = 0; i < propertyDefinition.LegacyAliases.Count; i++)
        {
            if (materialInstanceData.TryGetPropertyOverride(propertyDefinition.LegacyAliases[i], out value!))
            {
                return true;
            }
        }

        value = null!;
        return false;
    }

    private static bool CanApplyUnlitAlphaOverride(
        MaterialAsset materialAsset,
        Func<Guid, MaterialAsset> parentResolver)
    {
        if (materialAsset.IsTransparent
            || materialAsset.Queue == RenderQueue.Transparent
            || string.Equals(materialAsset.BlendStateName, MaterialAsset.DefaultBlendStateName, StringComparison.OrdinalIgnoreCase) == false)
        {
            return true;
        }

        return GetEffectiveFloat(materialAsset, "alpha", 1.0f, parentResolver) < 0.999f;
    }

    private static Color GetEffectiveColor(
        MaterialAsset materialAsset,
        string propertyKey,
        Color fallback,
        Func<Guid, MaterialAsset> parentResolver)
    {
        var value = materialAsset.GetPropertyValueOrDefault(propertyKey, parentResolver);
        return value != null && value.TryGetColor(out var color)
            ? color
            : fallback;
    }

    private static float GetEffectiveFloat(
        MaterialAsset materialAsset,
        string propertyKey,
        float fallback,
        Func<Guid, MaterialAsset> parentResolver)
    {
        var value = materialAsset.GetPropertyValueOrDefault(propertyKey, parentResolver);
        return value != null && value.TryGetFloat(out var floatValue)
            ? floatValue
            : fallback;
    }
}