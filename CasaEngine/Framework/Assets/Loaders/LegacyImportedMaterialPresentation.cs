
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Loaders;

public readonly record struct LegacyImportedMaterialPresentation(
    RenderQueue Queue,
    float AlphaCutoff,
    bool DisableBackfaceCulling,
    Vector3 AmbientColor,
    Vector3 EmissiveColor);

public static class LegacyImportedMaterialPresentationResolver
{
    private const float BrightAmbientFloor = 128f / 255f;
    private const float OpaqueAlphaCutoff = 0.5f;
    private const float AlphaCutoutCutoff = 0.35f;

    public static LegacyImportedMaterialPresentation Resolve(StaticModelImportedMaterial importedMaterial)
    {
        ArgumentNullException.ThrowIfNull(importedMaterial);

        bool alphaCutout = importedMaterial.AlphaCutoutHint;
        Vector3 ambientColor = importedMaterial.AmbientColor;
        if (importedMaterial.BrightAmbientHint)
        {
            Vector3 brightAmbientFloor = new(BrightAmbientFloor, BrightAmbientFloor, BrightAmbientFloor);
            ambientColor = Vector3.Max(ambientColor, brightAmbientFloor);
        }

        return new LegacyImportedMaterialPresentation(
            alphaCutout ? RenderQueue.AlphaTest : RenderQueue.Opaque,
            alphaCutout ? AlphaCutoutCutoff : OpaqueAlphaCutoff,
            alphaCutout,
            Vector3.Clamp(ambientColor, Vector3.Zero, Vector3.One),
            Vector3.Clamp(importedMaterial.EmissiveColor, Vector3.Zero, Vector3.One));
    }
}