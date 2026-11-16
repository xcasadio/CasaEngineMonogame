using System.Text.RegularExpressions;

using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T3.3 of the clear coat, SSAO and FXAA plan: Ssao.fx is compiled, registered as a built-in and hot reloadable, the composition sits
/// between the scene and the bloom and only in a view whose scene depth pre-pass is active, and its targets are stamped for eviction
/// and counted apart from the bloom, FXAA and pre-pass ones (ADR-0116). The checks read the text of the shader, the content file, the
/// catalog and the pipeline: the compiler itself is exercised by the build.
/// </summary>
public class SsaoShaderCoverageTests
{
    private static readonly Regex TechniqueRegex = new(
        @"technique\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",
        RegexOptions.Compiled);

    [Fact]
    public void SsaoShader_DeclaresTheFourTechniques()
    {
        var names = new List<string>();
        foreach (Match match in TechniqueRegex.Matches(ReadShader()))
        {
            names.Add(match.Groups["name"].Value);
        }

        Assert.Equal(new[] { "Ssao_Occlusion", "Ssao_BlurHorizontal", "Ssao_BlurVertical", "Ssao_Composite" }, names);
    }

    [Fact]
    public void SsaoShader_DeclaresTheParametersTheRendererSets()
    {
        string source = ReadShader();

        Assert.Contains($"float2 {ShaderParameterNames.SsaoTexelSize}", source, StringComparison.Ordinal);
        Assert.Contains($"float2 {ShaderParameterNames.SsaoProjectionScale}", source, StringComparison.Ordinal);
        Assert.Contains($"float {ShaderParameterNames.SsaoRadius}", source, StringComparison.Ordinal);
        Assert.Contains($"float {ShaderParameterNames.SsaoBias}", source, StringComparison.Ordinal);
        Assert.Contains($"float {ShaderParameterNames.SsaoIntensity}", source, StringComparison.Ordinal);
        Assert.Contains($"float {ShaderParameterNames.SsaoSkyThreshold}", source, StringComparison.Ordinal);
        Assert.Contains($"Texture2D {ShaderParameterNames.SsaoSourceTexture};", source, StringComparison.Ordinal);
        Assert.Contains($"Texture = <{ShaderParameterNames.SsaoSourceTexture}>", source, StringComparison.Ordinal);
        Assert.Contains($"Texture2D {ShaderParameterNames.SsaoDepthTexture};", source, StringComparison.Ordinal);
        Assert.Contains($"Texture = <{ShaderParameterNames.SsaoDepthTexture}>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SsaoShader_IsPixelOnlySoSpriteBatchKeepsItsVertexShader()
    {
        string source = ReadShader();

        Assert.DoesNotContain("VertexShader", source, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Matches(source, @"PixelShader\s*=\s*compile").Count);
    }

    [Fact]
    public void SsaoShader_ReadsWithTex2DOnly_AndFiltersWithPoint()
    {
        string source = ReadShader();

        // The OpenGL chain ignores the requested level (ADR-0110): no tex2Dlod. The depth and the occlusion are read texel by texel.
        Assert.DoesNotContain("tex2Dlod", source, StringComparison.Ordinal);
        Assert.DoesNotContain("tex2Dgrad", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ddx(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ddy(", source, StringComparison.Ordinal);
        Assert.Contains("tex2D(SsaoSourceSampler", source, StringComparison.Ordinal);
        Assert.Contains("tex2D(SsaoDepthSampler", source, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(source, @"MinFilter = Point;").Count);
        Assert.Equal(2, Regex.Matches(source, @"MagFilter = Point;").Count);
        Assert.DoesNotContain("Linear", source.Replace("linear", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void SsaoShader_UsesAFixedKernelOfEightToSixteenSamples_AndNoTexture()
    {
        string source = ReadShader();

        Match samples = Regex.Match(source, @"#define SSAO_SAMPLES (?<count>\d+)");
        Assert.True(samples.Success);
        Assert.InRange(int.Parse(samples.Groups["count"].Value), 8, 16);

        // The noise is computed from the pixel position: no noise texture is declared beside the two samplers.
        Assert.Equal(2, Regex.Matches(source, @"\bTexture2D\b").Count);
        Assert.Equal(2, Regex.Matches(source, @"\bsampler2D\b").Count);
    }

    [Fact]
    public void SsaoShader_HasNoEarlyReturnSoEveryReadStaysInUniformFlowControl()
    {
        string source = ReadShader();

        foreach (string function in new[] { "float4 PS_Occlusion(", "float4 BlurAlong(", "float4 PS_Composite(" })
        {
            int start = source.IndexOf(function, StringComparison.Ordinal);
            Assert.True(start >= 0, function);
            int end = source.IndexOf("\n}", start, StringComparison.Ordinal);
            Assert.True(end > start, function);
            Assert.Equal(1, Regex.Matches(source[start..end], @"\breturn\b").Count);
        }
    }

    [Fact]
    public void ContentPipeline_IncludesSsaoShader()
    {
        string mgcb = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Content.mgcb"));

        Assert.Contains("#begin Shaders/Ssao.fx", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Shaders/Ssao.fx", mgcb, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInShaderCatalog_RegistersSsaoShader()
    {
        Assert.True(BuiltInShaderCatalog.TryGetByContentName(BuiltInShaderCatalog.SsaoContentName, out var byName));
        Assert.Equal("Shaders/Ssao.fx", byName.SourceRelativePath);

        Assert.True(BuiltInShaderCatalog.TryGetBySourceRelativePath("Shaders/Ssao.fx", out var byPath));
        Assert.Equal(BuiltInShaderCatalog.SsaoContentName, byPath.ContentName);
    }

    [Fact]
    public void RenderPipeline_HoldsAndReloadsTheSsaoShader()
    {
        string pipeline = ReadSource("Framework", "Rendering", "RenderPipeline.cs");
        string game = ReadSource("Framework", "Application", "CasaEngineGame.cs");

        Assert.Contains("ShaderWrapper _ssaoShader", pipeline, StringComparison.Ordinal);
        Assert.Contains("BuiltInShaderCatalog.SsaoContentName", pipeline, StringComparison.Ordinal);
        Assert.Contains("case BuiltInShaderCatalog.SsaoContentName:", game, StringComparison.Ordinal);
        Assert.Contains("SetSsaoEffect(Content.Load<Effect>(BuiltInShaderCatalog.SsaoContentName))", game, StringComparison.Ordinal);
    }

    [Fact]
    public void SsaoRenderer_MultipliesTheSceneAndLeavesItsAlphaAndItsStatesAlone()
    {
        string renderer = ReadSource("Framework", "Rendering", "PostProcess", "SsaoRenderer.cs");

        Assert.Contains("ColorSourceBlend = Blend.Zero", renderer, StringComparison.Ordinal);
        Assert.Contains("ColorDestinationBlend = Blend.SourceColor", renderer, StringComparison.Ordinal);
        Assert.Contains("AlphaSourceBlend = Blend.Zero", renderer, StringComparison.Ordinal);
        Assert.Contains("AlphaDestinationBlend = Blend.One", renderer, StringComparison.Ordinal);
        // The depth buffer of the scene target is never touched: every draw has the depth test and write off.
        Assert.Contains("DepthStencilState.None", renderer, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(renderer, @"_spriteBatch\.Draw\(").Count);
        Assert.Contains("_graphicsDevice.BlendState = previousBlend;", renderer, StringComparison.Ordinal);
        Assert.Contains("_graphicsDevice.DepthStencilState = previousDepth;", renderer, StringComparison.Ordinal);
        Assert.Contains("_graphicsDevice.RasterizerState = previousRasterizer;", renderer, StringComparison.Ordinal);
        Assert.Contains("_graphicsDevice.SamplerStates[0] = previousSampler;", renderer, StringComparison.Ordinal);
        Assert.Contains("_graphicsDevice.SetRenderTarget(hdrTarget);", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHdrView_MultipliesTheSceneBetweenTheSceneAndTheBloomAndTheTonemapping()
    {
        string body = ReadRenderHdrView();

        int scene = body.IndexOf("scenePipeline.RenderScene(", StringComparison.Ordinal);
        int ssao = body.IndexOf("ApplySsao(hdrTarget, in frame", StringComparison.Ordinal);
        int bloom = body.IndexOf("ApplyBloom(hdrTarget, in frame);", StringComparison.Ordinal);
        int tonemap = body.IndexOf("DrawTonemap(hdrTarget, viewRectangle.Width", StringComparison.Ordinal);

        Assert.True(scene >= 0);
        Assert.True(scene < ssao, "the scene, then the occlusion");
        Assert.True(ssao < bloom, "the occlusion, then the bloom");
        Assert.True(bloom < tonemap, "the bloom, then the tonemapping");
        Assert.Equal(1, Regex.Matches(body, @"ApplySsao\(").Count);
    }

    [Fact]
    public void RenderHdrView_RunsTheCompositionOnlyWhenTheScenePrepassIsActiveForTheView()
    {
        string body = ReadRenderHdrView();

        // The prepass is activated by the four-term condition (see SceneDepthShaderCoverageTests: SSAO on, perspective,
        // composeInterfaceAfter) and the composition is inside the test of its activation: a view that renders scene and interface in
        // one go, the editor views, has its grid, handles and interface in the HDR target and is never darkened.
        int condition = body.IndexOf("if (sceneDepthActive)", StringComparison.Ordinal);
        int call = body.IndexOf("ApplySsao(hdrTarget, in frame", StringComparison.Ordinal);
        int activation = body.IndexOf("if (frame.Environment.SsaoEnabled && composeInterfaceAfter && frame.Projection.M44 == 0.0f)", StringComparison.Ordinal);

        Assert.True(activation >= 0);
        Assert.True(activation < condition);
        Assert.True(condition >= 0);
        Assert.True(call > condition);
        Assert.True(call < condition + 80, "the call is the body of the test of the active pre-pass");
        Assert.Contains("composeInterfaceAfter", body[activation..(activation + 120)], StringComparison.Ordinal);
    }

    [Fact]
    public void TheCompositionIsSkippedWithoutTheShaderOrWithoutIntensity_AndTheDepthIsTheOneOfThePrepass()
    {
        string source = ReadSource("Framework", "Rendering", "RenderPipeline.cs");
        int start = source.IndexOf("private void ApplySsao(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        string body = source[start..source.IndexOf("private bool BeginSceneDepth(", start, StringComparison.Ordinal)];

        Assert.Contains("!HasSsaoShader()", body, StringComparison.Ordinal);
        Assert.Contains("frame.Environment.SsaoIntensity <= 0.0f", body, StringComparison.Ordinal);
        Assert.Contains("_sceneDepth.Target", body, StringComparison.Ordinal);
        Assert.Contains("stats.SsaoPasses++;", body, StringComparison.Ordinal);
        Assert.Contains("1.0f / frame.Projection.M11", body, StringComparison.Ordinal);
        Assert.Contains("1.0f / frame.Projection.M22", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSsaoTargets_AreOneChannelWithoutDepth_NeverAHdrFormat_AndStampedForEviction()
    {
        string source = ReadSource("Framework", "Rendering", "RenderPipeline.cs");
        int start = source.IndexOf("private RenderTarget2D AcquireSsaoTarget(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        string body = source[start..source.IndexOf("private void ApplySsao(", start, StringComparison.Ordinal)];

        Assert.Contains("pool.Acquire(width, height, _ssaoFormat, DepthFormat.None)", body, StringComparison.Ordinal);
        Assert.Contains("SurfaceFormat _ssaoFormat = SurfaceFormat.Single;", source, StringComparison.Ordinal);
        Assert.Contains("_ssaoFormat = SurfaceFormat.HalfSingle;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("HalfVector4", body, StringComparison.Ordinal);
        Assert.DoesNotContain("HdrBlendable", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SurfaceFormat.Color", body, StringComparison.Ordinal);
        Assert.Contains("_hdrLastUsedRender[(width, height, target.Format, DepthFormat.None)] = _renderIndex;", body, StringComparison.Ordinal);
        Assert.Contains("SsaoTargetCreationCount++;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseLoop_CountsTheSsaoKeysApart_BeforeTheFxaaAndBloomBranches()
    {
        string source = ReadSource("Framework", "Rendering", "RenderPipeline.cs");
        int depth = source.IndexOf("key.Item4 == DepthFormat.Depth24)", StringComparison.Ordinal);
        int ssao = source.IndexOf("key.Item4 == DepthFormat.None && (key.Item3 == SurfaceFormat.Single || key.Item3 == SurfaceFormat.HalfSingle)", StringComparison.Ordinal);
        int fxaa = source.IndexOf("key.Item4 == DepthFormat.None && key.Item3 == SurfaceFormat.Color", StringComparison.Ordinal);
        int bloom = source.IndexOf("else if (key.Item4 == DepthFormat.None)", StringComparison.Ordinal);

        Assert.True(depth >= 0);
        Assert.True(ssao > depth);
        Assert.True(ssao < fxaa);
        Assert.True(ssao < bloom);
        Assert.Contains("SsaoTargetDisposalCount += disposed;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderStats_CountsTheSsaoPassesPerView_AndResetsThem()
    {
        var stats = new CasaEngine.Framework.Rendering.RenderStats { SsaoPasses = 3 };
        stats.Reset();

        Assert.Equal(0, stats.SsaoPasses);
    }

    private static string ReadRenderHdrView()
    {
        string source = ReadSource("Framework", "Rendering", "RenderPipeline.cs");
        int start = source.IndexOf("private void RenderHdrView(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        int end = source.IndexOf("private void ApplyBloom(", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return source[start..end];
    }

    private static string ReadSource(params string[] relative)
    {
        return File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot(), "CasaEngine" }.Concat(relative).ToArray()));
    }

    private static string ReadShader()
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Shaders", "Ssao.fx"));
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
