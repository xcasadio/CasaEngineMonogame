using System.Text.RegularExpressions;

using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.PostProcess;
using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T3.2 of the clear coat, SSAO and FXAA plan: the scene depth pre-pass (ADR-0115). SceneDepth.fx and the skinned techniques are
/// compiled, registered and hot reloadable; the pass sits after the shadow pass and before the sky and the colour passes; the host is
/// activated in <c>RenderHdrView</c> only when its four conditions hold, evaluated before the scene is rendered, and released in the
/// <c>finally</c>; the target is stamped for eviction and counted apart from the bloom and the FXAA targets. The checks read the text of
/// the shaders, the content file, the catalog and the pipeline: the compiler itself is exercised by the build, the behaviour by
/// <see cref="SceneDepthPrepassGpuTests"/>.
/// </summary>
public class SceneDepthShaderCoverageTests
{
    [Fact]
    public void SceneDepthShader_DeclaresTheSolidAndTheTexturedTechniques()
    {
        string source = ReadSource("Content", "Shaders", "SceneDepth.fx");
        Assert.Contains("TECHNIQUE(SceneDepth_Solid, VS_Main, PS_Solid)", source, StringComparison.Ordinal);
        Assert.Contains("TECHNIQUE(SceneDepth_Textured, VS_Main, PS_Textured)", source, StringComparison.Ordinal);
        Assert.Equal(SceneDepthPrepass.SolidTechniqueName, "SceneDepth_Solid");
        Assert.Equal(SceneDepthPrepass.TexturedTechniqueName, "SceneDepth_Textured");
    }

    [Fact]
    public void SceneDepthShader_WritesTheClipSpaceWInTheRedChannel_NotANormalisedDepth()
    {
        string source = ReadSource("Content", "Shaders", "SceneDepth.fx");

        Assert.Contains("output.ViewDepth = clipPosition.w;", source, StringComparison.Ordinal);
        Assert.Contains("return float4(input.ViewDepth, 0.0f, 0.0f, 1.0f);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("clipPosition.z / clipPosition.w", source, StringComparison.Ordinal);
        Assert.Contains($"float4x4 {ShaderParameterNames.WorldViewProj};", source, StringComparison.Ordinal);
        Assert.Contains($"float {ShaderParameterNames.AlphaCutoff}", source, StringComparison.Ordinal);
        Assert.Contains("DECLARE_TEXTURE(Texture, 0);", source, StringComparison.Ordinal);
        Assert.Equal("Texture", ShaderParameterNames.BasColorTexture);
    }

    [Fact]
    public void SkinEffect_HasTheTwoSceneDepthTechniques_AndTheTexturedAlphaCut()
    {
        string source = ReadSource("Content", "Shaders", "skinEffect.fx");

        Assert.Contains($"TECHNIQUE({SceneDepthPrepass.SkinnedTechniqueName}, VertexShaderRiggedModelSceneDepth, PixelShaderRiggedModelSceneDepth)", source, StringComparison.Ordinal);
        Assert.Contains($"TECHNIQUE({SceneDepthPrepass.SkinnedDualQuaternionTechniqueName}, VertexShaderRiggedModelSceneDepthDualQuaternion, PixelShaderRiggedModelSceneDepth)", source, StringComparison.Ordinal);
        Assert.Contains("ApplySkinnedAlphaTest(SAMPLE_TEXTURE(Texture, input.TexureCoordinateA).a);", source, StringComparison.Ordinal);

        // Both vertex shaders write the clip-space w, with the skinning of the colour techniques.
        int linear = source.IndexOf("VsSceneDepthOutput VertexShaderRiggedModelSceneDepth(", StringComparison.Ordinal);
        int dual = source.IndexOf("VsSceneDepthOutput VertexShaderRiggedModelSceneDepthDualQuaternion(", StringComparison.Ordinal);
        Assert.True(linear >= 0 && dual > linear);
        Assert.Contains("ApplyBoneTransforms(input, pos, norm);", source[linear..dual], StringComparison.Ordinal);
        Assert.Contains("ApplyBoneDualQuaternionTransforms(input, pos, norm);", source[dual..], StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(source, @"output\.ViewDepth = clipPosition\.w;").Count);
    }

    [Fact]
    public void ContentPipeline_IncludesSceneDepthShader()
    {
        string mgcb = ReadSource("Content", "Content.mgcb");

        Assert.Contains("#begin Shaders/SceneDepth.fx", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Shaders/SceneDepth.fx", mgcb, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInShaderCatalog_RegistersSceneDepthShader()
    {
        Assert.True(BuiltInShaderCatalog.TryGetByContentName(BuiltInShaderCatalog.SceneDepthContentName, out var byName));
        Assert.Equal("Shaders/SceneDepth.fx", byName.SourceRelativePath);

        Assert.True(BuiltInShaderCatalog.TryGetBySourceRelativePath("Shaders/SceneDepth.fx", out var byPath));
        Assert.Equal(BuiltInShaderCatalog.SceneDepthContentName, byPath.ContentName);
    }

    [Fact]
    public void TheStaticRenderer_LoadsAndReloadsTheSceneDepthShader_AndTheGameRoutesItsHotReload()
    {
        string renderer = ReadSource("Framework", "Application", "Components", "StaticMeshRendererComponent.cs");
        string game = ReadSource("Framework", "Application", "CasaEngineGame.cs");

        Assert.Contains("Game.Content.Load<Effect>(BuiltInShaderCatalog.SceneDepthContentName)", renderer, StringComparison.Ordinal);
        Assert.Contains("_pipeline.SetSceneDepthShader(_sceneDepthShaderWrapper);", renderer, StringComparison.Ordinal);
        Assert.Contains("_sceneDepthShaderWrapper.ReplaceEffect(effect);", renderer, StringComparison.Ordinal);
        Assert.Contains("case BuiltInShaderCatalog.SceneDepthContentName:", game, StringComparison.Ordinal);
        Assert.Contains("SceneDepth = frame.SceneDepth,", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardRenderPipeline_RunsTheSceneDepthPassAfterTheShadowPassAndBeforeTheSky()
    {
        string source = ReadSource("Framework", "Rendering", "ForwardRenderPipeline.cs");

        int shadow = source.IndexOf("_passes.Add(_shadowPass);", StringComparison.Ordinal);
        int sceneDepth = source.IndexOf("_passes.Add(_sceneDepthPass);", StringComparison.Ordinal);
        int sky = source.IndexOf("_passes.Add(_skyPass);", StringComparison.Ordinal);
        int opaque = source.IndexOf("_passes.Add(new OpaquePass());", StringComparison.Ordinal);

        Assert.True(shadow >= 0);
        Assert.True(shadow < sceneDepth, "shadow, then the scene depth");
        Assert.True(sceneDepth < sky, "scene depth, then the sky");
        Assert.True(sky < opaque);
    }

    [Fact]
    public void RenderPassType_GetsANewMember_AndDepthPrePassStaysAsItWas()
    {
        Assert.Equal(RenderPassType.SceneDepthPass, new SceneDepthPass().Type);

        // Appended: the values of the existing members do not move.
        Assert.Equal(0, (int)RenderPassType.DepthPrePass);
        Assert.Equal(5, (int)RenderPassType.ShadowPass);
        Assert.Equal(6, (int)RenderPassType.SceneDepthPass);
    }

    [Fact]
    public void TheBackgroundAndSkyConstants_AreTheDecidedOnes_AndRepresentableInHalfPrecision()
    {
        Assert.Equal(60000.0f, SceneDepthPrepass.BackgroundDepth);
        Assert.Equal(30000.0f, SceneDepthPrepass.SkyThreshold);
        Assert.True(SceneDepthPrepass.SkyThreshold < SceneDepthPrepass.BackgroundDepth);
        Assert.True(SceneDepthPrepass.BackgroundDepth <= (float)Half.MaxValue);
        Assert.Equal(SceneDepthPrepass.BackgroundDepth, (float)(Half)SceneDepthPrepass.BackgroundDepth, 32.0f);
    }

    [Fact]
    public void RenderStats_ResetsTheSceneDepthCounters()
    {
        var stats = new RenderStats { SceneDepthStaticDraws = 3, SceneDepthSkinnedDraws = 2 };

        stats.Reset();

        Assert.Equal(0, stats.SceneDepthStaticDraws);
        Assert.Equal(0, stats.SceneDepthSkinnedDraws);
    }

    [Fact]
    public void RenderHdrView_ActivatesThePrepassOnItsFourConditions_BeforeTheSceneIsRendered()
    {
        string body = ReadRenderHdrView();

        // The four conditions: SSAO on, a view that renders through the HDR target (this method), a perspective projection, and a
        // pipeline that separates the scene from the interface (composeInterfaceAfter, the condition of the FXAA pass).
        const string condition = "if (frame.Environment.SsaoEnabled && composeInterfaceAfter && frame.Projection.M44 == 0.0f)";
        int conditionIndex = body.IndexOf(condition, StringComparison.Ordinal);
        int begin = body.IndexOf("sceneDepthActive = BeginSceneDepth(", StringComparison.Ordinal);
        int scene = body.IndexOf(".RenderScene(_graphicsDevice, view, in frame, _renderers);", StringComparison.Ordinal);
        int wholeView = body.IndexOf("pipeline.RenderView(_graphicsDevice, view, in frame, _renderers);", StringComparison.Ordinal);
        int composeDefinition = body.IndexOf("bool composeInterfaceAfter = scenePipeline != null && scenePipeline.SupportsScenePhase;", StringComparison.Ordinal);

        Assert.True(composeDefinition >= 0, "composeInterfaceAfter is computed first");
        Assert.True(conditionIndex > composeDefinition);
        Assert.True(begin > conditionIndex, "the activation is inside the condition");
        Assert.True(begin < scene, "activated before RenderScene");
        Assert.True(begin < wholeView, "activated before the whole-view render too (it never runs for it)");
        Assert.Equal(1, Regex.Matches(body, @"BeginSceneDepth\(").Count);
    }

    [Fact]
    public void RenderHdrView_ReleasesThePrepassTargetInTheFinally_WithTheSceneTarget()
    {
        string body = ReadRenderHdrView();

        int finallyIndex = body.IndexOf("        finally", StringComparison.Ordinal);
        Assert.True(finallyIndex >= 0);
        string finallyBlock = body[finallyIndex..];

        int deactivate = finallyBlock.IndexOf("_sceneDepth.Deactivate()", StringComparison.Ordinal);
        int releaseDepth = finallyBlock.IndexOf("RenderTargetPool.Shared.Release(sceneDepthTarget);", StringComparison.Ordinal);
        int releaseHdr = finallyBlock.IndexOf("RenderTargetPool.Shared.Release(hdrTarget);", StringComparison.Ordinal);

        Assert.True(deactivate >= 0);
        Assert.True(deactivate < releaseDepth);
        Assert.True(releaseDepth < releaseHdr);
    }

    [Fact]
    public void ThePipeline_HandsTheHostToTheRenderers_ThroughTheFrame()
    {
        string source = ReadSource("Framework", "Rendering", "RenderPipeline.cs");

        Assert.Contains("with { Ibl = _iblCache, SceneDepth = _sceneDepth };", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSceneDepthTarget_IsStampedForEvictionWithItsDepthFormat_AndCountedApartFromTheBloomAndFxaa()
    {
        string source = ReadSource("Framework", "Rendering", "RenderPipeline.cs");

        Assert.Contains("_hdrLastUsedRender[(width, height, target.Format, DepthFormat.Depth24)] = _renderIndex;", source, StringComparison.Ordinal);
        Assert.Contains("pool.Acquire(width, height, _sceneDepthFormat, DepthFormat.Depth24)", source, StringComparison.Ordinal);
        Assert.Contains("SceneDepthTargetCreationCount++;", source, StringComparison.Ordinal);
        Assert.Contains("SceneDepthTargetDisposalCount += disposed;", source, StringComparison.Ordinal);

        // The release branch of the pre-pass (Depth24) comes before the FXAA one (Color, no depth) and the bloom one (no depth).
        int sceneDepth = source.IndexOf("if (key.Item4 == DepthFormat.Depth24)", StringComparison.Ordinal);
        int fxaa = source.IndexOf("else if (key.Item4 == DepthFormat.None && key.Item3 == SurfaceFormat.Color)", StringComparison.Ordinal);
        int bloom = source.IndexOf("else if (key.Item4 == DepthFormat.None)", StringComparison.Ordinal);
        Assert.True(sceneDepth >= 0 && sceneDepth < fxaa && fxaa < bloom);
    }

    [Fact]
    public void TheFormatChain_IsSingleThenHalfSingleThenHalfVector4_ThenTheFeatureSwitchesOff()
    {
        string source = ReadSource("Framework", "Rendering", "RenderPipeline.cs");

        Assert.Contains("private SurfaceFormat _sceneDepthFormat = SurfaceFormat.Single;", source, StringComparison.Ordinal);
        Assert.Contains("var fallback = _sceneDepthFormat == SurfaceFormat.Single ? SurfaceFormat.HalfSingle : SurfaceFormat.HalfVector4;", source, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception) when (_sceneDepthFormat != SurfaceFormat.HalfVector4)", source, StringComparison.Ordinal);
        Assert.Contains("_sceneDepthUnavailable = true;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSkinnedRenderer_DrawsItsDepthAfterTheShadowCastersAndBeforeItsColourDraws()
    {
        string source = ReadSource("Framework", "Application", "Components", "SkinnedMeshRendererComponent.cs");
        int flush = source.IndexOf("public void Flush(in RenderFrame frame, RenderStats stats = null)", StringComparison.Ordinal);
        Assert.True(flush >= 0);
        string body = source[flush..source.IndexOf("private void DrawShadowCasters(", flush, StringComparison.Ordinal)];

        int shadows = body.IndexOf("DrawShadowCasters(in context);", StringComparison.Ordinal);
        int sceneDepth = body.IndexOf("DrawSceneDepth(in context);", StringComparison.Ordinal);
        int colour = body.IndexOf("DrawRiggedModel(", StringComparison.Ordinal);

        Assert.True(shadows >= 0);
        Assert.True(shadows < sceneDepth, "shadow casters, then the depth");
        Assert.True(sceneDepth < colour, "the depth, then the colour draws");
    }

    [Fact]
    public void TheHost_RoundTripsTheTargetThroughAReusedBinding_NotThroughTheAllocatingGuard()
    {
        string host = ReadSource("Framework", "Rendering", "PostProcess", "SceneDepthPrepass.cs");

        Assert.Contains("private readonly RenderTargetBinding[] _savedBinding = new RenderTargetBinding[1];", host, StringComparison.Ordinal);
        Assert.Contains("device.GetRenderTargets(_savedBinding);", host, StringComparison.Ordinal);
        Assert.Contains("device.SetRenderTargets(_savedBinding);", host, StringComparison.Ordinal);
        Assert.DoesNotContain("new GraphicsStateGuard(", host, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRenderTargets()", host, StringComparison.Ordinal);

        // The pass of the static renderer and the skinned draw use the host, not their own guard.
        string pass = ReadSource("Framework", "Rendering", "Draw", "SceneDepthPass.cs");
        Assert.DoesNotContain("new GraphicsStateGuard(", pass, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStaticPass_FiltersLikeTheOpaquePass_NotLikeTheShadowPass()
    {
        string pass = ReadSource("Framework", "Rendering", "Draw", "SceneDepthPass.cs");

        Assert.Contains("item.Queue >= RenderQueue.Transparent", pass, StringComparison.Ordinal);
        Assert.Contains("(item.Features & ShaderFeature.Instanced) != 0", pass, StringComparison.Ordinal);
        Assert.Contains("item.Mesh.VertexBuffer is null", pass, StringComparison.Ordinal);
        Assert.Contains("item.Mesh.IndexBuffer is null", pass, StringComparison.Ordinal);
        Assert.DoesNotContain("EffectiveCastShadows", pass.Replace("<c>EffectiveCastShadows</c>", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
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
