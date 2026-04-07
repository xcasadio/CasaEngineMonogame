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

public readonly record struct LegacyMaterialImportContext(
    string SourceAssetPath,
    string SourceAssetName,
    StaticModelImportedMaterial ImportedMaterial);

public readonly record struct LegacyMaterialImportInterpretation(
    LegacyMaterialSurfaceIntent SurfaceIntent,
    LegacyMaterialImportHint Hints)
{
    public bool AlphaCutout => (Hints & LegacyMaterialImportHint.AlphaCutout) != 0;

    public bool BrightAmbient => (Hints & LegacyMaterialImportHint.BrightAmbient) != 0;

    public bool Reflection => (Hints & LegacyMaterialImportHint.Reflection) != 0;
}

/// <summary>
/// Interprets raw legacy material metadata into neutral surface semantics and hints.
/// Implementations may use asset-specific conventions, but the contract itself stays
/// generic and does not encode any game-specific naming rules.
/// </summary>
public interface ILegacyMaterialImportProfile
{
    LegacyMaterialImportInterpretation Interpret(in LegacyMaterialImportContext context);
}

public sealed class NeutralLegacyMaterialImportProfile : ILegacyMaterialImportProfile
{
    public static NeutralLegacyMaterialImportProfile Instance { get; } = new();

    public LegacyMaterialImportInterpretation Interpret(in LegacyMaterialImportContext context)
    {
        LegacyMaterialImportHint hints = LegacyMaterialImportHint.None;

        if (context.ImportedMaterial.AlphaCutoutHint)
        {
            hints |= LegacyMaterialImportHint.AlphaCutout;
        }

        if (context.ImportedMaterial.BrightAmbientHint)
        {
            hints |= LegacyMaterialImportHint.BrightAmbient;
        }

        if (context.ImportedMaterial.UsesReflection
            || !string.IsNullOrWhiteSpace(context.ImportedMaterial.ReflectionTextureFilePath))
        {
            hints |= LegacyMaterialImportHint.Reflection;
        }

        var surfaceIntent = (hints & LegacyMaterialImportHint.Reflection) != 0
            ? LegacyMaterialSurfaceIntent.ReflectiveLit
            : (hints & LegacyMaterialImportHint.AlphaCutout) != 0
                ? LegacyMaterialSurfaceIntent.AlphaCutoutLit
                : LegacyMaterialSurfaceIntent.OpaqueLit;

        return new LegacyMaterialImportInterpretation(surfaceIntent, hints);
    }
}