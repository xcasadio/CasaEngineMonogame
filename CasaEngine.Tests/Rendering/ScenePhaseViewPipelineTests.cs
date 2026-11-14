using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T1.4 of the PBR plan (ADR-0096): which view pipelines can render their scene without their interface, so the colour
/// pipeline composes the interface after the tonemapping.
/// </summary>
public class ScenePhaseViewPipelineTests
{
    private sealed class WholeViewPipeline : IViewRenderPipeline
    {
        public void RenderView(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame, IReadOnlyList<IViewFlushableRenderer> renderers)
        {
        }
    }

    [Fact]
    public void DefaultViewPipeline_RendersItsScenePhase()
    {
        var pipeline = Assert.IsAssignableFrom<IScenePhaseViewPipeline>(DefaultViewPipeline.Instance);

        Assert.True(pipeline.SupportsScenePhase);
    }

    [Fact]
    public void SkyBackgroundViewPipeline_AroundTheDefaultPipeline_RendersItsScenePhase()
    {
        var pipeline = new SkyBackgroundViewPipeline(new SkySettings());

        Assert.True(((IScenePhaseViewPipeline)pipeline).SupportsScenePhase);
    }

    [Fact]
    public void SkyBackgroundViewPipeline_AroundAPipelineWithoutScenePhase_DoesNotExposeIt()
    {
        var pipeline = new SkyBackgroundViewPipeline(new SkySettings(), new WholeViewPipeline());

        Assert.False(((IScenePhaseViewPipeline)pipeline).SupportsScenePhase);
    }

    [Fact]
    public void SkyBackgroundViewPipeline_AroundASkyAroundAWholePipeline_DoesNotExposeIt()
    {
        var inner = new SkyBackgroundViewPipeline(new SkySettings(), new WholeViewPipeline());
        var outer = new SkyBackgroundViewPipeline(new SkySettings(), inner);

        Assert.False(((IScenePhaseViewPipeline)outer).SupportsScenePhase);
    }

    [Fact]
    public void ACustomPipelineWithoutTheInterface_IsNotAScenePipeline()
    {
        Assert.False(new WholeViewPipeline() is IScenePhaseViewPipeline);
    }
}
