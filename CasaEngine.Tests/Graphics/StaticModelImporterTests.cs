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
        string modelPath = Path.Combine(FindWorkspaceRoot(), "RacingGame", "Content", "Models", "AlphaPalm.X");

        var result = importer.ImportWithMetadata(modelPath);

        StaticModelImportedMaterial leafMaterial = FindMaterialByDiffuseTexture(result.Materials, "PalmLeave.tga");
        AssertVector3Close(new Vector3(0.1f, 0.1f, 0.1f), leafMaterial.AmbientColor);
        Assert.Equal(new Color(255, 255, 255, 255), leafMaterial.DiffuseColor);
        AssertVector3Close(new Vector3(1.0f, 1.0f, 1.0f), leafMaterial.SpecularColor);
        Assert.Equal(16.0f, leafMaterial.SpecularPower);
        Assert.Equal("PalmLeaveNormal.tga", Path.GetFileName(leafMaterial.NormalTextureFilePath));
        Assert.Equal("NormalMapping.fx", Path.GetFileName(leafMaterial.EffectFilePath));
        Assert.Equal(4, leafMaterial.LegacyTechniqueIndex);
        Assert.True(leafMaterial.AlphaCutoutHint);
        Assert.False(leafMaterial.BrightAmbientHint);
    }

    [Fact]
    public void ImportWithMetadata_PreservesReflectionMetadata_ForSign()
    {
        var importer = new StaticModelImporter();
        string modelPath = Path.Combine(FindWorkspaceRoot(), "RacingGame", "Content", "Models", "Sign.X");

        var result = importer.ImportWithMetadata(modelPath);

        StaticModelImportedMaterial signMaterial = FindMaterialByDiffuseTexture(result.Materials, "Schild.tga");
        AssertVector3Close(new Vector3(0.313726f, 0.313726f, 0.313726f), signMaterial.AmbientColor);
        AssertColorClose(new Color(213, 213, 213, 255), signMaterial.DiffuseColor, tolerance: 1);
        AssertVector3Close(new Vector3(0.819608f, 0.819608f, 0.819608f), signMaterial.SpecularColor);
        Assert.Equal(16.0f, signMaterial.SpecularPower);
        Assert.Equal("SkyCubeMap.dds", Path.GetFileName(signMaterial.ReflectionTextureFilePath));
        Assert.Equal("NormalMapping.fx", Path.GetFileName(signMaterial.EffectFilePath));
        Assert.Equal(8, signMaterial.LegacyTechniqueIndex);
        Assert.True(signMaterial.UsesReflection);
        Assert.False(signMaterial.BrightAmbientHint);
        Assert.False(signMaterial.AlphaCutoutHint);
    }

    [Fact]
    public void GetTextureFilePaths_IncludesReflectionTexture_ForReflectiveModel()
    {
        var importer = new StaticModelImporter();
        string modelPath = Path.Combine(FindWorkspaceRoot(), "RacingGame", "Content", "Models", "Sign.X");

        var texturePaths = importer.GetTextureFilePaths(modelPath);

        Assert.Contains(texturePaths, path => string.Equals(Path.GetFileName(path), "SkyCubeMap.dds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ImportWithMetadata_AppliesOptionalLegacyImportProfileInterpretation()
    {
        var importer = new StaticModelImporter();
        string modelPath = Path.Combine(FindWorkspaceRoot(), "RacingGame", "Content", "Models", "Sign.X");

        var result = importer.ImportWithMetadata(
            modelPath,
            new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                LegacyMaterialSurfaceIntent.AlphaCutoutLit,
                LegacyMaterialImportHint.AlphaCutout | LegacyMaterialImportHint.Reflection)));

        StaticModelImportedMaterial signMaterial = FindMaterialByDiffuseTexture(result.Materials, "Schild.tga");
        Assert.Equal(LegacyMaterialSurfaceIntent.AlphaCutoutLit, signMaterial.SurfaceIntent);
        Assert.True(signMaterial.AlphaCutoutHint);
        Assert.False(signMaterial.BrightAmbientHint);
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

    private static void AssertColorClose(Color expected, Color actual, byte tolerance)
    {
        Assert.InRange(Math.Abs(expected.R - actual.R), 0, tolerance);
        Assert.InRange(Math.Abs(expected.G - actual.G), 0, tolerance);
        Assert.InRange(Math.Abs(expected.B - actual.B), 0, tolerance);
        Assert.InRange(Math.Abs(expected.A - actual.A), 0, tolerance);
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

    private static string FindWorkspaceRoot()
    {
        string repositoryRoot = FindRepositoryRoot();
        string? workspaceRoot = Directory.GetParent(repositoryRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new DirectoryNotFoundException("Unable to locate the workspace root from the repository root.");
        }

        return workspaceRoot;
    }

    private sealed class StubLegacyImportProfile : ILegacyMaterialImportProfile
    {
        private readonly LegacyMaterialImportInterpretation _interpretation;

        public StubLegacyImportProfile(LegacyMaterialImportInterpretation interpretation)
        {
            _interpretation = interpretation;
        }

        public LegacyMaterialImportInterpretation Interpret(in LegacyMaterialImportContext context)
            => _interpretation;
    }
}