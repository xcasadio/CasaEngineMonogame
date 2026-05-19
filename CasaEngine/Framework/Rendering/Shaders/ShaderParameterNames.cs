namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Constant names for shader parameters used across materials and shaders.
/// Using these constants avoids hard-coded strings scattered throughout the codebase.
/// </summary>
public static class ShaderParameterNames
{
    // --- Transforms ---
    public const string World = "World";
    public const string WorldInverseTranspose = "WorldInverseTranspose";
    public const string WorldViewProj = "WorldViewProj";
    public const string View = "View";
    public const string Projection = "Projection";
    public const string ViewProjection = "ViewProjection";
    public const string EyePosition = "EyePosition";

    // --- Material ---
    public const string DiffuseColor = "DiffuseColor";
    public const string EmissiveColor = "EmissiveColor";
    public const string MaterialAmbientColor = "MaterialAmbientColor";
    public const string SpecularColor = "SpecularColor";
    public const string SpecularPower = "SpecularPower";
    public const string BasColorTexture = "Texture";
    public const string ReflectionCubeTexture = "ReflectionCubeTexture";
    public const string TintColor = "TintColor";
    public const string TintStrength = "TintStrength";
    public const string TintMaskFromBaseAlpha = "TintMaskFromBaseAlpha";
    public const string ReflectionAddAmount = "ReflectionAddAmount";
    public const string ReflectionMultiplyBase = "ReflectionMultiplyBase";
    public const string ReflectionMultiplyFactor = "ReflectionMultiplyFactor";
    public const string Alpha = "Alpha";
    public const string AlphaCutoff = "AlphaCutoff";
    public const string OpacityTexture = "OpacityTexture";
    public const string NormalTexture = "NormalTexture";
    public const string ColorMultiplier = "ColorMultiplier";
    public const string SolidColor = "SolidColor";

    // --- Lighting ---
    public const string AmbientColor = "AmbientColor";
    public const string ActiveDirectionalLightCount = "ActiveDirectionalLightCount";
    public const string ActivePointLightCount = "ActivePointLightCount";
    public const string ActiveSpotLightCount = "ActiveSpotLightCount";
    public const string EnvironmentAmbientColor = "EnvironmentAmbientColor";
    public const string EnvironmentSpecularIntensity = "EnvironmentSpecularIntensity";
    public const string EnvironmentCubeTexture = "EnvironmentCubeTexture";
    public const string HasEnvironmentCubeTexture = "HasEnvironmentCubeTexture";
    public const string LocalReflectionProbeCubeTexture = "LocalReflectionProbeCubeTexture";
    public const string SecondaryLocalReflectionProbeCubeTexture = "SecondaryLocalReflectionProbeCubeTexture";
    public const string HasLocalReflectionProbeTexture = "HasLocalReflectionProbeTexture";
    public const string HasSecondaryLocalReflectionProbeTexture = "HasSecondaryLocalReflectionProbeTexture";
    public const string LocalReflectionProbeWeight = "LocalReflectionProbeWeight";
    public const string SecondaryLocalReflectionProbeWeight = "SecondaryLocalReflectionProbeWeight";
    public const string LocalReflectionProbeInfluence = "LocalReflectionProbeInfluence";
    public const string HasMaterialReflectionCube = "HasMaterialReflectionCube";
    public const string DirLight0Direction = "DirLight0Direction";
    public const string DirLight0DiffuseColor = "DirLight0DiffuseColor";
    public const string DirLight0SpecularColor = "DirLight0SpecularColor";
    public const string DirLight1Direction = "DirLight1Direction";
    public const string DirLight1DiffuseColor = "DirLight1DiffuseColor";
    public const string DirLight1SpecularColor = "DirLight1SpecularColor";
    public const string DirLight2Direction = "DirLight2Direction";
    public const string DirLight2DiffuseColor = "DirLight2DiffuseColor";
    public const string DirLight2SpecularColor = "DirLight2SpecularColor";
    public const string DirLight3Direction = "DirLight3Direction";
    public const string DirLight3DiffuseColor = "DirLight3DiffuseColor";
    public const string DirLight3SpecularColor = "DirLight3SpecularColor";
    public const string DirLight4Direction = "DirLight4Direction";
    public const string DirLight4DiffuseColor = "DirLight4DiffuseColor";
    public const string DirLight4SpecularColor = "DirLight4SpecularColor";
    public const string DirLight5Direction = "DirLight5Direction";
    public const string DirLight5DiffuseColor = "DirLight5DiffuseColor";
    public const string DirLight5SpecularColor = "DirLight5SpecularColor";
    public const string DirLight6Direction = "DirLight6Direction";
    public const string DirLight6DiffuseColor = "DirLight6DiffuseColor";
    public const string DirLight6SpecularColor = "DirLight6SpecularColor";
    public const string DirLight7Direction = "DirLight7Direction";
    public const string DirLight7DiffuseColor = "DirLight7DiffuseColor";
    public const string DirLight7SpecularColor = "DirLight7SpecularColor";

    public static readonly string[] DirectionalLightDirectionParameters =
    {
        DirLight0Direction,
        DirLight1Direction,
        DirLight2Direction,
        DirLight3Direction,
        DirLight4Direction,
        DirLight5Direction,
        DirLight6Direction,
        DirLight7Direction,
    };

    public static readonly string[] DirectionalLightDiffuseParameters =
    {
        DirLight0DiffuseColor,
        DirLight1DiffuseColor,
        DirLight2DiffuseColor,
        DirLight3DiffuseColor,
        DirLight4DiffuseColor,
        DirLight5DiffuseColor,
        DirLight6DiffuseColor,
        DirLight7DiffuseColor,
    };

    public static readonly string[] DirectionalLightSpecularParameters =
    {
        DirLight0SpecularColor,
        DirLight1SpecularColor,
        DirLight2SpecularColor,
        DirLight3SpecularColor,
        DirLight4SpecularColor,
        DirLight5SpecularColor,
        DirLight6SpecularColor,
        DirLight7SpecularColor,
    };
    public const string PointLightPositionAndRange = "PointLightPositionAndRange";
    public const string PointLightDiffuseColors = "PointLightDiffuseColors";
    public const string PointLightSpecularColors = "PointLightSpecularColors";
    public const string SpotLightPositionAndRange = "SpotLightPositionAndRange";
    public const string SpotLightDirectionAndInnerConeCos = "SpotLightDirectionAndInnerConeCos";
    public const string SpotLightDiffuseColors = "SpotLightDiffuseColors";
    public const string SpotLightSpecularColorsAndOuterConeCos = "SpotLightSpecularColorsAndOuterConeCos";
    public const string ShadowMapTexture = "ShadowMapTexture";
    public const string ActiveShadowLightCount = "ActiveShadowLightCount";
    public const string ShadowedDirectionalLightIndex = "ShadowedDirectionalLightIndex";
    public const string ShadowLightViewProjection = "ShadowLightViewProjection";
    public const string ShadowDepthBias = "ShadowDepthBias";
    public const string ShadowNormalBias = "ShadowNormalBias";
    public const string ShadowMapTexelSize = "ShadowMapTexelSize";
    public const string ReceiveShadows = "ReceiveShadows";

    // --- Skinning ---
    public const string Bones = "Bones";
    public const string BonesDualQuaternion = "BonesDualQuaternion";
}
