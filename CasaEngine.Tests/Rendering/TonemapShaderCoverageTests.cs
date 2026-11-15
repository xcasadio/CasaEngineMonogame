using System.Text.RegularExpressions;

using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T1.3 of the PBR plan: Tonemap.fx is compiled, registered as a built-in and hot reloadable (ADR-0102). The checks read
/// the text of the shader, the content file and the catalog: the compiler itself is exercised by the build.
/// </summary>
public class TonemapShaderCoverageTests
{
    private static readonly Regex TechniqueRegex = new(
        @"technique\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",
        RegexOptions.Compiled);

    [Fact]
    public void TonemapShader_DeclaresBothOperatorTechniques()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TechniqueRegex.Matches(ReadShader()))
        {
            names.Add(match.Groups["name"].Value);
        }

        Assert.Contains("Tonemap_Reinhard", names);
        Assert.Contains("Tonemap_AcesApprox", names);
    }

    [Fact]
    public void TonemapShader_DeclaresExposureAndTheSourceTexture()
    {
        string source = ReadShader();

        Assert.Contains($"float {ShaderParameterNames.Exposure}", source, StringComparison.Ordinal);
        Assert.Contains($"Texture2D {ShaderParameterNames.SceneTexture};", source, StringComparison.Ordinal);
        Assert.Contains($"Texture = <{ShaderParameterNames.SceneTexture}>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TonemapShader_AppliesExposureBeforeTheOperatorAndEncodesTheOutput()
    {
        string source = ReadShader();

        Assert.Equal(2, Regex.Matches(source, @"\*\s*Exposure").Count);
        Assert.Contains("c / (1.0f + c)", source, StringComparison.Ordinal);
        Assert.Contains("(c * (2.51f * c + 0.03f)) / (c * (2.43f * c + 0.59f) + 0.14f)", source, StringComparison.Ordinal);
        Assert.Contains("pow(saturate(linearColor), 1.0f / 2.2f)", source, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(source, @"float4\(EncodeForDisplay\(c\), 1\.0f\)").Count);
    }

    [Fact]
    public void TonemapShader_IsPixelOnlySoSpriteBatchKeepsItsVertexShader()
    {
        string source = ReadShader();

        Assert.DoesNotContain("VertexShader", source, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(source, @"PixelShader\s*=\s*compile").Count);
    }

    [Fact]
    public void ContentPipeline_IncludesTonemapShader()
    {
        string mgcb = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Content.mgcb"));

        Assert.Contains("#begin Shaders/Tonemap.fx", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Shaders/Tonemap.fx", mgcb, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInShaderCatalog_RegistersTonemapShader()
    {
        Assert.True(BuiltInShaderCatalog.TryGetByContentName(BuiltInShaderCatalog.TonemapContentName, out var byName));
        Assert.Equal("Shaders/Tonemap.fx", byName.SourceRelativePath);

        Assert.True(BuiltInShaderCatalog.TryGetBySourceRelativePath("Shaders/Tonemap.fx", out var byPath));
        Assert.Equal(BuiltInShaderCatalog.TonemapContentName, byPath.ContentName);
    }

    [Fact]
    public void RenderPipeline_HoldsAndReloadsTheTonemapShader()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "RenderPipeline.cs"));
        string game = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "CasaEngine", "Framework", "Application", "CasaEngineGame.cs"));

        Assert.Contains("ShaderWrapper _tonemapShader", source, StringComparison.Ordinal);
        Assert.Contains("case BuiltInShaderCatalog.TonemapContentName:", game, StringComparison.Ordinal);
        Assert.Contains("SetTonemapEffect(Content.Load<Effect>(BuiltInShaderCatalog.TonemapContentName))", game, StringComparison.Ordinal);
    }

    private static string ReadShader()
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Shaders", "Tonemap.fx"));
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
