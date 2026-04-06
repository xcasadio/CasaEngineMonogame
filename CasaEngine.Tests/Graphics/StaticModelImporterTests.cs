using CasaEngine.Framework.Assets.Loaders;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Graphics;

public class StaticModelImporterTests
{
    [Fact]
    public void ImportWithMetadata_PreservesLegacyEffectParameters_ForAlphaPalm()
    {
        var importer = new StaticModelImporter();
        string modelPath = Path.Combine(FindRepositoryRoot(), "RacingGame", "Content", "Models", "AlphaPalm.X");

        var result = importer.ImportWithMetadata(modelPath);

        StaticModelImportedMaterial leafMaterial = FindMaterialByDiffuseTexture(result.Materials, "PalmLeave.tga");
        AssertVector3Close(new Vector3(0.1f, 0.1f, 0.1f), leafMaterial.AmbientColor);
        Assert.Equal(new Color(255, 255, 255, 255), leafMaterial.DiffuseColor);
        AssertVector3Close(new Vector3(1.0f, 1.0f, 1.0f), leafMaterial.SpecularColor);
        Assert.Equal(16.0f, leafMaterial.SpecularPower);
        Assert.Equal("PalmLeaveNormal.tga", Path.GetFileName(leafMaterial.NormalTextureFilePath));
        Assert.Equal("NormalMapping.fx", Path.GetFileName(leafMaterial.EffectFilePath));
        Assert.Equal(4, leafMaterial.LegacyTechniqueIndex);
    }

    [Fact]
    public void ImportWithMetadata_PreservesReflectionMetadata_ForSign()
    {
        var importer = new StaticModelImporter();
        string modelPath = Path.Combine(FindRepositoryRoot(), "RacingGame", "Content", "Models", "Sign.X");

        var result = importer.ImportWithMetadata(modelPath);

        StaticModelImportedMaterial signMaterial = FindMaterialByDiffuseTexture(result.Materials, "Schild.tga");
        AssertVector3Close(new Vector3(0.313726f, 0.313726f, 0.313726f), signMaterial.AmbientColor);
        Assert.Equal(new Color(213, 213, 213, 255), signMaterial.DiffuseColor);
        AssertVector3Close(new Vector3(0.819608f, 0.819608f, 0.819608f), signMaterial.SpecularColor);
        Assert.Equal(16.0f, signMaterial.SpecularPower);
        Assert.Equal("SkyCubeMap.dds", Path.GetFileName(signMaterial.ReflectionTextureFilePath));
        Assert.Equal("NormalMapping.fx", Path.GetFileName(signMaterial.EffectFilePath));
        Assert.Equal(8, signMaterial.LegacyTechniqueIndex);
        Assert.True(signMaterial.UsesReflection);
    }

    private static StaticModelImportedMaterial FindMaterialByDiffuseTexture(
        IReadOnlyList<StaticModelImportedMaterial> materials,
        string textureFileName)
    {
        StaticModelImportedMaterial? material = materials.FirstOrDefault(
            candidate => string.Equals(Path.GetFileName(candidate.DiffuseTextureFilePath), textureFileName, StringComparison.OrdinalIgnoreCase));

        return Assert.Single(material is null ? Array.Empty<StaticModelImportedMaterial>() : new[] { material });
    }

    private static void AssertVector3Close(Vector3 expected, Vector3 actual, float tolerance = 0.001f)
    {
        Assert.InRange(Vector3.Distance(expected, actual), 0.0f, tolerance);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }
}