using CasaEngine.Framework.Assets.Loaders;
using Xunit;

namespace CasaEngine.Tests.Graphics;

public class NeutralLegacyMaterialImportProfileTests
{
    [Fact]
    public void Interpret_ConsumesExplicitImportedMetadataWithoutAddingNamingRules()
    {
        var profile = NeutralLegacyMaterialImportProfile.Instance;
        var importedMaterial = new StaticModelImportedMaterial
        {
            AlphaCutoutHint = true,
            BrightAmbientHint = true,
            UsesReflection = true,
        };

        var interpretation = profile.Interpret(new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\assets\AlphaPalm.X",
            SourceAssetName: "AlphaPalm",
            ImportedMaterial: importedMaterial));

        Assert.Equal(LegacyMaterialSurfaceIntent.ReflectiveLit, interpretation.SurfaceIntent);
        Assert.True(interpretation.AlphaCutout);
        Assert.True(interpretation.BrightAmbient);
        Assert.True(interpretation.Reflection);
    }

    [Fact]
    public void Interpret_DoesNotDependOnSourceAssetName()
    {
        var profile = NeutralLegacyMaterialImportProfile.Instance;
        var importedMaterial = new StaticModelImportedMaterial();

        var firstInterpretation = profile.Interpret(new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\assets\ModelA.X",
            SourceAssetName: "AlphaPalm",
            ImportedMaterial: importedMaterial));
        var secondInterpretation = profile.Interpret(new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\assets\ModelB.X",
            SourceAssetName: "Sign",
            ImportedMaterial: importedMaterial));

        Assert.Equal(firstInterpretation, secondInterpretation);
        Assert.Equal(LegacyMaterialSurfaceIntent.OpaqueLit, firstInterpretation.SurfaceIntent);
        Assert.Equal(LegacyMaterialImportHint.None, firstInterpretation.Hints);
    }

    [Fact]
    public void Interpret_IgnoresAlphaStyleAssetNamesWithoutExplicitHints()
    {
        var profile = NeutralLegacyMaterialImportProfile.Instance;

        var interpretation = profile.Interpret(new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\assets\AlphaPalm.X",
            SourceAssetName: "AlphaPalm",
            ImportedMaterial: new StaticModelImportedMaterial()));

        Assert.Equal(LegacyMaterialSurfaceIntent.OpaqueLit, interpretation.SurfaceIntent);
        Assert.False(interpretation.AlphaCutout);
        Assert.False(interpretation.BrightAmbient);
        Assert.False(interpretation.Reflection);
    }

    [Fact]
    public void Interpret_EnablesReflectionFromExplicitReflectionMetadata()
    {
        var profile = NeutralLegacyMaterialImportProfile.Instance;

        var interpretation = profile.Interpret(new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\assets\MirrorPlate.X",
            SourceAssetName: "MirrorPlate",
            ImportedMaterial: new StaticModelImportedMaterial
            {
                ReflectionTextureFilePath = "SkyCubeMap.dds",
            }));

        Assert.Equal(LegacyMaterialSurfaceIntent.OpaqueLit, interpretation.SurfaceIntent);
        Assert.False(interpretation.Reflection);
        Assert.False(interpretation.AlphaCutout);
    }
}