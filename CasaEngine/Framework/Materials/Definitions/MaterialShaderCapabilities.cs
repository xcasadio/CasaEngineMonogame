namespace CasaEngine.Framework.Materials.Definitions;

public enum MaterialShaderFamily
{
    Unknown,
    Lit,
    Unlit,

    /// <summary>The static-mesh PBR shader (LitPbr.fx). A skinned draw keeps the skinning shader until phase 8 of the PBR plan.</summary>
    LitPbr,
}

public readonly struct MaterialShaderCapabilities
{
    public MaterialShaderCapabilities(
        MaterialShaderFamily shaderFamily,
        bool hasBasColorTexture = false,
        bool hasNormalMap = false,
        bool hasEmissive = false,
        bool hasReflection = false,
        bool isAlphaTest = false,
        bool isTransparent = false)
    {
        ShaderFamily = shaderFamily;
        HasBasColorTexture = hasBasColorTexture;
        HasNormalMap = hasNormalMap;
        HasEmissive = hasEmissive;
        HasReflection = hasReflection;
        IsAlphaTest = isAlphaTest;
        IsTransparent = isTransparent;
    }

    public MaterialShaderFamily ShaderFamily { get; }

    public bool HasBasColorTexture { get; }

    public bool HasNormalMap { get; }

    public bool HasEmissive { get; }

    public bool HasReflection { get; }

    public bool IsAlphaTest { get; }

    public bool IsTransparent { get; }
}