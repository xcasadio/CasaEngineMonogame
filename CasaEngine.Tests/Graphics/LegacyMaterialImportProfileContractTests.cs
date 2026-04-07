using CasaEngine.Framework.Assets.Loaders;
using Xunit;

namespace CasaEngine.Tests.Graphics;

public class LegacyMaterialImportProfileContractTests
{
    [Fact]
    public void ResolveMethods_CanExpressGenericSurfaceIntentAndCombinedHints()
    {
        ILegacyMaterialImportProfile profile = new StubProfile(
            LegacyMaterialSurfaceIntent.ReflectiveLit,
            LegacyMaterialImportHint.AlphaCutout | LegacyMaterialImportHint.BrightAmbient | LegacyMaterialImportHint.Reflection);

        var importedMaterial = new StaticModelImportedMaterial
        {
            DisplayName = "LeafPanel",
            LegacyTechniqueIndex = 8,
        };

        Assert.Equal(LegacyMaterialSurfaceIntent.ReflectiveLit, profile.ResolveSurfaceIntent(importedMaterial, "AlphaPalm"));
        Assert.Equal(
            LegacyMaterialImportHint.AlphaCutout | LegacyMaterialImportHint.BrightAmbient | LegacyMaterialImportHint.Reflection,
            profile.ResolveHints(importedMaterial, "AlphaPalm"));
    }

    private sealed class StubProfile : ILegacyMaterialImportProfile
    {
        private readonly LegacyMaterialSurfaceIntent _surfaceIntent;
        private readonly LegacyMaterialImportHint _hints;

        public StubProfile(LegacyMaterialSurfaceIntent surfaceIntent, LegacyMaterialImportHint hints)
        {
            _surfaceIntent = surfaceIntent;
            _hints = hints;
        }

        public LegacyMaterialSurfaceIntent ResolveSurfaceIntent(StaticModelImportedMaterial importedMaterial, string sourceAssetName)
            => _surfaceIntent;

        public LegacyMaterialImportHint ResolveHints(StaticModelImportedMaterial importedMaterial, string sourceAssetName)
            => _hints;
    }
}