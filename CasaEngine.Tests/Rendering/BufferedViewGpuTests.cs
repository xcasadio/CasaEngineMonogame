using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The default view of a buffered mode on a real device (ADR-0086): a render target of the virtual size, a
/// <see cref="VirtualResolutionPresenter"/> placed for the window, the camera at the virtual viewport and zoom 1, which a
/// full resize keeps, and a target released on a world change. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class BufferedViewGpuTests
{
    [GpuFact]
    public void CreateDefaultView_InABufferedMode_RendersIntoATargetOfTheVirtualSize()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var camera = new Camera2dComponent();
            camera.OnScreenResized(1200, 720);
            camera.Zoom = 3f;
            var viewManager = new ViewManager();
            var settings = new VirtualResolutionSettings { Width = 640, Height = 480, Mode = VirtualResolutionMode.BufferedFit };

            var id = DefaultRuntimeViewBootstrapper.CreateDefaultView(
                new World(), viewManager, camera, 1200, 720, settings, GpuDeviceHost.Instance.GraphicsDevice);

            Assert.True(viewManager.TryGetView(id, out var view));
            var surface = Assert.IsType<RenderTargetSurface>(view.Surface);
            Assert.Equal(640, surface.RenderTarget.Width);
            Assert.Equal(480, surface.RenderTarget.Height);
            var presenter = Assert.IsType<VirtualResolutionPresenter>(view.Presenter);
            Assert.Equal(new Rectangle(120, 0, 960, 720), presenter.PresentedRect);
            Assert.Equal(640, camera.Viewport.Width);
            Assert.Equal(480, camera.Viewport.Height);
            Assert.Equal(1f, camera.Zoom);
            Assert.Null(view.Host);

            presenter.Dispose();
        });
    }

    [GpuFact]
    public void AFullResize_KeepsTheTargetSize_AndTheCameraAtTheVirtualViewport()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var camera = new Camera2dComponent();
            var viewManager = new ViewManager();
            var settings = new VirtualResolutionSettings { Width = 320, Height = 240, Mode = VirtualResolutionMode.BufferedIntegerFit };
            var id = DefaultRuntimeViewBootstrapper.CreateDefaultView(
                new World(), viewManager, camera, 1200, 720, settings, GpuDeviceHost.Instance.GraphicsDevice);
            Assert.True(viewManager.TryGetView(id, out var view));

            // CasaEngineGame.OnScreenResized: the world first sizes every camera to the window, then the buffered view.
            camera.OnScreenResized(1600, 900);
            VirtualResolutionRuntime.TryResizeBufferedView(viewManager.Views, 1600, 900);

            var surface = Assert.IsType<RenderTargetSurface>(view.Surface);
            Assert.Equal(320, surface.RenderTarget.Width);
            Assert.Equal(240, surface.RenderTarget.Height);
            Assert.Equal(320, camera.Viewport.Width);
            Assert.Equal(240, camera.Viewport.Height);
            Assert.Equal(1f, camera.Zoom);
            Assert.Equal(new Rectangle(320, 90, 960, 720), ((VirtualResolutionPresenter)view.Presenter).PresentedRect);

            ((VirtualResolutionPresenter)view.Presenter).Dispose();
        });
    }

    [GpuFact]
    public void AWorldChange_ReleasesTheTarget()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var viewManager = new ViewManager();
            viewManager.ViewRemoved += VirtualResolutionRuntime.ReleaseOwnedPresentation;
            var settings = new VirtualResolutionSettings { Width = 320, Height = 240, Mode = VirtualResolutionMode.BufferedIntegerFit };
            var id = DefaultRuntimeViewBootstrapper.CreateDefaultView(
                new World(), viewManager, new Camera2dComponent(), 1200, 720, settings, GpuDeviceHost.Instance.GraphicsDevice);
            Assert.True(viewManager.TryGetView(id, out var view));
            var surface = (RenderTargetSurface)view.Surface;

            viewManager.Clear();

            Assert.Null(surface.RenderTarget);
            Assert.True(((VirtualResolutionPresenter)view.Presenter).IsDisposed);
        });
    }
}
