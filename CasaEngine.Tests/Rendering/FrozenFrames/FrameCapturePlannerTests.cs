using CasaEngine.Framework.Rendering.FrozenFrames;
using Xunit;

namespace CasaEngine.Tests.Rendering.FrozenFrames;

/// <summary>
/// E19.TR-4a, T1a: the pure drawing decision (D-TR4A-7). The old rule is the condition of
/// <c>RenderPipeline.Render</c>: <c>IsBackBuffer &amp;&amp; initialRenderTarget == null &amp;&amp; Shadows.Enabled
/// &amp;&amp; ClearColorBuffer</c>.
/// </summary>
public class FrameCapturePlannerTests
{
    /// <summary>An eligible target view with the retention on, shadows off, nothing requested.</summary>
    private static FrameCaptureInputs Eligible() => new(
        ShadowsEnabled: false,
        ClearColorBuffer: true,
        InitialTargetIsBackBuffer: true,
        SurfaceIsBackBuffer: true,
        ViewRectangleIsEmpty: false,
        RetentionEnabled: true,
        IsTargetView: true,
        CaptureRequested: false,
        RetainedFrameValid: true);

    [Theory]
    // shadows, clear, initialIsBackBuffer, surfaceIsBackBuffer -> shadows && clear && initial && surface
    [InlineData(false, false, false, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, true, true, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, true, true, false, false)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void WithoutRetention_TheOldRuleHolds(bool shadows, bool clear, bool initialIsBackBuffer, bool surfaceIsBackBuffer, bool expected)
    {
        var inputs = Eligible() with
        {
            ShadowsEnabled = shadows,
            ClearColorBuffer = clear,
            InitialTargetIsBackBuffer = initialIsBackBuffer,
            SurfaceIsBackBuffer = surfaceIsBackBuffer,
            RetentionEnabled = false,
        };

        Assert.Equal(expected, FrameCapturePlanner.Plan(in inputs).UseSceneRt);
    }

    [Fact]
    public void Retention_OnTheTargetView_RedirectsEvenWithoutShadows()
    {
        var inputs = Eligible();

        Assert.False(inputs.ShadowsEnabled);
        Assert.True(FrameCapturePlanner.Plan(in inputs).UseSceneRt);
    }

    [Fact]
    public void Retention_IgnoresANonTargetView()
    {
        var inputs = Eligible() with { IsTargetView = false };

        Assert.False(FrameCapturePlanner.Plan(in inputs).UseSceneRt);
    }

    [Theory]
    [InlineData(false, true, true, false)] // no colour clear
    [InlineData(true, false, true, false)] // initial target is not the back buffer
    [InlineData(true, true, false, false)] // surface is a render target
    [InlineData(true, true, true, true)]   // empty rectangle
    public void Retention_NeedsAClearedBackBufferView(bool clear, bool initialIsBackBuffer, bool surfaceIsBackBuffer, bool emptyRectangle)
    {
        var inputs = Eligible() with
        {
            ClearColorBuffer = clear,
            InitialTargetIsBackBuffer = initialIsBackBuffer,
            SurfaceIsBackBuffer = surfaceIsBackBuffer,
            ViewRectangleIsEmpty = emptyRectangle,
        };

        Assert.False(FrameCapturePlanner.Plan(in inputs).UseSceneRt);
    }

    [Fact]
    public void Request_OnTheTargetView_CopiesWhenAFrameIsRetained()
    {
        var inputs = Eligible() with { CaptureRequested = true };

        var plan = FrameCapturePlanner.Plan(in inputs);

        Assert.True(plan.UseSceneRt);
        Assert.True(plan.CopyRetainedFrameToCapture);
        Assert.False(plan.CaptureUnavailable);
    }

    [Theory]
    [InlineData("invalid retained frame")]
    [InlineData("option off")]
    [InlineData("ineligible view")]
    public void Request_WithoutARetainedFrame_IsUnavailable(string cause)
    {
        var inputs = Eligible() with { CaptureRequested = true };
        inputs = cause switch
        {
            "invalid retained frame" => inputs with { RetainedFrameValid = false },
            "option off" => inputs with { RetentionEnabled = false },
            _ => inputs with { ClearColorBuffer = false },
        };

        var plan = FrameCapturePlanner.Plan(in inputs);

        Assert.False(plan.CopyRetainedFrameToCapture);
        Assert.True(plan.CaptureUnavailable);
    }

    [Fact]
    public void Request_OnANonTargetView_StaysPending()
    {
        var inputs = Eligible() with { CaptureRequested = true, IsTargetView = false };

        var plan = FrameCapturePlanner.Plan(in inputs);

        Assert.False(plan.CopyRetainedFrameToCapture);
        Assert.False(plan.CaptureUnavailable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NoRequest_NeverCopiesNorFails(bool retainedFrameValid)
    {
        var inputs = Eligible() with { CaptureRequested = false, RetainedFrameValid = retainedFrameValid };

        var plan = FrameCapturePlanner.Plan(in inputs);

        Assert.False(plan.CopyRetainedFrameToCapture);
        Assert.False(plan.CaptureUnavailable);
    }
}
