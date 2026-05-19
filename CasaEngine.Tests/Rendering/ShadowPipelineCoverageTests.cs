using System.Reflection;

using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Draw;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ShadowPipelineCoverageTests
{
    [Fact]
    public void ForwardRenderPipeline_RegistersShadowPassBeforeOpaquePass()
    {
        var pipeline = new ForwardRenderPipeline();
        var field = typeof(ForwardRenderPipeline).GetField("_passes", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var passes = Assert.IsAssignableFrom<IReadOnlyList<RenderPass>>(field!.GetValue(pipeline));
        int shadowPassIndex = FindPassIndex(passes, RenderPassType.ShadowPass);
        int opaquePassIndex = FindPassIndex(passes, RenderPassType.OpaquePass);

        Assert.True(shadowPassIndex >= 0);
        Assert.True(opaquePassIndex >= 0);
        Assert.True(shadowPassIndex < opaquePassIndex);
    }

    [Fact]
    public void ContentPipeline_IncludesShadowDepthShader()
    {
        string mgcb = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Content", "Content.mgcb"));

        Assert.Contains("#begin Shaders/ShadowDepth.fx", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Shaders/ShadowDepth.fx", mgcb, StringComparison.Ordinal);
    }

    [Fact]
    public void ShadowPass_SourceFiltersDirectionalShadowCasters()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "Draw", "ShadowPass.cs"));

        Assert.Contains("RenderPassType.ShadowPass", source, StringComparison.Ordinal);
        Assert.Contains("EffectiveCastShadows", source, StringComparison.Ordinal);
        Assert.Contains("CastShadows", source, StringComparison.Ordinal);
        Assert.Contains("ShadowLightType.Directional", source, StringComparison.Ordinal);
        Assert.Contains("!context.Shadows.Settings.Enabled", source, StringComparison.Ordinal);
        Assert.Contains("SurfaceFormat.Single", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RegularDrawPasses_SkipInstancedItems_WhenShadowPassKeepsFullList()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "Draw", "OpaqueAndTransparentPasses.cs"));

        Assert.Contains("ShaderFeature.Instanced", source, StringComparison.Ordinal);
    }

    private static int FindPassIndex(IReadOnlyList<RenderPass> passes, RenderPassType type)
    {
        for (int i = 0; i < passes.Count; i++)
        {
            if (passes[i].Type == type)
            {
                return i;
            }
        }

        return -1;
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