using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// E19.TR-4a, T1a: the order of the post-UI overlays of a view (D-TR4A-4): lower order first, then
/// registration order.
/// </summary>
public class RenderViewPostUIOverlayOrderTests
{
    private static readonly RenderFrame EmptyFrame =
        new(Matrix.Identity, Matrix.Identity, Vector3.Zero, new Rectangle(0, 0, 1, 1));

    [Fact]
    public void DefaultOrder_KeepsTheRegistrationOrder()
    {
        var view = CreateView();
        var first = new NamedOverlay("first", new List<string>());
        var second = new NamedOverlay("second", new List<string>());

        view.RegisterPostUIOverlay(first);
        view.RegisterPostUIOverlay(second);

        Assert.Equal(new IPostUIOverlay[] { first, second }, view.PostUIOverlays);
    }

    [Fact]
    public void ALowerOrder_DrawsFirst_WhicheverRegisteredFirst()
    {
        var view = CreateView();
        var veil = new NamedOverlay("veil", new List<string>());
        var frozen = new NamedOverlay("frozen", new List<string>());

        view.RegisterPostUIOverlay(veil, PostUIOverlayOrder.Default);
        view.RegisterPostUIOverlay(frozen, PostUIOverlayOrder.FrozenFrame);

        Assert.Equal(new IPostUIOverlay[] { frozen, veil }, view.PostUIOverlays);
    }

    [Fact]
    public void EqualOrders_KeepTheRegistrationOrder()
    {
        var view = CreateView();
        var a = new NamedOverlay("a", new List<string>());
        var b = new NamedOverlay("b", new List<string>());
        var c = new NamedOverlay("c", new List<string>());

        view.RegisterPostUIOverlay(a, 5);
        view.RegisterPostUIOverlay(b, 5);
        view.RegisterPostUIOverlay(c, 5);

        Assert.Equal(new IPostUIOverlay[] { a, b, c }, view.PostUIOverlays);
    }

    [Fact]
    public void RegisteringTwice_IsIgnored_EvenWithAnotherOrder()
    {
        var view = CreateView();
        var overlay = new NamedOverlay("overlay", new List<string>());

        view.RegisterPostUIOverlay(overlay, 3);
        view.RegisterPostUIOverlay(overlay, -50);

        Assert.Single(view.PostUIOverlays);
    }

    [Fact]
    public void Unregister_RemovesTheOverlayWhateverItsOrder()
    {
        var view = CreateView();
        var a = new NamedOverlay("a", new List<string>());
        var b = new NamedOverlay("b", new List<string>());
        view.RegisterPostUIOverlay(a, 1);
        view.RegisterPostUIOverlay(b, 2);

        view.UnregisterPostUIOverlay(a);

        Assert.Equal(new IPostUIOverlay[] { b }, view.PostUIOverlays);
    }

    [Fact]
    public void DefaultUICompositionService_DrawsInTheOrderedList()
    {
        var calls = new List<string>();
        var view = CreateView();
        view.RegisterPostUIOverlay(new NamedOverlay("veil", calls), PostUIOverlayOrder.Default);
        view.RegisterPostUIOverlay(new NamedOverlay("frozen", calls), PostUIOverlayOrder.FrozenFrame);

        DefaultUICompositionService.Instance.Compose(null, view, in EmptyFrame);

        Assert.Equal(new[] { "frozen", "veil" }, calls);
    }

    private static RenderView CreateView()
    {
        var camera = new CasaEngine.Framework.Scene.Entities.Components.Camera2dComponent();
        camera.OnScreenResized(320, 240);
        return new RenderView(new CasaEngine.Framework.Scene.World.World(), camera, new StubRenderSurface());
    }

    private sealed class NamedOverlay : IPostUIOverlay
    {
        private readonly string _name;
        private readonly List<string> _calls;

        public NamedOverlay(string name, List<string> calls)
        {
            _name = name;
            _calls = calls;
        }

        public void Draw(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
        {
            _calls.Add(_name);
        }
    }

    private sealed class StubRenderSurface : IRenderSurface
    {
        public bool IsBackBuffer => false;

        public Rectangle ViewportRect => new(0, 0, 1, 1);

        public RenderTarget2D RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }
    }
}
