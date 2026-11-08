using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Input;

/// <summary>
/// The pointer over a buffered view (ADR-0087): the presenter's rectangle is the view's place in the window, so a point
/// anywhere in the shown image -- not only inside the target's own (0,0,w,h) -- finds the view, its local position is in
/// virtual pixels, and a point in a band finds no view. One conversion serves the view manager and the input router.
/// </summary>
public class BufferedPointerTests
{
    private sealed class TargetSurface : IRenderSurface
    {
        private readonly int _width;
        private readonly int _height;

        public TargetSurface(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public bool IsBackBuffer => false;

        public Rectangle ViewportRect => new(0, 0, _width, _height);

        public RenderTarget2D RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }
    }

    private static (ViewManager ViewManager, RenderView View) BufferedView(
        VirtualResolutionMode mode, int virtualWidth, int virtualHeight, int windowWidth, int windowHeight)
    {
        var viewManager = new ViewManager();
        var presenter = new VirtualResolutionPresenter(null, mode, virtualWidth, virtualHeight, windowWidth, windowHeight);
        var id = viewManager.CreateView(new ViewDefinition
        {
            World = new World(),
            Camera = new Camera2dComponent(),
            Surface = new TargetSurface(virtualWidth, virtualHeight),
            Presenter = presenter,
            Name = "Default view",
        });
        Assert.True(viewManager.TryGetView(id, out var view));
        return (viewManager, view);
    }

    [Theory]
    [InlineData(VirtualResolutionMode.BufferedIntegerFit, 320, 240, 152, 61, 10, 20)]
    [InlineData(VirtualResolutionMode.BufferedFit, 640, 480, 135, 30, 10, 20)]
    [InlineData(VirtualResolutionMode.BufferedIntegerFit, 320, 240, 10, 10, -37, 3)]
    public void WindowToVirtualPixel_UndoesThePresentation(
        VirtualResolutionMode mode, int virtualWidth, int virtualHeight, int windowX, int windowY, int x, int y)
    {
        // Both images are 960x720 at (120,0) in a 1200x720 window: factor 3 for 320x240, 1.5 for 640x480.
        var presenter = new VirtualResolutionPresenter(null, mode, virtualWidth, virtualHeight, 1200, 720);

        Assert.Equal(new Point(x, y), presenter.WindowToVirtualPixel(new Point(windowX, windowY)));
    }

    [Fact]
    public void ScreenToView_InsideTheImageButOutsideTheTargetSize_FindsTheBufferedView_InVirtualPixels()
    {
        var (viewManager, view) = BufferedView(VirtualResolutionMode.BufferedIntegerFit, 320, 240, 1200, 720);

        // (620, 600) is inside the 960x720 image at (120,0), far outside (0,0,320,240).
        var (found, local) = viewManager.ScreenToView(new Point(620, 600));

        Assert.Same(view, found);
        Assert.Equal((620f - 120f) / 3f, local.X, 3);
        Assert.Equal(600f / 3f, local.Y, 3);
    }

    [Fact]
    public void ScreenToView_InABand_FindsNoView()
    {
        var (viewManager, _) = BufferedView(VirtualResolutionMode.BufferedIntegerFit, 320, 240, 1200, 720);

        var (found, _) = viewManager.ScreenToView(new Point(30, 300));

        Assert.Null(found);
    }

    [Fact]
    public void TheInputRouter_FindsTheSameView_AndRoutesTheMouseInVirtualPixels()
    {
        var (viewManager, view) = BufferedView(VirtualResolutionMode.BufferedFit, 640, 480, 1200, 720);
        var router = new InputRouter(viewManager);

        var (found, _) = router.RouteMouseToView(new Point(135, 30));
        Assert.Same(view, found);

        var local = InputRouter.CreateLocalMouseState(
            new MouseState(135, 30, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            view,
            new Rectangle(120, 0, 960, 720));

        Assert.Equal(new Point(10, 20), local.Position);
        Assert.Equal(ButtonState.Pressed, local.LeftButton);
    }

    [Fact]
    public void AnIntegerFitView_KeepsItsConversion()
    {
        var viewManager = new ViewManager();
        Framework.Application.DefaultRuntimeViewBootstrapper.CreateDefaultView(
            new World(), viewManager, new Camera2dComponent(), 1200, 720, new VirtualResolutionSettings { Width = 400, Height = 240 });

        var (found, local) = viewManager.ScreenToView(new Point(300, 200));

        Assert.NotNull(found);
        Assert.Equal(new Vector2(300, 200), local);
        Assert.Equal(new Point(300, 200), InputRouter.CreateLocalMouseState(new MouseState(300, 200, 0, 0, 0, 0, 0, 0), found, new Rectangle(0, 0, 1200, 720)).Position);
    }
}
