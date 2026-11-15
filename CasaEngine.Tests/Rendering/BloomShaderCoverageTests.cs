using System.Globalization;
using System.Text.RegularExpressions;

using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T2.1 of the PBR plan: Bloom.fx is compiled, registered as a built-in and hot reloadable (ADR-0107). The checks read the
/// text of the shader, the content file and the catalog: the compiler itself is exercised by the build.
/// </summary>
public class BloomShaderCoverageTests
{
    private static readonly Regex TechniqueRegex = new(
        @"technique\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",
        RegexOptions.Compiled);

    private static readonly Regex BlurWeightRegex = new(
        @"static\s+const\s+float\s+BlurWeight(?<index>\d+)\s*=\s*(?<value>[0-9.]+)f\s*;",
        RegexOptions.Compiled);

    [Fact]
    public void BloomShader_DeclaresTheFiveTechniques()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TechniqueRegex.Matches(ReadShader()))
        {
            names.Add(match.Groups["name"].Value);
        }

        Assert.Equal(5, names.Count);
        Assert.Contains("Bloom_BrightPass", names);
        Assert.Contains("Bloom_Downsample", names);
        Assert.Contains("Bloom_BlurHorizontal", names);
        Assert.Contains("Bloom_BlurVertical", names);
        Assert.Contains("Bloom_Composite", names);
    }

    [Fact]
    public void BloomShader_DeclaresItsParametersAndTheSourceTexture()
    {
        string source = ReadShader();

        Assert.Contains($"float {ShaderParameterNames.BloomThreshold}", source, StringComparison.Ordinal);
        Assert.Contains($"float {ShaderParameterNames.BloomIntensity}", source, StringComparison.Ordinal);
        Assert.Contains($"float2 {ShaderParameterNames.BloomTexelSize}", source, StringComparison.Ordinal);
        Assert.Contains($"Texture2D {ShaderParameterNames.BloomSourceTexture};", source, StringComparison.Ordinal);
        Assert.Contains($"Texture = <{ShaderParameterNames.BloomSourceTexture}>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BloomShader_BlurWeightsAreNormalizedAndSumToOne()
    {
        var weights = new SortedDictionary<int, double>();
        foreach (Match match in BlurWeightRegex.Matches(ReadShader()))
        {
            weights[int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture)] =
                double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        }

        Assert.Equal(5, weights.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, weights.Keys.ToArray());

        // Taps at offsets 0 and +-1 ... +-4: the centre once, the others twice.
        double sum = weights[0];
        for (int i = 1; i <= 4; i++)
        {
            sum += 2.0 * weights[i];
        }

        Assert.InRange(sum, 1.0 - 1e-6, 1.0 + 1e-6);

        // A blur is bell shaped: the weights decrease away from the centre.
        for (int i = 1; i <= 4; i++)
        {
            Assert.True(weights[i] < weights[i - 1]);
        }
    }

    [Fact]
    public void BloomShader_IsPixelOnlySoSpriteBatchKeepsItsVertexShader()
    {
        string source = ReadShader();

        Assert.DoesNotContain("VertexShader", source, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Matches(source, @"PixelShader\s*=\s*compile").Count);
    }

    [Fact]
    public void BloomShader_DoesNotEncodeItsOutput()
    {
        // Bloom reads and writes linear light; the encoding for display belongs to the tonemapping (Tonemap.fx).
        Assert.DoesNotContain("pow(", ReadShader(), StringComparison.Ordinal);
    }

    [Fact]
    public void ContentPipeline_IncludesBloomShader()
    {
        string mgcb = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Content.mgcb"));

        Assert.Contains("#begin Shaders/Bloom.fx", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Shaders/Bloom.fx", mgcb, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInShaderCatalog_RegistersBloomShader()
    {
        Assert.True(BuiltInShaderCatalog.TryGetByContentName(BuiltInShaderCatalog.BloomContentName, out var byName));
        Assert.Equal("Shaders/Bloom.fx", byName.SourceRelativePath);

        Assert.True(BuiltInShaderCatalog.TryGetBySourceRelativePath("Shaders/Bloom.fx", out var byPath));
        Assert.Equal(BuiltInShaderCatalog.BloomContentName, byPath.ContentName);
    }

    [Fact]
    public void RenderPipeline_HoldsAndReloadsTheBloomShader()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "RenderPipeline.cs"));
        string game = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "CasaEngine", "Framework", "Application", "CasaEngineGame.cs"));

        Assert.Contains("ShaderWrapper _bloomShader", source, StringComparison.Ordinal);
        Assert.Contains("case BuiltInShaderCatalog.BloomContentName:", game, StringComparison.Ordinal);
        Assert.Contains("SetBloomEffect(Content.Load<Effect>(BuiltInShaderCatalog.BloomContentName))", game, StringComparison.Ordinal);
    }

    [Fact]
    public void BloomRenderer_UsesTheFormatOfTheSceneTargetAndNoDepth_AndSkipsAColorTarget()
    {
        string renderer = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "PostProcess", "BloomRenderer.cs"));

        Assert.Contains("var format = hdrTarget.Format;", renderer, StringComparison.Ordinal);
        Assert.Contains("pool.Acquire(width, height, format, DepthFormat.None)", renderer, StringComparison.Ordinal);
        Assert.Contains("lastUsedRender[(width, height, format, DepthFormat.None)] = renderIndex;", renderer, StringComparison.Ordinal);
        Assert.Contains("hdrTarget.Format == SurfaceFormat.Color", renderer, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(renderer, @"WriteWarning\(").Count);
        Assert.Contains("_colorFormatWarningLogged", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHdrView_AppliesTheBloomBetweenTheSceneAndTheTonemapping()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "RenderPipeline.cs"));
        int start = source.IndexOf("private void RenderHdrView(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        string body = source[start..];

        int scene = body.IndexOf("scenePipeline.RenderScene(", StringComparison.Ordinal);
        int bloom = body.IndexOf("ApplyBloom(hdrTarget, in frame);", StringComparison.Ordinal);
        int tonemap = body.IndexOf("DrawTonemap(hdrTarget,", StringComparison.Ordinal);
        int interfaceCompose = body.IndexOf(".Compose(_graphicsDevice, view, in frame)", StringComparison.Ordinal);

        Assert.True(scene >= 0 && scene < bloom);
        Assert.True(bloom < tonemap);
        Assert.True(tonemap < interfaceCompose);
        Assert.Contains("if (frame.Environment.BloomEnabled)", body, StringComparison.Ordinal);
    }

    private static string ReadShader()
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Shaders", "Bloom.fx"));
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
