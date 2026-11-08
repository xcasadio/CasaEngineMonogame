using CasaEngine.Framework.Rendering.FrozenFrames;
using Xunit;

namespace CasaEngine.Tests.Rendering.FrozenFrames;

/// <summary>
/// E19.TR-4a, T1a: the state machine of <see cref="FrozenFrameService"/> (D-TR4A-6). Every expected value is
/// an input of the test or the formula written beside it.
/// </summary>
public class FrozenFrameServiceTests
{
    private static FrozenFrameService CreateWithImage(int width = 320, int height = 240)
    {
        var service = new FrozenFrameService();
        service.RequestCapture();
        service.CompleteCapture(width, height);
        return service;
    }

    [Fact]
    public void Defaults_RetainNothing_RequestNothing_ShowNothing()
    {
        var service = new FrozenFrameService();

        Assert.False(service.RetainLastFrame);
        Assert.False(service.CaptureRequested);
        Assert.Equal(FrozenFrameCaptureState.None, service.LastCapture);
        Assert.Equal(0, service.CaptureCount);
        Assert.False(service.HasImage);
        Assert.Equal(0, service.CapturedWidth);
        Assert.Equal(0, service.CapturedHeight);
        Assert.False(service.BaseVisible);
        Assert.False(service.IsBaseDrawable);
        Assert.False(service.ShouldRegisterBase);
    }

    [Fact]
    public void RequestCapture_IsIdempotent_UntilCompleted()
    {
        var service = new FrozenFrameService();

        service.RequestCapture();
        service.RequestCapture();

        Assert.True(service.CaptureRequested);
        Assert.Equal(0, service.CaptureCount);

        service.CompleteCapture(320, 240);

        // One completion consumes both calls: one capture, nothing left waiting.
        Assert.False(service.CaptureRequested);
        Assert.Equal(1, service.CaptureCount);
    }

    [Fact]
    public void CompleteCapture_RecordsTheImageSizeAndCountsIt()
    {
        var service = new FrozenFrameService();
        service.RequestCapture();

        service.CompleteCapture(320, 240);

        Assert.True(service.HasImage);
        Assert.Equal(320, service.CapturedWidth);
        Assert.Equal(240, service.CapturedHeight);
        Assert.Equal(FrozenFrameCaptureState.Captured, service.LastCapture);
        Assert.Equal(1, service.CaptureCount);
        Assert.False(service.CaptureRequested);

        service.RequestCapture();
        service.CompleteCapture(640, 480);

        Assert.Equal(2, service.CaptureCount);
        Assert.Equal(640, service.CapturedWidth);
    }

    [Fact]
    public void FailCapture_DropsAnyEarlierImage_AndReportsUnavailable()
    {
        var service = CreateWithImage();
        service.RequestCapture();

        service.FailCapture();

        Assert.False(service.HasImage);
        Assert.Equal(FrozenFrameCaptureState.Unavailable, service.LastCapture);
        Assert.False(service.CaptureRequested);
    }

    [Theory]
    [InlineData(false, false, false)] // BaseVisible && HasImage = false && false
    [InlineData(true, false, false)]  // true && false
    [InlineData(false, true, false)]  // false && true
    [InlineData(true, true, true)]    // true && true
    public void IsBaseDrawable_NeedsBothTheVisibleFlagAndAnImage(bool visible, bool hasImage, bool expected)
    {
        var service = hasImage ? CreateWithImage() : new FrozenFrameService();
        if (visible)
        {
            service.ShowBase();
        }
        else
        {
            service.HideBase();
        }

        Assert.Equal(visible, service.BaseVisible);
        Assert.Equal(hasImage, service.HasImage);
        Assert.Equal(expected, service.IsBaseDrawable);
    }

    [Fact]
    public void Release_HidesDropsAndForgetsThePendingRequest_ButKeepsTheCount()
    {
        var service = CreateWithImage();
        service.ShowBase();
        service.RequestCapture();
        Assert.Equal(1, service.CaptureCount);

        service.Release();

        Assert.False(service.BaseVisible);
        Assert.False(service.HasImage);
        Assert.False(service.CaptureRequested);
        Assert.False(service.IsBaseDrawable);
        Assert.Equal(1, service.CaptureCount);
    }

    [Fact]
    public void ARequestWhileAnImageIsHeld_KeepsTheOldImageUntilTheNewOneArrives()
    {
        var service = CreateWithImage(320, 240);

        service.RequestCapture();

        Assert.True(service.HasImage);
        Assert.Equal(320, service.CapturedWidth);
        Assert.Equal(240, service.CapturedHeight);

        service.CompleteCapture(640, 480);

        Assert.True(service.HasImage);
        Assert.Equal(640, service.CapturedWidth);
        Assert.Equal(480, service.CapturedHeight);
        Assert.Equal(2, service.CaptureCount);
    }

    [Fact]
    public void ShowBase_BeforeAnyCapture_DrawsNothing_ThenDrawsOnceCaptured()
    {
        var service = new FrozenFrameService();

        service.ShowBase();

        Assert.False(service.IsBaseDrawable);
        Assert.False(service.ShouldRegisterBase); // no image, no request

        service.RequestCapture();

        Assert.False(service.IsBaseDrawable);    // still no image
        Assert.True(service.ShouldRegisterBase); // a pending request is enough to register (R-TR5-1)

        service.CompleteCapture(320, 240);

        Assert.True(service.IsBaseDrawable);
    }

    [Theory]
    // visible, hasImage, requested -> visible && (hasImage || requested)
    [InlineData(false, false, false, false)] // false && (false || false)
    [InlineData(false, false, true, false)]  // false && (false || true)
    [InlineData(false, true, false, false)]  // false && (true || false)
    [InlineData(false, true, true, false)]   // false && (true || true)
    [InlineData(true, false, false, false)]  // true && (false || false)
    [InlineData(true, false, true, true)]    // true && (false || true)
    [InlineData(true, true, false, true)]    // true && (true || false)
    [InlineData(true, true, true, true)]     // true && (true || true)
    public void ShouldRegisterBase_NeedsTheVisibleFlagAndEitherAnImageOrAPendingRequest(
        bool visible, bool hasImage, bool requested, bool expected)
    {
        var service = hasImage ? CreateWithImage() : new FrozenFrameService();
        if (visible)
        {
            service.ShowBase();
        }
        else
        {
            service.HideBase();
        }

        if (requested)
        {
            service.RequestCapture();
        }

        Assert.Equal(visible, service.BaseVisible);
        Assert.Equal(hasImage, service.HasImage);
        Assert.Equal(requested, service.CaptureRequested);
        Assert.Equal(expected, service.ShouldRegisterBase);
    }
}
