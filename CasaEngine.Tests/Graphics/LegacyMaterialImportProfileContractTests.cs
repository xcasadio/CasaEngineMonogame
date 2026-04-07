using CasaEngine.Framework.Assets.Loaders;
using Xunit;

namespace CasaEngine.Tests.Graphics;

public class LegacyMaterialImportProfileContractTests
{
    [Fact]
    public void Interpret_CanExpressGenericSurfaceIntentAndCombinedHints()
    {
        var interpretation = new LegacyMaterialImportInterpretation(
            LegacyMaterialSurfaceIntent.ReflectiveLit,
            LegacyMaterialImportHint.AlphaCutout | LegacyMaterialImportHint.BrightAmbient | LegacyMaterialImportHint.Reflection);
        ILegacyMaterialImportProfile profile = new StubProfile(interpretation);

        var importedMaterial = new StaticModelImportedMaterial
        {
            DisplayName = "LeafPanel",
            LegacyTechniqueIndex = 8,
        };

        var context = new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\assets\AlphaPalm.X",
            SourceAssetName: "AlphaPalm",
            ImportedMaterial: importedMaterial);

        Assert.Equal(interpretation, profile.Interpret(context));
    }

    private sealed class StubProfile : ILegacyMaterialImportProfile
    {
        private readonly LegacyMaterialImportInterpretation _interpretation;

        public StubProfile(LegacyMaterialImportInterpretation interpretation)
        {
            _interpretation = interpretation;
        }

        public LegacyMaterialImportInterpretation Interpret(in LegacyMaterialImportContext context)
            => _interpretation;
    }
}