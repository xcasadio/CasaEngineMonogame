using CasaEngine.Framework.Assets.Loaders;
using Xunit;

namespace CasaEngine.Tests.Graphics;

public class LegacyMaterialImportContextTests
{
    [Fact]
    public void Interpretation_BooleanHelpersReflectCombinedHints()
    {
        var interpretation = new LegacyMaterialImportInterpretation(
            LegacyMaterialSurfaceIntent.ReflectiveLit,
            LegacyMaterialImportHint.AlphaCutout | LegacyMaterialImportHint.BrightAmbient | LegacyMaterialImportHint.Reflection);

        Assert.True(interpretation.AlphaCutout);
        Assert.True(interpretation.BrightAmbient);
        Assert.True(interpretation.Reflection);
    }

    [Fact]
    public void Context_PreservesSourceMetadataAlongsideImportedMaterial()
    {
        var importedMaterial = new StaticModelImportedMaterial
        {
            DisplayName = "LeafPanel",
            LegacyTechniqueIndex = 4,
        };

        var context = new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\assets\AlphaPalm.X",
            SourceAssetName: "AlphaPalm",
            ImportedMaterial: importedMaterial);

        Assert.Equal(@"D:\assets\AlphaPalm.X", context.SourceAssetPath);
        Assert.Equal("AlphaPalm", context.SourceAssetName);
        Assert.Same(importedMaterial, context.ImportedMaterial);
    }
}