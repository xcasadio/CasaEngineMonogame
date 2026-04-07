using System.Text.RegularExpressions;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ShaderTechniqueCoverageTests
{
    private static readonly Regex TechniqueRegex = new(
        @"TECHNIQUE\s*\(\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*,",
        RegexOptions.Compiled);

    private static readonly ShaderFeature[] CanonicalFeatureSets =
    {
        ShaderFeature.None,
        ShaderFeature.BasColorTexture,
        ShaderFeature.AlphaTest,
        ShaderFeature.AlphaTest | ShaderFeature.BasColorTexture,
        ShaderFeature.Transparent,
        ShaderFeature.Transparent | ShaderFeature.BasColorTexture,
        ShaderFeature.Skinned,
        ShaderFeature.Skinned | ShaderFeature.BasColorTexture,
        ShaderFeature.VertexColor,
        ShaderFeature.VertexColor | ShaderFeature.BasColorTexture,
        ShaderFeature.Instanced,
        ShaderFeature.Instanced | ShaderFeature.BasColorTexture,
    };

    [Fact]
    public void LitDiffuseTechniqueRequests_ExistInBasicEffect()
    {
        var availableTechniques = LoadTechniqueNames("basicEffect.fx");
        var requestedTechniques = new HashSet<string>(StringComparer.Ordinal);

        foreach (bool hasBasColor in new[] { false, true })
        {
            foreach (bool hasNormalMap in new[] { false, true })
            {
                foreach (bool hasReflection in new[] { false, true })
                {
                    foreach (bool oneLight in new[] { false, true })
                    {
                        var features = ShaderFeature.None;
                        if (hasBasColor)
                        {
                            features |= ShaderFeature.BasColorTexture;
                        }

                        if (hasBasColor && hasNormalMap)
                        {
                            features |= ShaderFeature.NormalMap;
                        }

                        if (hasReflection)
                        {
                            features |= ShaderFeature.Reflection;
                        }

                        requestedTechniques.Add(LitDiffuseMaterial.GetTechniqueName(features, oneLight));
                    }
                }
            }
        }

        AssertTechniquesExist(availableTechniques, requestedTechniques, "basicEffect.fx");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnlitTechniqueRequests_ExistInDeclaredEffects(bool hasBasColor)
    {
        var unlitTechniques = LoadTechniqueNames("UnlitTexture.fx");
        var basicEffectTechniques = LoadTechniqueNames("basicEffect.fx");

        Assert.Contains(UnlitTextureMaterial.GetPrimaryTechniqueName(hasBasColor), unlitTechniques);
        Assert.Contains(UnlitTextureMaterial.GetFallbackTechniqueName(hasBasColor), basicEffectTechniques);
    }

    [Fact]
    public void MaterialFacingAliasMaps_PointToExistingTechniques()
    {
        AssertAliasMapResolvesToExistingTechniques(
            ShaderVariantLibrary.BuildBasicEffectAliases(),
            LoadTechniqueNames("basicEffect.fx"),
            "basicEffect.fx");
        AssertAliasMapResolvesToExistingTechniques(
            ShaderVariantLibrary.BuildUnlitTextureAliases(),
            LoadTechniqueNames("UnlitTexture.fx"),
            "UnlitTexture.fx");
        AssertAliasMapResolvesToExistingTechniques(
            ShaderVariantLibrary.BuildSkinnedEffectAliases(),
            LoadTechniqueNames("skinEffect.fx"),
            "skinEffect.fx");
    }

    [Fact]
    public void CanonicalTechniqueRequests_AreCoveredByAllMaterialFacingAliasMaps()
    {
        var aliasMaps = new[]
        {
            (Name: "basicEffect.fx", Aliases: ShaderVariantLibrary.BuildBasicEffectAliases()),
            (Name: "UnlitTexture.fx", Aliases: ShaderVariantLibrary.BuildUnlitTextureAliases()),
            (Name: "skinEffect.fx", Aliases: ShaderVariantLibrary.BuildSkinnedEffectAliases()),
        };

        foreach (var features in CanonicalFeatureSets)
        {
            var canonicalTechnique = ShaderVariantLibrary.BuildTechniqueName(features);
            Assert.False(string.IsNullOrWhiteSpace(canonicalTechnique));

            foreach (var aliasMap in aliasMaps)
            {
                Assert.True(
                    aliasMap.Aliases.ContainsKey(canonicalTechnique!),
                    $"Alias map for '{aliasMap.Name}' does not define canonical technique '{canonicalTechnique}' for features '{features}'.");
            }
        }
    }

    private static void AssertAliasMapResolvesToExistingTechniques(
        IReadOnlyDictionary<string, string> aliases,
        IReadOnlySet<string> availableTechniques,
        string shaderFileName)
    {
        var resolvedTechniques = new HashSet<string>(aliases.Values, StringComparer.Ordinal);
        AssertTechniquesExist(availableTechniques, resolvedTechniques, shaderFileName);
    }

    private static void AssertTechniquesExist(
        IReadOnlySet<string> availableTechniques,
        IEnumerable<string> expectedTechniques,
        string shaderFileName)
    {
        foreach (var technique in expectedTechniques)
        {
            Assert.True(
                availableTechniques.Contains(technique),
                $"Shader '{shaderFileName}' does not declare technique '{technique}'.");
        }
    }

    private static IReadOnlySet<string> LoadTechniqueNames(string shaderFileName)
    {
        string shaderPath = Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Shaders", shaderFileName);
        string source = File.ReadAllText(shaderPath);
        var techniques = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in TechniqueRegex.Matches(source))
        {
            techniques.Add(match.Groups["name"].Value);
        }

        return techniques;
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