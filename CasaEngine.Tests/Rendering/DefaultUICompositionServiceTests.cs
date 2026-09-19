using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Covers <see cref="DefaultUICompositionService.Compose"/>'s post-UI overlay hook (D2,
/// ai-agent/tasks/screen-effect-above-ui-tasks.md, T1.1): overlays registered on a
/// <see cref="RenderView"/> are drawn immediately after <c>view.UIView.Draw()</c>, in every pipeline
/// that goes through this service, and never left registered once removed.
/// </summary>
public class DefaultUICompositionServiceTests
{
    [Fact]
    public void Compose_WithUIViewAndOverlay_DrawsUIViewThenOverlay()
    {
        var recordedCalls = new List<string>();
        var view = CreateView(new RecordingUIViewRuntime(recordedCalls));
        var overlay = new RecordingPostUIOverlay(recordedCalls);
        view.RegisterPostUIOverlay(overlay);

        DefaultUICompositionService.Instance.Compose(null, view, in EmptyFrame);

        Assert.Equal(new[] { "UIView.Draw", "Overlay.Draw" }, recordedCalls);
    }

    [Fact]
    public void Compose_WithUIViewAndNoOverlay_DrawsOnlyUIView()
    {
        var recordedCalls = new List<string>();
        var view = CreateView(new RecordingUIViewRuntime(recordedCalls));

        DefaultUICompositionService.Instance.Compose(null, view, in EmptyFrame);

        Assert.Equal(new[] { "UIView.Draw" }, recordedCalls);
    }

    [Fact]
    public void Compose_WithNoUIViewAndAnOverlay_DrawsOnlyTheOverlay()
    {
        var recordedCalls = new List<string>();
        var view = CreateView(uiView: null);
        var overlay = new RecordingPostUIOverlay(recordedCalls);
        view.RegisterPostUIOverlay(overlay);

        DefaultUICompositionService.Instance.Compose(null, view, in EmptyFrame);

        Assert.Equal(new[] { "Overlay.Draw" }, recordedCalls);
    }

    [Fact]
    public void Compose_AfterOverlayIsUnregistered_NoLongerCallsIt()
    {
        var recordedCalls = new List<string>();
        var view = CreateView(new RecordingUIViewRuntime(recordedCalls));
        var overlay = new RecordingPostUIOverlay(recordedCalls);
        view.RegisterPostUIOverlay(overlay);
        view.UnregisterPostUIOverlay(overlay);

        DefaultUICompositionService.Instance.Compose(null, view, in EmptyFrame);

        Assert.Equal(new[] { "UIView.Draw" }, recordedCalls);
    }

    private static readonly RenderFrame EmptyFrame =
        new(Matrix.Identity, Matrix.Identity, Vector3.Zero, new Rectangle(0, 0, 1, 1));

    private static RenderView CreateView(IUIViewRuntime uiView)
    {
        return new RenderView(new CasaEngine.Framework.Scene.World.World(), new ArcBallCameraComponent(), new StubRenderSurface())
        {
            UIView = uiView,
        };
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

    private sealed class RecordingUIViewRuntime : IUIViewRuntime
    {
        private readonly List<string> _recordedCalls;

        public RecordingUIViewRuntime(List<string> recordedCalls)
        {
            _recordedCalls = recordedCalls;
        }

        public bool IsPointerOverUI => false;

        public bool IsPointerCaptured => false;

        public bool IsKeyboardCaptured => false;

        public UIViewInputState InputState => default;

        public bool HasModalInput => false;

        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime)
        {
        }

        public void Draw()
        {
            _recordedCalls.Add("UIView.Draw");
        }

        public void UpdateMetrics(UIViewMetrics metrics)
        {
            Metrics = metrics;
        }

        public void PushScreen(IUIScreen screen)
        {
        }

        public IUIScreen PopScreen() => null;

        public void RemoveScreen(IUIScreen screen)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingPostUIOverlay : IPostUIOverlay
    {
        private readonly List<string> _recordedCalls;

        public RecordingPostUIOverlay(List<string> recordedCalls)
        {
            _recordedCalls = recordedCalls;
        }

        public void Draw(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
        {
            _recordedCalls.Add("Overlay.Draw");
        }
    }
}
