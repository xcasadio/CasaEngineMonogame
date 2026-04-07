using CasaEngine.Framework.Materials;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Identifies the shader that should actually be used for a material at runtime,
/// even when the material does not reference an explicit shader asset.
/// </summary>
public readonly struct EffectiveShaderReference
{
    public EffectiveShaderReference(Guid shaderId, string? contentName = null)
    {
        ShaderId = shaderId;
        ContentName = contentName;
    }

    /// <summary>
    /// Stable runtime identifier for the resolved shader. For built-in shaders this uses
    /// deterministic internal ids so the renderer can sort and cache consistently.
    /// </summary>
    public Guid ShaderId { get; }

    /// <summary>
    /// Optional MonoGame content path used for built-in shaders that are not backed by asset files.
    /// </summary>
    public string? ContentName { get; }

    public bool IsBuiltIn => !string.IsNullOrWhiteSpace(ContentName);
}

/// <summary>
/// Resolves the effective shader for a material, including stable fallbacks for built-in materials.
/// </summary>
public static class EffectiveShaderResolver
{
    public static readonly Guid BasicEffectShaderId = Guid.Parse("563375cb-78fb-4d0b-bce6-a267cf89b88d");
    public static readonly Guid UnlitTextureShaderId = Guid.Parse("13dbf2e6-4b26-4204-83e4-39c8e239931c");
    public static readonly Guid ReflectiveBasicEffectShaderId = Guid.Parse("2d0c7a46-6ac3-4d2a-91d8-dac5015b651d");

    public const string BasicEffectContentName = "Shaders\\basicEffect";
    public const string UnlitTextureContentName = "Shaders\\UnlitTexture";
    public const string ReflectiveBasicEffectContentName = BasicEffectContentName;

    /// <summary>
    /// Resolves the runtime shader reference for <paramref name="material"/>.
    /// Asset-backed shader references always win. Built-in runtime materials receive stable fallbacks.
    /// </summary>
    public static EffectiveShaderReference Resolve(MaterialBase material)
    {
        ArgumentNullException.ThrowIfNull(material);

        if (material.ShaderAssetId != Guid.Empty)
        {
            return new EffectiveShaderReference(material.ShaderAssetId);
        }

        var capabilities = material.GetShaderCapabilities();

        return capabilities.ShaderFamily switch
        {
            MaterialShaderFamily.Unlit => new EffectiveShaderReference(UnlitTextureShaderId, UnlitTextureContentName),
            MaterialShaderFamily.Lit when capabilities.HasReflection
                => new EffectiveShaderReference(ReflectiveBasicEffectShaderId, ReflectiveBasicEffectContentName),
            MaterialShaderFamily.Lit => new EffectiveShaderReference(BasicEffectShaderId, BasicEffectContentName),
            _ => ResolveLegacyFallback(material),
        };
    }

    private static EffectiveShaderReference ResolveLegacyFallback(MaterialBase material)
        => material switch
        {
            UnlitTextureMaterial => new EffectiveShaderReference(UnlitTextureShaderId, UnlitTextureContentName),
            LitDiffuseMaterial lit when lit.ReflectionCube is not null || lit.ReflectionCubeAssetId != Guid.Empty
                => new EffectiveShaderReference(ReflectiveBasicEffectShaderId, ReflectiveBasicEffectContentName),
            LitDiffuseMaterial => new EffectiveShaderReference(BasicEffectShaderId, BasicEffectContentName),
            _ => new EffectiveShaderReference(BasicEffectShaderId, BasicEffectContentName),
        };
}