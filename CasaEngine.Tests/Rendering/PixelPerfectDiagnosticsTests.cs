using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public sealed class PixelPerfectDiagnosticsTests
{
    [Fact]
    public void RequiresPixelPerfect_OnlyForPixelSnappedCamera2d()
    {
        Assert.False(PixelPerfectDiagnostics.RequiresPixelPerfect(null));
        Assert.False(PixelPerfectDiagnostics.RequiresPixelPerfect(new Camera3dIn2dAxisComponent()));
        Assert.False(PixelPerfectDiagnostics.RequiresPixelPerfect(new Camera2dComponent { PixelSnap = false }));
        Assert.True(PixelPerfectDiagnostics.RequiresPixelPerfect(new Camera2dComponent { PixelSnap = true }));
    }

    [Theory]
    [InlineData(1.0f, 1.0f)]
    [InlineData(2.0f, 1.0f)]
    [InlineData(4.0f, 1.0f)]
    public void Evaluate_IntegerZoomAtScaleOne_IsNotDegraded(float zoom, float resolutionScale)
    {
        Assert.Equal(PixelPerfectDegradation.None, PixelPerfectDiagnostics.Evaluate(zoom, resolutionScale));
    }

    [Fact]
    public void Evaluate_NonUnitResolutionScale_ReportsResolutionScale()
    {
        Assert.Equal(
            PixelPerfectDegradation.ResolutionScale,
            PixelPerfectDiagnostics.Evaluate(2.0f, 0.5f));
    }

    [Fact]
    public void Evaluate_NonIntegerZoom_ReportsZoom()
    {
        Assert.Equal(
            PixelPerfectDegradation.NonIntegerZoom,
            PixelPerfectDiagnostics.Evaluate(1.5f, 1.0f));
    }

    [Fact]
    public void Evaluate_BothIssues_ReportsBothReasons()
    {
        var degradation = PixelPerfectDiagnostics.Evaluate(1.5f, 0.75f);

        Assert.Equal(
            PixelPerfectDegradation.ResolutionScale | PixelPerfectDegradation.NonIntegerZoom,
            degradation);
    }

    [Fact]
    public void Evaluate_Camera_WithoutPixelSnap_IsNeverDegraded()
    {
        var camera = new Camera2dComponent { Zoom = 1.5f, PixelSnap = false };

        Assert.Equal(PixelPerfectDegradation.None, PixelPerfectDiagnostics.Evaluate(camera, 0.5f));
    }

    [Fact]
    public void Evaluate_Camera_WithPixelSnap_UsesCameraZoom()
    {
        var camera = new Camera2dComponent { Zoom = 3.0f, PixelSnap = true };

        Assert.Equal(PixelPerfectDegradation.None, PixelPerfectDiagnostics.Evaluate(camera, 1.0f));

        camera.Zoom = 3.25f;
        Assert.Equal(PixelPerfectDegradation.NonIntegerZoom, PixelPerfectDiagnostics.Evaluate(camera, 1.0f));
    }

    [Fact]
    public void Evaluate_NonCamera2d_IsNeverDegraded()
    {
        Assert.Equal(PixelPerfectDegradation.None, PixelPerfectDiagnostics.Evaluate(new Camera3dIn2dAxisComponent(), 0.5f));
        Assert.Equal(PixelPerfectDegradation.None, PixelPerfectDiagnostics.Evaluate(null, 0.5f));
    }

    [Fact]
    public void DescribeOverlayLine_UsesCachedStringsForEveryDegradation()
    {
        Assert.Equal("PixelPerfect: OK", PixelPerfectDiagnostics.DescribeOverlayLine(PixelPerfectDegradation.None));
        Assert.Same(
            PixelPerfectDiagnostics.DescribeOverlayLine(PixelPerfectDegradation.ResolutionScale),
            PixelPerfectDiagnostics.DescribeOverlayLine(PixelPerfectDegradation.ResolutionScale));

        Assert.StartsWith("PixelPerfect: degraded (",
            PixelPerfectDiagnostics.DescribeOverlayLine(PixelPerfectDegradation.ResolutionScale));
        Assert.StartsWith("PixelPerfect: degraded (",
            PixelPerfectDiagnostics.DescribeOverlayLine(PixelPerfectDegradation.NonIntegerZoom));
        Assert.StartsWith("PixelPerfect: degraded (",
            PixelPerfectDiagnostics.DescribeOverlayLine(
                PixelPerfectDegradation.ResolutionScale | PixelPerfectDegradation.NonIntegerZoom));
    }

    [Fact]
    public void DescribeReason_ReturnsCachedInstances()
    {
        var both = PixelPerfectDegradation.ResolutionScale | PixelPerfectDegradation.NonIntegerZoom;

        Assert.Same(
            PixelPerfectDiagnostics.DescribeReason(PixelPerfectDegradation.ResolutionScale),
            PixelPerfectDiagnostics.DescribeReason(PixelPerfectDegradation.ResolutionScale));
        Assert.Contains("ResolutionScale", PixelPerfectDiagnostics.DescribeReason(PixelPerfectDegradation.ResolutionScale));
        Assert.Contains("Zoom", PixelPerfectDiagnostics.DescribeReason(PixelPerfectDegradation.NonIntegerZoom));
        Assert.Contains("ResolutionScale", PixelPerfectDiagnostics.DescribeReason(both));
        Assert.Contains("Zoom", PixelPerfectDiagnostics.DescribeReason(both));
    }
}
