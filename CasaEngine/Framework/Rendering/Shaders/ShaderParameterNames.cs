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
    public const string SpecularColor = "SpecularColor";
    public const string SpecularPower = "SpecularPower";
    public const string AlbedoTexture = "Texture";
    public const string TintColor = "TintColor";
    public const string Alpha = "Alpha";
    public const string OpacityTexture = "OpacityTexture";
    public const string NormalTexture = "NormalTexture";

    // --- Lighting ---
    public const string AmbientColor = "AmbientColor";
    public const string DirLightDirections = "DirLightDirections";
    public const string DirLightDiffuseColors = "DirLightDiffuseColors";
    public const string DirLightSpecularColors = "DirLightSpecularColors";

    // --- Skinning ---
    public const string Bones = "Bones";
}
