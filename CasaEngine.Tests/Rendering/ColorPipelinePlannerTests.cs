using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.FrozenFrames;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T1.4 of the PBR plan (ADR-0096): the pure colour pipeline decision. The paths are the destinations of
/// <c>RenderPipeline.Render</c>: back buffer direct, back buffer through the scene target, retained target, the view's own
/// target, the editor's host texture, and split-screen back-buffer views sharing the back buffer.
/// </summary>
public class ColorPipelinePlannerTests
{
    private static FrameCapturePlan Direct() => new(UseSceneRt: false, CopyRetainedFrameToCapture: false, CaptureUnavailable: false, RetainThisFrame: false);

    private static FrameCapturePlan ThroughSceneRt() => new(UseSceneRt: true, CopyRetainedFrameToCapture: false, CaptureUnavailable: false, RetainThisFrame: false);

    private static FrameCapturePlan Retained() => new(UseSceneRt: true, CopyRetainedFrameToCapture: false, CaptureUnavailable: false, RetainThisFrame: true);

    private static ColorPipelineInputs Inputs(
        ColorPipelineMode mode,
        FrameCapturePlan capturePlan,
        bool surfaceIsBackBuffer,
        bool clearColorBuffer = true,
        bool viewRectangleIsEmpty = false) =>
        new(mode, clearColorBuffer, viewRectangleIsEmpty, surfaceIsBackBuffer, capturePlan);

    [Theory]
    [InlineData("back buffer direct", true)]
    [InlineData("own target (render-to-texture, buffered virtual resolution)", false)]
    public void Legacy_NeverUsesAnHdrTarget(string path, bool surfaceIsBackBuffer)
    {
        var plan = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.Legacy, Direct(), surfaceIsBackBuffer));

        Assert.False(plan.UseHdrSceneTarget, path);
        Assert.Equal(ColorPipelineDestination.None, plan.Destination);
    }

    [Theory]
    [InlineData("back buffer direct", true, nameof(ColorPipelineDestination.InitialTarget))]
    [InlineData("own target (render-to-texture, buffered virtual resolution)", false, nameof(ColorPipelineDestination.ViewSurface))]
    public void LinearHdr_TonemapsIntoTheDestinationTheLegacyPathDrawsTo(string path, bool surfaceIsBackBuffer, string destinationName)
    {
        var plan = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, Direct(), surfaceIsBackBuffer));

        Assert.True(plan.UseHdrSceneTarget, path);
        Assert.Equal(Enum.Parse<ColorPipelineDestination>(destinationName), plan.Destination);
    }

    [Fact]
    public void LinearHdr_BackBufferThroughTheSceneTarget_TonemapsIntoTheSceneTarget()
    {
        var plan = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, ThroughSceneRt(), surfaceIsBackBuffer: true));

        Assert.True(plan.UseHdrSceneTarget);
        Assert.Equal(ColorPipelineDestination.SceneRt, plan.Destination);
    }

    [Fact]
    public void LinearHdr_RetainedFrame_TonemapsIntoTheRetainedTarget()
    {
        var plan = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, Retained(), surfaceIsBackBuffer: true));

        Assert.True(plan.UseHdrSceneTarget);
        Assert.Equal(ColorPipelineDestination.RetainedTarget, plan.Destination);
    }

    [Fact]
    public void LinearHdr_EditorHostTexture_IsTheInitialTarget()
    {
        // The editor's host texture is the render target active on entering the render: the frame planner sees a back
        // buffer surface with an initial target that is not the back buffer, so it never uses the scene target, and the
        // destination is the initial target, as in the legacy path.
        var capturePlan = FrameCapturePlanner.Plan(new FrameCaptureInputs(
            ShadowsEnabled: true,
            ClearColorBuffer: true,
            InitialTargetIsBackBuffer: false,
            SurfaceIsBackBuffer: true,
            ViewRectangleIsEmpty: false,
            RetentionEnabled: false,
            IsTargetView: false,
            CaptureRequested: false,
            RetainedFrameValid: false));

        var plan = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, capturePlan, surfaceIsBackBuffer: true));

        Assert.False(capturePlan.UseSceneRt);
        Assert.True(plan.UseHdrSceneTarget);
        Assert.Equal(ColorPipelineDestination.InitialTarget, plan.Destination);
    }

    [Fact]
    public void LinearHdr_SplitScreenViewsSharingTheBackBuffer_EachTonemapIntoTheirRectangle()
    {
        // Two back-buffer views with distinct rectangles decide independently and identically: the rectangle is applied
        // by the surface, not by the plan.
        var left = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, Direct(), surfaceIsBackBuffer: true));
        var right = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, Direct(), surfaceIsBackBuffer: true));

        Assert.Equal(left, right);
        Assert.Equal(ColorPipelineDestination.InitialTarget, left.Destination);
    }

    [Fact]
    public void LinearHdr_EmptyViewRectangle_IsNotTonemapped()
    {
        var plan = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, Direct(), surfaceIsBackBuffer: true, viewRectangleIsEmpty: true));

        Assert.False(plan.UseHdrSceneTarget);
        Assert.Equal(ColorPipelineDestination.None, plan.Destination);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LinearHdr_ViewThatKeepsItsColourBuffer_IsDrawnByTheLegacyPath(bool surfaceIsBackBuffer)
    {
        // The tonemapping overwrites the whole rectangle: a view that composites onto existing content cannot use it.
        var plan = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, Direct(), surfaceIsBackBuffer, clearColorBuffer: false));

        Assert.False(plan.UseHdrSceneTarget);
        Assert.Equal(ColorPipelineDestination.None, plan.Destination);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Legacy_IsNeverAffectedByTheCapturePlan(bool useSceneRt, bool retain, bool surfaceIsBackBuffer)
    {
        var capturePlan = new FrameCapturePlan(useSceneRt, false, false, retain);

        Assert.Equal(default, ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.Legacy, capturePlan, surfaceIsBackBuffer)));
    }

    [Fact]
    public void FrameCapturePlanner_AllowsTheLinearPipelineToReuseItsDecision()
    {
        // The two planners compose: a retained frame implies the scene target path, which the colour planner maps to the
        // retained target. FrameCapturePlanner itself is unchanged by the colour pipeline.
        var capturePlan = FrameCapturePlanner.Plan(new FrameCaptureInputs(
            ShadowsEnabled: false,
            ClearColorBuffer: true,
            InitialTargetIsBackBuffer: true,
            SurfaceIsBackBuffer: true,
            ViewRectangleIsEmpty: false,
            RetentionEnabled: true,
            IsTargetView: true,
            CaptureRequested: false,
            RetainedFrameValid: true));

        var plan = ColorPipelinePlanner.Plan(Inputs(ColorPipelineMode.LinearHdr, capturePlan, surfaceIsBackBuffer: true));

        Assert.True(capturePlan.RetainThisFrame);
        Assert.Equal(ColorPipelineDestination.RetainedTarget, plan.Destination);
    }
}
