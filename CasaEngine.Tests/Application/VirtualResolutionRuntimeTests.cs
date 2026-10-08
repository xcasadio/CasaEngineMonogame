using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Application;

/// <summary>
/// The runtime side of the virtual resolution (ADR-0048, E19.s S-R4): the view and its camera at creation, after a
/// resize, with the setting absent, and the black bands. Everything here runs on the same static seams
/// <see cref="CasaEngineGame"/> and <see cref="DefaultRuntimeViewBootstrapper"/> call; the game itself needs a
/// graphics device and is not built.
/// </summary>
public class VirtualResolutionRuntimeTests
{
    private static readonly VirtualResolutionSettings Native320x240 = new() { Width = 320, Height = 240 };

    /// <summary>A camera as the world leaves it: viewport = the window, zoom as loaded from the entity file.</summary>
    private static Camera2dComponent NewCamera(int windowWidth, int windowHeight, float zoom)
    {
        var camera = new Camera2dComponent();
        camera.OnScreenResized(windowWidth, windowHeight);
        camera.Zoom = zoom;
        return camera;
    }

    private static (float Width, float Height) VisibleWorldArea(Camera2dComponent camera)
        => (camera.Viewport.Width / camera.Zoom, camera.Viewport.Height / camera.Zoom);

    [Fact]
    public void CreateDefaultView_At1920x1080_PlacesTheImageAndFramesExactly320x240()
    {
        var camera = NewCamera(1920, 1080, zoom: 4f);
        var viewManager = new ViewManager();

        var id = DefaultRuntimeViewBootstrapper.CreateDefaultView(new World(), viewManager, camera, 1920, 1080, Native320x240);

        Assert.True(viewManager.TryGetView(id, out var view));
        var surface = Assert.IsType<BackBufferSurface>(view.Surface);
        Assert.Equal(new Rectangle(320, 60, 1280, 960), surface.ViewportRect);
        Assert.Equal(4f, camera.Zoom);
        Assert.Equal(1280, camera.Viewport.Width);
        Assert.Equal(960, camera.Viewport.Height);
        Assert.Equal((320f, 240f), VisibleWorldArea(camera));
    }

    [Fact]
    public void CreateDefaultView_WithoutTheSetting_KeepsTheFullWindowViewAndTheCameraUntouched()
    {
        var camera = NewCamera(1920, 1080, zoom: 4f);
        var viewManager = new ViewManager();

        var id = DefaultRuntimeViewBootstrapper.CreateDefaultView(new World(), viewManager, camera, 1920, 1080, null);

        Assert.True(viewManager.TryGetView(id, out var view));
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), Assert.IsType<BackBufferSurface>(view.Surface).ViewportRect);
        Assert.Equal(4f, camera.Zoom);
        Assert.Equal(1920, camera.Viewport.Width);
        Assert.Equal(1080, camera.Viewport.Height);
        Assert.Equal((480f, 270f), VisibleWorldArea(camera));
    }

    [Fact]
    public void ResizeSingleView_AfterTheWorldResizedItsCamera_PutsTheViewportBackToTheCroppedImage()
    {
        var camera = NewCamera(1280, 960, zoom: 4f);
        var surface = new BackBufferSurface(new Rectangle(0, 0, 1280, 960));
        var view = new RenderView(new World(), camera, surface);

        // What CasaEngineGame.OnScreenResized does: World.OnScreenResized sizes every camera to the window, then
        // the single view is laid out.
        camera.OnScreenResized(1920, 1080);
        VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, 1920, 1080, Native320x240);

        Assert.Equal(new Rectangle(320, 60, 1280, 960), surface.ViewportRect);
        Assert.Equal(4f, camera.Zoom);
        Assert.Equal(1280, camera.Viewport.Width);
        Assert.Equal(960, camera.Viewport.Height);
        Assert.Equal((320f, 240f), VisibleWorldArea(camera));

        camera.OnScreenResized(1280, 960);
        VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, 1280, 960, Native320x240);

        Assert.Equal(new Rectangle(0, 0, 1280, 960), surface.ViewportRect);
        Assert.Equal((320f, 240f), VisibleWorldArea(camera));

        camera.OnScreenResized(640, 480);
        VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, 640, 480, Native320x240);

        Assert.Equal(2f, camera.Zoom);
        Assert.Equal((320f, 240f), VisibleWorldArea(camera));
    }

    [Fact]
    public void ResizeSingleView_At400x200_ShowsTheCroppedCenterOfTheImage()
    {
        var camera = NewCamera(1280, 960, zoom: 4f);
        var surface = new BackBufferSurface(new Rectangle(0, 0, 1280, 960));
        var view = new RenderView(new World(), camera, surface);

        camera.OnScreenResized(400, 200);
        VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, 400, 200, Native320x240);

        Assert.Equal(new Rectangle(40, 0, 320, 200), surface.ViewportRect);
        Assert.Equal(1f, camera.Zoom);
        Assert.Equal(320, camera.Viewport.Width);
        Assert.Equal(200, camera.Viewport.Height);
        Assert.Equal((320f, 200f), VisibleWorldArea(camera));
    }

    [Fact]
    public void ResizeSingleView_WithoutTheSetting_DoesWhatTheEngineAlwaysDid()
    {
        var camera = NewCamera(1280, 960, zoom: 4f);
        var surface = new BackBufferSurface(new Rectangle(0, 0, 1280, 960));
        var view = new RenderView(new World(), camera, surface);

        VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, 1920, 1080, null);

        Assert.Equal(new Rectangle(0, 0, 1920, 1080), surface.ViewportRect);
        Assert.Equal(4f, camera.Zoom);
        Assert.Equal(1920, camera.Viewport.Width);
        Assert.Equal(1080, camera.Viewport.Height);
    }

    [Fact]
    public void ResizeSingleView_WithAnotherKindOfCamera_LaysOutTheViewButLeavesTheZoomAlone()
    {
        var camera = new CameraLookAtComponent();
        camera.OnScreenResized(1920, 1080);
        var surface = new BackBufferSurface(new Rectangle(0, 0, 1920, 1080));
        var view = new RenderView(new World(), camera, surface);

        VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, 1920, 1080, Native320x240);

        Assert.Equal(new Rectangle(320, 60, 1280, 960), surface.ViewportRect);
        Assert.Equal(1280, camera.Viewport.Width);
        Assert.Equal(960, camera.Viewport.Height);
    }

    [Theory]
    [InlineData(1280, 960, false)]
    [InlineData(1920, 1080, true)]
    [InlineData(1281, 961, true)]
    [InlineData(640, 480, false)]
    public void ShouldClearBands_IsTrueOnlyWhenTheImageDoesNotCoverTheWindow(int width, int height, bool expected)
    {
        var camera = NewCamera(width, height, zoom: 1f);
        var surface = new BackBufferSurface(new Rectangle(0, 0, width, height));
        var view = new RenderView(new World(), camera, surface);
        VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, width, height, Native320x240);

        Assert.Equal(expected, VirtualResolutionRuntime.ShouldClearBands(new[] { view }, Native320x240, width, height));
    }

    [Fact]
    public void ShouldClearBands_WithoutTheSetting_IsFalse()
    {
        var surface = new BackBufferSurface(new Rectangle(0, 0, 640, 480));
        var view = new RenderView(new World(), NewCamera(1920, 1080, 1f), surface);

        Assert.False(VirtualResolutionRuntime.ShouldClearBands(new[] { view }, null, 1920, 1080));
    }

    [Fact]
    public void ShouldClearBands_WithSeveralViewsOrNone_IsFalse()
    {
        var first = new RenderView(new World(), NewCamera(1920, 1080, 1f), new BackBufferSurface(new Rectangle(0, 0, 960, 1080)));
        var second = new RenderView(new World(), NewCamera(1920, 1080, 1f), new BackBufferSurface(new Rectangle(960, 0, 960, 1080)));

        Assert.False(VirtualResolutionRuntime.ShouldClearBands(new[] { first, second }, Native320x240, 1920, 1080));
        Assert.False(VirtualResolutionRuntime.ShouldClearBands(Array.Empty<RenderView>(), Native320x240, 1920, 1080));
    }

    // ---- Layout area of the view manager (ADR-0070) ----

    [Fact]
    public void CreateDefaultView_WithALeftLayoutMargin_PlacesTheViewInTheAreaAndSizesTheCameraToIt()
    {
        var camera = NewCamera(1024, 768, zoom: 1f);
        var viewManager = new ViewManager { LayoutInsets = new ViewLayoutInsets(280, 0, 0, 0) };

        var id = DefaultRuntimeViewBootstrapper.CreateDefaultView(new World(), viewManager, camera, 1024, 768, null);

        Assert.True(viewManager.TryGetView(id, out var view));
        Assert.Equal(new Rectangle(280, 0, 744, 768), Assert.IsType<BackBufferSurface>(view.Surface).ViewportRect);
        Assert.Equal(744, camera.Viewport.Width);
        Assert.Equal(768, camera.Viewport.Height);
    }

    [Fact]
    public void CreateDefaultView_WithLayoutMarginsAndAVirtualResolution_Throws()
    {
        var camera = NewCamera(1920, 1080, zoom: 4f);
        var viewManager = new ViewManager { LayoutInsets = new ViewLayoutInsets(280, 0, 0, 0) };

        Assert.Throws<InvalidOperationException>(
            () => DefaultRuntimeViewBootstrapper.CreateDefaultView(new World(), viewManager, camera, 1920, 1080, Native320x240));
        Assert.Empty(viewManager.Views);
    }

    [Fact]
    public void ResizeSingleView_WithALayoutArea_PutsTheViewAndItsCameraInTheArea()
    {
        var camera = NewCamera(1024, 768, zoom: 1f);
        var surface = new BackBufferSurface(new Rectangle(0, 0, 1024, 768));
        var view = new RenderView(new World(), camera, surface);

        VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, 1280, 800, new Rectangle(280, 0, 1000, 800), null);

        Assert.Equal(new Rectangle(280, 0, 1000, 800), surface.ViewportRect);
        Assert.Equal(1000, camera.Viewport.Width);
        Assert.Equal(800, camera.Viewport.Height);
    }

    [Fact]
    public void ResizeSingleView_WithALayoutAreaAndAVirtualResolution_ThrowsAndLeavesTheViewAlone()
    {
        var camera = NewCamera(1920, 1080, zoom: 4f);
        var surface = new BackBufferSurface(new Rectangle(0, 0, 1920, 1080));
        var view = new RenderView(new World(), camera, surface);

        Assert.Throws<InvalidOperationException>(
            () => VirtualResolutionRuntime.ResizeSingleBackBufferView(view, surface, 1920, 1080, new Rectangle(280, 0, 1640, 1080), Native320x240));
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), surface.ViewportRect);
    }

    [Fact]
    public void ResizeSingleView_WithTheWholeWindowAsLayoutArea_MatchesTheWindowOverload()
    {
        var cameraA = NewCamera(1280, 960, zoom: 4f);
        var surfaceA = new BackBufferSurface(new Rectangle(0, 0, 1280, 960));
        var cameraB = NewCamera(1280, 960, zoom: 4f);
        var surfaceB = new BackBufferSurface(new Rectangle(0, 0, 1280, 960));

        VirtualResolutionRuntime.ResizeSingleBackBufferView(new RenderView(new World(), cameraA, surfaceA), surfaceA, 1920, 1080, Native320x240);
        VirtualResolutionRuntime.ResizeSingleBackBufferView(
            new RenderView(new World(), cameraB, surfaceB), surfaceB, 1920, 1080, new Rectangle(0, 0, 1920, 1080), Native320x240);

        Assert.Equal(surfaceA.ViewportRect, surfaceB.ViewportRect);
        Assert.Equal(cameraA.Zoom, cameraB.Zoom);
        Assert.Equal(cameraA.Viewport, cameraB.Viewport);
    }
}
