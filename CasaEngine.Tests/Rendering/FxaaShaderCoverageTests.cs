using System.Text.RegularExpressions;

using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T1.2 of the clear coat, SSAO and FXAA plan: Fxaa.fx is compiled, registered as a built-in and hot reloadable, and the FXAA
/// pass sits after the tonemapping and before the post-tonemap renderers and the interface (ADR-0113). The checks read the text
/// of the shader, the content file, the catalog and the pipeline: the compiler itself is exercised by the build.
/// </summary>
public class FxaaShaderCoverageTests
{
    private static readonly Regex TechniqueRegex = new(
        @"technique\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",
        RegexOptions.Compiled);

    [Fact]
    public void FxaaShader_DeclaresOneTechnique()
    {
        var names = new List<string>();
        foreach (Match match in TechniqueRegex.Matches(ReadShader()))
        {
            names.Add(match.Groups["name"].Value);
        }

        Assert.Equal(new[] { "Fxaa" }, names);
    }

    [Fact]
    public void FxaaShader_DeclaresItsTexelSizeAndTheSourceTexture()
    {
        string source = ReadShader();

        Assert.Contains($"float2 {ShaderParameterNames.FxaaTexelSize}", source, StringComparison.Ordinal);
        Assert.Contains($"Texture2D {ShaderParameterNames.FxaaSourceTexture};", source, StringComparison.Ordinal);
        Assert.Contains($"Texture = <{ShaderParameterNames.FxaaSourceTexture}>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FxaaShader_IsPixelOnlySoSpriteBatchKeepsItsVertexShader()
    {
        string source = ReadShader();

        Assert.DoesNotContain("VertexShader", source, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(source, @"PixelShader\s*=\s*compile").Count);
    }

    [Fact]
    public void FxaaShader_ReadsWithTex2DOnly_FiltersLinearlyAndDoesNotEncode()
    {
        string source = ReadShader();

        // The OpenGL chain ignores the requested level (ADR-0109): no tex2Dlod, and the pass reads the display colours as they are.
        Assert.DoesNotContain("tex2Dlod", source, StringComparison.Ordinal);
        Assert.DoesNotContain("tex2Dgrad", source, StringComparison.Ordinal);
        Assert.DoesNotContain("pow(", source, StringComparison.Ordinal);
        Assert.Contains("tex2D(FxaaSourceSampler", source, StringComparison.Ordinal);
        Assert.Contains("MinFilter = Linear;", source, StringComparison.Ordinal);
        Assert.Contains("MagFilter = Linear;", source, StringComparison.Ordinal);
        Assert.Contains("AddressU = Clamp;", source, StringComparison.Ordinal);
        Assert.Contains("AddressV = Clamp;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FxaaShader_HasNoEarlyReturnSoEveryReadStaysInUniformFlowControl()
    {
        string source = ReadShader();
        int body = source.IndexOf("float4 PS_Fxaa(", StringComparison.Ordinal);
        Assert.True(body >= 0);

        Assert.Equal(1, Regex.Matches(source[body..], @"\breturn\b").Count);
    }

    [Fact]
    public void ContentPipeline_IncludesFxaaShader()
    {
        string mgcb = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Content.mgcb"));

        Assert.Contains("#begin Shaders/Fxaa.fx", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Shaders/Fxaa.fx", mgcb, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInShaderCatalog_RegistersFxaaShader()
    {
        Assert.True(BuiltInShaderCatalog.TryGetByContentName(BuiltInShaderCatalog.FxaaContentName, out var byName));
        Assert.Equal("Shaders/Fxaa.fx", byName.SourceRelativePath);

        Assert.True(BuiltInShaderCatalog.TryGetBySourceRelativePath("Shaders/Fxaa.fx", out var byPath));
        Assert.Equal(BuiltInShaderCatalog.FxaaContentName, byPath.ContentName);
    }

    [Fact]
    public void RenderPipeline_HoldsAndReloadsTheFxaaShader()
    {
        string pipeline = ReadSource("Framework", "Rendering", "RenderPipeline.cs");
        string game = ReadSource("Framework", "Application", "CasaEngineGame.cs");

        Assert.Contains("ShaderWrapper _fxaaShader", pipeline, StringComparison.Ordinal);
        Assert.Contains("BuiltInShaderCatalog.FxaaContentName", pipeline, StringComparison.Ordinal);
        Assert.Contains("case BuiltInShaderCatalog.FxaaContentName:", game, StringComparison.Ordinal);
        Assert.Contains("SetFxaaEffect(Content.Load<Effect>(BuiltInShaderCatalog.FxaaContentName))", game, StringComparison.Ordinal);
    }

    [Fact]
    public void FxaaRenderer_DrawsOneSpriteBatchQuadAndPutsTheStatesBack()
    {
        string renderer = ReadSource("Framework", "Rendering", "PostProcess", "FxaaRenderer.cs");

        Assert.Contains("SamplerState.LinearClamp", renderer, StringComparison.Ordinal);
        Assert.Contains("BlendState.Opaque", renderer, StringComparison.Ordinal);
        Assert.Contains("_graphicsDevice.BlendState = previousBlend;", renderer, StringComparison.Ordinal);
        Assert.Contains("_graphicsDevice.DepthStencilState = previousDepth;", renderer, StringComparison.Ordinal);
        Assert.Contains("_graphicsDevice.RasterizerState = previousRasterizer;", renderer, StringComparison.Ordinal);
        Assert.Contains("_graphicsDevice.SamplerStates[0] = previousSampler;", renderer, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(renderer, @"_spriteBatch\.Draw\(").Count);
    }

    [Fact]
    public void RenderHdrView_PutsTheFxaaPassAfterTheTonemappingAndBeforeThePostTonemapRenderersAndTheInterface()
    {
        string body = ReadRenderHdrView();

        int tonemap = body.IndexOf("DrawTonemap(hdrTarget, viewRectangle.Width", StringComparison.Ordinal);
        int fxaa = body.IndexOf("_fxaaRenderer.Draw(", StringComparison.Ordinal);
        int flush = body.IndexOf("FlushPostTonemapRenderers(view, in frame);", StringComparison.Ordinal);
        int compose = body.IndexOf(".Compose(_graphicsDevice, view, in frame)", StringComparison.Ordinal);

        Assert.True(tonemap >= 0);
        Assert.True(tonemap < fxaa, "tonemapping, then FXAA");
        Assert.True(fxaa < flush, "FXAA, then the post-tonemap renderers");
        Assert.True(flush < compose, "post-tonemap renderers, then the interface");
        Assert.Equal(1, Regex.Matches(body, @"_fxaaRenderer\.Draw\(").Count);
    }

    [Fact]
    public void RenderHdrView_RunsTheFxaaPassOnlyWhenTheInterfaceIsComposedAfter()
    {
        string body = ReadRenderHdrView();

        // The call sits inside the branch whose condition names composeInterfaceAfter: a view that renders everything in one go,
        // interface included, is not filtered. The else branch is the unchanged tonemapping path.
        const string condition = "if (composeInterfaceAfter && frame.Environment.AntiAliasing == AntiAliasingMode.Fxaa && HasFxaaShader())";
        int conditionIndex = body.IndexOf(condition, StringComparison.Ordinal);
        Assert.True(conditionIndex >= 0);

        int fxaa = body.IndexOf("_fxaaRenderer.Draw(", StringComparison.Ordinal);
        Match elseBranch = Regex.Match(body[conditionIndex..], @"\r?\n            else\r?\n");
        Assert.True(elseBranch.Success);

        Assert.True(fxaa > conditionIndex);
        Assert.True(fxaa < conditionIndex + elseBranch.Index, "the FXAA call is in the branch of the condition, not in the else branch");
    }

    [Fact]
    public void TheFxaaTarget_IsStampedForEvictionAndCountedApart_FromTheBloom()
    {
        string source = ReadSource("Framework", "Rendering", "RenderPipeline.cs");

        Assert.Contains("_hdrLastUsedRender[(width, height, SurfaceFormat.Color, DepthFormat.None)] = _renderIndex;", source, StringComparison.Ordinal);
        Assert.Contains("FxaaTargetCreationCount++;", source, StringComparison.Ordinal);
        Assert.Contains("FxaaTargetDisposalCount += disposed;", source, StringComparison.Ordinal);
        Assert.Contains("pool.Acquire(width, height, SurfaceFormat.Color, DepthFormat.None)", source, StringComparison.Ordinal);
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
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Shaders", "Fxaa.fx"));
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
