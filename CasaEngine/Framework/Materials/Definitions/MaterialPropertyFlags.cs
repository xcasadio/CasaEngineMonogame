namespace CasaEngine.Framework.Materials.Definitions;

[Flags]
public enum MaterialPropertyFlags
{
    None = 0,
    Required = 1 << 0,
    AssetReference = 1 << 1,
    SupportsOverrides = 1 << 2,
    AffectsShaderCompilation = 1 << 3,
    AffectsTransparency = 1 << 4,
    AffectsRenderState = 1 << 5,
    Hidden = 1 << 6,
}