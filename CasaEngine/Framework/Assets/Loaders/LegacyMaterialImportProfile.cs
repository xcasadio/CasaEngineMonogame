namespace CasaEngine.Framework.Assets.Loaders;

[Flags]
public enum LegacyMaterialImportHint
{
    None = 0,
    AlphaCutout = 1 << 0,
    BrightAmbient = 1 << 1,
    Reflection = 1 << 2,
}

public enum LegacyMaterialSurfaceIntent
{
    OpaqueLit,
    AlphaCutoutLit,
    ReflectiveLit,
}

/// <summary>
/// Interprets raw legacy material metadata into neutral surface semantics and hints.
/// Implementations may use asset-specific conventions, but the contract itself stays
/// generic and does not encode any game-specific naming rules.
/// </summary>
public interface ILegacyMaterialImportProfile
{
    LegacyMaterialSurfaceIntent ResolveSurfaceIntent(StaticModelImportedMaterial importedMaterial, string sourceAssetName);

    LegacyMaterialImportHint ResolveHints(StaticModelImportedMaterial importedMaterial, string sourceAssetName);
}