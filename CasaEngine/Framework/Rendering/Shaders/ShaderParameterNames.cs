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
    public const string BasColorTexture = "Texture";
    public const string TintColor = "TintColor";
    public const string Alpha = "Alpha";
    public const string OpacityTexture = "OpacityTexture";
    public const string NormalTexture = "NormalTexture";

    // --- Lighting ---
    public const string AmbientColor = "AmbientColor";
    public const string DirLight0Direction = "DirLight0Direction";
    public const string DirLight0DiffuseColor = "DirLight0DiffuseColor";
    public const string DirLight0SpecularColor = "DirLight0SpecularColor";
    public const string DirLight1Direction = "DirLight1Direction";
    public const string DirLight1DiffuseColor = "DirLight1DiffuseColor";
    public const string DirLight1SpecularColor = "DirLight1SpecularColor";
    public const string DirLight2Direction = "DirLight2Direction";
    public const string DirLight2DiffuseColor = "DirLight2DiffuseColor";
    public const string DirLight2SpecularColor = "DirLight2SpecularColor";

    // --- Skinning ---
    public const string Bones = "Bones";
}
