using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Application;

/// <summary>
/// The runtime side of a buffered view (ADR-0086) that needs no graphics device: after a window resize the camera goes
/// back to the virtual viewport at zoom 1 and the presenter follows the window; the target the default view owns is
/// released when the view is removed, including by <see cref="ViewManager.Clear"/> on a world change, and nothing else is.
/// </summary>
public class BufferedViewRuntimeTests
{
    private sealed class OwnedSurface : IRenderSurface, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public bool IsBackBuffer => false;

        public Rectangle ViewportRect => new(0, 0, 640, 480);

        public RenderTarget2D RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }

        public void Dispose() => IsDisposed = true;
    }

    private static RenderView AddBufferedView(ViewManager viewManager, Camera2dComponent camera, VirtualResolutionPresenter presenter, IRenderSurface surface)
    {
        var id = viewManager.CreateView(new ViewDefinition
        {
            World = new World(),
            Camera = camera,
            Surface = surface,
            Presenter = presenter,
            Name = "Default view",
        });
        Assert.True(viewManager.TryGetView(id, out var view));
        return view;
    }

    [Fact]
    public void AResize_PutsTheCameraBackToTheVirtualViewportAtZoomOne_AndMovesThePresentation()
    {
        var surface = new OwnedSurface();
        var presenter = new VirtualResolutionPresenter(surface, VirtualResolutionMode.BufferedFit, 640, 480, 1200, 720);
        var camera = new Camera2dComponent();
        var viewManager = new ViewManager();
        AddBufferedView(viewManager, camera, presenter, surface);

        // What World.OnScreenResized does just before: every camera to the window.
        camera.OnScreenResized(1280, 720);
        camera.Zoom = 3f;

        bool handled = VirtualResolutionRuntime.TryResizeBufferedView(viewManager.Views, 1280, 720);

        Assert.True(handled);
        Assert.Equal(640, camera.Viewport.Width);
        Assert.Equal(480, camera.Viewport.Height);
        Assert.Equal(1f, camera.Zoom);
        Assert.Equal(new Rectangle(160, 0, 960, 720), presenter.PresentedRect);
        Assert.Equal(new Rectangle(0, 0, 640, 480), surface.ViewportRect);
    }

    [Fact]
    public void AResize_OfAnIntegerFitView_IsNotHandledHere()
    {
        var viewManager = new ViewManager();
        DefaultRuntimeViewBootstrapper.CreateDefaultView(
            new World(), viewManager, new Camera2dComponent(), 1200, 720, new VirtualResolutionSettings { Width = 400, Height = 240 });

        Assert.False(VirtualResolutionRuntime.TryResizeBufferedView(viewManager.Views, 1280, 720));
    }

    [Fact]
    public void RemovingTheView_ReleasesTheOwnedTarget()
    {
        var surface = new OwnedSurface();
        var presenter = new VirtualResolutionPresenter(surface, VirtualResolutionMode.BufferedIntegerFit, 640, 480, 1200, 720);
        var viewManager = new ViewManager();
        viewManager.ViewRemoved += VirtualResolutionRuntime.ReleaseOwnedPresentation;
        var view = AddBufferedView(viewManager, new Camera2dComponent(), presenter, surface);

        viewManager.Remove(view);

        Assert.True(presenter.IsDisposed);
        Assert.True(surface.IsDisposed);
    }

    [Fact]
    public void AWorldChange_ReleasesTheOldTarget_AndKeepsOnlyTheNewOne()
    {
        var viewManager = new ViewManager();
        viewManager.ViewRemoved += VirtualResolutionRuntime.ReleaseOwnedPresentation;

        var firstSurface = new OwnedSurface();
        var first = new VirtualResolutionPresenter(firstSurface, VirtualResolutionMode.BufferedFit, 640, 480, 1200, 720);
        AddBufferedView(viewManager, new Camera2dComponent(), first, firstSurface);

        // GameManager.UpdateWorld clears the views before the next world builds its own.
        viewManager.Clear();
        var secondSurface = new OwnedSurface();
        var second = new VirtualResolutionPresenter(secondSurface, VirtualResolutionMode.BufferedIntegerFit, 320, 240, 1200, 720);
        AddBufferedView(viewManager, new Camera2dComponent(), second, secondSurface);

        Assert.True(firstSurface.IsDisposed);
        Assert.False(secondSurface.IsDisposed);
        Assert.False(second.IsDisposed);
    }

    [Fact]
    public void RemovingAViewWithoutABufferedPresenter_ReleasesNothing()
    {
        var viewManager = new ViewManager();
        viewManager.ViewRemoved += VirtualResolutionRuntime.ReleaseOwnedPresentation;
        var surface = new OwnedSurface();
        var id = viewManager.CreateView(new ViewDefinition
        {
            World = new World(),
            Camera = new Camera2dComponent(),
            Surface = surface,
            Name = "Custom view",
        });

        Assert.True(viewManager.TryGetView(id, out var view));
        viewManager.Remove(view);

        Assert.False(surface.IsDisposed);
    }

    [Fact]
    public void ThePresenter_RefusesTheDirectMode()
    {
        Assert.Throws<ArgumentException>(() =>
            new VirtualResolutionPresenter(null, VirtualResolutionMode.IntegerFit, 400, 240, 1200, 720));
    }
}
